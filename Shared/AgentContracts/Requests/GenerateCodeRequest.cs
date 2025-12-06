using AgentContracts.Responses;

namespace AgentContracts.Requests;

/// <summary>
/// Request to generate code via the CodingAgent
/// </summary>
public class GenerateCodeRequest
{
    /// <summary>
    /// The task description
    /// </summary>
    public required string Task { get; set; }

    /// <summary>
    /// Context from Lightning (past solutions, patterns, etc.)
    /// </summary>
    public CodeContext? Context { get; set; }

    /// <summary>
    /// Previous validation feedback (for fix iterations)
    /// </summary>
    public ValidationFeedback? PreviousFeedback { get; set; }

    /// <summary>
    /// Target files to focus on (if modifying existing code)
    /// </summary>
    public List<string>? TargetFiles { get; set; }

    /// <summary>
    /// The workspace path
    /// </summary>
    public required string WorkspacePath { get; set; }
}

/// <summary>
/// Context information from Lightning memory
/// </summary>
public class CodeContext
{
    /// <summary>
    /// Similar past solutions from Q&A memory
    /// </summary>
    public List<PastSolution> SimilarSolutions { get; set; } = new();

    /// <summary>
    /// Relevant code patterns to apply
    /// </summary>
    public List<CodePattern> Patterns { get; set; } = new();

    /// <summary>
    /// Related files that often change together
    /// </summary>
    public List<string> RelatedFiles { get; set; } = new();

    /// <summary>
    /// Architecture recommendations
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// A past solution from Lightning Q&A memory
/// </summary>
public class PastSolution
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public List<string> RelevantFiles { get; set; } = new();
    public double Similarity { get; set; }
}

/// <summary>
/// A code pattern from the pattern library
/// </summary>
public class CodePattern
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? CodeExample { get; set; }
    public string? BestPractice { get; set; }
}

/// <summary>
/// Feedback from validation agent
/// </summary>
public class ValidationFeedback
{
    public int Score { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public string? Summary { get; set; }
    
    /// <summary>
    /// Models that have already been tried (for smart rotation)
    /// </summary>
    public HashSet<string> TriedModels { get; set; } = new();
}

