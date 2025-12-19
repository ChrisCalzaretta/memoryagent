using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Core routing service that uses FunctionGemma to plan and execute workflows
/// </summary>
public interface IRouterService
{
    /// <summary>
    /// Execute a user request using FunctionGemma for intelligent routing
    /// </summary>
    Task<WorkflowResult> ExecuteRequestAsync(
        string userRequest,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default);
}

