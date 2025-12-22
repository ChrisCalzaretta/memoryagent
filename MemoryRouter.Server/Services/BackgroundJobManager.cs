using System.Collections.Concurrent;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Manages background execution of long-running tasks
/// Allows immediate response to user while task executes in background
/// </summary>
public class BackgroundJobManager : IBackgroundJobManager
{
    private readonly ConcurrentDictionary<string, BackgroundJob> _jobs = new();
    private readonly ILogger<BackgroundJobManager> _logger;

    public BackgroundJobManager(ILogger<BackgroundJobManager> logger)
    {
        _logger = logger;
    }

    public string StartJob(string toolName, Func<CancellationToken, Task<object>> workload, long estimatedDurationMs)
    {
        var jobId = Guid.NewGuid().ToString();
        var job = new BackgroundJob
        {
            JobId = jobId,
            ToolName = toolName,
            Status = JobStatus.Running,
            StartedAt = DateTime.UtcNow,
            EstimatedDurationMs = estimatedDurationMs
        };

        _jobs[jobId] = job;

        _logger.LogInformation("🚀 Starting background job {JobId} for {Tool} (est. {Ms}ms)",
            jobId, toolName, estimatedDurationMs);

        // Fire and forget - run in background
        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource();
                var result = await workload(cts.Token);

                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.Result = result;
                job.ActualDurationMs = (long)(DateTime.UtcNow - job.StartedAt).TotalMilliseconds;

                _logger.LogInformation("✅ Background job {JobId} completed in {Ms}ms (estimated: {Est}ms)",
                    jobId, job.ActualDurationMs, estimatedDurationMs);
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.Error = ex.Message;
                job.ActualDurationMs = (long)(DateTime.UtcNow - job.StartedAt).TotalMilliseconds;

                _logger.LogError(ex, "❌ Background job {JobId} failed after {Ms}ms", 
                    jobId, job.ActualDurationMs);
            }
        });

        return jobId;
    }

    public BackgroundJob? GetJob(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return job;
    }

    public JobProgress GetProgress(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return new JobProgress
            {
                JobId = jobId,
                Status = JobStatus.NotFound,
                Message = "Job not found"
            };
        }

        var elapsed = (long)(DateTime.UtcNow - job.StartedAt).TotalMilliseconds;
        var progressPercent = job.EstimatedDurationMs > 0
            ? Math.Min((int)((elapsed / (double)job.EstimatedDurationMs) * 100), 99)
            : 0;

        return new JobProgress
        {
            JobId = jobId,
            Status = job.Status,
            ProgressPercent = job.Status == JobStatus.Completed ? 100 : progressPercent,
            ElapsedMs = elapsed,
            EstimatedRemainingMs = job.Status == JobStatus.Running
                ? Math.Max(0, job.EstimatedDurationMs - elapsed)
                : 0,
            Message = job.Status switch
            {
                JobStatus.Running => $"In progress... ({progressPercent}% estimated)",
                JobStatus.Completed => "Completed successfully",
                JobStatus.Failed => $"Failed: {job.Error}",
                _ => "Unknown status"
            },
            Result = job.Result,
            Error = job.Error
        };
    }

    public void CleanupOldJobs(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        var oldJobs = _jobs.Where(kvp => 
            kvp.Value.CompletedAt.HasValue && 
            kvp.Value.CompletedAt.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var jobId in oldJobs)
        {
            _jobs.TryRemove(jobId, out _);
        }

        if (oldJobs.Any())
        {
            _logger.LogDebug("🧹 Cleaned up {Count} old jobs", oldJobs.Count);
        }
    }
}

public interface IBackgroundJobManager
{
    string StartJob(string toolName, Func<CancellationToken, Task<object>> workload, long estimatedDurationMs);
    BackgroundJob? GetJob(string jobId);
    JobProgress GetProgress(string jobId);
    void CleanupOldJobs(TimeSpan maxAge);
}

public class BackgroundJob
{
    public required string JobId { get; set; }
    public required string ToolName { get; set; }
    public JobStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long EstimatedDurationMs { get; set; }
    public long ActualDurationMs { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}

public class JobProgress
{
    public required string JobId { get; set; }
    public JobStatus Status { get; set; }
    public int ProgressPercent { get; set; }
    public long ElapsedMs { get; set; }
    public long EstimatedRemainingMs { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Result { get; set; }
    public string? Error { get; set; }
}

public enum JobStatus
{
    NotFound,
    Running,
    Completed,
    Failed,
    Cancelled
}





