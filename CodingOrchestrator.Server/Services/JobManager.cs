using System.Collections.Concurrent;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Manages background jobs for coding tasks
/// </summary>
public class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<string, JobState> _jobs = new();
    private readonly ITaskOrchestrator _orchestrator;
    private readonly ILogger<JobManager> _logger;

    public JobManager(ITaskOrchestrator orchestrator, ILogger<JobManager> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<string> StartJobAsync(OrchestrateTaskRequest request, CancellationToken cancellationToken)
    {
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
        _logger.LogInformation("Job {JobId} created for task: {Task}", jobId, request.Task);

        // Start the job in the background
        _ = Task.Run(async () =>
        {
            try
            {
                UpdateJobStatus(jobId, TaskState.Running, 5, "Starting");
                
                var result = await _orchestrator.ExecuteTaskAsync(request, jobId, cts.Token);
                
                if (result.Status == TaskState.Complete)
                {
                    CompleteJob(jobId, result.Result!);
                }
                else if (result.Status == TaskState.Failed)
                {
                    // Validation failed but code was generated - include partial result
                    if (result.Error != null)
                    {
                        FailJob(jobId, result.Error);
                    }
                    else
                    {
                        // Validation didn't pass but we have a result
                        FailJob(jobId, new TaskError
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
                UpdateJobStatus(jobId, TaskState.Cancelled, 0, "Cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed with exception", jobId);
                FailJob(jobId, new TaskError
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
        return _jobs.Values.Select(j => j.Status).ToList();
    }

    public void UpdateJobStatus(string jobId, TaskState status, int progress, string? phase = null, int iteration = 0)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            jobState.Status.Status = status;
            jobState.Status.Progress = progress;
            jobState.Status.CurrentPhase = phase;
            jobState.Status.Iteration = iteration;
            
            _logger.LogDebug("Job {JobId} updated: {Status}, {Progress}%, Phase: {Phase}", 
                jobId, status, progress, phase);
        }
    }

    public void CompleteJob(string jobId, TaskResult result)
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
        }
    }

    public void FailJob(string jobId, TaskError error)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            jobState.Status.Status = TaskState.Failed;
            jobState.Status.Error = error;
            jobState.Status.Message = error.Message;
            
            _logger.LogWarning("Job {JobId} failed: {ErrorType} - {Message}", 
                jobId, error.Type, error.Message);
        }
    }

    public void AddPhaseToTimeline(string jobId, PhaseInfo phase)
    {
        if (_jobs.TryGetValue(jobId, out var jobState))
        {
            jobState.Status.Timeline.Add(phase);
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



