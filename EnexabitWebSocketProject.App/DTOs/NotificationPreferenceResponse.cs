using EnexabitWebSocketProject.App.Enums;

namespace EnexabitWebSocketProject.App.DTOs;


/// <summary>Response for a single notification preference.</summary>
public class NotificationPreferenceResponse
{
    /// <summary>The notification type this preference applies to.</summary>
    public NotificationTypes Type { get; set; }

    /// <summary>Whether this notification type is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Whether to play a sound.</summary>
    public bool PlaySound { get; set; }

    /// <summary>Whether to show a toast.</summary>
    public bool ShowToast { get; set; }
}