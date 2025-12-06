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
    /// Convert to feedback for next iteration
    /// </summary>
    public ValidationFeedback ToFeedback() => new()
    {
        Score = Score,
        Issues = Issues,
        Summary = Summary
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



