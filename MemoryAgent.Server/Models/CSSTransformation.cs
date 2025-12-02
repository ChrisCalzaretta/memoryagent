namespace MemoryAgent.Server.Models;

/// <summary>
/// CSS transformation and modernization result
/// </summary>
public class CSSTransformation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceFilePath { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    
    // CSS Analysis
    public int InlineStyleCount { get; set; }
    public List<string> CSSIssues { get; set; } = new();
    public List<string> DetectedPatterns { get; set; } = new();
    
    // Extracted CSS
    public string GeneratedCSS { get; set; } = string.Empty;
    public Dictionary<string, string> CSSVariables { get; set; } = new();
    
    // Improvements
    public bool UsesVariables { get; set; }
    public bool UsesModernLayout { get; set; }  // Grid/Flexbox
    public bool IsResponsive { get; set; }
    public bool HasAccessibility { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// CSS quality analysis result
/// </summary>
public class CSSAnalysisResult
{
    public int InlineStyleCount { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public float QualityScore { get; set; }
    public Dictionary<string, int> IssueBreakdown { get; set; } = new();
}

/// <summary>
/// Generated modern CSS result
/// </summary>
public class ModernCSSResult
{
    public string CSS { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = new();
    public bool UsesModernLayout { get; set; }
    public bool IsResponsive { get; set; }
    public bool HasAccessibility { get; set; }
    public List<string> Improvements { get; set; } = new();
}

