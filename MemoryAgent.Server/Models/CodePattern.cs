namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a detected coding pattern (best practice, anti-pattern, etc.)
/// </summary>
public class CodePattern
{
    /// <summary>
    /// Unique identifier for the pattern instance
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Pattern name (e.g., "UserService_MemoryCache", "Polly_RetryPolicy")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Pattern type (Caching, Resilience, Validation, DependencyInjection, Logging, ErrorHandling)
    /// </summary>
    public PatternType Type { get; set; }

    /// <summary>
    /// Pattern category for Azure Well-Architected Framework
    /// (Performance, Reliability, Security, Operational, Cost)
    /// </summary>
    public PatternCategory Category { get; set; }

    /// <summary>
    /// Specific implementation detected (e.g., "IMemoryCache", "Polly", "Pydantic")
    /// </summary>
    public string Implementation { get; set; } = string.Empty;

    /// <summary>
    /// Programming language
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// File path where pattern was found
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number where pattern starts
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// End line number (for multi-line patterns)
    /// </summary>
    public int EndLineNumber { get; set; }

    /// <summary>
    /// Code snippet showing the pattern (with context)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Description of the best practice being followed
    /// </summary>
    public string BestPractice { get; set; } = string.Empty;

    /// <summary>
    /// Azure best practice documentation URL
    /// </summary>
    public string AzureBestPracticeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Pattern confidence score (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; } = 1.0f;

    /// <summary>
    /// Whether this is a positive pattern (best practice) or negative (anti-pattern)
    /// </summary>
    public bool IsPositivePattern { get; set; } = true;

    /// <summary>
    /// Additional metadata about the pattern
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Project/context this pattern belongs to
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// When this pattern was detected
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of coding patterns
/// </summary>
public enum PatternType
{
    Caching,
    Resilience,
    Validation,
    DependencyInjection,
    Logging,
    ErrorHandling,
    Security,
    Performance,
    Configuration,
    Testing,
    Monitoring,
    DataAccess,
    ApiDesign,
    Messaging,
    BackgroundJobs,
    
    // AI Agent Frameworks
    AgentFramework,      // Microsoft Agent Framework
    AGUI,                // AG-UI Protocol Integration (Agent UI)
    SemanticKernel,      // Semantic Kernel (legacy, migrating to Agent Framework)
    AutoGen,             // AutoGen (legacy, migrating to Agent Framework)
    AgentLightning,      // Agent Lightning (RL-based optimization)
    
    Unknown
}

/// <summary>
/// Azure Well-Architected Framework categories + AI Agent specific categories
/// </summary>
public enum PatternCategory
{
    Performance,
    Reliability,
    Security,
    Operational,
    Cost,
    General,
    
    // AI Agent Framework specific categories
    AIAgents,                    // AI agent creation and configuration
    MultiAgentOrchestration,     // Multi-agent workflows and patterns
    StateManagement,             // Thread-based state, checkpointing
    ToolIntegration,             // MCP servers, plugins, tools
    Interceptors,                // Middleware, filters, safety checks
    HumanInLoop,                 // Human interaction patterns
    AgentOptimization,           // RL-based agent training and optimization (Agent Lightning)
    AntiPatterns                 // Anti-patterns and migration recommendations
}

/// <summary>
/// Result of best practice validation
/// </summary>
public class BestPracticeValidation
{
    public string Project { get; set; } = string.Empty;
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    public float OverallScore { get; set; }
    public List<PatternValidationResult> Results { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class PatternValidationResult
{
    public string Practice { get; set; } = string.Empty;
    public PatternType Type { get; set; }
    public bool Implemented { get; set; }
    public int Count { get; set; }
    public List<string> Examples { get; set; } = new();
    public string AzureUrl { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

