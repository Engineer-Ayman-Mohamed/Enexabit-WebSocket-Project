using EnexabitWebSocketProject.App.DTOs.Export;

namespace EnexabitWebSocketProject.App.Services.Export;

/// <summary>
/// Represents an export job and its current state.
/// Stored in-memory for fast status polling. The actual result bytes
/// live in ExportJobStore._results and are cleaned up after download.
/// </summary>
public sealed class ExportJob
{
    /// <summary>Unique job identifier — used as the Hangfire job ID.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Export type: "users" or "messages".</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Current lifecycle state. Typed enum instead of raw string.</summary>
    public ExportJobStatus Status { get; set; } = ExportJobStatus.Pending;

    /// <summary>File name for the download response (set on completion).</summary>
    public string? FileName { get; set; }

    /// <summary>Error message if the job failed (null on success).</summary>
    public string? Error { get; set; }

    /// <summary>UTC timestamp when the job was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the job completed or failed (null while running).</summary>
    public DateTime? CompletedAt { get; set; }
}