using System;

namespace EnexabitWebSocketProject.App.Models;

/// <summary>
/// A refresh token used for silent token rotation.
/// Supports theft detection — if a revoked token is reused, all user sessions are invalidated.
/// </summary>
public class RefreshToken
{
    /// <summary>Unique identifier for the refresh token record.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key to the owning user.</summary>
    public int UserId { get; set; }

    /// <summary>The 64-byte random token value stored as a Base64 string.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>UTC timestamp after which this token is no longer valid.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC timestamp when the token was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the token was revoked (null if still active).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>The token that replaced this one during rotation.</summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>Navigation property to the associated user.</summary>
    public User User { get; set; } = null!;
}