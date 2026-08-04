using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnexabitWebSocketProject.App.Config;

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasKey(np => np.Id);

        builder.Property(np => np.IsEnabled)
            .HasDefaultValue(true);

        builder.Property(np => np.PlaySound)
            .HasDefaultValue(true);

        builder.Property(np => np.ShowToast)
            .HasDefaultValue(true);

        builder.HasIndex(np => new { np.UserId, np.Type })
            .IsUnique();

        builder.HasOne(np => np.User)
            .WithMany()
            .HasForeignKey(np => np.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}