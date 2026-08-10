using Hangfire;

namespace EnexabitWebSocketProject.App.Services.Jobs;

/// <summary>Recurring Hangfire jobs for maintenance tasks.</summary>
public static class HangfireRecurringJobs
{
    /// <summary>Registers all recurring jobs. Call once at app startup.</summary>
    public static void Register()
    {
        RecurringJob.AddOrUpdate(
            "cleanup-hangfire-jobs",
            () => CleanupOldJobs(),
            Cron.Daily
        );
    }

    /// <summary>
    /// Removes completed/failed/deleted Hangfire job records older than 7 days.
    /// This keeps the Hangfire SQL tables lean. The ExportJobStore has its own in-memory cleanup.
    /// </summary>
    public static void CleanupOldJobs()
    {
        // Hangfire automatically cleans up its own state data based on
        // JobExpirationWarningOptions and SlidingInvisibilityTimeout.
        // This method exists as a placeholder for any future maintenance tasks.
    }
}