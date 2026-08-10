using EnexabitWebSocketProject.App.DTOs.Export;
using Hangfire;

namespace EnexabitWebSocketProject.App.Services.Export;

/// <summary>Entry point that Hangfire calls to execute export jobs.
/// Resolves ImportExportService from DI, generates the .xlsx, and stores the result
/// in ExportJobStore for the client to download.
///
/// Hangfire creates instances via ActivatorUtilities — constructor dependencies
/// must all be registered in DI.</summary>
public sealed class HangfireExportJobHandler
{
    private readonly ImportExportService _exportService;
    private readonly ExportJobStore _store;
    private readonly ILogger<HangfireExportJobHandler> _logger;

    public HangfireExportJobHandler(
        ImportExportService exportService,
        ExportJobStore store,
        ILogger<HangfireExportJobHandler> logger
    ) {
        _exportService = exportService;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire calls this method. The CancellationToken is automatically
    /// replaced by Hangfire's internal cancellation token (shutdown + job state changes).
    /// Type: "users" or "messages".
    /// </summary>
    /// <remarks>
    /// AutomaticRetry(Attempts = 1) means: 1 initial + 1 retry = 2 total attempts.
    /// If the job fails, Hangfire waits before retrying.
    /// </remarks>
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteExport(string jobId, string type, CancellationToken ct)
    {
        _logger.LogInformation("Hangfire executing export job {JobId} ({Type})", jobId, type);

        var job = _store.Get(jobId);
        if (job is null)
        {
            _logger.LogWarning("Export job {JobId} not found in store — possibly expired", jobId);
            return;
        }

        job.Status = ExportJobStatus.Processing;

        try
        {
            var bytes = type.ToLowerInvariant() switch
            {
                "users" => await _exportService.ExportUsersAsync(ct),
                "messages" => await _exportService.ExportMessagesAsync(ct),
                _ => throw new InvalidOperationException($"Unknown export type: {type}")
            };

            var fileName = type.ToLowerInvariant() switch
            {
                "users" => $"users_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx",
                "messages" => $"messages_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx",
                _ => "export.xlsx"
            };

            _store.SetResult(jobId, bytes, fileName);
            _logger.LogInformation("Export job {JobId} completed — {Size:N0} bytes", jobId, bytes.Length);
        }
        catch (Exception ex)
        {
            _store.SetFailed(jobId, ex.Message);
            _logger.LogError(ex, "Export job {JobId} failed", jobId);
            throw; //? if the job fails and hangfire see throw he retries again once
        }
    }
}