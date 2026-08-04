namespace EnexabitWebSocketProject.App.Enums;

public enum NotificationTypes
{
    /// <summary>User was @mentioned in a channel message.</summary>
    MentionUser = 0,

    /// <summary>Channel name or settings were updated.</summary>
    ChannelUpdate = 1,

    /// <summary>System maintenance is scheduled.</summary>
    SystemMaintenance = 2,

    /// <summary>System-wide announcement from admin.</summary>
    SystemAnnouncement = 3
}