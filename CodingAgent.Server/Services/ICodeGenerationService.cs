using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingAgent.Server.Services;

/// <summary>
/// Service for generating code using LLM
/// </summary>
public interface ICodeGenerationService
{
    /// <summary>
    /// Generate code for a task
    /// </summary>
    Task<GenerateCodeResponse> GenerateAsync(GenerateCodeRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Fix code based on validation feedback
    /// </summary>
    Task<GenerateCodeResponse> FixAsync(GenerateCodeRequest request, CancellationToken cancellationToken);
}



