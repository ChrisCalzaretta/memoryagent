namespace CodingAgent.Server.Services;

/// <summary>
/// 🧠🧠🧠 MULTI-MODEL THINKING SERVICE
/// Coordinates multiple reasoning models (Phi4, Gemma3, Qwen)
/// Strategies: Solo, Duo Debate, Trio Consensus, Multi-Round Debate
/// </summary>
public interface IMultiModelThinkingService
{
    /// <summary>
    /// Smart strategy selection based on context and attempt number
    /// </summary>
    Task<ThinkingResult> ThinkSmartAsync(
        ThinkingContext context,
        int attemptNumber,
        CancellationToken cancellationToken = default);
}



