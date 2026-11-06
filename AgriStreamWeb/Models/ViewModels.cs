using System.ComponentModel.DataAnnotations;

namespace AgriStreamWeb.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class DashboardViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string UserStatus { get; set; } = "Idle";
    public DateTime? LastIrrigationAt { get; set; }
}

public class IrrigationRequest
{
    [Required, MaxLength(50)]
    public string FieldId { get; set; } = string.Empty;

    [Range(1, 240)]
    public int DurationMinutes { get; set; } = 15;
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
