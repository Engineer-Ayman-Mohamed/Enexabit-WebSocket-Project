using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Services;

public class AuthService
{
    private readonly AppDbContext _db;

    public AuthService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;
        return user;
    }

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
