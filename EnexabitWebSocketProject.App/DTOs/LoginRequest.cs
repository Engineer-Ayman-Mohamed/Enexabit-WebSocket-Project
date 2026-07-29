using System.ComponentModel.DataAnnotations;

namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/auth/login.</summary>
public class LoginRequest
{
    /// <summary>The user's login name.</summary>
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = string.Empty;

    /// <summary>The user's password (plain text).</summary>
    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;
}
