namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/auth/login.</summary>
public class LoginRequest
{
    /// <summary>The user's login name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The user's password (plain text).</summary>
    public string Password { get; set; } = string.Empty;
}
