namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Response returned on successful login.</summary>
public class LoginResponse
{
    /// <summary>JWT access token (Bearer) for authenticated requests.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>The user's display name for UI rendering.</summary>
    public string DisplayName { get; set; } = string.Empty;
}
