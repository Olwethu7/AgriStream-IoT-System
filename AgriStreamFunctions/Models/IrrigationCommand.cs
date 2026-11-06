namespace AgriStreamFunctions.Models;

/// <summary>Body accepted by HttpPostIrrigation.</summary>
public class IrrigationRequest
{
    public string? FieldId { get; set; }
    public int DurationMinutes { get; set; }
    public string? RequestedBy { get; set; }
}

/// <summary>Message placed on the irrigation-commands queue.</summary>
public class IrrigationCommand
{
    public string CommandId { get; set; } = Guid.NewGuid().ToString();
    public string Command { get; set; } = "StartIrrigation";
    public string FieldId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
}
