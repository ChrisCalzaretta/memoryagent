using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Implementation of pattern validation service
/// </summary>
public class PatternValidationService : IPatternValidationService
{
    private readonly IPatternIndexingService _patternIndexingService;
    private readonly ISemgrepService _semgrepService;
    private readonly ILogger<PatternValidationService> _logger;

    public PatternValidationService(
        IPatternIndexingService patternIndexingService,
        ISemgrepService semgrepService,
        ILogger<PatternValidationService> logger)
    {
        _patternIndexingService = patternIndexingService;
        _semgrepService = semgrepService;
        _logger = logger;
    }

    public async Task<PatternQualityResult> ValidatePatternQualityAsync(
        string patternId,
        string? context = null,
        bool includeAutoFix = true,
        CancellationToken cancellationToken = default)
    {
        // Get the pattern
        var pattern = await _patternIndexingService.GetPatternByIdAsync(patternId, cancellationToken);

        if (pattern == null)
        {
            _logger.LogWarning("Pattern not found: {PatternId}", patternId);
            return new PatternQualityResult
            {
                Pattern = new CodePattern { Id = patternId, Name = "Not Found" },
                Score = 0,
                Summary = "Pattern not found"
            };
        }

        // Validate based on pattern type
        var result = pattern.Type switch
        {
            PatternType.Caching => ValidateCachingPattern(pattern),
            PatternType.Resilience => ValidateResiliencePattern(pattern),
            PatternType.Validation => ValidateValidationPattern(pattern),
            PatternType.AgentFramework => ValidateAgentFrameworkPattern(pattern),
            PatternType.AgentLightning => ValidateAgentLightningPattern(pattern),
            PatternType.SemanticKernel => ValidateSemanticKernelPattern(pattern),
            PatternType.AutoGen => ValidateAutoGenPattern(pattern),
            PatternType.Security => ValidateSecurityPattern(pattern),
            PatternType.ErrorHandling => ValidateErrorHandlingPattern(pattern),
            PatternType.PluginArchitecture => ValidatePluginArchitecturePattern(pattern),
            _ => new PatternQualityResult
            {
                Pattern = pattern,
                Score = 5,
                Summary = "No specific validation rules for this pattern type yet"
            }
        };

        // Generate auto-fix if requested and issues found
        if (includeAutoFix && result.Issues.Any())
        {
            result.AutoFixCode = GenerateAutoFix(pattern, result.Issues);
        }

        return result;
    }

    public async Task<FindAntiPatternsResponse> FindAntiPatternsAsync(
        string context,
        IssueSeverity minSeverity = IssueSeverity.Medium,
        bool includeLegacy = true,
        CancellationToken cancellationToken = default)
    {
        var response = new FindAntiPatternsResponse { Summary = $"Anti-pattern analysis for {context}" };

        // Get all patterns for context
        var allPatterns = new List<CodePattern>();
        
        // Get patterns by type
        foreach (PatternType type in Enum.GetValues(typeof(PatternType)))
        {
            var patterns = await _patternIndexingService.GetPatternsByTypeAsync(type, context, 1000, cancellationToken);
            allPatterns.AddRange(patterns);
        }

        // Validate each pattern
        foreach (var pattern in allPatterns)
        {
            // Skip if it's a positive pattern and we're not including legacy
            if (!includeLegacy && pattern.IsPositivePattern)
                continue;

            var validation = await ValidatePatternQualityAsync(pattern.Id, context, false, cancellationToken);

            // Check if it has issues meeting severity threshold
            var hasSignificantIssues = validation.Issues.Any(i => i.Severity >= minSeverity);
            var isLegacyPattern = !pattern.IsPositivePattern;

            if (hasSignificantIssues || isLegacyPattern)
            {
                response.AntiPatterns.Add(validation);

                if (validation.Issues.Any(i => i.Severity == IssueSeverity.Critical))
                {
                    response.CriticalCount++;
                }
            }
        }

        response.TotalCount = response.AntiPatterns.Count;
        response.OverallSecurityScore = CalculateOverallSecurityScore(response.AntiPatterns);
        response.Summary = $"Found {response.TotalCount} anti-patterns ({response.CriticalCount} critical)";

        return response;
    }

    public async Task<ValidateSecurityResponse> ValidateSecurityAsync(
        string context,
        List<PatternType>? patternTypes = null,
        CancellationToken cancellationToken = default)
    {
        var response = new ValidateSecurityResponse();

        // High-risk pattern types if not specified
        patternTypes ??= new List<PatternType>
        {
            PatternType.Security,
            PatternType.AutoGen, // Code execution risks
            PatternType.ApiDesign,
            PatternType.Validation
        };

        var vulnerabilities = new List<SecurityVulnerability>();

        // 1. Validate existing detected patterns
        foreach (var type in patternTypes)
        {
            var patterns = await _patternIndexingService.GetPatternsByTypeAsync(type, context, 1000, cancellationToken);

            foreach (var pattern in patterns)
            {
                var validation = await ValidatePatternQualityAsync(pattern.Id, context, false, cancellationToken);

                // Extract security issues
                foreach (var issue in validation.Issues.Where(i => i.Category == IssueCategory.Security))
                {
                    vulnerabilities.Add(new SecurityVulnerability
                    {
                        Severity = issue.Severity,
                        PatternName = pattern.Name,
                        FilePath = pattern.FilePath,
                        Description = issue.Message,
                        Reference = issue.SecurityReference,
                        Remediation = issue.FixGuidance ?? "See pattern best practices"
                    });
                }
            }
        }

        // 2. Also check for Semgrep-detected security issues
        var semgrepPatterns = await _patternIndexingService.GetPatternsByTypeAsync(
            PatternType.Security, context, 1000, cancellationToken);
        
        var semgrepFindings = semgrepPatterns.Where(p => 
            p.Metadata.ContainsKey("is_semgrep_finding") && 
            (bool)p.Metadata["is_semgrep_finding"]);
        
        foreach (var finding in semgrepFindings)
        {
            var severity = finding.Metadata.GetValueOrDefault("severity")?.ToString() switch
            {
                "ERROR" => IssueSeverity.Critical,
                "WARNING" => IssueSeverity.High,
                "INFO" => IssueSeverity.Medium,
                _ => IssueSeverity.Low
            };

            vulnerabilities.Add(new SecurityVulnerability
            {
                Severity = severity,
                PatternName = $"Semgrep: {finding.Metadata.GetValueOrDefault("semgrep_rule")}",
                FilePath = finding.FilePath,
                Description = finding.Implementation,
                Reference = $"CWE: {finding.Metadata.GetValueOrDefault("cwe")}, OWASP: {finding.Metadata.GetValueOrDefault("owasp")}",
                Remediation = finding.Metadata.GetValueOrDefault("fix")?.ToString() ?? "See Semgrep rule documentation"
            });
        }

        response.Vulnerabilities = vulnerabilities.OrderByDescending(v => v.Severity).ToList();
        response.SecurityScore = CalculateSecurityScore(vulnerabilities);
        response.RemediationSteps = GenerateRemediationSteps(vulnerabilities);
        response.Summary = $"Security Score: {response.SecurityScore}/10 ({response.Grade}), {vulnerabilities.Count} vulnerabilities found ({semgrepFindings.Count()} from Semgrep)";

        return response;
    }

    public async Task<PatternMigrationPath?> GetMigrationPathAsync(
        string patternId,
        bool includeCodeExample = true,
        CancellationToken cancellationToken = default)
    {
        var pattern = await _patternIndexingService.GetPatternByIdAsync(patternId, cancellationToken);

        if (pattern == null || pattern.IsPositivePattern)
            return null;

        // Generate migration path based on pattern type
        return pattern.Type switch
        {
            PatternType.AutoGen => GenerateAutoGenMigrationPath(pattern, includeCodeExample),
            PatternType.SemanticKernel when pattern.Name.Contains("Planner") => 
                GenerateSemanticKernelPlannerMigrationPath(pattern, includeCodeExample),
            _ => null
        };
    }

    public async Task<ProjectValidationReport> ValidateProjectAsync(
        string context,
        CancellationToken cancellationToken = default)
    {
        var report = new ProjectValidationReport { Context = context };

        // Get all patterns
        var allPatterns = new List<CodePattern>();
        foreach (PatternType type in Enum.GetValues(typeof(PatternType)))
        {
            var patterns = await _patternIndexingService.GetPatternsByTypeAsync(type, context, 1000, cancellationToken);
            allPatterns.AddRange(patterns);
        }

        report.TotalPatterns = allPatterns.Count;

        // Validate each pattern
        var detailedResults = new List<PatternQualityResult>();
        foreach (var pattern in allPatterns)
        {
            var validation = await ValidatePatternQualityAsync(pattern.Id, context, false, cancellationToken);
            detailedResults.Add(validation);

            // Track by grade
            var grade = validation.Grade;
            if (!report.PatternsByGrade.ContainsKey(grade))
                report.PatternsByGrade[grade] = 0;
            report.PatternsByGrade[grade]++;

            // Collect critical issues
            var criticalIssues = validation.Issues.Where(i => i.Severity == IssueSeverity.Critical);
            report.CriticalIssues.AddRange(criticalIssues);
        }

        report.DetailedResults = detailedResults;
        
        // Calculate overall score (handle empty case)
        if (detailedResults.Any())
        {
            report.OverallScore = (int)detailedResults.Average(r => r.Score);
        }
        else
        {
            report.OverallScore = 0;
            _logger.LogWarning("No patterns found for context: {Context}", context);
        }
        
        // Get security analysis
        var securityReport = await ValidateSecurityAsync(context, null, cancellationToken);
        report.SecurityScore = securityReport.SecurityScore;
        report.SecurityVulnerabilities = securityReport.Vulnerabilities;

        // Get legacy patterns
        foreach (var pattern in allPatterns.Where(p => !p.IsPositivePattern))
        {
            var migrationPath = await GetMigrationPathAsync(pattern.Id, true, cancellationToken);
            if (migrationPath != null)
                report.LegacyPatterns.Add(migrationPath);
        }

        // Generate top recommendations
        report.TopRecommendations = GenerateTopRecommendations(report);

        if (report.TotalPatterns == 0)
        {
            report.Summary = $"No patterns detected in context '{context}'. Index the project first using the index_directory tool.";
        }
        else
        {
            report.Summary = $"Project Score: {report.OverallScore}/10, Security: {report.SecurityScore}/10, " +
                            $"{report.TotalPatterns} patterns ({report.CriticalIssues.Count} critical issues)";
        }

        return report;
    }

    #region Pattern-Specific Validation

    private PatternQualityResult ValidateCachingPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for expiration policy
        if (!Regex.IsMatch(pattern.Content, @"Expiration|TTL|TimeSpan|AbsoluteExpiration|SlidingExpiration", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Reliability,
                Message = "No cache expiration policy set - risk of stale data and memory leaks",
                ScoreImpact = 3,
                FixGuidance = "Add AbsoluteExpirationRelativeToNow or SlidingExpiration to cache options"
            });
            result.Score -= 3;
        }

        // Check for null handling
        if (!Regex.IsMatch(pattern.Content, @"!=\s*null|\?\.|if\s*\(\s*\w+\s*!=\s*null\)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.High,
                Category = IssueCategory.Correctness,
                Message = "Missing null check after data fetch - can cache null values",
                ScoreImpact = 2,
                FixGuidance = "Add null check: if (data != null) before caching"
            });
            result.Score -= 2;
        }

        // Check for concurrency protection
        if (!Regex.IsMatch(pattern.Content, @"lock|SemaphoreSlim|DistributedLock|Interlocked", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Reliability,
                Message = "No concurrency protection - race condition possible with multiple threads",
                ScoreImpact = 2,
                FixGuidance = "Use lock, SemaphoreSlim, or distributed lock for thread safety"
            });
            result.Score -= 2;
        }

        // Check for cache key prefix
        if (!Regex.IsMatch(pattern.Content, @"\$""[a-z]+:|[A-Z][a-z]+:", RegexOptions.None))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.BestPractice,
                Message = "Cache keys not prefixed - risk of key collisions",
                ScoreImpact = 1,
                FixGuidance = "Prefix cache keys with entity type: $\"user:{id}\""
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Caching Pattern Quality: {result.Grade} ({result.Score}/10) - {result.Issues.Count} issues found";

        // Generate recommendations
        foreach (var issue in result.Issues)
        {
            if (issue.FixGuidance != null)
                result.Recommendations.Add(issue.FixGuidance);
        }

        return result;
    }

    private PatternQualityResult ValidateResiliencePattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for exponential backoff
        if (Regex.IsMatch(pattern.Content, @"RetryAsync|WaitAndRetry", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(pattern.Content, @"Math\.Pow|exponential|backoff", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.BestPractice,
                Message = "Retry policy without exponential backoff - can overwhelm failing service",
                ScoreImpact = 2,
                FixGuidance = "Use exponential backoff: TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))"
            });
            result.Score -= 2;
        }

        // Check for circuit breaker
        if (!Regex.IsMatch(pattern.Content, @"CircuitBreaker|CircuitBreakerAsync", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.BestPractice,
                Message = "No circuit breaker - system won't fail fast during outages",
                ScoreImpact = 1,
                FixGuidance = "Consider adding circuit breaker for faster failure detection"
            });
            result.Score -= 1;
        }

        // Check for logging
        if (!Regex.IsMatch(pattern.Content, @"Log|_logger|ILogger", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.Maintainability,
                Message = "No logging for retry attempts - hard to diagnose issues",
                ScoreImpact = 1,
                FixGuidance = "Add logging in onRetry callback"
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Resilience Pattern Quality: {result.Grade} ({result.Score}/10)";

        foreach (var issue in result.Issues.Where(i => i.FixGuidance != null))
        {
            result.Recommendations.Add(issue.FixGuidance!);
        }

        return result;
    }

    private PatternQualityResult ValidateValidationPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for proper error messages
        if (!Regex.IsMatch(pattern.Content, @"ValidationException|ArgumentException|message", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.Maintainability,
                Message = "Validation without descriptive error messages",
                ScoreImpact = 1
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Validation Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    private PatternQualityResult ValidateAgentFrameworkPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for timeout configuration
        if (!Regex.IsMatch(pattern.Content, @"Timeout|CancellationToken", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.High,
                Category = IssueCategory.Reliability,
                Message = "No timeout configured - agent calls can hang indefinitely",
                ScoreImpact = 2,
                FixGuidance = "Add timeout or pass CancellationToken"
            });
            result.Score -= 2;
        }

        // Check for retry policy
        if (!Regex.IsMatch(pattern.Content, @"Retry|Polly|Circuit", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Reliability,
                Message = "No retry policy - transient failures will immediately fail requests",
                ScoreImpact = 1,
                FixGuidance = "Add Polly retry policy for resilience"
            });
            result.Score -= 1;
        }

        // Check for input validation
        if (!Regex.IsMatch(pattern.Content, @"ArgumentNullException|Guard|Validate|if\s*\(.*==\s*null\)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Security,
                Message = "No input validation - risk of injection attacks or crashes",
                ScoreImpact = 3,
                SecurityReference = "CWE-20: Improper Input Validation",
                FixGuidance = "Validate all user inputs before passing to agent"
            });
            result.Score -= 3;
        }

        // Check for telemetry
        if (!Regex.IsMatch(pattern.Content, @"Log|Telemetry|_logger", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.Maintainability,
                Message = "No telemetry/logging - hard to monitor agent performance",
                ScoreImpact = 1,
                FixGuidance = "Add logging for agent calls and responses"
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Agent Framework Pattern Quality: {result.Grade} ({result.Score}/10)";

        foreach (var issue in result.Issues.Where(i => i.FixGuidance != null))
        {
            result.Recommendations.Add(issue.FixGuidance!);
        }

        return result;
    }

    private PatternQualityResult ValidateAgentLightningPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for reward signals if RL training
        if (pattern.Name.Contains("RLTraining"))
        {
            result.MissingPatterns.Add("RewardSignals");
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Correctness,
                Message = "RL training requires reward signals - check if RewardSignals pattern exists",
                ScoreImpact = 5,
                FixGuidance = "Implement reward signal calculation before training"
            });
            result.Score -= 5;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Agent Lightning Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    private PatternQualityResult ValidateSemanticKernelPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for [Description] attribute on functions
        if (pattern.Name.Contains("Plugin") && 
            !Regex.IsMatch(pattern.Content, @"\[Description\(", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.BestPractice,
                Message = "Missing [Description] attribute - LLM won't understand function purpose",
                ScoreImpact = 2,
                FixGuidance = "Add [Description] to help LLM understand when to call this function"
            });
            result.Score -= 2;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Semantic Kernel Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    private PatternQualityResult ValidateAutoGenPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 2, // Start with low score since it's legacy
            SecurityScore = 5
        };

        // All AutoGen patterns are legacy
        result.Issues.Add(new ValidationIssue
        {
            Severity = IssueSeverity.High,
            Category = IssueCategory.Maintainability,
            Message = "AutoGen pattern is deprecated - migrate to Agent Framework",
            ScoreImpact = 0, // Already penalized with low base score
            FixGuidance = "See migration path for upgrading to Agent Framework"
        });

        // Code execution is especially dangerous
        if (pattern.Name.Contains("CodeExecution"))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Security,
                Message = "Code execution without sandboxing - CRITICAL security risk",
                ScoreImpact = 5,
                SecurityReference = "CWE-94: Improper Control of Generation of Code",
                FixGuidance = "Implement Docker/container sandboxing or migrate to Agent Framework with MCP"
            });
            result.SecurityScore = 1;
        }

        result.Summary = $"AutoGen Pattern (LEGACY): {result.Grade} - Migration Recommended";
        result.Recommendations.Add("Migrate to Agent Framework for better reliability and security");

        return result;
    }

    private PatternQualityResult ValidateSecurityPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // This is a positive security pattern, score it high
        result.Summary = $"Security Pattern: {result.Grade} ({result.Score}/10)";

        return result;
    }

    private PatternQualityResult ValidateErrorHandlingPattern(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for logging
        if (!Regex.IsMatch(pattern.Content, @"Log|_logger", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Maintainability,
                Message = "Error handling without logging - errors will be silent",
                ScoreImpact = 2
            });
            result.Score -= 2;
        }

        // Check for generic catch
        if (Regex.IsMatch(pattern.Content, @"catch\s*\(\s*Exception\s+\w+\s*\)", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(pattern.Content, @"throw;|throw\s+new", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.BestPractice,
                Message = "Catching all exceptions without rethrowing - can hide serious bugs",
                ScoreImpact = 1,
                FixGuidance = "Catch specific exceptions or rethrow after logging"
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Error Handling Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    private PatternQualityResult ValidatePluginArchitecturePattern(CodePattern pattern)
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

    #endregion

    #region Migration Paths

    private PatternMigrationPath GenerateAutoGenMigrationPath(CodePattern pattern, bool includeCodeExample)
    {
        var migration = new PatternMigrationPath
        {
            CurrentPattern = pattern.Name,
            TargetPattern = "Agent Framework Workflow",
            Status = MigrationStatus.Critical,
            EffortEstimate = "2-4 hours",
            Complexity = MigrationComplexity.Medium
        };

        migration.Steps = new List<MigrationStep>
        {
            new() { StepNumber = 1, Title = "Create Workflow Class", 
                   Instructions = "Create new class inheriting from Workflow<TInput, TOutput>",
                   FilesToModify = new List<string> { "Workflows/MyWorkflow.cs" }},
            new() { StepNumber = 2, Title = "Define Input/Output Types",
                   Instructions = "Create strongly-typed input and output records"},
            new() { StepNumber = 3, Title = "Implement ExecuteAsync",
                   Instructions = "Move AutoGen logic to workflow ExecuteAsync method"},
            new() { StepNumber = 4, Title = "Register in DI",
                   Instructions = "Add services.AddSingleton<MyWorkflow>() to Program.cs",
                   FilesToModify = new List<string> { "Program.cs" }},
            new() { StepNumber = 5, Title = "Update Calling Code",
                   Instructions = "Replace AutoGen calls with workflow.ExecuteAsync(input)"},
            new() { StepNumber = 6, Title = "Test & Remove",
                   Instructions = "Test thoroughly, then remove AutoGen references"}
        };

        migration.Benefits = new List<string>
        {
            "Type-safe execution (no runtime errors)",
            "Deterministic workflows (easier debugging)",
            "Better observability (built-in telemetry)",
            "Enterprise features (checkpointing, state management)",
            "Active support and updates"
        };

        migration.Risks = new List<string>
        {
            "AutoGen is deprecated and will not receive updates",
            "Non-deterministic execution makes debugging hard",
            "No type safety leads to runtime errors",
            "Limited enterprise features"
        };

        if (includeCodeExample)
        {
            migration.CodeExample = new PatternCodeExample
            {
                Before = @"// AutoGen (Legacy)
var agent = new ConversableAgent(""assistant"");
var response = await agent.GenerateReplyAsync(messages);",
                After = @"// Agent Framework
public class MyWorkflow : Workflow<MyInput, MyOutput>
{
    protected override async Task<MyOutput> ExecuteAsync(
        MyInput input, CancellationToken cancellationToken)
    {
        var agent = new ChatCompletionAgent(...);
        var response = await agent.InvokeAsync(input.Message);
        return new MyOutput { Response = response };
    }
}",
                Description = "Type-safe workflow with ChatCompletionAgent"
            };
        }

        return migration;
    }

    private PatternMigrationPath GenerateSemanticKernelPlannerMigrationPath(CodePattern pattern, bool includeCodeExample)
    {
        var migration = new PatternMigrationPath
        {
            CurrentPattern = "Semantic Kernel Planner",
            TargetPattern = "Agent Framework Workflow",
            Status = MigrationStatus.Deprecated,
            EffortEstimate = "2-4 hours",
            Complexity = MigrationComplexity.Medium
        };

        migration.Steps = new List<MigrationStep>
        {
            new() { StepNumber = 1, Title = "Analyze Current Plan Steps",
                   Instructions = "Document what your planner currently does"},
            new() { StepNumber = 2, Title = "Create Workflow Class",
                   Instructions = "Create Workflow<TInput, TOutput> with explicit steps"},
            new() { StepNumber = 3, Title = "Move Functions to Workflow",
                   Instructions = "Convert planner functions to workflow methods"},
            new() { StepNumber = 4, Title = "Add Type Safety",
                   Instructions = "Define input/output types for each step"},
            new() { StepNumber = 5, Title = "Test & Migrate",
                   Instructions = "Test workflow, then switch calling code"}
        };

        migration.Benefits = new List<string>
        {
            "Deterministic execution (planners are non-deterministic)",
            "Type safety (no runtime errors from wrong function calls)",
            "Better debugging (can step through workflow)",
            "Explicit control flow (vs AI-generated plan)"
        };

        return migration;
    }

    #endregion

    #region Helper Methods

    private string? GenerateAutoFix(CodePattern pattern, List<ValidationIssue> issues)
    {
        // Simple auto-fix generation based on common issues
        if (pattern.Type == PatternType.Caching)
        {
            var needsExpiration = issues.Any(i => i.Message.Contains("expiration"));
            var needsNullCheck = issues.Any(i => i.Message.Contains("null check"));

            if (needsExpiration || needsNullCheck)
            {
                return @"// Auto-Fix Suggestion:
_cache.Set(key, value, new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
    SlidingExpiration = TimeSpan.FromMinutes(1)
});";
            }
        }

        return null;
    }

    private int CalculateOverallSecurityScore(List<PatternQualityResult> antiPatterns)
    {
        if (!antiPatterns.Any()) return 10;

        var criticalCount = antiPatterns.Sum(p => p.Issues.Count(i => i.Severity == IssueSeverity.Critical));
        var highCount = antiPatterns.Sum(p => p.Issues.Count(i => i.Severity == IssueSeverity.High));

        var score = 10 - (criticalCount * 3) - (highCount * 1);
        return Math.Max(0, score);
    }

    private int CalculateSecurityScore(List<SecurityVulnerability> vulnerabilities)
    {
        if (!vulnerabilities.Any()) return 10;

        var criticalCount = vulnerabilities.Count(v => v.Severity == IssueSeverity.Critical);
        var highCount = vulnerabilities.Count(v => v.Severity == IssueSeverity.High);

        var score = 10 - (criticalCount * 4) - (highCount * 2);
        return Math.Max(0, score);
    }

    private List<string> GenerateRemediationSteps(List<SecurityVulnerability> vulnerabilities)
    {
        var steps = new List<string>();

        foreach (var vuln in vulnerabilities.OrderByDescending(v => v.Severity).Take(5))
        {
            steps.Add($"{vuln.PatternName}: {vuln.Remediation}");
        }

        return steps;
    }

    private List<string> GenerateTopRecommendations(ProjectValidationReport report)
    {
        var recommendations = new List<string>();

        // Critical issues first
        if (report.CriticalIssues.Any())
        {
            recommendations.Add($"🚨 Fix {report.CriticalIssues.Count} critical issues immediately");
        }

        // Security vulnerabilities
        if (report.SecurityVulnerabilities.Any())
        {
            recommendations.Add($"🔒 Address {report.SecurityVulnerabilities.Count} security vulnerabilities");
        }

        // Legacy patterns
        if (report.LegacyPatterns.Any())
        {
            recommendations.Add($"⚠️ Migrate {report.LegacyPatterns.Count} legacy patterns to modern frameworks");
        }

        // Low scoring patterns
        var lowScoreCount = report.DetailedResults.Count(r => r.Score < 5);
        if (lowScoreCount > 0)
        {
            recommendations.Add($"📉 Improve {lowScoreCount} patterns with quality score below 5");
        }

        return recommendations.Take(10).ToList();
    }

    #endregion
}

