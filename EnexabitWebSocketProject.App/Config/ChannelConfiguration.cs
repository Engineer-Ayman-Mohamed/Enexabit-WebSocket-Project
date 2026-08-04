using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnexabitWebSocketProject.App.Config;

public class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.Name)
            .IsUnique();

        builder.HasData(
            new Channel { Id = 1, Name = "general" },
            new Channel { Id = 2, Name = "random" },
            new Channel { Id = 3, Name = "tech" },
            new Channel { Id = 4, Name = "support" },
            new Channel { Id = 5, Name = "off-topic" }
        );
    }
}
