namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a page transformation (V1 → V2, Razor → Blazor, etc.)
/// </summary>
public class PageTransformation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceFilePath { get; set; } = string.Empty;
    public string TargetFilePath { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    
    // Transformation metadata
    public TransformationType Type { get; set; }
    public TransformationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    // Analysis results
    public int OriginalLines { get; set; }
    public int TransformedLines { get; set; }
    public int ComponentsExtracted { get; set; }
    public int InlineStylesRemoved { get; set; }
    public List<string> IssuesDetected { get; set; } = new();
    public List<string> ImprovementsMade { get; set; } = new();
    
    // Generated files
    public List<GeneratedFile> GeneratedFiles { get; set; } = new();
    
    // LLM metadata
    public string LLMModel { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// File generated during transformation
/// </summary>
public class GeneratedFile
{
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public FileType Type { get; set; }
    public int LineCount { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Types of transformations supported
/// </summary>
public enum TransformationType
{
    RazorPageToBlazor,      // Razor Pages → Blazor
    BlazorV1ToV2,           // Old Blazor → Modern Blazor
    ComponentExtraction,    // Monolith → Components
    CSSModernization,       // Inline → Modern CSS
    FullTransformation      // Everything at once
}

/// <summary>
/// Transformation execution status
/// </summary>
public enum TransformationStatus
{
    Analyzing,
    Transforming,
    Completed,
    Failed
}

/// <summary>
/// Types of generated files
/// </summary>
public enum FileType
{
    Component,
    CSS,
    Service,
    Model,
    Test,
    Documentation
}

