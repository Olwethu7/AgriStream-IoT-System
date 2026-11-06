using Azure;
using Azure.Data.Tables;

namespace AgriStreamFunctions.Models;

/// <summary>
/// Row in the TelemetryLogs table.
/// PartitionKey = "telemetry" (single partition keeps "latest N" queries cheap for a small farm).
/// RowKey = inverted ticks + guid, so the newest rows sort first.
/// </summary>
public class TelemetryEntity : ITableEntity
{
    public const string Partition = "telemetry";

    public string PartitionKey { get; set; } = Partition;
    public string RowKey { get; set; } = NewRowKey();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string FieldId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? BlobName { get; set; }
    public long? SizeBytes { get; set; }
    public string? ContentType { get; set; }
    public string? UploadedBy { get; set; }
    public double? SoilMoisture { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;

    public static string NewRowKey() =>
        $"{DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks:D19}_{Guid.NewGuid():N}";
}

/// <summary>Shape returned by HttpGetTelemetry (matches the web app's Telemetry model).</summary>
public record TelemetryDto(
    string FieldId,
    string EventId,
    string EventType,
    string? BlobName,
    long? SizeBytes,
    double? SoilMoisture,
    double? Temperature,
    double? Humidity,
    DateTimeOffset RecordedAt);
