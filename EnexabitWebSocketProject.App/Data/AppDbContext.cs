using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Message> Messages => Set<Message>();

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
    
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