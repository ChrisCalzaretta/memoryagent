using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Manages background jobs for coding tasks
/// </summary>
public interface IJobManager
{
    /// <summary>
    /// Initialize by loading persisted jobs from disk
    /// </summary>
    Task InitializeAsync();

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
    void CompleteJob(string jobId, TaskResult result, CloudUsage? cloudUsage = null);

    /// <summary>
    /// Fail a job with error
    /// </summary>
    void FailJob(string jobId, TaskError error);

    /// <summary>
    /// Add phase to timeline
    /// </summary>
    void AddPhaseToTimeline(string jobId, PhaseInfo phase);
    
    /// <summary>
    /// Set the task plan for a job (called after plan generation)
    /// </summary>
    void SetJobPlan(string jobId, TaskPlanInfo plan);
    
    /// <summary>
    /// Add a generated file to the job's file list
    /// </summary>
    void AddGeneratedFile(string jobId, string filePath);
    
    /// <summary>
    /// Update step status in the plan by file name
    /// </summary>
    void UpdatePlanStep(string jobId, string fileName, string status);
    
    /// <summary>
    /// Update step status in the plan by step index (for step-by-step execution)
    /// </summary>
    void UpdateStepStatus(string? jobId, int stepIndex, string status);
    
    /// <summary>
    /// Resume a job that is waiting for help (NeedsHelp state)
    /// </summary>
    Task<bool> ResumeWithHelpAsync(string jobId, object helpRequest, CancellationToken cancellationToken);
}



