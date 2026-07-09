namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/channels/{id}/messages.</summary>
public class SendMessageRequest
{
    /// <summary>The message text to send. HTML tags are stripped server-side.</summary>
    public string Text { get; set; } = string.Empty;
}
