using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Client for FunctionGemma - the AI that decides which tools to call
/// </summary>
public interface IFunctionGemmaClient
{
    /// <summary>
    /// Generate a workflow plan for a user request
    /// </summary>
    Task<WorkflowPlan> PlanWorkflowAsync(
        string userRequest,
        IEnumerable<ToolDefinition> availableTools,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);
}

