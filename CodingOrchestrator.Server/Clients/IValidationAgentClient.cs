using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// Client for communicating with ValidationAgent.Server
/// </summary>
public interface IValidationAgentClient
{
    /// <summary>
    /// Validate code quality
    /// </summary>
    Task<ValidateCodeResponse> ValidateAsync(ValidateCodeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Check if ValidationAgent is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}












