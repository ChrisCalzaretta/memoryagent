using AgentContracts.Requests;
using AgentContracts.Responses;

namespace ValidationAgent.Server.Services;

/// <summary>
/// Service for validating code quality
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validate code against rules and best practices
    /// </summary>
    Task<ValidateCodeResponse> ValidateAsync(ValidateCodeRequest request, CancellationToken cancellationToken);
}








