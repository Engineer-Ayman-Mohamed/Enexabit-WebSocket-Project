namespace EnexabitWebSocketProject.App.Models;

/// <summary>Represents a named chat channel (e.g. general, tech, random).</summary>
public class Channel
{
    /// <summary>Unique identifier for the channel.</summary>
    public int Id { get; set; }

    /// <summary>Display name of the channel (lowercase, no spaces).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Messages posted in this channel.</summary>
    public List<Message> Messages { get; set; } = [];
}