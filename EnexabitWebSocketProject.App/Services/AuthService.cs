using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Services;

/// <summary>Handles user registration and authentication using BCrypt password hashing.</summary>
public class AuthService
{
    private readonly AppDbContext _db;

    /// <param name="db">Database context for user persistence.</param>
    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Validates credentials and returns the matching user.</summary>
    /// <param name="username">The username to look up.</param>
    /// <param name="password">Plain-text password to verify against the stored BCrypt hash.</param>
    /// <returns>The authenticated user, or <c>null</c> if credentials are invalid.</returns>
    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;
        return user;
    }

    /// <summary>Creates a new user account with a BCrypt-hashed password.</summary>
    /// <param name="username">Unique username.</param>
    /// <param name="password">Plain-text password (hashed before storage).</param>
    /// <param name="displayName">Display name shown in chat. Falls back to <paramref name="username"/> if empty.</param>
    /// <returns>A tuple: the created user and null on success; null and an error message on failure.</returns>
    public async Task<(User? user, string? error)> RegisterAsync(string username, string password, string displayName)
    {
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return (null, "Username is already taken");

        var user = new User
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return (user, null);
    }
}
