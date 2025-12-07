namespace MemoryAgent.PatternManagement.Models;

/// <summary>
/// A global pattern that gets synced to all workspaces.
/// Stored in both Neo4j (relationships) and Qdrant (semantic search).
/// </summary>
public class GlobalPattern
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Human-readable pattern name
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Pattern version (incremented on updates)
    /// </summary>
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// Category: Reflection, Security, Async, DependencyInjection, Architecture, etc.
    /// </summary>
    public required string Category { get; set; }
    
    /// <summary>
    /// Severity level for validation
    /// </summary>
    public PatternSeverity Severity { get; set; } = PatternSeverity.Recommended;
    
    /// <summary>
    /// Detailed description of the pattern
    /// </summary>
    public required string Description { get; set; }
    
    /// <summary>
    /// What NOT to do (the anti-pattern)
    /// </summary>
    public string? AntiPattern { get; set; }
    
    /// <summary>
    /// What TO do (the correct pattern)
    /// </summary>
    public string? CorrectPattern { get; set; }
    
    /// <summary>
    /// Full working code example
    /// </summary>
    public string? CodeExample { get; set; }
    
    /// <summary>
    /// Validation rules in plain English
    /// </summary>
    public List<string> ValidationRules { get; set; } = new();
    
    /// <summary>
    /// Regex pattern for quick detection (optional)
    /// </summary>
    public string? DetectionRegex { get; set; }
    
    /// <summary>
    /// Tags for filtering
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    // ============ Relationships (Neo4j) ============
    
    /// <summary>
    /// Pattern IDs that this pattern requires (dependencies)
    /// </summary>
    public List<string> RequiresPatterns { get; set; } = new();
    
    /// <summary>
    /// Pattern IDs that this pattern conflicts with
    /// </summary>
    public List<string> ConflictsWithPatterns { get; set; } = new();
    
    /// <summary>
    /// Pattern ID that this pattern supersedes (for evolution)
    /// </summary>
    public string? SupersedesPattern { get; set; }
    
    /// <summary>
    /// Applicability rules - when does this pattern apply?
    /// </summary>
    public List<ApplicabilityRule> AppliesTo { get; set; } = new();
    
    // ============ Lifecycle ============
    
    /// <summary>
    /// Whether this pattern is deprecated
    /// </summary>
    public bool IsDeprecated { get; set; } = false;
    
    /// <summary>
    /// Reason for deprecation
    /// </summary>
    public string? DeprecatedReason { get; set; }
    
    /// <summary>
    /// When the pattern was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the pattern was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who created/updated the pattern
    /// </summary>
    public string? Author { get; set; }
}

/// <summary>
/// Pattern severity levels
/// </summary>
public enum PatternSeverity
{
    /// <summary>
    /// Just informational, no enforcement
    /// </summary>
    Info = 0,
    
    /// <summary>
    /// Recommended but not required
    /// </summary>
    Recommended = 1,
    
    /// <summary>
    /// Should be followed, warning if violated
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Must be followed, error if violated
    /// </summary>
    Required = 3,
    
    /// <summary>
    /// Critical security/safety pattern, blocks validation
    /// </summary>
    Critical = 4
}

/// <summary>
/// Rule for when a pattern applies
/// </summary>
public class ApplicabilityRule
{
    /// <summary>
    /// Entity type: Class, Method, Interface, Property, File
    /// </summary>
    public required string EntityType { get; set; }
    
    /// <summary>
    /// Condition in plain text: "implements IPlugin", "has attribute [DynamicInvoke]", "in namespace *.Plugins"
    /// </summary>
    public required string Condition { get; set; }
    
    /// <summary>
    /// Optional regex for automated matching
    /// </summary>
    public string? ConditionRegex { get; set; }
}

/// <summary>
/// Categories for organizing patterns
/// </summary>
public static class PatternCategories
{
    public const string Reflection = "Reflection";
    public const string Security = "Security";
    public const string Async = "Async";
    public const string DependencyInjection = "DependencyInjection";
    public const string Architecture = "Architecture";
    public const string ErrorHandling = "ErrorHandling";
    public const string Performance = "Performance";
    public const string Testing = "Testing";
    public const string Logging = "Logging";
    public const string Configuration = "Configuration";
    public const string DataAccess = "DataAccess";
    public const string Caching = "Caching";
    public const string Validation = "Validation";
    public const string Serialization = "Serialization";
    
    public static readonly string[] All = new[]
    {
        Reflection, Security, Async, DependencyInjection, Architecture,
        ErrorHandling, Performance, Testing, Logging, Configuration,
        DataAccess, Caching, Validation, Serialization
    };
}

