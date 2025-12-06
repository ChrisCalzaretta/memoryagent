using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// Client for communicating with CodingAgent.Server
/// </summary>
public interface ICodingAgentClient
{
    /// <summary>
    /// Generate code for a task
    /// </summary>
    Task<GenerateCodeResponse> GenerateAsync(GenerateCodeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Fix code based on validation feedback
    /// </summary>
    Task<GenerateCodeResponse> FixAsync(GenerateCodeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Check if CodingAgent is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}



