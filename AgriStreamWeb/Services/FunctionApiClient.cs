using System.Net.Http.Json;
using AgriStreamWeb.Models;

namespace AgriStreamWeb.Services;

/// <summary>Calls the AgriStream Function App HTTP endpoints.</summary>
public class FunctionApiClient
{
    private readonly HttpClient _http;
    private readonly string _functionKey;

    public FunctionApiClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _functionKey = config["FunctionApp:FunctionKey"] ?? string.Empty;
    }

    private string WithKey(string path) =>
        string.IsNullOrWhiteSpace(_functionKey) || _functionKey.StartsWith('<')
            ? path
            : $"{path}{(path.Contains('?') ? '&' : '?')}code={Uri.EscapeDataString(_functionKey)}";

    public async Task<List<Telemetry>> GetTelemetryAsync(int top, string? fieldId, CancellationToken ct = default)
    {
        var path = $"telemetry?top={top}";
        if (!string.IsNullOrWhiteSpace(fieldId)) path += $"&fieldId={Uri.EscapeDataString(fieldId)}";

        var result = await _http.GetFromJsonAsync<List<Telemetry>>(WithKey(path), ct);
        return result ?? new List<Telemetry>();
    }

    public async Task<HttpResponseMessage> SendIrrigationCommandAsync(string fieldId, int durationMinutes, string username, CancellationToken ct = default)
    {
        var payload = new { fieldId, durationMinutes, requestedBy = username };
        return await _http.PostAsJsonAsync(WithKey("irrigation"), payload, ct);
    }
}
