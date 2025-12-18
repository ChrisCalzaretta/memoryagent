using System.ComponentModel.DataAnnotations;

namespace AgentContracts.Requests;

/// <summary>
/// Request to validate code via the ValidationAgent
/// </summary>
public class ValidateCodeRequest : IValidatableObject
{
    /// <summary>
    /// Files to validate
    /// </summary>
    [Required(ErrorMessage = "At least one file is required for validation")]
    [MinLength(1, ErrorMessage = "At least one file is required")]
    [MaxLength(50, ErrorMessage = "Cannot validate more than 50 files at once")]
    public required List<CodeFile> Files { get; set; }

    /// <summary>
    /// Project context for pattern matching
    /// </summary>
    [Required(ErrorMessage = "Context is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Context must be between 1 and 200 characters")]
    public required string Context { get; set; }

    /// <summary>
    /// Target programming language (for language-specific validation rules)
    /// If not specified, will be auto-detected from file extensions
    /// </summary>
    [RegularExpression(@"^(python|csharp|blazor|typescript|javascript|go|rust|java|ruby|php|swift|kotlin|dart|flutter|sql|html|css|shell|auto)?$",
        ErrorMessage = "Invalid language specified")]
    public string? Language { get; set; }

    /// <summary>
    /// Validation rules to apply
    /// </summary>
    [MaxLength(20, ErrorMessage = "Cannot specify more than 20 validation rules")]
    public List<string> Rules { get; set; } = new() { "best_practices", "security", "patterns" };

    /// <summary>
    /// Validation strictness mode:
    /// - "standard" (default): Relaxed rules, focus on bugs/security. Good for generated code.
    /// - "enterprise": Full strict mode with all best practices (XML docs, DI, CancellationToken, etc.)
    /// </summary>
    [RegularExpression(@"^(standard|enterprise)?$", ErrorMessage = "ValidationMode must be 'standard' or 'enterprise'")]
    public string ValidationMode { get; set; } = "standard";

    /// <summary>
    /// The original task (for context)
    /// </summary>
    [StringLength(10000, ErrorMessage = "OriginalTask cannot exceed 10000 characters")]
    public string? OriginalTask { get; set; }

    /// <summary>
    /// Workspace path for additional context
    /// </summary>
    [StringLength(500, ErrorMessage = "WorkspacePath cannot exceed 500 characters")]
    public string? WorkspacePath { get; set; }

    /// <summary>
    /// Custom validation
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Check total content size (prevent memory exhaustion)
        var totalSize = Files?.Sum(f => f.Content?.Length ?? 0) ?? 0;
        if (totalSize > 10_000_000) // 10MB limit
        {
            yield return new ValidationResult(
                "Total file content exceeds 10MB limit",
                new[] { nameof(Files) });
        }

        // Validate file paths
        if (Files != null)
        {
            foreach (var file in Files)
            {
                if (file.Path.Contains(".."))
                {
                    yield return new ValidationResult(
                        $"File path '{file.Path}' contains path traversal characters",
                        new[] { nameof(Files) });
                }
            }
        }
    }
}

/// <summary>
/// A code file to validate
/// </summary>
public class CodeFile
{
    /// <summary>
    /// Relative path to the file
    /// </summary>
    [Required(ErrorMessage = "File path is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Path must be between 1 and 500 characters")]
    public required string Path { get; set; }

    /// <summary>
    /// File content
    /// </summary>
    [Required(ErrorMessage = "File content is required")]
    public required string Content { get; set; }

    /// <summary>
    /// Whether this is a new file or modified existing
    /// </summary>
    public bool IsNew { get; set; }
}



