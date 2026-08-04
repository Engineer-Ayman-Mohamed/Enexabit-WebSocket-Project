using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Data;

/// <summary>Entity Framework Core database context for the chat application.</summary>
public class AppDbContext : DbContext
{
    /// <summary>Registered user accounts.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Refresh tokens for JWT rotation and theft detection.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Chat channels (seeded with 5 defaults: general, random, tech, support, off-topic).</summary>
    public DbSet<Channel> Channels => Set<Channel>();

    /// <summary>Chat messages posted to channels.</summary>
    public DbSet<Message> Messages => Set<Message>();
    
    /// <summary>Notifications sent to users.</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>User notification preferences by type.</summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    /// <param name="options">DbContext options configured in <c>Program.cs</c> for SQL Server.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    /// <summary>Discovers and applies all IEntityTypeConfiguration implementations from the assembly.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}