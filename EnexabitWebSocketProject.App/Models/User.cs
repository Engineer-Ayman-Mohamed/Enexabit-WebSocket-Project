using System;
using System.Collections.Generic;

namespace EnexabitWebSocketProject.App.Models;

/// <summary>Represents a registered chat user account.</summary>
public class User
{
    /// <summary>Unique identifier for the user.</summary>
    public int Id { get; set; }

    /// <summary>Unique login username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the user's password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Display name shown in chat messages.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Refresh tokens associated with this user.</summary>
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}