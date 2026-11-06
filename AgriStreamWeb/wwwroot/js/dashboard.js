// AgriStream dashboard: polls telemetry, drives the chart, KPIs and irrigation form.
(function () {
    "use strict";

    const POLL_MS = 10000;
    const el = (id) => document.getElementById(id);
    const seen = new Set();
    let chart;

    const escapeHtml = (v) => String(v ?? "").replace(/[&<>"']/g, (c) =>
        ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

    const fmt = (v, digits = 1) => (v === null || v === undefined ? "—" : Number(v).toFixed(digits));

    function antiForgeryToken(form) {
        const input = form.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : "";
    }

    function setLive(online) {
        const ind = el("liveIndicator");
        if (!ind) return;
        ind.classList.toggle("offline", !online);
        ind.querySelector(".live-label").textContent = online ? "Live Telemetry" : "Telemetry Offline";
    }

    function setKpi(id, value) {
        const node = el(id);
        if (!node) return;
        const text = value === null ? "--" : value;
        if (node.textContent !== String(text)) {
            node.textContent = text;
            node.classList.remove("flash");
            void node.offsetWidth; // restart animation
            node.classList.add("flash");
        }
    }

    const average = (rows, key) => {
        const vals = rows.map((r) => r[key]).filter((v) => typeof v === "number");
        return vals.length ? (vals.reduce((a, b) => a + b, 0) / vals.length).toFixed(1) : null;
    };

    function initChart() {
        const canvas = el("trendChart");
        if (!canvas || typeof Chart === "undefined") return;
        Chart.defaults.color = "#8aa39a";
        Chart.defaults.font.family = "Inter, system-ui, sans-serif";

        chart = new Chart(canvas, {
            type: "line",
            data: {
                labels: [],
                datasets: [
                    {
                        label: "Soil moisture (%)", data: [], yAxisID: "y",
                        borderColor: "#34d399", backgroundColor: "rgba(52,211,153,.12)",
                        fill: true, tension: .35, pointRadius: 2, spanGaps: true
                    },
                    {
                        label: "Temperature (°C)", data: [], yAxisID: "y1",
                        borderColor: "#fbbf24", backgroundColor: "transparent",
                        tension: .35, pointRadius: 2, spanGaps: true
                    }
                ]
            },
            options: {
                responsive: true, maintainAspectRatio: false,
                interaction: { mode: "index", intersect: false },
                plugins: { legend: { labels: { usePointStyle: true, boxWidth: 8 } } },
                scales: {
                    x: { grid: { color: "rgba(110,231,183,.06)" } },
                    y: { position: "left", grid: { color: "rgba(110,231,183,.06)" }, title: { display: true, text: "%" } },
                    y1: { position: "right", grid: { drawOnChartArea: false }, title: { display: true, text: "°C" } }
                }
            }
        });
    }

    function render(rows) {
        // Rows arrive newest first; the chart reads oldest -> newest.
        const sensorRows = rows.filter((r) => r.soilMoisture != null || r.temperature != null).slice().reverse();

        setKpi("kpiMoisture", average(rows, "soilMoisture"));
        setKpi("kpiTemp", average(rows, "temperature"));
        setKpi("kpiHumidity", average(rows, "humidity"));
        setKpi("kpiEvents", rows.filter((r) => r.eventType === "ImageUploaded").length);

        if (chart) {
            chart.data.labels = sensorRows.map((r) => new Date(r.recordedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }));
            chart.data.datasets[0].data = sensorRows.map((r) => r.soilMoisture);
            chart.data.datasets[1].data = sensorRows.map((r) => r.temperature);
            chart.update();
        }
        el("trendChart")?.parentElement.classList.toggle("is-empty", sensorRows.length === 0);

        const body = el("telemetryBody");
        if (!rows.length) {
            body.innerHTML = '<tr><td colspan="7" class="text-center text-secondary py-4">No telemetry yet. Upload a drone image to generate the first event.</td></tr>';
            return;
        }

        body.innerHTML = rows.map((r) => {
            const isNew = seen.size > 0 && !seen.has(r.eventId);
            return `<tr class="${isNew ? "row-new" : ""}">
                <td class="mono">${escapeHtml(new Date(r.recordedAt).toLocaleString())}</td>
                <td><span class="fw-semibold">${escapeHtml(r.fieldId)}</span></td>
                <td><span class="event-badge">${escapeHtml(r.eventType)}</span></td>
                <td class="text-end mono">${fmt(r.soilMoisture)}%</td>
                <td class="text-end mono">${fmt(r.temperature)}°</td>
                <td class="text-end mono">${fmt(r.humidity)}%</td>
                <td class="small text-secondary text-truncate" style="max-width:220px" title="${escapeHtml(r.blobName)}">${escapeHtml(r.blobName ?? "—")}</td>
            </tr>`;
        }).join("");
        rows.forEach((r) => seen.add(r.eventId));
    }

    async function loadTelemetry() {
        try {
            const res = await fetch("/Dashboard/Telemetry?top=30", { headers: { Accept: "application/json" } });
            if (!res.ok) throw new Error("HTTP " + res.status);
            render(await res.json());
            setLive(true);
            el("lastUpdated").textContent = "updated " + new Date().toLocaleTimeString();
        } catch (e) {
            setLive(false);
            el("lastUpdated").textContent = "connection lost — retrying";
            if (!seen.size) {
                el("telemetryBody").innerHTML = '<tr><td colspan="7" class="text-center text-secondary py-4">Telemetry service unreachable. Check FunctionApp settings in appsettings.json.</td></tr>';
            }
        }
    }

    async function loadStatus() {
        try {
            const res = await fetch("/Dashboard/Status");
            if (!res.ok) return;
            const s = await res.json();
            const chip = el("userStatus");
            el("userStatusText").textContent = s.status;
            chip.className = "status-chip " + (s.status === "Irrigating" ? "status-active" : "status-idle");
            if (s.lastIrrigationAt) {
                el("lastIrrigation").textContent = "Last irrigation " + new Date(s.lastIrrigationAt + (s.lastIrrigationAt.endsWith("Z") ? "" : "Z")).toLocaleString();
            }
        } catch { /* keep last known */ }
    }

    function wireIrrigation() {
        const form = el("irrigationForm");
        if (!form) return;
        form.addEventListener("submit", async (e) => {
            e.preventDefault();
            const btn = el("irrigateBtn");
            const out = el("irrigationResult");
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Sending…';
            out.className = "small mt-2";
            out.textContent = "";

            try {
                const res = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form),
                    headers: { RequestVerificationToken: antiForgeryToken(form) }
                });
                const data = await res.json().catch(() => ({}));
                if (!res.ok) throw new Error(data.error || "Request failed (" + res.status + ")");
                out.classList.add("text-success");
                out.textContent = "Command queued" + (data.commandId ? " · " + data.commandId.slice(0, 8) : "");
                setTimeout(loadStatus, 4000);
            } catch (err) {
                out.classList.add("text-danger");
                out.textContent = err.message;
            } finally {
                btn.disabled = false;
                btn.innerHTML = '<i class="bi bi-send me-2"></i>Send irrigation command';
            }
        });
    }

    function wireDropzone() {
        const zone = el("dropzone"), input = el("imageInput"), text = el("dropzoneText");
        if (!zone) return;
        const show = () => { if (input.files.length) text.textContent = input.files[0].name; };
        input.addEventListener("change", show);
        ["dragenter", "dragover"].forEach((t) => zone.addEventListener(t, (e) => { e.preventDefault(); zone.classList.add("dragover"); }));
        ["dragleave", "drop"].forEach((t) => zone.addEventListener(t, (e) => { e.preventDefault(); zone.classList.remove("dragover"); }));
        zone.addEventListener("drop", (e) => { if (e.dataTransfer.files.length) { input.files = e.dataTransfer.files; show(); } });
    }

    document.addEventListener("DOMContentLoaded", () => {
        initChart();
        wireIrrigation();
        wireDropzone();
        el("refreshBtn")?.addEventListener("click", loadTelemetry);
        loadTelemetry();
        setInterval(loadTelemetry, POLL_MS);
        setInterval(loadStatus, POLL_MS * 3);
    });
})();
