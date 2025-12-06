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
    /// Run as background job (returns job ID immediately)
    /// </summary>
    public bool Background { get; set; } = true;

    /// <summary>
    /// Maximum iterations before giving up
    /// </summary>
    public int MaxIterations { get; set; } = 5;

    /// <summary>
    /// Minimum validation score to pass (0-10)
    /// </summary>
    public int MinValidationScore { get; set; } = 8;
}



