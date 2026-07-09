namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Response returned on successful login.</summary>
/// <remarks>
/// The <c>RefreshToken</c> field is only populated when the client identifies
/// itself as a mobile device via the <c>X-Client-Type: mobile</c> header.
/// Web clients receive the refresh token exclusively through an HttpOnly cookie.
/// </remarks>
public class LoginResponse
{
    /// <summary>JWT access token (Bearer) for authenticated requests.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>The user's display name for UI rendering.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for silent token rotation.
    /// <c>null</c> for web clients (cookie-only); populated for mobile clients.
    /// </summary>
    public string? RefreshToken { get; set; }
}
