using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Manages background jobs for coding tasks
/// </summary>
public interface IJobManager
{
    /// <summary>
    /// Start a new background job
    /// </summary>
    Task<string> StartJobAsync(OrchestrateTaskRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Get status of a job
    /// </summary>
    TaskStatusResponse? GetJobStatus(string jobId);

    /// <summary>
    /// Cancel a running job
    /// </summary>
    bool CancelJob(string jobId);

    /// <summary>
    /// Get all jobs
    /// </summary>
    IEnumerable<TaskStatusResponse> GetAllJobs();

    /// <summary>
    /// Update job status (called by orchestrator)
    /// </summary>
    void UpdateJobStatus(string jobId, TaskState status, int progress, string? phase = null, int iteration = 0);

    /// <summary>
    /// Complete a job with result
    /// </summary>
    void CompleteJob(string jobId, TaskResult result);

    /// <summary>
    /// Fail a job with error
    /// </summary>
    void FailJob(string jobId, TaskError error);

    /// <summary>
    /// Add phase to timeline
    /// </summary>
    void AddPhaseToTimeline(string jobId, PhaseInfo phase);
}



