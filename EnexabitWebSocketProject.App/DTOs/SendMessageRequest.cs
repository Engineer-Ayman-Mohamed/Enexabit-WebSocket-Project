using System.ComponentModel.DataAnnotations;

namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/channels/{id}/messages.</summary>
public class SendMessageRequest
{
    /// <summary>The message text to send. HTML tags are stripped server-side.</summary>
    [StringLength(4000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 4000 characters")]
    public string Text { get; set; } = string.Empty;
}
