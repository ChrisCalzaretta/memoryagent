namespace AgentContracts.Requests;

/// <summary>
/// Request to validate code via the ValidationAgent
/// </summary>
public class ValidateCodeRequest
{
    /// <summary>
    /// Files to validate
    /// </summary>
    public required List<CodeFile> Files { get; set; }

    /// <summary>
    /// Project context for pattern matching
    /// </summary>
    public required string Context { get; set; }

    /// <summary>
    /// Target programming language (for language-specific validation rules)
    /// If not specified, will be auto-detected from file extensions
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Validation rules to apply
    /// </summary>
    public List<string> Rules { get; set; } = new() { "best_practices", "security", "patterns" };

    /// <summary>
    /// The original task (for context)
    /// </summary>
    public string? OriginalTask { get; set; }

    /// <summary>
    /// Workspace path for additional context
    /// </summary>
    public string? WorkspacePath { get; set; }
}

/// <summary>
/// A code file to validate
/// </summary>
public class CodeFile
{
    /// <summary>
    /// Relative path to the file
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// File content
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Whether this is a new file or modified existing
    /// </summary>
    public bool IsNew { get; set; }
}



