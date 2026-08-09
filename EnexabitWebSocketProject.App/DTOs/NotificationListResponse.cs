namespace EnexabitWebSocketProject.App.DTOs;


/// <summary>Paginated response for notification list.</summary>
public class NotificationListResponse
{
    /// <summary>List of notifications.</summary>
    public List<NotificationResponse> Notifications { get; set; } = [];

    /// <summary>Total count of notifications for the user.</summary>
    public int TotalCount { get; set; }

    /// <summary>Number of unread notifications.</summary>
    public int UnreadCount { get; set; }

    /// <summary>Current page number (1-based).</summary>
    public int Page { get; set; }

    /// <summary>Page size.</summary>
    public int PageSize { get; set; }

    /// <summary>Whether there are more pages available.</summary>
    public bool HasMore { get; set; }
}