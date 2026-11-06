using System.Text.Json;
using AgriStreamFunctions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriStreamFunctions;

/// <summary>
/// Fires for each message on irrigation-commands and updates the requesting
/// user's status in the Azure SQL Users table.
/// </summary>
public class QueueTriggerFunction
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _sqlConnectionString;
    private readonly ILogger<QueueTriggerFunction> _logger;

    public QueueTriggerFunction(IConfiguration config, ILogger<QueueTriggerFunction> logger)
    {
        _sqlConnectionString = config["SqlConnectionString"]
            ?? throw new InvalidOperationException("SqlConnectionString setting is missing.");
        _logger = logger;
    }

    [Function(nameof(QueueTriggerFunction))]
    public async Task Run(
        [QueueTrigger("irrigation-commands", Connection = "AgriStreamStorage")] string message,
        CancellationToken ct)
    {
        IrrigationCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<IrrigationCommand>(message, Json);
        }
        catch (JsonException ex)
        {
            // Bad payloads will never succeed; log and drop instead of retrying.
            _logger.LogError(ex, "Discarding malformed irrigation message.");
            return;
        }

        if (command == null || string.IsNullOrWhiteSpace(command.RequestedBy))
        {
            _logger.LogWarning("Discarding irrigation message without requestedBy.");
            return;
        }

        const string sql = """
            UPDATE dbo.Users
            SET Status = @Status,
                LastIrrigationAt = @IssuedAt,
                LastCommandId = @CommandId
            WHERE Username = @Username;
            """;

        await using var conn = new SqlConnection(_sqlConnectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@Status", System.Data.SqlDbType.NVarChar, 50).Value = "Irrigating";
        cmd.Parameters.Add("@IssuedAt", System.Data.SqlDbType.DateTime2).Value = command.IssuedAt.UtcDateTime;
        cmd.Parameters.Add("@CommandId", System.Data.SqlDbType.NVarChar, 100).Value = command.CommandId;
        cmd.Parameters.Add("@Username", System.Data.SqlDbType.NVarChar, 100).Value = command.RequestedBy;

        var rows = await cmd.ExecuteNonQueryAsync(ct);

        if (rows == 0)
            _logger.LogWarning("No user '{User}' found for command {CommandId}.", command.RequestedBy, command.CommandId);
        else
            _logger.LogInformation("User '{User}' set to Irrigating for {Field} (command {CommandId}).",
                command.RequestedBy, command.FieldId, command.CommandId);
    }
}
