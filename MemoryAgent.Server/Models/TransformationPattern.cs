namespace MemoryAgent.Server.Models;

/// <summary>
/// Learned transformation pattern from example files
/// </summary>
public class TransformationPattern
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    
    // Example files used to learn pattern
    public string ExampleOldFilePath { get; set; } = string.Empty;
    public string ExampleNewFilePath { get; set; } = string.Empty;
    
    // Learned transformations
    public List<TransformationRule> Rules { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    
    // Usage tracking
    public int TimesApplied { get; set; }
    public float SuccessRate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// Individual transformation rule within a pattern
/// </summary>
public class TransformationRule
{
    public string Description { get; set; } = string.Empty;
    public RuleType Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Types of transformation rules
/// </summary>
public enum RuleType
{
    ExtractComponent,
    MoveCSSToExternal,
    AddErrorHandling,
    AddLoadingStates,
    SplitLargeMethod,
    UseStateService,
    AddAccessibility,
    AddRenderMode,
    UseModernPatterns
}

