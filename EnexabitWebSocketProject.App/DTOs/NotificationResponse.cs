using EnexabitWebSocketProject.App.Enums;

namespace EnexabitWebSocketProject.App.DTOs;


/// <summary>Response returned when fetching a single notification.</summary>
public class NotificationResponse
{
    /// <summary>Unique identifier for the notification.</summary>
    public int Id { get; set; }

    /// <summary>The type of notification.</summary>
    public NotificationTypes Type { get; set; }

    /// <summary>Short description of the notification.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>JSON payload with additional data.</summary>
    public string? Data { get; set; }

    /// <summary>Whether the user has read this notification.</summary>
    public bool IsRead { get; set; }

    /// <summary>Whether a system maintenance notification has been acknowledged.</summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>Whether this notification is visible to all users.</summary>
    public bool IsSystemWide { get; set; }

    /// <summary>UTC timestamp when the notification was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp when the notification was read.</summary>
    public DateTime? ReadAt { get; set; }
}