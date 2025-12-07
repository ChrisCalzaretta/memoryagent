namespace AgentContracts.Requests;

/// <summary>
/// Request to orchestrate a multi-agent coding task
/// </summary>
public class OrchestrateTaskRequest
{
    /// <summary>
    /// The task description (e.g., "Add caching to UserService")
    /// </summary>
    public required string Task { get; set; }

    /// <summary>
    /// Project context name for Lightning memory
    /// </summary>
    public required string Context { get; set; }

    /// <summary>
    /// Path to the workspace root
    /// </summary>
    public required string WorkspacePath { get; set; }

    /// <summary>
    /// Target programming language for code generation.
    /// Supported: python, csharp, typescript, javascript, go, rust, java, ruby, php, swift, kotlin, dart, sql, html, css, shell
    /// If not specified, will be auto-detected from workspace or task description
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Run as background job (returns job ID immediately)
    /// </summary>
    public bool Background { get; set; } = true;

    /// <summary>
    /// Maximum iterations before giving up (default: 10 for robust retry)
    /// </summary>
    public int MaxIterations { get; set; } = 10;

    /// <summary>
    /// Minimum validation score to pass (0-10)
    /// </summary>
    public int MinValidationScore { get; set; } = 8;
    
    /// <summary>
    /// If true, automatically write generated files to workspace (default: false)
    /// When false, files are returned in the response for manual review
    /// </summary>
    public bool AutoWriteFiles { get; set; } = false;
}



