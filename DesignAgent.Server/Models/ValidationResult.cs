namespace DesignAgent.Server.Models;

/// <summary>
/// Result of design validation against brand guidelines
/// </summary>
public class DesignValidationResult
{
    public bool IsCompliant { get; set; }
    public int Score { get; set; } // 0-10
    public string Grade { get; set; } = string.Empty; // A, B, C, D, F
    public List<DesignIssue> Issues { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public ValidationSummary Summary { get; set; } = new();
}

public class DesignIssue
{
    public string Type { get; set; } = string.Empty; // color, spacing, typography, accessibility, voice
    public IssueSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string? CodeSnippet { get; set; }
    public string? Fix { get; set; }
    public string? FixCode { get; set; }
}

public enum IssueSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public class ValidationSummary
{
    public int TotalIssues { get; set; }
    public int Critical { get; set; }
    public int High { get; set; }
    public int Medium { get; set; }
    public int Low { get; set; }
    
    public Dictionary<string, int> ByType { get; set; } = new();
}

/// <summary>
/// Accessibility-specific validation result
/// </summary>
public class AccessibilityValidationResult
{
    public string WcagLevel { get; set; } = "AA";
    public bool Passes { get; set; }
    public int Score { get; set; }
    public List<AccessibilityIssue> Issues { get; set; } = new();
}

public class AccessibilityIssue
{
    public string Criterion { get; set; } = string.Empty; // e.g., "1.4.3 Contrast"
    public string Level { get; set; } = string.Empty; // A, AA, AAA
    public IssueSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Element { get; set; } = string.Empty;
    public string? Fix { get; set; }
    public string? Impact { get; set; }
}

/// <summary>
/// Color contrast check result
/// </summary>
public class ContrastCheckResult
{
    public string Foreground { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
    public double Ratio { get; set; }
    public bool PassesAA { get; set; }
    public bool PassesAAA { get; set; }
    public bool PassesLargeTextAA { get; set; }
    public string RequiredRatio { get; set; } = string.Empty;
}

