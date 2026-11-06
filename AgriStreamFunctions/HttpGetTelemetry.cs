using AgriStreamFunctions.Models;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStreamFunctions;

/// <summary>
/// GET /api/telemetry?top=20&amp;fieldId=FIELD-01
/// Returns the latest TelemetryLogs rows, newest first.
/// </summary>
public class HttpGetTelemetry
{
    private readonly TableClient _table;
    private readonly ILogger<HttpGetTelemetry> _logger;

    public HttpGetTelemetry(TableClient table, ILogger<HttpGetTelemetry> logger)
    {
        _table = table;
        _logger = logger;
    }

    [Function(nameof(HttpGetTelemetry))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry")] HttpRequest req,
        CancellationToken ct)
    {
        var top = int.TryParse(req.Query["top"], out var t) ? Math.Clamp(t, 1, 100) : 20;
        var fieldId = req.Query["fieldId"].ToString();

        // RowKeys are inverted timestamps, so table order is already newest first.
        var filter = string.IsNullOrWhiteSpace(fieldId)
            ? TableClient.CreateQueryFilter($"PartitionKey eq {TelemetryEntity.Partition}")
            : TableClient.CreateQueryFilter($"PartitionKey eq {TelemetryEntity.Partition} and FieldId eq {fieldId.Trim().ToUpperInvariant()}");

        var results = new List<TelemetryDto>(top);
        await foreach (var e in _table.QueryAsync<TelemetryEntity>(filter, maxPerPage: top, cancellationToken: ct))
        {
            results.Add(new TelemetryDto(e.FieldId, e.RowKey, e.EventType, e.BlobName, e.SizeBytes,
                e.SoilMoisture, e.Temperature, e.Humidity, e.RecordedAt));
            if (results.Count >= top) break;
        }

        _logger.LogInformation("Returned {Count} telemetry rows.", results.Count);
        return new OkObjectResult(results);
    }
}
