using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for CSS transformation and modernization
/// </summary>
public interface ICSSTransformationService
{
    /// <summary>
    /// Extract inline styles and generate modern CSS
    /// </summary>
    Task<CSSTransformation> TransformCSSAsync(
        string sourceFilePath,
        CSSTransformationOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze CSS quality
    /// </summary>
    Task<CSSAnalysisResult> AnalyzeCSSAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Extract inline styles from HTML/Razor code
    /// </summary>
    List<InlineStyle> ExtractInlineStyles(string code);
    
    /// <summary>
    /// Generate modern CSS from inline styles
    /// </summary>
    Task<ModernCSSResult> GenerateModernCSSAsync(
        List<InlineStyle> inlineStyles,
        CSSAnalysisResult analysis,
        CSSTransformationOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for CSS transformation
/// </summary>
public class CSSTransformationOptions
{
    public bool ExtractInlineStyles { get; set; } = true;
    public bool GenerateVariables { get; set; } = true;
    public bool ModernizeLayout { get; set; } = true;
    public bool AddResponsive { get; set; } = true;
    public bool AddAccessibility { get; set; } = true;
    public string? OutputCSSPath { get; set; }
}

/// <summary>
/// Inline style occurrence
/// </summary>
public class InlineStyle
{
    public string Element { get; set; } = string.Empty;
    public Dictionary<string, string> Styles { get; set; } = new();
    public int LineNumber { get; set; }
    public string Context { get; set; } = string.Empty;
}

