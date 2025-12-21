using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Persists jobs to disk so they survive container restarts
/// </summary>
public interface IJobPersistenceService
{
    /// <summary>
    /// Save a job to disk
    /// </summary>
    Task SaveJobAsync(PersistedJob job);

    /// <summary>
    /// Save generated code files for a job (called after each iteration)
    /// </summary>
    Task SaveJobFilesAsync(string jobId, List<GeneratedFile> files, int iteration);

    /// <summary>
    /// Load all jobs from disk
    /// </summary>
    Task<IEnumerable<PersistedJob>> LoadAllJobsAsync();

    /// <summary>
    /// Load generated files for a job
    /// </summary>
    Task<List<GeneratedFile>> LoadJobFilesAsync(string jobId);

    /// <summary>
    /// Delete a job from disk (for cleanup of old completed jobs)
    /// </summary>
    Task DeleteJobAsync(string jobId);

    /// <summary>
    /// Cleanup old completed jobs (older than retention period)
    /// </summary>
    Task CleanupOldJobsAsync(TimeSpan retentionPeriod);
}

/// <summary>
/// Serializable job state for persistence
/// </summary>
public class PersistedJob
{
    public required string JobId { get; set; }
    public required OrchestrateTaskRequest Request { get; set; }
    public required TaskStatusResponse Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    
    /// <summary>
    /// Last iteration that had files saved
    /// </summary>
    public int LastSavedIteration { get; set; }
    
    /// <summary>
    /// Whether this job has persisted code files
    /// </summary>
    public bool HasPersistedFiles { get; set; }
}

