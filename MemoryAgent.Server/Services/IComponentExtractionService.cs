using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for detecting and extracting reusable components
/// </summary>
public interface IComponentExtractionService
{
    /// <summary>
    /// Detect reusable component patterns across project
    /// </summary>
    Task<List<ComponentCandidate>> DetectReusableComponentsAsync(
        string projectPath,
        int minOccurrences = 2,
        float minSimilarity = 0.7f,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract a specific component
    /// </summary>
    Task<ExtractedComponent> ExtractComponentAsync(
        ComponentCandidate candidate,
        string outputPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find similar HTML blocks across files (returns groups of similar blocks)
    /// </summary>
    Task<List<List<HTMLBlock>>> FindSimilarBlocksAsync(
        List<HTMLBlock> blocks,
        float minSimilarity = 0.7f);
}

/// <summary>
/// HTML block extracted from code
/// </summary>
public class HTMLBlock
{
    public string FilePath { get; set; } = string.Empty;
    public int LineStart { get; set; }
    public int LineEnd { get; set; }
    public string HTML { get; set; } = string.Empty;
    public int ElementCount { get; set; }
    public bool HasImage { get; set; }
    public bool HasButton { get; set; }
    public bool HasPrice { get; set; }
    public bool HasForm { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

