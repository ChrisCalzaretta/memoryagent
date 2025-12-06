using AgentContracts.Requests;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// Client for communicating with MemoryAgent.Server
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Get context for a task (similar questions, patterns, etc.)
    /// </summary>
    Task<CodeContext?> GetContextAsync(string task, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Store a successful Q&A for future recall
    /// </summary>
    Task StoreQaAsync(string question, string answer, List<string> relevantFiles, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Get active prompt by name from Lightning
    /// </summary>
    Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken);

    /// <summary>
    /// Record feedback on prompt performance
    /// </summary>
    Task RecordPromptFeedbackAsync(string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken);

    /// <summary>
    /// Check if MemoryAgent is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Prompt information from Lightning
/// </summary>
public class PromptInfo
{
    public required string Name { get; set; }
    public required string Content { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
}

