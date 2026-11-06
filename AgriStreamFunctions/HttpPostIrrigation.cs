using System.Text.Json;
using AgriStreamFunctions.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStreamFunctions;

/// <summary>
/// POST /api/irrigation  { "fieldId": "FIELD-01", "durationMinutes": 15, "requestedBy": "admin" }
/// Validates the request and puts an irrigation command on the irrigation-commands queue.
/// </summary>
public class HttpPostIrrigation
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ILogger<HttpPostIrrigation> _logger;

    public HttpPostIrrigation(ILogger<HttpPostIrrigation> logger) => _logger = logger;

    [Function(nameof(HttpPostIrrigation))]
    public async Task<IrrigationOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation")] HttpRequest req)
    {
        IrrigationRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<IrrigationRequest>(req.Body, Json);
        }
        catch (JsonException)
        {
            return IrrigationOutput.Bad("Body must be valid JSON.");
        }

        if (body == null || string.IsNullOrWhiteSpace(body.FieldId) || body.FieldId.Length > 50)
            return IrrigationOutput.Bad("fieldId is required (max 50 characters).");
        if (body.DurationMinutes is < 1 or > 240)
            return IrrigationOutput.Bad("durationMinutes must be between 1 and 240.");
        if (string.IsNullOrWhiteSpace(body.RequestedBy))
            return IrrigationOutput.Bad("requestedBy is required.");

        var command = new IrrigationCommand
        {
            FieldId = body.FieldId.Trim().ToUpperInvariant(),
            DurationMinutes = body.DurationMinutes,
            RequestedBy = body.RequestedBy.Trim()
        };

        _logger.LogInformation("Queueing irrigation {CommandId} for {Field} ({Minutes} min) by {User}.",
            command.CommandId, command.FieldId, command.DurationMinutes, command.RequestedBy);

        return new IrrigationOutput
        {
            Message = JsonSerializer.Serialize(command, Json),
            HttpResponse = new AcceptedResult((string?)null, new
            {
                commandId = command.CommandId,
                status = "Queued",
                command.FieldId,
                command.DurationMinutes
            })
        };
    }
}

/// <summary>Multi-output: HTTP response + queue message.</summary>
public class IrrigationOutput
{
    [QueueOutput("irrigation-commands", Connection = "AgriStreamStorage")]
    public string? Message { get; set; }

    [HttpResult]
    public IActionResult HttpResponse { get; set; } = new OkResult();

    public static IrrigationOutput Bad(string error) =>
        new() { Message = null, HttpResponse = new BadRequestObjectResult(new { error }) };
}
