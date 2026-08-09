using System.ComponentModel.DataAnnotations;

namespace EnexabitWebSocketProject.App.DTOs;

/// <summary>Request body for POST /api/admin/notifications/maintenance.</summary>
public class CreateMaintainanceNotificationRequest
{
    /// <summary>Short description of the maintenance.</summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed reason for the maintenance.</summary>
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 1000 characters")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>UTC timestamp when maintenance is scheduled.</summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>Estimated duration (e.g., "30 minutes", "2 hours").</summary>
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Duration must be between 1 and 100 characters")]
    public string Duration { get; set; } = string.Empty;

    /// <summary>Severity level: low, medium, high, critical.</summary>
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Severity must be between 1 and 20 characters")]
    public string Severity { get; set; } = "medium";
}