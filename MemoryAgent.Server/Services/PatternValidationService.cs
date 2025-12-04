using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services.PatternValidation;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Orchestrator service for pattern validation
/// Routes validation requests to specific pattern validators
/// </summary>
public class PatternValidationService : IPatternValidationService
{
    private readonly IPatternIndexingService _patternIndexingService;
    private readonly ISemgrepService _semgrepService;
    private readonly ILogger<PatternValidationService> _logger;
    private readonly Dictionary<PatternType, IPatternValidator> _validators;

    public PatternValidationService(
        IPatternIndexingService patternIndexingService,
        ISemgrepService semgrepService,
        IEnumerable<IPatternValidator> validators,
        ILogger<PatternValidationService> logger)
    {
        _patternIndexingService = patternIndexingService;
        _semgrepService = semgrepService;
        _logger = logger;

        // Build validator dictionary from injected validators
        _validators = new Dictionary<PatternType, IPatternValidator>();
        foreach (var validator in validators)
        {
            foreach (var type in validator.SupportedPatternTypes)
            {
                _validators[type] = validator;
            }
        }
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

        // Route to appropriate validator
        PatternQualityResult result;
        if (_validators.TryGetValue(pattern.Type, out var validator))
        {
            result = validator.Validate(pattern);
        }
        else
        {
            result = new PatternQualityResult
            {
                Pattern = pattern,
                Score = 5,
                Summary = "No specific validation rules for this pattern type yet"
            };
        }

        // Generate auto-fix if requested and issues found
        if (includeAutoFix && result.Issues.Any() && string.IsNullOrEmpty(result.AutoFixCode))
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
        
        foreach (PatternType type in Enum.GetValues(typeof(PatternType)))
        {
            var patterns = await _patternIndexingService.GetPatternsByTypeAsync(type, context, 1000, cancellationToken);
            if (patterns.Any())
                allPatterns.AddRange(patterns);
        }

        // Validate each pattern
        foreach (var pattern in allPatterns)
        {
            if (!includeLegacy && pattern.IsPositivePattern)
                continue;

            var validation = await ValidatePatternQualityAsync(pattern.Id, context, false, cancellationToken);

            if (validation.Pattern.Name == "Not Found" && validation.Score == 0)
                continue;

            var hasSignificantIssues = validation.Issues.Any(i => i.Severity >= minSeverity);
            var isLegacyPattern = !pattern.IsPositivePattern;

            if (hasSignificantIssues || isLegacyPattern)
            {
                response.AntiPatterns.Add(validation);

                if (validation.Issues.Any(i => i.Severity == IssueSeverity.Critical))
                    response.CriticalCount++;
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

        patternTypes ??= new List<PatternType>
        {
            PatternType.Security,
            PatternType.AutoGen,
            PatternType.ApiDesign,
            PatternType.Validation
        };

        var vulnerabilities = new List<SecurityVulnerability>();

        foreach (var type in patternTypes)
        {
            var patterns = await _patternIndexingService.GetPatternsByTypeAsync(type, context, 1000, cancellationToken);

            foreach (var pattern in patterns)
            {
                var validation = await ValidatePatternQualityAsync(pattern.Id, context, false, cancellationToken);

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

        response.Vulnerabilities = vulnerabilities.OrderByDescending(v => v.Severity).ToList();
        response.SecurityScore = CalculateSecurityScore(vulnerabilities);
        response.RemediationSteps = GenerateRemediationSteps(vulnerabilities);
        response.Summary = $"Security Score: {response.SecurityScore}/10 ({response.Grade}), {vulnerabilities.Count} vulnerabilities found";

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

        // Check if validator provides migration path
        if (_validators.TryGetValue(pattern.Type, out var validator))
        {
            var migrationPath = validator.GenerateMigrationPath(pattern, includeCodeExample);
            if (migrationPath != null)
                return migrationPath;
        }

        // Fallback to built-in migration paths
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

        var allPatterns = new List<CodePattern>();
        foreach (PatternType type in Enum.GetValues(typeof(PatternType)))
        {
            var patterns = await _patternIndexingService.GetPatternsByTypeAsync(type, context, 1000, cancellationToken);
            allPatterns.AddRange(patterns);
        }

        report.TotalPatterns = allPatterns.Count;

        var detailedResults = new List<PatternQualityResult>();
        foreach (var pattern in allPatterns)
        {
            var validation = await ValidatePatternQualityAsync(pattern.Id, context, false, cancellationToken);
            detailedResults.Add(validation);

            var grade = validation.Grade;
            if (!report.PatternsByGrade.ContainsKey(grade))
                report.PatternsByGrade[grade] = 0;
            report.PatternsByGrade[grade]++;

            var criticalIssues = validation.Issues.Where(i => i.Severity == IssueSeverity.Critical);
            report.CriticalIssues.AddRange(criticalIssues);
        }

        report.DetailedResults = detailedResults;
        report.OverallScore = detailedResults.Any() ? (int)detailedResults.Average(r => r.Score) : 0;

        var securityReport = await ValidateSecurityAsync(context, null, cancellationToken);
        report.SecurityScore = securityReport.SecurityScore;
        report.SecurityVulnerabilities = securityReport.Vulnerabilities;

        foreach (var pattern in allPatterns.Where(p => !p.IsPositivePattern))
        {
            var migrationPath = await GetMigrationPathAsync(pattern.Id, true, cancellationToken);
            if (migrationPath != null)
                report.LegacyPatterns.Add(migrationPath);
        }

        report.TopRecommendations = GenerateTopRecommendations(report);
        report.Summary = report.TotalPatterns == 0
            ? $"No patterns detected in context '{context}'. Index the project first using the index_directory tool."
            : $"Project Score: {report.OverallScore}/10, Security: {report.SecurityScore}/10, " +
              $"{report.TotalPatterns} patterns ({report.CriticalIssues.Count} critical issues)";

        return report;
    }

    #region Helper Methods

    private string? GenerateAutoFix(CodePattern pattern, List<ValidationIssue> issues)
    {
        if (pattern.Type == PatternType.Caching)
        {
            var needsExpiration = issues.Any(i => i.Message.Contains("expiration"));
            if (needsExpiration)
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
        return vulnerabilities
            .OrderByDescending(v => v.Severity)
            .Take(5)
            .Select(v => $"{v.PatternName}: {v.Remediation}")
            .ToList();
    }

    private List<string> GenerateTopRecommendations(ProjectValidationReport report)
    {
        var recommendations = new List<string>();

        if (report.CriticalIssues.Any())
            recommendations.Add($"🚨 Fix {report.CriticalIssues.Count} critical issues immediately");

        if (report.SecurityVulnerabilities.Any())
            recommendations.Add($"🔒 Address {report.SecurityVulnerabilities.Count} security vulnerabilities");

        if (report.LegacyPatterns.Any())
            recommendations.Add($"⚠️ Migrate {report.LegacyPatterns.Count} legacy patterns to modern frameworks");

        var lowScoreCount = report.DetailedResults.Count(r => r.Score < 5);
        if (lowScoreCount > 0)
            recommendations.Add($"📉 Improve {lowScoreCount} patterns with quality score below 5");

        return recommendations.Take(10).ToList();
    }

    private PatternMigrationPath GenerateAutoGenMigrationPath(CodePattern pattern, bool includeCodeExample)
    {
        return new PatternMigrationPath
        {
            CurrentPattern = pattern.Name,
            TargetPattern = "Agent Framework Workflow",
            Status = MigrationStatus.Critical,
            EffortEstimate = "2-4 hours",
            Complexity = MigrationComplexity.Medium,
            Steps = new List<MigrationStep>
            {
                new() { StepNumber = 1, Title = "Create Workflow Class", Instructions = "Create new class inheriting from Workflow<TInput, TOutput>"},
                new() { StepNumber = 2, Title = "Define Input/Output Types", Instructions = "Create strongly-typed input and output records"},
                new() { StepNumber = 3, Title = "Implement ExecuteAsync", Instructions = "Move AutoGen logic to workflow ExecuteAsync method"},
                new() { StepNumber = 4, Title = "Register in DI", Instructions = "Add services.AddSingleton<MyWorkflow>() to Program.cs"},
                new() { StepNumber = 5, Title = "Update Calling Code", Instructions = "Replace AutoGen calls with workflow.ExecuteAsync(input)"},
                new() { StepNumber = 6, Title = "Test & Remove", Instructions = "Test thoroughly, then remove AutoGen references"}
            },
            Benefits = new List<string>
            {
                "Type-safe execution (no runtime errors)",
                "Deterministic workflows (easier debugging)",
                "Better observability (built-in telemetry)",
                "Enterprise features (checkpointing, state management)"
            },
            Risks = new List<string>
            {
                "AutoGen is deprecated and will not receive updates",
                "Non-deterministic execution makes debugging hard",
                "No type safety leads to runtime errors"
            },
            CodeExample = includeCodeExample ? new PatternCodeExample
            {
                Before = "// AutoGen (Legacy)\nvar agent = new ConversableAgent(\"assistant\");\nvar response = await agent.GenerateReplyAsync(messages);",
                After = "// Agent Framework\npublic class MyWorkflow : Workflow<MyInput, MyOutput>\n{\n    protected override async Task<MyOutput> ExecuteAsync(\n        MyInput input, CancellationToken cancellationToken)\n    {\n        var agent = new ChatCompletionAgent(...);\n        var response = await agent.InvokeAsync(input.Message);\n        return new MyOutput { Response = response };\n    }\n}",
                Description = "Type-safe workflow with ChatCompletionAgent"
            } : null
        };
    }

    private PatternMigrationPath GenerateSemanticKernelPlannerMigrationPath(CodePattern pattern, bool includeCodeExample)
    {
        return new PatternMigrationPath
        {
            CurrentPattern = "Semantic Kernel Planner",
            TargetPattern = "Agent Framework Workflow",
            Status = MigrationStatus.Deprecated,
            EffortEstimate = "2-4 hours",
            Complexity = MigrationComplexity.Medium,
            Steps = new List<MigrationStep>
            {
                new() { StepNumber = 1, Title = "Analyze Current Plan Steps", Instructions = "Document what your planner currently does"},
                new() { StepNumber = 2, Title = "Create Workflow Class", Instructions = "Create Workflow<TInput, TOutput> with explicit steps"},
                new() { StepNumber = 3, Title = "Move Functions to Workflow", Instructions = "Convert planner functions to workflow methods"},
                new() { StepNumber = 4, Title = "Add Type Safety", Instructions = "Define input/output types for each step"},
                new() { StepNumber = 5, Title = "Test & Migrate", Instructions = "Test workflow, then switch calling code"}
            },
            Benefits = new List<string>
            {
                "Deterministic execution (planners are non-deterministic)",
                "Type safety (no runtime errors from wrong function calls)",
                "Better debugging (can step through workflow)",
                "Explicit control flow (vs AI-generated plan)"
            }
        };
    }

    #endregion
}
