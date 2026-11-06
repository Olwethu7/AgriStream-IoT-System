using System.ComponentModel.DataAnnotations;

namespace AgriStreamWeb.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    // Changed from PasswordHash to Password to match the SQL table
    [Required, MaxLength(500)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? FullName { get; set; }

    [MaxLength(50)]
    public string Role { get; set; } = "Operator";

    [MaxLength(50)]
    public string Status { get; set; } = "Idle";

    public DateTime? LastIrrigationAt { get; set; }

    [MaxLength(100)]
    public string? LastCommandId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}