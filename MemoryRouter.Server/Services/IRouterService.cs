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
    /// <param name="forceSync">If true, forces all steps to run synchronously even if AI recommends async</param>
    Task<WorkflowResult> ExecuteRequestAsync(
        string userRequest,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default,
        bool forceSync = false);
}


