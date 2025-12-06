using AgentContracts.Requests;

namespace ValidationAgent.Server.Services;

/// <summary>
/// Interface for building validation prompts - NOW WITH LEARNING FROM LIGHTNING
/// </summary>
public interface IValidationPromptBuilder
{
    /// <summary>
    /// Build a prompt for code validation - fetches from Lightning if available
    /// </summary>
    Task<string> BuildValidationPromptAsync(ValidateCodeRequest request, CancellationToken cancellationToken);
}
