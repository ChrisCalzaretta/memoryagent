namespace CodingAgent.Server.Clients;

/// <summary>
/// Client for communicating with MemoryAgent.Server (Lightning)
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Get the active prompt by name from Lightning
    /// </summary>
    Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken);

    /// <summary>
    /// Find similar past solutions for learning
    /// </summary>
    Task<List<SimilarSolution>> FindSimilarSolutionsAsync(string task, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Get relevant patterns for the task
    /// </summary>
    Task<List<PatternInfo>> GetPatternsAsync(string task, string context, CancellationToken cancellationToken);

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

/// <summary>
/// Similar solution from Q&A memory
/// </summary>
public class SimilarSolution
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public List<string> RelevantFiles { get; set; } = new();
    public double Similarity { get; set; }
}

/// <summary>
/// Pattern from Lightning
/// </summary>
public class PatternInfo
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? CodeExample { get; set; }
    public string? BestPractice { get; set; }
}



