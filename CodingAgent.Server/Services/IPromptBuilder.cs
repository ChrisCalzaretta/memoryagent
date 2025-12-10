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
    
    /// <summary>
    /// Build a FOCUSED prompt for fixing build errors only.
    /// Much smaller than regular fix prompt - puts errors at the TOP.
    /// </summary>
    Task<string> BuildBuildErrorFixPromptAsync(
        string buildErrors,
        Dictionary<string, string> brokenFiles,
        string language,
        CancellationToken cancellationToken);
}
