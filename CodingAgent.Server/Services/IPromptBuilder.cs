using AgentContracts.Requests;

namespace CodingAgent.Server.Services;

/// <summary>
/// Interface for building prompts - NOW WITH LEARNING FROM LIGHTNING
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Build a prompt for code generation - fetches from Lightning if available
    /// </summary>
    Task<string> BuildGeneratePromptAsync(GenerateCodeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Build a prompt for code fixing - fetches from Lightning if available
    /// </summary>
    Task<string> BuildFixPromptAsync(GenerateCodeRequest request, CancellationToken cancellationToken);
}
