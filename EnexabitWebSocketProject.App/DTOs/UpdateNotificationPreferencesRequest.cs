namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for updating notification preferences.</summary>
public class UpdateNotificationPreferencesRequest
{
    /// <summary>List of preferences to update.</summary>
    public List<NotificationPreferenceResponse> Preferences { get; set; } = [];
}