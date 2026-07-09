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

    /// <param name="options">DbContext options configured in <c>Program.cs</c> for SQL Server.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    /// <summary>Configures entity relationships and seeds initial channel data.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Channel)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Channel>().HasData(
            new Channel { Id = 1, Name = "general" },
            new Channel { Id = 2, Name = "random" },
            new Channel { Id = 3, Name = "tech" },
            new Channel { Id = 4, Name = "support" },
            new Channel { Id = 5, Name = "off-topic" }
        );
    }
}