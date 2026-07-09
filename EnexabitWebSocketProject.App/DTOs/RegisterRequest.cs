namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/auth/register.</summary>
public class RegisterRequest
{
    /// <summary>Desired unique username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Password (plain text, hashed server-side via BCrypt).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Optional display name shown in chat. Falls back to username if empty.</summary>
    public string DisplayName { get; set; } = string.Empty;
}
