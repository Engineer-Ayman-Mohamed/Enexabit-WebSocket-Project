using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Services;

/// <summary>Persistence and retrieval operations for chat messages with XSS sanitization.</summary>
public class MessageServices
{
    private readonly AppDbContext _db;

    /// <param name="db">Database context for message persistence.</param>
    public MessageServices(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the 50 most recent messages for a given channel, ordered oldest-first.</summary>
    /// <param name="channelId">The channel to fetch messages for.</param>
    /// <returns>A list of up to 50 messages.</returns>
    public async Task<List<Message>> GetRecentMessagesAsync(int channelId)
    {
        return await _db.Messages
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Checks whether a channel with the given ID exists.</summary>
    /// <param name="channelId">The channel ID to look up.</param>
    /// <returns><c>true</c> if the channel exists; otherwise <c>false</c>.</returns>
    public async Task<bool> ChannelExistsAsync(int channelId)
    {
        return await _db.Channels.AnyAsync(c => c.Id == channelId);
    }

    /// <summary>Sanitizes message text via <see cref="Sanitizer.StripHtml"/> and persists it.</summary>
    /// <param name="channelId">The channel to post in.</param>
    /// <param name="userName">Display name of the sender.</param>
    /// <param name="text">Raw message text (HTML tags are stripped).</param>
    /// <returns>The saved message entity with a generated Id and CreatedAt timestamp.</returns>
    public async Task<Message> SaveMessageAsync(int channelId, string userName, string text)
    {
        var cleanText = Sanitizer.StripHtml(text);

        var message = new Message
        {
            ChannelId = channelId,
            UserName = userName,
            Text = cleanText,
            CreatedAt = DateTime.UtcNow
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }
}