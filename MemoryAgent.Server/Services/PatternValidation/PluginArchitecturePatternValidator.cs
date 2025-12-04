using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for plugin architecture patterns
/// Validates plugin loading, isolation, security, versioning, and lifecycle
/// </summary>
public class PluginArchitecturePatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.PluginArchitecture };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10,
            SecurityScore = 10
        };

        // Category-specific validation based on plugin pattern name
        switch (pattern.Name)
        {
            // Plugin Loading & Isolation - Check for proper isolation
            case "Plugin_AssemblyLoadContext":
                if (!Regex.IsMatch(pattern.Content, @"protected\s+override\s+Assembly\s+Load", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Correctness,
                        Message = "AssemblyLoadContext without Load override - plugins won't be isolated properly",
                        ScoreImpact = 3,
                        FixGuidance = "Override Load(AssemblyName) to implement custom assembly resolution"
                    });
                    result.Score -= 3;
                }
                break;

            case "Plugin_CollectibleLoadContext":
                if (!Regex.IsMatch(pattern.Content, @"isCollectible\s*:\s*true", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.BestPractice,
                        Message = "AssemblyLoadContext not marked as collectible - plugins cannot be unloaded for hot reload",
                        ScoreImpact = 2,
                        FixGuidance = "Pass isCollectible: true to AssemblyLoadContext constructor"
                    });
                    result.Score -= 2;
                }
                break;

            // Plugin Lifecycle - Check for proper disposal and lifecycle management
            case "Plugin_InterfaceContract":
                if (!Regex.IsMatch(pattern.Content, @":\s*IDisposable", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.BestPractice,
                        Message = "Plugin interface doesn't implement IDisposable - resource leaks possible",
                        ScoreImpact = 2,
                        FixGuidance = "Add : IDisposable to plugin interface and implement Dispose()"
                    });
                    result.Score -= 2;
                }
                break;

            case "Plugin_StatelessDesign":
                // Positive pattern - ensure it's truly stateless
                if (Regex.IsMatch(pattern.Content, @"private\s+(?!readonly|const)\w+\s+\w+\s*;", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Correctness,
                        Message = "Plugin has mutable state - not truly stateless, thread-safety risks",
                        ScoreImpact = 4,
                        FixGuidance = "Remove mutable fields or make them readonly. Use constructor injection for dependencies."
                    });
                    result.Score -= 4;
                }
                break;

            // Plugin Security - Critical security validations
            case "Plugin_SignatureVerification":
                if (!Regex.IsMatch(pattern.Content, @"GetPublicKey|SecurityException", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.Security,
                        Message = "Signature verification incomplete - plugins not properly validated before load",
                        ScoreImpact = 5,
                        SecurityReference = "CWE-494: Download of Code Without Integrity Check",
                        FixGuidance = "Check assemblyName.GetPublicKey() != null and throw SecurityException if invalid"
                    });
                    result.Score -= 5;
                    result.SecurityScore -= 5;
                }
                break;

            case "Plugin_ProcessIsolation":
                // Good security pattern
                result.Recommendations.Add("Excellent security practice - process isolation prevents plugin attacks on host");
                break;

            case "Plugin_CircuitBreaker":
            case "Plugin_BulkheadIsolation":
                // Resilience patterns - check for proper configuration
                if (!Regex.IsMatch(pattern.Content, @"TimeSpan\.|maxParallelization|maxQueuingActions", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Configuration,
                        Message = "Resilience pattern without proper timeout/limit configuration",
                        ScoreImpact = 2,
                        FixGuidance = "Configure appropriate timeouts and concurrency limits"
                    });
                    result.Score -= 2;
                }
                break;

            // Plugin Versioning - Check for semantic versioning
            case "Plugin_SemanticVersioning":
                if (!Regex.IsMatch(pattern.Content, @"\d+\.\d+\.\d+", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Low,
                        Category = IssueCategory.BestPractice,
                        Message = "Version doesn't follow SemVer format (major.minor.patch)",
                        ScoreImpact = 1,
                        FixGuidance = "Use semantic versioning: <major>.<minor>.<patch> format"
                    });
                    result.Score -= 1;
                }
                break;

            case "Plugin_CompatibilityMatrix":
                var hasMinVersion = pattern.Metadata.ContainsKey("MinHostVersion") || 
                                   Regex.IsMatch(pattern.Content, "MinHostVersion", RegexOptions.IgnoreCase);
                var hasMaxVersion = pattern.Metadata.ContainsKey("MaxHostVersion") ||
                                   Regex.IsMatch(pattern.Content, "MaxHostVersion", RegexOptions.IgnoreCase);
                
                if (!hasMinVersion || !hasMaxVersion)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Low,
                        Category = IssueCategory.BestPractice,
                        Message = "Incomplete compatibility matrix - missing Min or Max host version",
                        ScoreImpact = 1,
                        FixGuidance = "Add both MinHostVersion and MaxHostVersion metadata to prevent incompatible loading"
                    });
                    result.Score -= 1;
                }
                break;

            // MEF Patterns - Check for proper metadata
            case "Plugin_MEFExport":
                if (!Regex.IsMatch(pattern.Content, @"\[ExportMetadata\(", RegexOptions.IgnoreCase))
                {
                    result.Recommendations.Add("Consider adding [ExportMetadata] for version and capabilities");
                }
                break;

            case "Plugin_MEFMetadata":
                // Positive pattern
                result.Recommendations.Add("Excellent use of metadata - enables filtering without loading plugins");
                break;

            // Plugin Communication - Event bus patterns
            case "Plugin_EventBus":
                if (!Regex.IsMatch(pattern.Content, @"Publish|Subscribe", RegexOptions.IgnoreCase))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Correctness,
                        Message = "Event bus interface missing Publish/Subscribe methods",
                        ScoreImpact = 2,
                        FixGuidance = "Add Publish<TEvent> and Subscribe<TEvent> methods to event bus"
                    });
                    result.Score -= 2;
                }
                break;
        }

        // General validation for all plugin patterns
        
        // Check for logging/observability
        if (!Regex.IsMatch(pattern.Content, @"ILogger|_logger|Log\(", RegexOptions.IgnoreCase))
        {
            result.Recommendations.Add("Consider adding logging for plugin lifecycle events and errors");
        }

        // Check for cancellation token support
        if (Regex.IsMatch(pattern.Content, @"async|Task<", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(pattern.Content, @"CancellationToken", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.BestPractice,
                Message = "Async method without CancellationToken - cannot cancel long-running plugin operations",
                ScoreImpact = 1,
                FixGuidance = "Add CancellationToken parameter to async methods"
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        result.Summary = $"Plugin Architecture Pattern Quality: {result.Grade} ({result.Score}/10) | Security: {result.SecurityScore}/10";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

