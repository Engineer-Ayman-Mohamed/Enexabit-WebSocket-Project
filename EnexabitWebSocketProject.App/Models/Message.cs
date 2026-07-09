namespace EnexabitWebSocketProject.App.Models;

/// <summary>A single chat message posted to a channel.</summary>
public class Message
{
    /// <summary>Unique identifier for the message.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key to the channel this message belongs to.</summary>
    public int ChannelId { get; set; }

    /// <summary>Navigation property to the parent channel.</summary>
    public Channel Channel { get; set; } = null!;

    /// <summary>Display name of the user who sent the message.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Message body (HTML-stripped via Sanitizer).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the message was sent.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}