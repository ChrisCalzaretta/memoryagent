namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents classified user intent from natural language
/// </summary>
public class UserIntent
{
    /// <summary>
    /// Detected project type
    /// </summary>
    public ProjectType ProjectType { get; set; } = ProjectType.Unknown;

    /// <summary>
    /// Primary user goal
    /// </summary>
    public UserGoal PrimaryGoal { get; set; } = UserGoal.Unknown;

    /// <summary>
    /// Detected technologies
    /// </summary>
    public List<string> Technologies { get; set; } = new();

    /// <summary>
    /// Relevant pattern categories
    /// </summary>
    public List<PatternCategory> RelevantCategories { get; set; } = new();

    /// <summary>
    /// Application domain (e-commerce, healthcare, fintech, etc.)
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Complexity estimate
    /// </summary>
    public ComplexityLevel Complexity { get; set; } = ComplexityLevel.Medium;

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    public float Confidence { get; set; } = 0.0f;

    /// <summary>
    /// Original user request
    /// </summary>
    public string OriginalRequest { get; set; } = string.Empty;

    /// <summary>
    /// Additional insights from LLM
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Type of project being built
/// </summary>
public enum ProjectType
{
    Unknown,
    MobileApp,          // Flutter, React Native, Xamarin
    WebAPI,             // ASP.NET Core API, FastAPI, Express
    AIAgent,            // Agent-based systems
    WebApp,             // Blazor, React, Angular
    DesktopApp,         // WPF, WinForms, Electron
    BackendService,     // Microservices, workers
    Library,            // Reusable packages
    DataPipeline,       // ETL, data processing
    MicroService        // Containerized service
}

/// <summary>
/// Primary user goal
/// </summary>
public enum UserGoal
{
    Unknown,
    Performance,        // Optimize speed, reduce latency
    Security,           // Add auth, prevent attacks
    Refactoring,        // Clean up code, improve architecture
    NewFeature,         // Build new functionality
    BugFix,             // Fix existing issues
    Migration,          // Upgrade framework/library
    Testing,            // Add test coverage
    Observability,      // Add logging, monitoring
    Scalability,        // Handle more load
    CostOptimization    // Reduce cloud costs
}

/// <summary>
/// Complexity level
/// </summary>
public enum ComplexityLevel
{
    Simple,     // 1-2 files, < 1 hour
    Medium,     // 3-10 files, 1-4 hours
    Complex,    // 10-50 files, 1-3 days
    Enterprise  // 50+ files, 1+ weeks
}

