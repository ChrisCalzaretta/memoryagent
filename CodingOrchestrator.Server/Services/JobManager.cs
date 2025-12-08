using System.Collections.Concurrent;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Manages background jobs for coding tasks with disk persistence
/// </summary>
public class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<string, JobState> _jobs = new();
    private readonly ITaskOrchestrator _orchestrator;
    private readonly IJobPersistenceService _persistence;
    private readonly ILogger<JobManager> _logger;
    private bool _initialized;

    public JobManager(
        ITaskOrchestrator orchestrator, 
        IJobPersistenceService persistence,
        ILogger<JobManager> logger)
    {
        _orchestrator = orchestrator;
        _persistence = persistence;
        _logger = logger;
    }

    /// <summary>
    /// Initialize by loading persisted jobs from disk
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        
        try
        {
            var persistedJobs = await _persistence.LoadAllJobsAsync();
            var recoveredCount = 0;
            var markedFailedCount = 0;

            foreach (var job in persistedJobs)
            {
                // Recover jobs into memory
                var jobState = new JobState
                {
                    JobId = job.JobId,
                    Request = job.Request,
                    Status = job.Status,
                    CancellationTokenSource = new CancellationTokenSource(),
                    StartedAt = job.StartedAt
                };

                // If job was running when container restarted, mark it as failed
                if (job.Status.Status is TaskState.Running or TaskState.Queued)
                {
                    jobState.Status.Status = TaskState.Failed;
                    jobState.Status.Error = new TaskError
                    {
                        Type = "container_restart",
                        Message = $"Job was interrupted by container restart at {job.LastUpdatedAt:u}. Progress was {job.Status.Progress}% in phase '{job.Status.CurrentPhase}'.",
                        CanRetry = true
                    };
                    jobState.Status.CurrentPhase = "Interrupted";
                    
                    // Persist the updated status
                    await PersistJobAsync(jobState);
                    markedFailedCount++;
                    
                    _logger.LogWarning(
                        "Job {JobId} was running when container restarted. Marked as failed. " +
                        "Task: {Task}, Progress: {Progress}%, Phase: {Phase}",
                        job.JobId, job.Request.Task, job.Status.Progress, job.Status.CurrentPhase);
                }
                else
                {
                    recoveredCount++;
                }

                _jobs[job.JobId] = jobState;
            }

            _logger.LogInformation(
                "Job recovery complete: {Total} jobs loaded, {Recovered} completed/failed, {Interrupted} marked as interrupted",
                _jobs.Count, recoveredCount, markedFailedCount);

            // Cleanup old completed jobs (older than 7 days)
            await _persistence.CleanupOldJobsAsync(TimeSpan.FromDays(7));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize job manager from persisted state");
        }

        _initialized = true;
    }

    public async Task<string> StartJobAsync(OrchestrateTaskRequest request, CancellationToken cancellationToken)
    {
        // Ensure we're initialized
        await InitializeAsync();
        
        var jobId = GenerateJobId();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var jobState = new JobState
        {
            JobId = jobId,
            Request = request,
            Status = new TaskStatusResponse
            {
                JobId = jobId,
                Status = TaskState.Queued,
                Progress = 0,
                CurrentPhase = "Queued",
                MaxIterations = request.MaxIterations,
                Timeline = new List<PhaseInfo>()
            },
            CancellationTokenSource = cts,
            StartedAt = DateTime.UtcNow
        };

        _jobs[jobId] = jobState;
        
        // Persist immediately
        await PersistJobAsync(jobState);
        
        _logger.LogInformation("Job {JobId} created for task: {Task}", jobId, request.Task);

        // Start the job in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await UpdateJobStatusAsync(jobId, TaskState.Running, 5, "Starting");
                
                var result = await _orchestrator.ExecuteTaskAsync(request, jobId, cts.Token);
                
                if (result.Status == TaskState.Complete)
                {
                    await CompleteJobAsync(jobId, result.Result!);
                }
                else if (result.Status == TaskState.Failed)
                {
                    // Validation failed but code was generated - include partial result
                    if (result.Error != null)
                    {
                        await FailJobAsync(jobId, result.Error);
                    }
                    else
                    {
                        // Validation didn't pass but we have a result
                        await FailJobAsync(jobId, new TaskError
                        {
                            Type = "validation_failed",
                            Message = result.Result?.Summary ?? $"Validation score {result.Result?.ValidationScore}/10 did not meet minimum threshold",
                            CanRetry = true,
                            PartialResult = result.Result
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Job {JobId} was cancelled", jobId);
                await UpdateJobStatusAsync(jobId, TaskState.Cancelled, 0, "Cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed with exception", jobId);
                await FailJobAsync(jobId, new TaskError
                {
                    Type = "exception",
                    Message = ex.Message,
                    CanRetry = true
                });
            }
        }, cts.Token);

        return jobId;
    }

    public TaskStatusResponse? GetJobStatus(string jobId)
    {
        // Ensure we're initialized (loads persisted jobs from disk)
        if (!_initialized)
        {
            InitializeAsync().GetAwaiter().GetResult();
        }
        
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            // Calculate duration
            if (jobState.Status.Result != null)
            {
                jobState.Status.Result.TotalDurationMs = (long)(DateTime.UtcNow - jobState.StartedAt).TotalMilliseconds;
            }
            return jobState.Status;
        }
        return null;
    }

    public bool CancelJob(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            if (jobState.Status.Status == TaskState.Running || jobState.Status.Status == TaskState.Queued)
            {
                jobState.CancellationTokenSource.Cancel();
                UpdateJobStatus(jobId, TaskState.Cancelled, 0, "Cancelled by user");
                return true;
            }
        }
        return false;
    }

    public IEnumerable<TaskStatusResponse> GetAllJobs()
    {
        // Ensure we're initialized (loads persisted jobs from disk)
        if (!_initialized)
        {
            InitializeAsync().GetAwaiter().GetResult();
        }
        
        return _jobs.Values.Select(j => j.Status).ToList();
    }

    public void UpdateJobStatus(string jobId, TaskState status, int progress, string? phase = null, int iteration = 0)
    {
        // Fire and forget the async persistence
        _ = UpdateJobStatusAsync(jobId, status, progress, phase, iteration);
    }

    private async Task UpdateJobStatusAsync(string jobId, TaskState status, int progress, string? phase = null, int iteration = 0)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            jobState.Status.Status = status;
            jobState.Status.Progress = progress;
            jobState.Status.CurrentPhase = phase;
            jobState.Status.Iteration = iteration;
            
            _logger.LogDebug("Job {JobId} updated: {Status}, {Progress}%, Phase: {Phase}", 
                jobId, status, progress, phase);

            // Persist the update
            await PersistJobAsync(jobState);
        }
    }

    public void CompleteJob(string jobId, TaskResult result)
    {
        _ = CompleteJobAsync(jobId, result);
    }

    private async Task CompleteJobAsync(string jobId, TaskResult result)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            result.TotalDurationMs = (long)(DateTime.UtcNow - jobState.StartedAt).TotalMilliseconds;
            
            jobState.Status.Status = TaskState.Complete;
            jobState.Status.Progress = 100;
            jobState.Status.CurrentPhase = "Complete";
            jobState.Status.Result = result;
            jobState.Status.Message = result.Summary;
            
            _logger.LogInformation("Job {JobId} completed successfully. Score: {Score}, Files: {FileCount}", 
                jobId, result.ValidationScore, result.Files.Count);

            // Persist completion
            await PersistJobAsync(jobState, completed: true);
        }
    }

    public void FailJob(string jobId, TaskError error)
    {
        _ = FailJobAsync(jobId, error);
    }

    private async Task FailJobAsync(string jobId, TaskError error)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            jobState.Status.Status = TaskState.Failed;
            jobState.Status.Error = error;
            jobState.Status.Message = error.Message;
            
            _logger.LogWarning("Job {JobId} failed: {ErrorType} - {Message}", 
                jobId, error.Type, error.Message);

            // Persist failure
            await PersistJobAsync(jobState, completed: true);
        }
    }

    public void AddPhaseToTimeline(string jobId, PhaseInfo phase)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            jobState.Status.Timeline.Add(phase);
            // Persist timeline update
            _ = PersistJobAsync(jobState);
        }
    }

    private async Task PersistJobAsync(JobState jobState, bool completed = false)
    {
        try
        {
            var persistedJob = new PersistedJob
            {
                JobId = jobState.JobId,
                Request = jobState.Request,
                Status = jobState.Status,
                StartedAt = jobState.StartedAt,
                CompletedAt = completed ? DateTime.UtcNow : null,
                LastUpdatedAt = DateTime.UtcNow
            };

            await _persistence.SaveJobAsync(persistedJob);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist job {JobId}", jobState.JobId);
        }
    }

    private static string GenerateJobId()
    {
        return $"job_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}";
    }

    private class JobState
    {
        public required string JobId { get; set; }
        public required OrchestrateTaskRequest Request { get; set; }
        public required TaskStatusResponse Status { get; set; }
        public required CancellationTokenSource CancellationTokenSource { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
