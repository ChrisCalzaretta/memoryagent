using AgentContracts.Requests;

namespace AgentContracts.Responses;

/// <summary>
/// Response from ValidationAgent code review
/// </summary>
public class ValidateCodeResponse
{
    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Validation score (0-10)
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Issues found during validation
    /// </summary>
    public List<ValidationIssue> Issues { get; set; } = new();

    /// <summary>
    /// Suggestions for improvement
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// Summary of the validation
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Raw build/execution errors from Docker (if any)
    /// When set, indicates this is a build/execution failure
    /// </summary>
    public string? BuildErrors { get; set; }
    
    /// <summary>
    /// Confidence score (0.0-1.0) - how confident are we in this validation?
    /// Used for ensemble validation to indicate model agreement
    /// 1.0 = perfect agreement, 0.0 = complete disagreement
    /// </summary>
    public double Confidence { get; set; } = 1.0;
    
    /// <summary>
    /// Models that participated in this validation
    /// Empty for single model validation, multiple entries for ensemble
    /// </summary>
    public List<string> ModelsUsed { get; set; } = new();
    
    /// <summary>
    /// Individual validation results from each model (for ensemble debugging)
    /// Only populated when ensemble validation is used
    /// </summary>
    public List<EnsembleMemberResult>? EnsembleResults { get; set; }

    /// <summary>
    /// Convert to feedback for next iteration
    /// </summary>
    public ValidationFeedback ToFeedback() => new()
    {
        Score = Score,
        Issues = Issues,
        Summary = Summary,
        BuildErrors = BuildErrors  // 🔧 CRITICAL: Copy build errors so focused prompt is used!
    };
}

/// <summary>
/// A validation issue found in the code
/// </summary>
public class ValidationIssue
{
    /// <summary>
    /// Severity level
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// File where issue was found
    /// </summary>
    public string? File { get; set; }

    /// <summary>
    /// Line number (if applicable)
    /// </summary>
    public int? Line { get; set; }

    /// <summary>
    /// Issue description
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Suggested fix
    /// </summary>
    public string? Suggestion { get; set; }

    /// <summary>
    /// Rule that triggered this issue
    /// </summary>
    public string? Rule { get; set; }
}

/// <summary>
/// Result from a single model in an ensemble
/// </summary>
public class EnsembleMemberResult
{
    /// <summary>
    /// Model that produced this result
    /// </summary>
    public required string Model { get; set; }
    
    /// <summary>
    /// Score from this model (0-10)
    /// </summary>
    public int Score { get; set; }
    
    /// <summary>
    /// Number of issues found by this model
    /// </summary>
    public int IssueCount { get; set; }
    
    /// <summary>
    /// Validation duration (ms)
    /// </summary>
    public long DurationMs { get; set; }
    
    /// <summary>
    /// Whether this model was already loaded (warm)
    /// </summary>
    public bool WasWarm { get; set; }
}













