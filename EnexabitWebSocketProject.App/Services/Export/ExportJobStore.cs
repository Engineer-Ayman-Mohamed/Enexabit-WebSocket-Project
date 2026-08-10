using System.Collections.Concurrent;
using EnexabitWebSocketProject.App.DTOs.Export;

namespace EnexabitWebSocketProject.App.Services.Export;

/// <summary>
/// Thread-safe in-memory store for tracking export jobs and their result bytes.
/// Hangfire handles persistence and retry — this store only holds the .xlsx bytes
/// temporarily until the client downloads them.
/// 
/// Memory safety: results are cleaned up (1) after download via RemoveResult(),
/// (2) when the job expires after 1 hour, (3) on every Get() call.
/// </summary>
public sealed class ExportJobStore
{
    private readonly ConcurrentDictionary<string, ExportJob> _jobs = new();
    private readonly ConcurrentDictionary<string, byte[]> _results = new();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(1); //? (time to live)
    private DateTime _lastCleanup = DateTime.UtcNow;

    /// <summary>Registers a new job for tracking.</summary>
    public void Add(ExportJob job)
    {
        MaybeCleanExpired();
        _jobs[job.Id] = job;
    }

    /// <summary>
    /// Gets a job by ID. Returns null if not found or expired.
    /// Also triggers periodic cleanup of expired jobs.
    /// </summary>
    public ExportJob? Get(string jobId)
    {
        MaybeCleanExpired();
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    /// <summary>
    /// Stores the result bytes for a completed job.
    /// The bytes are held in memory until the client downloads and calls RemoveResult().
    /// </summary>
    public void SetResult(string jobId, byte[] result, string fileName)
    {
        _results[jobId] = result;
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = ExportJobStatus.Completed;
            job.FileName = fileName;
            job.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Marks a job as failed with an error message.</summary>
    public void SetFailed(string jobId, string error)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = ExportJobStatus.Failed;
            job.Error = error;
            job.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Gets the result bytes for a completed job.</summary>
    public byte[]? GetResult(string jobId)
    {
        _results.TryGetValue(jobId, out var result);
        return result;
    }

    /// <summary>
    /// Frees the result bytes from memory after the client has downloaded the file.
    /// Call this immediately after returning the file response.
    /// </summary>
    public void RemoveResult(string jobId)
    {
        _results.TryRemove(jobId, out _);
    }

    /// <summary>
    /// Removes expired jobs and results to prevent memory leaks.
    /// Runs at most once per minute to avoid excessive iteration.
    /// </summary>
    private void MaybeCleanExpired()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(1))
            return;

        _lastCleanup = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow - _ttl;

        foreach (var kvp in _jobs)
        {
            if (kvp.Value.CreatedAt < cutoff)
            {
                _jobs.TryRemove(kvp.Key, out _);
                _results.TryRemove(kvp.Key, out _);
            }
        }
    }
}