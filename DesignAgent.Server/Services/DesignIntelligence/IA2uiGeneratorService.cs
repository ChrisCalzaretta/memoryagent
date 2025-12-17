using DesignAgent.Server.Models.DesignIntelligence;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for generating A2UI (Agent-to-User Interface) JSON from learned patterns
/// </summary>
public interface IA2uiGeneratorService
{
    /// <summary>
    /// Generate A2UI JSON for a new design based on brand and patterns
    /// </summary>
    /// <param name="brandContext">Brand name/context</param>
    /// <param name="componentType">Component type (e.g., "pricing-page", "hero-section")</param>
    /// <param name="requirements">User requirements (e.g., "3 tiers with annual toggle")</param>
    /// <returns>A2UI JSON with design tokens</returns>
    Task<A2uiOutput> GenerateA2uiAsync(string brandContext, string componentType, string requirements, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Convert a learned pattern to A2UI format
    /// </summary>
    /// <param name="patternId">Pattern ID</param>
    /// <param name="brandContext">Brand to apply</param>
    /// <returns>A2UI JSON</returns>
    Task<A2uiOutput> ConvertPatternToA2uiAsync(string patternId, string brandContext, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate A2UI similar to an existing design
    /// </summary>
    /// <param name="designId">Design to use as reference</param>
    /// <param name="brandContext">Brand to apply</param>
    /// <param name="variations">Variations to apply (e.g., "more colorful", "minimal")</param>
    /// <returns>A2UI JSON</returns>
    Task<A2uiOutput> GenerateSimilarA2uiAsync(string designId, string brandContext, string? variations = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Convert A2UI JSON to code (HTML/CSS or Blazor)
    /// </summary>
    /// <param name="a2uiJson">A2UI JSON</param>
    /// <param name="targetFramework">Target framework ("html", "blazor", "react")</param>
    /// <returns>Generated code</returns>
    Task<string> ConvertA2uiToCodeAsync(string a2uiJson, string targetFramework = "html", CancellationToken cancellationToken = default);
}

/// <summary>
/// A2UI output with design tokens and component JSON
/// </summary>
public class A2uiOutput
{
    /// <summary>
    /// Brand/context name
    /// </summary>
    public required string Brand { get; set; }
    
    /// <summary>
    /// Design tokens (CSS variables)
    /// </summary>
    public required Dictionary<string, Dictionary<string, string>> DesignTokens { get; set; }
    
    /// <summary>
    /// A2UI component JSON
    /// </summary>
    public required object A2uiJson { get; set; }
    
    /// <summary>
    /// Component metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

