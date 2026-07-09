using System;
using System.Linq;
using System.Threading.Tasks;
using EnexabitWebSocketProject.App.Models;

namespace EnexabitWebSocketProject.App.Data;

/// <summary>Seeds the database with test users on first run (idempotent).</summary>
public static class DbInitializer
{
    /// <summary>Seeds two test users (alice, bob) with BCrypt-hashed passwords if no users exist.</summary>
    /// <param name="db">The database context.</param>
    public static async Task SeedAsync(AppDbContext db)
    {
        if (db.Users.Any()) return;

        db.Users.AddRange(
            new User
            {
                Username = "alice",
                DisplayName = "Alice",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "bob",
                DisplayName = "Bob",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                CreatedAt = DateTime.UtcNow
            }
        );

        await db.SaveChangesAsync();
    }
}