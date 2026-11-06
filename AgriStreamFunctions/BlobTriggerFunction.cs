using System.Globalization;
using AgriStreamFunctions.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStreamFunctions;

/// <summary>
/// Fires when a drone image lands in the drone-images container and writes a
/// log entry (with any sensor readings sent as blob metadata) to TelemetryLogs.
/// </summary>
public class BlobTriggerFunction
{
    private readonly TableClient _table;
    private readonly ILogger<BlobTriggerFunction> _logger;

    public BlobTriggerFunction(TableClient table, ILogger<BlobTriggerFunction> logger)
    {
        _table = table;
        _logger = logger;
    }

    [Function(nameof(BlobTriggerFunction))]
    public async Task Run(
        [BlobTrigger("drone-images/{name}", Connection = "AgriStreamStorage")] BlobClient blob,
        string name,
        CancellationToken ct)
    {
        var props = (await blob.GetPropertiesAsync(cancellationToken: ct)).Value;
        var meta = new Dictionary<string, string>(props.Metadata, StringComparer.OrdinalIgnoreCase);

        // Field ID: metadata first, else the first folder in the blob path, else a default.
        var fieldId = meta.GetValueOrDefault("fieldId")
                      ?? (name.Contains('/') ? name[..name.IndexOf('/')] : "UNASSIGNED");

        var entity = new TelemetryEntity
        {
            FieldId = fieldId,
            EventType = "ImageUploaded",
            BlobName = name,
            SizeBytes = props.ContentLength,
            ContentType = props.ContentType,
            UploadedBy = meta.GetValueOrDefault("uploadedBy"),
            SoilMoisture = ParseDouble(meta, "soilMoisture"),
            Temperature = ParseDouble(meta, "temperature"),
            Humidity = ParseDouble(meta, "humidity"),
            RecordedAt = DateTimeOffset.UtcNow
        };

        await _table.AddEntityAsync(entity, ct);

        _logger.LogInformation("Logged drone image {Blob} ({Bytes} bytes) for field {Field}.",
            name, props.ContentLength, fieldId);
    }

    private static double? ParseDouble(IDictionary<string, string> meta, string key) =>
        meta.TryGetValue(key, out var raw) &&
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
}
