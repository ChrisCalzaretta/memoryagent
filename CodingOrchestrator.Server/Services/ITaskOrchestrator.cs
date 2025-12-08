using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Orchestrates the multi-agent coding workflow
/// </summary>
public interface ITaskOrchestrator
{
    /// <summary>
    /// Execute a coding task (synchronous version)
    /// </summary>
    Task<TaskStatusResponse> ExecuteTaskAsync(OrchestrateTaskRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Execute a coding task with job tracking
    /// </summary>
    Task<TaskStatusResponse> ExecuteTaskAsync(OrchestrateTaskRequest request, string jobId, CancellationToken cancellationToken);
}




