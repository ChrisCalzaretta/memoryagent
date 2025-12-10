using AgentContracts.Responses;
using System.ComponentModel.DataAnnotations;

namespace AgentContracts.Requests;

/// <summary>
/// Request to generate code via the CodingAgent
/// </summary>
public class GenerateCodeRequest : IValidatableObject
{
    /// <summary>
    /// The task description
    /// </summary>
    [Required(ErrorMessage = "Task description is required")]
    [StringLength(10000, MinimumLength = 5, ErrorMessage = "Task must be between 5 and 10000 characters")]
    public required string Task { get; set; }

    /// <summary>
    /// Target programming language (e.g., "python", "csharp", "typescript", "javascript", "go", "rust")
    /// If not specified, will be auto-detected from workspace or default to C#
    /// </summary>
    [RegularExpression(@"^(python|csharp|typescript|javascript|go|rust|java|ruby|php|swift|kotlin|dart|sql|html|css|shell|auto)?$",
        ErrorMessage = "Invalid language specified")]
    public string? Language { get; set; }

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
    [MaxLength(100, ErrorMessage = "Cannot target more than 100 files")]
    public List<string>? TargetFiles { get; set; }

    /// <summary>
    /// Existing/accumulated files from previous steps (step-by-step mode)
    /// These are files generated in earlier steps that the LLM should reference
    /// </summary>
    public List<ExistingFile>? ExistingFiles { get; set; }

    /// <summary>
    /// The workspace path
    /// </summary>
    [Required(ErrorMessage = "WorkspacePath is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "WorkspacePath must be between 1 and 500 characters")]
    public required string WorkspacePath { get; set; }

    /// <summary>
    /// Custom validation
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Prevent path traversal
        if (WorkspacePath.Contains("..") || WorkspacePath.Contains("~"))
        {
            yield return new ValidationResult(
                "WorkspacePath cannot contain path traversal characters",
                new[] { nameof(WorkspacePath) });
        }

        // Validate target files don't have path traversal
        if (TargetFiles != null)
        {
            foreach (var file in TargetFiles)
            {
                if (file.Contains(".."))
                {
                    yield return new ValidationResult(
                        $"Target file '{file}' contains path traversal characters",
                        new[] { nameof(TargetFiles) });
                }
            }
        }
    }
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
/// An existing file from previous generation steps
/// </summary>
public class ExistingFile
{
    /// <summary>
    /// File path (e.g., "Card.cs", "Models/Hand.cs")
    /// </summary>
    public required string Path { get; set; }
    
    /// <summary>
    /// Full file content
    /// </summary>
    public required string Content { get; set; }
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
    
    /// <summary>
    /// Raw build errors from Docker execution (if any)
    /// When set, indicates this is a BUILD failure that needs focused fix prompt
    /// </summary>
    public string? BuildErrors { get; set; }
    
    /// <summary>
    /// Check if this feedback is specifically for build errors (not validation)
    /// </summary>
    public bool HasBuildErrors => !string.IsNullOrEmpty(BuildErrors);
}

