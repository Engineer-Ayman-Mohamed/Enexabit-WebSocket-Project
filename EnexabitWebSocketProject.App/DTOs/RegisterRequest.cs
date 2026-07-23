using System.ComponentModel.DataAnnotations;

namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/auth/register.</summary>
public class RegisterRequest
{
    /// <summary>Desired unique username.</summary>
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Password (plain text, hashed server-side via BCrypt).</summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Optional display name shown in chat. Falls back to username if empty.</summary>
    [StringLength(50, ErrorMessage = "Display name cannot exceed 50 characters")]
    public string DisplayName { get; set; } = string.Empty;
}
