using AgentContracts.Models;

namespace AgentContracts.Responses;

/// <summary>
/// Response from CodingAgent code generation
/// </summary>
public class GenerateCodeResponse
{
    /// <summary>
    /// Whether generation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Generated/modified files
    /// </summary>
    public List<FileChange> FileChanges { get; set; } = new();

    /// <summary>
    /// Explanation of what was done
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Tokens used for this generation
    /// </summary>
    public int TokensUsed { get; set; }
    
    /// <summary>
    /// Which model was used for this generation (for tracking/rotation)
    /// </summary>
    public string? ModelUsed { get; set; }
    
    /// <summary>
    /// 🐳 Execution instructions from the LLM
    /// Tells ExecutionService exactly how to run this code
    /// </summary>
    public ExecutionInstructions? Execution { get; set; }
    
    /// <summary>
    /// ☁️ Cloud LLM usage (when Anthropic/OpenAI was used)
    /// </summary>
    public CloudGenerationUsage? CloudUsage { get; set; }
}

/// <summary>
/// Usage info from a single cloud LLM generation call
/// </summary>
public class CloudGenerationUsage
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal Cost { get; set; }
    public int? TokensRemaining { get; set; }
    public int? RequestsRemaining { get; set; }
}

/// <summary>
/// A file change from code generation
/// </summary>
public class FileChange
{
    /// <summary>
    /// Relative path to the file
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// New file content
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Type of change
    /// </summary>
    public FileChangeType Type { get; set; }

    /// <summary>
    /// Why this file was changed
    /// </summary>
    public string? Reason { get; set; }
}


