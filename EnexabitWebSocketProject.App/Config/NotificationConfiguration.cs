using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnexabitWebSocketProject.App.Config;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Data)
            .HasMaxLength(1000);

        builder.Property(n => n.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.IsRead)
            .HasDefaultValue(false);

        builder.Property(n => n.IsAcknowledged)
            .HasDefaultValue(false);

        builder.Property(n => n.IsSystemWide)
            .HasDefaultValue(false);

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => n.CreatedAt);
        builder.HasIndex(n => new { n.UserId, n.IsRead });

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}