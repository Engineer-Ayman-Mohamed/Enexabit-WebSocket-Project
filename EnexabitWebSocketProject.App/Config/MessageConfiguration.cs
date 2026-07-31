using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnexabitWebSocketProject.App.Config;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Text)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.HasOne(m => m.Channel)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
