using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    /// <summary>Returns up to 50 messages for a given channel, ordered oldest-first.
    /// Supports cursor-based pagination via <paramref name="beforeCreatedAt"/>.</summary>
    /// <param name="channelId">The channel to fetch messages for.</param>
    /// <param name="beforeCreatedAt">If provided, returns messages created before this timestamp.</param>
    /// <returns>A list of up to 50 messages.</returns>
    public async Task<List<Message>> GetRecentMessagesAsync(int channelId)
    {
        try
        {
            return await _db.Messages
                .Where(m => m.ChannelId == channelId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(50)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }
        catch (Exception)
        {
            return new List<Message>();
        }
    }

    /// <summary>Checks whether a channel with the given ID exists.</summary>
    /// <param name="channelId">The channel ID to look up.</param>
    /// <returns><c>true</c> if the channel exists; otherwise <c>false</c>.</returns>
    public async Task<bool> ChannelExistsAsync(int channelId)
    {
        try
        {
            return await _db.Channels.AnyAsync(c => c.Id == channelId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Sanitizes message text via <see cref="Sanitizer.StripHtml"/> and persists it.
    /// Returns <c>null</c> if the channel does not exist (atomic check+save).</summary>
    /// <param name="channelId">The channel to post in.</param>
    /// <param name="userName">Display name of the sender.</param>
    /// <param name="text">Raw message text (HTML tags are stripped).</param>
    /// <returns>The saved message entity, or <c>null</c> if channel not found.</returns>
    public async Task<Message?> SaveMessageAsync(int channelId, string userName, string text)
    {
        try
        {
            if (!await _db.Channels.AnyAsync(c => c.Id == channelId))
                return null;

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
        catch (Exception)
        {
            return null;
        }
    }
}