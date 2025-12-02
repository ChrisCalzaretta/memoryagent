namespace MemoryAgent.Server.Models;

/// <summary>
/// Candidate for component extraction (detected reusable pattern)
/// </summary>
public class ComponentCandidate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Pattern detection
    public int Occurrences { get; set; }
    public float Similarity { get; set; }
    public List<ComponentOccurrence> Locations { get; set; } = new();
    
    // Proposed component interface
    public ComponentInterface ProposedInterface { get; set; } = new();
    
    // Impact analysis
    public int LinesSaved { get; set; }
    public Priority Priority { get; set; }
    public float ValueScore { get; set; }  // 0-100
    
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Location where component pattern was found
/// </summary>
public class ComponentOccurrence
{
    public string FilePath { get; set; } = string.Empty;
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public string Code { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Proposed interface for extracted component
/// </summary>
public class ComponentInterface
{
    public List<ComponentParameter> Parameters { get; set; } = new();
    public List<ComponentEvent> Events { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Component parameter definition
/// </summary>
public class ComponentParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Component event definition
/// </summary>
public class ComponentEvent
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Extracted component result
/// </summary>
public class ExtractedComponent
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? CSS { get; set; }
    public ComponentInterface Interface { get; set; } = new();
    public List<ComponentRefactoring> Refactorings { get; set; } = new();
}

/// <summary>
/// Refactoring needed to use extracted component
/// </summary>
public class ComponentRefactoring
{
    public string FilePath { get; set; } = string.Empty;
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public string OldCode { get; set; } = string.Empty;
    public string NewCode { get; set; } = string.Empty;
}

/// <summary>
/// Priority levels for component extraction
/// </summary>
public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

