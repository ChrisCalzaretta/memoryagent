namespace MemoryAgent.Server.Models;

/// <summary>
/// Request for validating best practices in a project
/// </summary>
public class BestPracticeValidationRequest
{
    /// <summary>
    /// Project context to validate
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Specific best practices to check (optional, defaults to all)
    /// </summary>
    public List<string>? BestPractices { get; set; }

    /// <summary>
    /// Include detailed code examples in results
    /// </summary>
    public bool IncludeExamples { get; set; } = true;

    /// <summary>
    /// Maximum examples per practice (default 5)
    /// </summary>
    public int MaxExamplesPerPractice { get; set; } = 5;

    /// <summary>
    /// Minimum confidence threshold (0.0 to 1.0)
    /// </summary>
    public float MinimumConfidence { get; set; } = 0.7f;
}

