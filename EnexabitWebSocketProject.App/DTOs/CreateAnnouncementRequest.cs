using System.ComponentModel.DataAnnotations;

namespace EnexabitWebSocketProject.App.DTOs;


/// <summary>Request body for POST /api/admin/notifications/announce.</summary>
public class CreateAnnouncementRequest
{
    /// <summary>The announcement message.</summary>
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 1000 characters")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional link for more information.</summary>
    [StringLength(500, ErrorMessage = "Link must not exceed 500 characters")]
    public string? Link { get; set; }

}