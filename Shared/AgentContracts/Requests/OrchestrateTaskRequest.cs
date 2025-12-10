using System.ComponentModel.DataAnnotations;

namespace AgentContracts.Requests;

/// <summary>
/// Request to orchestrate a multi-agent coding task
/// </summary>
public class OrchestrateTaskRequest : IValidatableObject
{
    /// <summary>
    /// The task description (e.g., "Add caching to UserService")
    /// </summary>
    [Required(ErrorMessage = "Task description is required")]
    [StringLength(10000, MinimumLength = 10, ErrorMessage = "Task must be between 10 and 10000 characters")]
    public required string Task { get; set; }

    /// <summary>
    /// Project context name for Lightning memory
    /// </summary>
    [Required(ErrorMessage = "Context is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Context must be between 1 and 200 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_\-\.]+$", ErrorMessage = "Context can only contain letters, numbers, underscores, hyphens, and dots")]
    public required string Context { get; set; }

    /// <summary>
    /// Path to the workspace root
    /// </summary>
    [Required(ErrorMessage = "WorkspacePath is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "WorkspacePath must be between 1 and 500 characters")]
    public required string WorkspacePath { get; set; }

    /// <summary>
    /// Target programming language for code generation.
    /// Supported: python, csharp, typescript, javascript, go, rust, java, ruby, php, swift, kotlin, dart, sql, html, css, shell
    /// If not specified, will be auto-detected from workspace or task description
    /// </summary>
    [RegularExpression(@"^(python|csharp|typescript|javascript|go|rust|java|ruby|php|swift|kotlin|dart|sql|html|css|shell|auto)?$", 
        ErrorMessage = "Invalid language. Supported: python, csharp, typescript, javascript, go, rust, java, ruby, php, swift, kotlin, dart, sql, html, css, shell")]
    public string? Language { get; set; }

    /// <summary>
    /// Run as background job (returns job ID immediately)
    /// </summary>
    public bool Background { get; set; } = true;

    /// <summary>
    /// Maximum iterations before giving up (default: 10 for robust retry)
    /// </summary>
    [Range(1, 1000, ErrorMessage = "MaxIterations must be between 1 and 1000")]
    public int MaxIterations { get; set; } = 100;

    /// <summary>
    /// Minimum validation score to pass (0-10)
    /// </summary>
    [Range(0, 10, ErrorMessage = "MinValidationScore must be between 0 and 10")]
    public int MinValidationScore { get; set; } = 8;
    
    /// <summary>
    /// Validation strictness mode:
    /// - "standard" (default): Relaxed rules - only bugs, security issues, and syntax errors
    /// - "enterprise": Full strict mode - XML docs, CancellationToken, DI patterns, etc.
    /// </summary>
    [RegularExpression(@"^(standard|enterprise)?$", ErrorMessage = "ValidationMode must be 'standard' or 'enterprise'")]
    public string ValidationMode { get; set; } = "standard";
    
    /// <summary>
    /// If true, automatically write generated files to workspace (default: false)
    /// When false, files are returned in the response for manual review
    /// </summary>
    public bool AutoWriteFiles { get; set; } = false;
    
    /// <summary>
    /// Execution mode for multi-step tasks:
    /// - "batch" (default): Generate all code at once, then validate
    /// - "stepbystep": Generate each plan step separately, validate after each step
    /// Step-by-step mode is better for complex multi-file tasks with dependencies
    /// </summary>
    [RegularExpression(@"^(batch|stepbystep)?$", ErrorMessage = "ExecutionMode must be 'batch' or 'stepbystep'")]
    public string ExecutionMode { get; set; } = "batch";

    /// <summary>
    /// Custom validation for security checks
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Prevent path traversal attacks
        if (WorkspacePath.Contains("..") || WorkspacePath.Contains("~"))
        {
            yield return new ValidationResult(
                "WorkspacePath cannot contain path traversal characters (.., ~)",
                new[] { nameof(WorkspacePath) });
        }

        // Prevent injection in task description
        var dangerousPatterns = new[] { "<script", "javascript:", "data:", "vbscript:" };
        foreach (var pattern in dangerousPatterns)
        {
            if (Task.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult(
                    $"Task description contains potentially dangerous content: {pattern}",
                    new[] { nameof(Task) });
            }
        }
    }
}



