namespace AgriStreamWeb.Models;

/// <summary>One row from the TelemetryLogs table, as returned by HttpGetTelemetry.</summary>
public class Telemetry
{
    public string FieldId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? BlobName { get; set; }
    public long? SizeBytes { get; set; }
    public double? SoilMoisture { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}
