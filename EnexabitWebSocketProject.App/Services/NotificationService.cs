using System.Text.Json;
using System.Text.RegularExpressions;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Enums;
using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

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
    /// Creates a mention notification for a specific user.
    /// Checks user preferences before creating.
    /// </summary>
    /// <param name="userId">The mentioned user's ID.</param>
    /// <param name="channelId">The channel where the mention occurred.</param>
    /// <param name="channelName">The channel name.</param>
    /// <param name="senderName">Display name of the person who mentioned.</param>
    /// <param name="mainMessage">First 100 chars of the message.</param>
    /// <returns>The created notification, or null if disabled by user.</returns>
    public async Task<Notification?> MentionNotifyAsync(
        int userId,
        int channelId,
        string channelName,
        string senderName,
        string mainMessage
    ) {
        try
        {
            if (!await IsNotificationEnabledAsync(userId, NotificationTypes.MentionUser))
                return null;

            var title = $"{senderName} mentioned you in #{channelName}";
            var data = JsonSerializer.Serialize(new
            {
                channelId,
                channelName,
                senderName,
                mainMessage = mainMessage.Length > 100
                    ? mainMessage[..100] + "..."
                    : mainMessage
            });

            return await CreateNotificationAsync(userId, NotificationTypes.MentionUser, title, data);
        }
        catch (Exception)
        {
            return null;
        }

    }
    
    
    /// <summary>
    /// Creates channel update notifications for all members of a channel.
    /// </summary>
    /// <param name="channelId">The updated channel's ID.</param>
    /// <param name="channelName">The new channel name.</param>
    /// <param name="oldName">The previous channel name.</param>
    /// <param name="updatedBy">Display name of the admin who made the change.</param>
    /// <returns>List of created notifications.</returns>
    public async Task<List<Notification>> NotifyChannelUpdatesAsync(
        int channelId,
        string channelName,
        string oldName,
        string updatedBy
    ) {
        var notifications = new List<Notification>();
        try
        {
            var membersUserNames = await _context.Messages
                .Where(m => m.ChannelId == channelId)
                .Select(m => m.UserName)
                .Distinct()
                .ToListAsync();

            var usersToNotify = await _context.Users
                .Where(user => membersUserNames.Contains(user.Username))
                .ToListAsync();

            var title = oldName != channelName
                ? $"Channel #{oldName} renamed to #{channelName}"
                : $"Channel #{channelName} was updated";

            var data = JsonSerializer.Serialize(new
            {
                channelId,
                channelName,
                oldName,
                updatedBy
            });

            foreach (var user in usersToNotify)
            {
                if (!await IsNotificationEnabledAsync(user.Id, NotificationTypes.ChannelUpdate))
                    continue;

                var notification = await CreateNotificationAsync(
                    user.Id,
                    NotificationTypes.ChannelUpdate,
                    title,
                    data
                );

                if (notification != null)
                    notifications.Add(notification);
            }
        }
        catch (Exception)
        {
            // ignored
        }
        return notifications;
    }

    /// <summary>
    /// Creates a system-wide maintenance notification for all users.
    /// </summary>
    /// <param name="title">Short description of the maintenance.</param>
    /// <param name="reason">Detailed reason.</param>
    /// <param name="scheduledAt">When maintenance is scheduled.</param>
    /// <param name="duration">Estimated duration.</param>
    /// <param name="severity">Severity level.</param>
    /// <returns>The created notification.</returns>
    public async Task<Notification?> NotifySystemMaintenanceAsync(
        string title,
        string reason,
        DateTime scheduledAt,
        string duration,
        string severity
    ) {
        try
        {
            var data = JsonSerializer.Serialize(new
            {
                scheduledAt,
                duration,
                reason,
                severity
            });

            var notification = new Notification
            {
                UserId = 0,
                Type = NotificationTypes.SystemMaintenance,
                Title = title,
                Data = data,
                IsSystemWide = true,
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
    
    /// <summary>
    /// Creates a system-wide announcement notification for all users.
    /// </summary>
    /// <param name="message">The announcement message.</param>
    /// <param name="link">Optional link for more information.</param>
    /// <returns>The created notification.</returns>
    public async Task<Notification?> NotifySystemAnnouncementAsync(string message, string? link)
    {
        try
        {
            var data = JsonSerializer.Serialize(new
            {
                announcementText = message,
                link
            });

            var notification = new Notification
            {
                UserId = 0,
                Type = NotificationTypes.SystemAnnouncement,
                Title = message.Length > 200 ? message[..200] + "..." : message,
                Data = data,
                IsSystemWide = true,
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
    
    /// <summary>
    /// Extracts usernames from @mentions in a message.
    /// </summary>
    /// <param name="messageText">The message text to parse.</param>
    /// <returns>List of usernames found (without @ symbol).</returns>
    public static List<string> ExtractMentions(string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return [];

        var matches = _mentionRegex.Matches(messageText);
        return matches
            .Select(m => m.Groups[1].Value)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Finds users matching the mentioned usernames.
    /// </summary>
    /// <param name="usernames">List of usernames to look up.</param>
    /// <returns>List of matching users.</returns>
    public async Task<List<User>> FindUsersByUsernamesAsync(List<string> usernames)
    {
        if (usernames.Count == 0)
            return [];

        return await _context.Users
            .Where(u => usernames.Contains(u.Username))
            .ToListAsync();
    }
    
    /// <summary>
    /// Gets a paginated list of notifications for a user.
    /// Includes system-wide notifications.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page.</param>
    /// <returns>Paginated notification list.</returns>
    public async Task<NotificationListResponse> GetNotificationsAsync(
        int userId,
        int page = 1,
        int pageSize = 20
    ) {
        try
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId || (n.IsSystemWide && n.UserId == 0))
                .OrderByDescending(n => n.CreatedAt);

            var totalCount = await query.CountAsync();
            var unreadCount = await query.CountAsync(n => !n.IsRead);

            var notifications = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationResponse
                {
                    Id = n.Id,
                    Type = n.Type,
                    Title = n.Title,
                    Data = n.Data,
                    IsRead = n.IsRead,
                    IsAcknowledged = n.IsAcknowledged,
                    IsSystemWide = n.IsSystemWide,
                    CreatedAt = n.CreatedAt,
                    ReadAt = n.ReadAt
                })
                .ToListAsync();

            return new NotificationListResponse
            {
                Notifications = notifications,
                TotalCount = totalCount,
                UnreadCount = unreadCount,
                Page = page,
                PageSize = pageSize,
                HasMore = (page * pageSize) < totalCount
            };
        }
        catch (Exception)
        {
            return new NotificationListResponse
            {
                Notifications = [],
                TotalCount = 0,
                UnreadCount = 0,
                Page = page,
                PageSize = pageSize,
                HasMore = false
            };
        }
    }
    
    /// <summary>
    /// Gets the unread notification count for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Unread count.</returns>
    public async Task<int> GetUnreadCountAsync(int userId)
    {
        try
        {
            return await _context.Notifications
                .CountAsync(n => (n.UserId == userId || (n.IsSystemWide && n.UserId == 0)) && !n.IsRead);
        }
        catch (Exception)
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Marks a single notification as read.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="userId">The user ID (for ownership check).</param>
    /// <returns>True if successful, false if not found or unauthorized.</returns>
    public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId &&
                (n.UserId == userId || (n.IsSystemWide && n.UserId == 0)));

            if (notification == null)
                return false;

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Acknowledges a system maintenance notification.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> AcknowledgeNotificationAsync(int notificationId, int userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId &&
                (n.UserId == userId || (n.IsSystemWide && n.UserId == 0)));

            if (notification == null || notification.Type != NotificationTypes.SystemMaintenance)
                return false;

            notification.IsAcknowledged = true;
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Marks all notifications as read for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>Number of notifications marked as read.</returns>
    public async Task<int> MarkAllAsReadAsync(int userId)
    {
        try
        {
            var unread = await _context.Notifications
                .Where(n => (n.UserId == userId || (n.IsSystemWide && n.UserId == 0)) && !n.IsRead)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
            }

            await _context.SaveChangesAsync();
            return unread.Count;
        }
        catch (Exception)
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Deletes a single notification.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="userId">The user ID (for ownership check).</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> DeleteNotificationAsync(int notificationId, int userId)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId &&
                                          (n.UserId == userId || (n.IsSystemWide && n.UserId == 0)));

            if (notification == null)
                return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Gets all notification preferences for a user.
    /// Returns defaults if no preferences exist.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>List of preferences for all notification types.</returns>
    public async Task<List<NotificationPreferenceResponse>> GetPreferencesAsync(int userId)
    {
        try
        {
            var existing = await _context.NotificationPreferences
                .Where(np => np.UserId == userId)
                .ToListAsync();

            return Enum.GetValues<NotificationTypes>()
                .Select(type =>
                {
                    var pref = existing.FirstOrDefault(p => p.Type == type);
                    return new NotificationPreferenceResponse
                    {
                        Type = type,
                        IsEnabled = pref?.IsEnabled ?? true,
                        PlaySound = pref?.PlaySound ?? true,
                        ShowToast = pref?.ShowToast ?? true
                    };
                })
                .ToList();
        }
        catch (Exception)
        {
            return Enum.GetValues<NotificationTypes>()
                .Select(type => new NotificationPreferenceResponse
                {
                    Type = type,
                    IsEnabled = true,
                    PlaySound = true,
                    ShowToast = true
                })
                .ToList();
        }
    }
    
    /// <summary>
    /// Updates notification preferences for a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="preferences">List of preferences to update.</param>
    /// <returns>True if successful.</returns>
    public async Task<bool> UpdatePreferencesAsync(int userId, List<NotificationPreferenceResponse> preferences)
    {
        try
        {
            var existing = await _context.NotificationPreferences
                .Where(np => np.UserId == userId)
                .ToListAsync();

            foreach (var pref in preferences)
            {
                var existingPref = existing.FirstOrDefault(p => p.Type == pref.Type);

                if (existingPref != null)
                {
                    existingPref.IsEnabled = pref.IsEnabled;
                    existingPref.PlaySound = pref.PlaySound;
                    existingPref.ShowToast = pref.ShowToast;
                }
                else
                {
                    _context.NotificationPreferences.Add(new NotificationPreference
                    {
                        UserId = userId,
                        Type = pref.Type,
                        IsEnabled = pref.IsEnabled,
                        PlaySound = pref.PlaySound,
                        ShowToast = pref.ShowToast
                    });
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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