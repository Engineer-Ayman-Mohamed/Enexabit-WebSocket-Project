namespace EnexabitWebSocketProject.App.DTOs.Export;

/// <summary>Lifecycle states of a background export job.</summary>
public enum ExportJobStatus
{
    /// <summary>Job created, waiting to be picked up by Hangfire.</summary>
    Pending,

    /// <summary>Hangfire is executing the export.</summary>
    Processing,

    /// <summary>Export completed. Result bytes are ready for download.</summary>
    Completed,

    /// <summary>Export failed. See Error message for details.</summary>
    Failed
}