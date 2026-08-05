namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Response for unread count endpoint.</summary>
public class UnreadNotificationsCountResponse
{
    /// <summary>Number of unread notifications.</summary>
    public int Count { get; set; }
}