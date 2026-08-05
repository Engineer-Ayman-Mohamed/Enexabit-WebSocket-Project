using System.Text.RegularExpressions;
using EnexabitWebSocketProject.App.Data;

namespace EnexabitWebSocketProject.App.Services;

public class NotificationService
{
    private readonly AppDbContext _context;
    
    /// <summary>Regex pattern for matching @mentions in messages.</summary>
    private static readonly Regex _mentionRegex = new(
        @"@(\w+)",
        RegexOptions.Compiled | RegexOptions.NonBacktracking
    );
    
    public NotificationService(AppDbContext context)
    {
        _context = context;
    }
    /// <summary>
    /// Checks if a notification type is enabled for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="type">The notification type.</param>
    /// <returns>True if enabled (default: true).</returns>
    private async Task<bool> IsNotificationEnabledAsync(int userId, NotificationTypes type)
    {
        try
        {
            var pref = await _context.NotificationPreferences
                .FirstOrDefaultAsync(np => np.UserId == userId && np.Type == type);

            return pref?.IsEnabled ?? true;
        }
        catch (Exception)
        {
            return true;
        }
    }
    
    /// <summary>
    /// Creates a notification in the database.
    /// </summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="type">Notification type.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="data">JSON data payload.</param>
    /// <returns>The created notification, or null on failure.</returns>
    private async Task<Notification?> CreateNotificationAsync(
        int userId,
        NotificationTypes type,
        string title,
        string data
    ) {
        try
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Data = data,
                IsRead = false,
                IsSystemWide = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            return notification;
        }
        catch (Exception)
        {
            return null;
        }
    }
}