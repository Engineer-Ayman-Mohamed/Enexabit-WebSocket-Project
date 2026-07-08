using EnexabitWebSocketProject.App.Models;

namespace EnexabitWebSocketProject.App.Data;

public static class DbInitializer
{
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