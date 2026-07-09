namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/auth/refresh (mobile fallback).</summary>
/// <remarks>
/// Used when the refresh token cannot be sent via cookie (mobile clients).
/// Web clients send the token via the HttpOnly cookie; this body is ignored.
/// </remarks>
public class RefreshRequest
{
    /// <summary>The refresh token to validate and rotate.</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
