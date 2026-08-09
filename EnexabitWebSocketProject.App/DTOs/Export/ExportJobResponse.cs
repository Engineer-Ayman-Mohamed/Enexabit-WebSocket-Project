namespace EnexabitWebSocketProject.App.DTOs.Export;

/// <summary>
/// Response for a queued export job — used for status polling.
/// Uses typed ExportJobStatus enum instead of raw string.
/// </summary>
public record ExportJobResponse(
    string JobId,
    ExportJobStatus Status,
    string? FileName,
    string? Error,
    DateTime CreatedAt,
    DateTime? CompletedAt = null
);
