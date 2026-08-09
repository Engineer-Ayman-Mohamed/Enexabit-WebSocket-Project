using EnexabitWebSocketProject.App.Enums;

namespace EnexabitWebSocketProject.App.Models;

/// <summary>
/// Stores a user's preferences for each notification type.
/// Controls whether notifications are enabled, play sound, or show toast.
/// </summary>

public class NotificationPreference
{
    /// <summary>Unique identifier for the preference record.</summary>
    public int Id { get; set; }

    /// <summary>Foreign key to the user.</summary>
    public int UserId { get; set; }

    /// <summary>The notification type this preference applies to.</summary>
    public NotificationTypes Type { get; set; }

    /// <summary>Whether this notification type is enabled for the user.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether to play a sound when this notification arrives.</summary>
    public bool PlaySound { get; set; } = true;

    /// <summary>Whether to show a toast/snackbar for this notification.</summary>
    public bool ShowToast { get; set; } = true;

    /// <summary>Navigation property to the user.</summary>
    public User User { get; set; } = null!;
}