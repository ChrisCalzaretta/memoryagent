namespace MemoryAgent.Server.Models;

/// <summary>
/// Result of pattern quality validation
/// </summary>
public class PatternQualityResult
{
    /// <summary>
    /// Pattern being validated
    /// </summary>
    public CodePattern Pattern { get; set; } = new();

    /// <summary>
    /// Quality score (0-10)
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Grade (A-F)
    /// </summary>
    public string Grade => Score switch
    {
        >= 9 => "A",
        >= 8 => "B",
        >= 7 => "C",
        >= 6 => "D",
        _ => "F"
    };

    /// <summary>
    /// Security score (0-10)
    /// </summary>
    public int SecurityScore { get; set; } = 10;

    /// <summary>
    /// Validation issues found
    /// </summary>
    public List<ValidationIssue> Issues { get; set; } = new();

    /// <summary>
    /// Recommendations to fix issues
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Auto-fix code (if available)
    /// </summary>
    public string? AutoFixCode { get; set; }

    /// <summary>
    /// Missing complementary patterns
    /// </summary>
    public List<string> MissingPatterns { get; set; } = new();

    /// <summary>
    /// Configuration issues
    /// </summary>
    public List<ConfigurationIssue> ConfigurationIssues { get; set; } = new();

    /// <summary>
    /// Overall assessment
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Individual validation issue
/// </summary>
public class ValidationIssue
{
    /// <summary>
    /// Issue severity
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// Issue category
    /// </summary>
    public IssueCategory Category { get; set; }

    /// <summary>
    /// Issue description
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Line number where issue occurs
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Points deducted from score
    /// </summary>
    public int ScoreImpact { get; set; }

    /// <summary>
    /// CWE/CVE reference (for security issues)
    /// </summary>
    public string? SecurityReference { get; set; }

    /// <summary>
    /// How to fix this issue
    /// </summary>
    public string? FixGuidance { get; set; }
}

/// <summary>
/// Configuration validation issue
/// </summary>
public class ConfigurationIssue
{
    /// <summary>
    /// Configuration parameter name
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// Current value
    /// </summary>
    public string CurrentValue { get; set; } = string.Empty;

    /// <summary>
    /// Recommended value
    /// </summary>
    public string RecommendedValue { get; set; } = string.Empty;

    /// <summary>
    /// Why this is an issue
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Severity of configuration issue
    /// </summary>
    public IssueSeverity Severity { get; set; }
}

/// <summary>
/// Issue severity levels
/// </summary>
public enum IssueSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Issue category
/// </summary>
public enum IssueCategory
{
    Security,
    Performance,
    Reliability,
    Correctness,
    BestPractice,
    Configuration,
    Maintainability
}

/// <summary>
/// Pattern migration path
/// </summary>
public class PatternMigrationPath
{
    /// <summary>
    /// Current (legacy) pattern
    /// </summary>
    public string CurrentPattern { get; set; } = string.Empty;

    /// <summary>
    /// Target (modern) pattern
    /// </summary>
    public string TargetPattern { get; set; } = string.Empty;

    /// <summary>
    /// Migration status
    /// </summary>
    public MigrationStatus Status { get; set; }

    /// <summary>
    /// Estimated effort in hours
    /// </summary>
    public string EffortEstimate { get; set; } = string.Empty;

    /// <summary>
    /// Migration complexity
    /// </summary>
    public MigrationComplexity Complexity { get; set; }

    /// <summary>
    /// Step-by-step migration instructions
    /// </summary>
    public List<MigrationStep> Steps { get; set; } = new();

    /// <summary>
    /// Before/after code example
    /// </summary>
    public PatternCodeExample? CodeExample { get; set; }

    /// <summary>
    /// Benefits of migrating
    /// </summary>
    public List<string> Benefits { get; set; } = new();

    /// <summary>
    /// Risks of NOT migrating
    /// </summary>
    public List<string> Risks { get; set; } = new();
}

/// <summary>
/// Individual migration step
/// </summary>
public class MigrationStep
{
    /// <summary>
    /// Step number
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// Step title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed instructions
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Code snippet for this step
    /// </summary>
    public string? CodeSnippet { get; set; }

    /// <summary>
    /// Files to create/modify
    /// </summary>
    public List<string> FilesToModify { get; set; } = new();

    /// <summary>
    /// Validation criteria (how to know this step is complete)
    /// </summary>
    public string? ValidationCriteria { get; set; }
}

/// <summary>
/// Code example showing before/after for pattern migration
/// </summary>
public class PatternCodeExample
{
    /// <summary>
    /// Code before migration
    /// </summary>
    public string Before { get; set; } = string.Empty;

    /// <summary>
    /// Code after migration
    /// </summary>
    public string After { get; set; } = string.Empty;

    /// <summary>
    /// Description of changes
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Migration status
/// </summary>
public enum MigrationStatus
{
    Recommended,
    Deprecated,
    Critical,
    Optional
}

/// <summary>
/// Migration complexity
/// </summary>
public enum MigrationComplexity
{
    Low,
    Medium,
    High
}

/// <summary>
/// Request for pattern quality validation
/// </summary>
public class ValidatePatternQualityRequest
{
    /// <summary>
    /// Pattern ID to validate
    /// </summary>
    public string PatternId { get; set; } = string.Empty;

    /// <summary>
    /// Context/project to search in
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Include auto-fix code
    /// </summary>
    public bool IncludeAutoFix { get; set; } = true;

    /// <summary>
    /// Minimum severity level to report
    /// </summary>
    public IssueSeverity MinSeverity { get; set; } = IssueSeverity.Low;
}

/// <summary>
/// Request to find anti-patterns
/// </summary>
public class FindAntiPatternsRequest
{
    /// <summary>
    /// Context/project to search in
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Minimum severity level
    /// </summary>
    public IssueSeverity MinSeverity { get; set; } = IssueSeverity.Medium;

    /// <summary>
    /// Include legacy patterns
    /// </summary>
    public bool IncludeLegacyPatterns { get; set; } = true;
}

/// <summary>
/// Response for anti-patterns search
/// </summary>
public class FindAntiPatternsResponse
{
    /// <summary>
    /// Anti-patterns found
    /// </summary>
    public List<PatternQualityResult> AntiPatterns { get; set; } = new();

    /// <summary>
    /// Total count
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Critical issues count
    /// </summary>
    public int CriticalCount { get; set; }

    /// <summary>
    /// Overall security score for project
    /// </summary>
    public int OverallSecurityScore { get; set; }

    /// <summary>
    /// Summary
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Request for security validation
/// </summary>
public class ValidateSecurityRequest
{
    /// <summary>
    /// Context/project to validate
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Pattern types to check (optional, defaults to all high-risk)
    /// </summary>
    public List<PatternType>? PatternTypes { get; set; }
}

/// <summary>
/// Security validation response
/// </summary>
public class ValidateSecurityResponse
{
    /// <summary>
    /// Overall security score (0-10)
    /// </summary>
    public int SecurityScore { get; set; }

    /// <summary>
    /// Security grade
    /// </summary>
    public string Grade => SecurityScore switch
    {
        >= 9 => "A (Excellent)",
        >= 8 => "B (Good)",
        >= 7 => "C (Fair)",
        >= 6 => "D (Poor)",
        _ => "F (Critical Issues)"
    };

    /// <summary>
    /// Vulnerabilities found
    /// </summary>
    public List<SecurityVulnerability> Vulnerabilities { get; set; } = new();

    /// <summary>
    /// Remediation steps
    /// </summary>
    public List<string> RemediationSteps { get; set; } = new();

    /// <summary>
    /// Summary
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Security vulnerability
/// </summary>
public class SecurityVulnerability
{
    /// <summary>
    /// Vulnerability severity
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// Pattern where vulnerability was found
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// File path
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Vulnerability description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// CVE/CWE reference
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// How to fix
    /// </summary>
    public string Remediation { get; set; } = string.Empty;

    /// <summary>
    /// CVSS score (if applicable)
    /// </summary>
    public float? CvssScore { get; set; }
}

