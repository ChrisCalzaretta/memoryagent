namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Service for executing generated code in Docker containers
/// Ensures code actually compiles and runs before returning to user
/// </summary>
public interface IExecutionService
{
    /// <summary>
    /// Build and run generated code in a Docker container
    /// </summary>
    /// <param name="language">Target programming language (fallback if not in instructions)</param>
    /// <param name="files">Generated files to execute</param>
    /// <param name="workspacePath">Base workspace path</param>
    /// <param name="instructions">Execution instructions from LLM (optional - will infer if null)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result with success/failure and output</returns>
    Task<ExecutionResult> ExecuteAsync(
        string language,
        List<ExecutionFile> files,
        string workspacePath,
        AgentContracts.Models.ExecutionInstructions? instructions,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Build and optionally run generated code in a Docker container
    /// </summary>
    /// <param name="language">Target programming language</param>
    /// <param name="files">Generated files to execute</param>
    /// <param name="workspacePath">Base workspace path</param>
    /// <param name="instructions">Execution instructions from LLM</param>
    /// <param name="buildOnly">If true, only build/compile without running</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result with success/failure and output</returns>
    Task<ExecutionResult> ExecuteAsync(
        string language,
        List<ExecutionFile> files,
        string workspacePath,
        AgentContracts.Models.ExecutionInstructions? instructions,
        bool buildOnly,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of code execution in Docker
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Whether the code built and ran successfully
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Whether the build/compile step passed
    /// </summary>
    public bool BuildPassed { get; set; }
    
    /// <summary>
    /// Whether the execution/run step passed
    /// </summary>
    public bool ExecutionPassed { get; set; }
    
    /// <summary>
    /// Standard output from execution
    /// </summary>
    public string Output { get; set; } = "";
    
    /// <summary>
    /// Error output from execution
    /// </summary>
    public string Errors { get; set; } = "";
    
    /// <summary>
    /// Combined output for feedback
    /// </summary>
    public string CombinedOutput => string.IsNullOrEmpty(Errors) 
        ? Output 
        : $"STDOUT:\n{Output}\n\nSTDERR:\n{Errors}";
    
    /// <summary>
    /// Exit code from the container
    /// </summary>
    public int ExitCode { get; set; }
    
    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
    
    /// <summary>
    /// The Docker image used
    /// </summary>
    public string DockerImage { get; set; } = "";
    
    /// <summary>
    /// Commands that were executed
    /// </summary>
    public List<string> CommandsExecuted { get; set; } = new();
}

/// <summary>
/// Generated file for execution (maps from AgentContracts types)
/// </summary>
public class ExecutionFile
{
    public required string Path { get; set; }
    public required string Content { get; set; }
    public int ChangeType { get; set; }
    public string? Reason { get; set; }
}

