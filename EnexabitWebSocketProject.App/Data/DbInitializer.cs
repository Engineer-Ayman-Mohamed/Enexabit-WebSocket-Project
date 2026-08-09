using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Models;

/// <summary>Seeds the database with test users on first run (idempotent).</summary>
public static class DbInitializer
{
    /// <summary>Seeds an admin and 9 test users with BCrypt-hashed passwords if no users exist.</summary>
    /// <param name="db">The database context.</param>
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!db.Users.Any(u => u.Role == "admin"))
        {
            db.Users.Add(
            new User {
                Username = "admin",
                DisplayName = "Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "admin",
                CreatedAt = DateTime.UtcNow
            }
        );
            await db.SaveChangesAsync();
        }

        if (db.Users.Any(user => user.Username == "charlie")) return;

        db.Users.AddRange(
            new User
            {
                Username = "alice",
                DisplayName = "Alice Johnson",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "bob",
                DisplayName = "Bob Smith",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "charlie",
                DisplayName = "Charlie Brown",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "diana",
                DisplayName = "Diana Prince",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "eve",
                DisplayName = "Eve Williams",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "frank",
                DisplayName = "Frank Castle",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "grace",
                DisplayName = "Grace Hopper",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "henry",
                DisplayName = "Henry Carter",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Username = "iris",
                DisplayName = "Iris Chen",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                Role = "user",
                CreatedAt = DateTime.UtcNow
            }
        );

        await db.SaveChangesAsync();
    }
}