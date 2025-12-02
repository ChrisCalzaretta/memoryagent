using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for transforming Blazor/Razor pages to modern architecture
/// </summary>
public interface IPageTransformationService
{
    /// <summary>
    /// Transform a page to modern architecture
    /// </summary>
    Task<PageTransformation> TransformPageAsync(
        string sourceFilePath,
        TransformationOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Learn transformation pattern from example (old → new)
    /// </summary>
    Task<TransformationPattern> LearnPatternAsync(
        string exampleOldPath,
        string exampleNewPath,
        string patternName,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Apply learned pattern to new page
    /// </summary>
    Task<PageTransformation> ApplyPatternAsync(
        string patternId,
        string targetFilePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all transformation patterns
    /// </summary>
    Task<List<TransformationPattern>> GetPatternsAsync(string? context = null);
}

/// <summary>
/// Options for page transformation
/// </summary>
public class TransformationOptions
{
    public bool ExtractComponents { get; set; } = true;
    public bool ModernizeCSS { get; set; } = true;
    public bool AddErrorHandling { get; set; } = true;
    public bool AddLoadingStates { get; set; } = true;
    public bool AddAccessibility { get; set; } = true;
    public bool GenerateTests { get; set; } = false;
    public string? OutputDirectory { get; set; }
    public string? CSSOutputPath { get; set; }
}

/// <summary>
/// Page analysis result
/// </summary>
public class PageAnalysis
{
    public List<string> Issues { get; set; } = new();
    public bool HasInlineStyles { get; set; }
    public bool NeedsErrorHandling { get; set; }
    public bool NeedsLoadingStates { get; set; }
    public List<ComponentCandidate> ComponentCandidates { get; set; } = new();
    public int FileSize { get; set; }
}

/// <summary>
/// Transformation execution plan
/// </summary>
public class TransformationPlan
{
    public string MainComponentPath { get; set; } = string.Empty;
    public string MainComponentCode { get; set; } = string.Empty;
    public List<ExtractedComponent> ExtractedComponents { get; set; } = new();
    public string CSS { get; set; } = string.Empty;
    public Dictionary<string, string> CSSVariables { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
    public float Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Transformation execution result
/// </summary>
public class TransformationExecutionResult
{
    public List<GeneratedFile> Files { get; set; } = new();
    public int ComponentsExtracted { get; set; }
    public int InlineStylesRemoved { get; set; }
    public List<string> Improvements { get; set; } = new();
    public int TotalLines { get; set; }
    public float Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

