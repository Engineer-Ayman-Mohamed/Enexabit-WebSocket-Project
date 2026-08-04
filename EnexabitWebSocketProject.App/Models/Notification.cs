using EnexabitWebSocketProject.App.Enums;

namespace EnexabitWebSocketProject.App.Models;

/// <summary>
/// Represents a notification sent to a user.
/// Supports both user-specific and system-wide notifications.
/// </summary>

public class Notification
{
    /// <summary>Unique identifier for the notification.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key to the target user. 0 for system-wide notifications.</summary>
    public int UserId { get; set; }

    /// <summary>The type of notification (mention, channel update, etc.).</summary>
    public NotificationTypes Type { get; set; }

    /// <summary>Short description of the notification (max 200 chars).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>JSON payload with additional data (max 1000 chars).</summary>
    public string? Data { get; set; }

    /// <summary>Whether the user has read this notification.</summary>
    public bool IsRead { get; set; }

    /// <summary>Whether a system maintenance notification has been acknowledged.</summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>Whether this notification is visible to all users.</summary>
    public bool IsSystemWide { get; set; }

    /// <summary>UTC timestamp when the notification was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the notification was read (null if unread).</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>Optional UTC timestamp when the notification expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Navigation property to the target user.</summary>
    public User User { get; set; } = null!;
}