using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Services;

public class MessageServices
{
    private readonly AppDbContext _db;

    public MessageServices(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Message>> GetRecentMessagesAsync(int channelId)
    {
        return await _db.Messages
            .Where(m => m.ChannelId == channelId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

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