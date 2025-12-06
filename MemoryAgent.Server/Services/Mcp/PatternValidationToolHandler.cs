using System.Text;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for pattern validation, best practices, and recommendations
/// Tools: search_patterns, validate_best_practices, get_recommendations, get_available_best_practices,
///        validate_pattern_quality, find_anti_patterns, validate_security, get_migration_path, validate_project
/// </summary>
public class PatternValidationToolHandler : IMcpToolHandler
{
    private readonly IPatternIndexingService _patternService;
    private readonly IBestPracticeValidationService _bestPracticeValidation;
    private readonly IRecommendationService _recommendationService;
    private readonly IPatternValidationService _patternValidationService;
    private readonly IIntentClassificationService _intentClassifier;
    private readonly ILogger<PatternValidationToolHandler> _logger;

    public PatternValidationToolHandler(
        IPatternIndexingService patternService,
        IBestPracticeValidationService bestPracticeValidation,
        IRecommendationService recommendationService,
        IPatternValidationService patternValidationService,
        IIntentClassificationService intentClassifier,
        ILogger<PatternValidationToolHandler> logger)
    {
        _patternService = patternService;
        _bestPracticeValidation = bestPracticeValidation;
        _recommendationService = recommendationService;
        _patternValidationService = patternValidationService;
        _intentClassifier = intentClassifier;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "search_patterns",
                Description = "Search for code patterns (caching, retry logic, validation, etc.) using semantic search. Returns detected patterns with confidence scores and Azure best practice links.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Pattern search query (e.g., 'caching patterns', 'retry logic', 'validation')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results", @default = 20 }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "validate_best_practices",
                Description = "Validate a project against Azure best practices. Returns compliance score, which practices are implemented, and which are missing with recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to validate" },
                        bestPractices = new { type = "array", items = new { type = "string" }, description = "Specific practices to check (optional, defaults to all 21 practices)" },
                        includeExamples = new { type = "boolean", description = "Include code examples in results", @default = true },
                        maxExamplesPerPractice = new { type = "number", description = "Maximum examples per practice", @default = 5 }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_recommendations",
                Description = "Analyze a project and get prioritized recommendations for missing or weak patterns. Returns health score and actionable recommendations with code examples.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to analyze" },
                        categories = new { type = "array", items = new { type = "string" }, description = "Focus on specific categories (optional)" },
                        includeLowPriority = new { type = "boolean", description = "Include low-priority recommendations", @default = false },
                        maxRecommendations = new { type = "number", description = "Maximum recommendations to return", @default = 10 }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_available_best_practices",
                Description = "Get list of all available Azure best practices that can be validated.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "validate_pattern_quality",
                Description = "Deep validation of a specific pattern's implementation quality. Returns quality score (1-10), grade (A-F), issues found, and auto-fix code if available.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern_id = new { type = "string", description = "Pattern ID to validate" },
                        context = new { type = "string", description = "Project context (optional)" },
                        include_auto_fix = new { type = "boolean", description = "Include auto-fix code", @default = true },
                        min_severity = new { type = "string", description = "Minimum severity to report (low|medium|high|critical)", @default = "low" }
                    },
                    required = new[] { "pattern_id" }
                }
            },
            new McpTool
            {
                Name = "find_anti_patterns",
                Description = "Find all anti-patterns and badly implemented patterns in a project. Returns patterns with issues, security vulnerabilities, and overall security score.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to search" },
                        min_severity = new { type = "string", description = "Minimum severity (low|medium|high|critical)", @default = "medium" },
                        include_legacy = new { type = "boolean", description = "Include legacy/deprecated patterns", @default = true }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "validate_security",
                Description = "Security audit of detected patterns. Returns overall security score, vulnerabilities found, and remediation steps.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to validate" },
                        pattern_types = new { type = "array", items = new { type = "string" }, description = "Specific pattern types to check (optional)" }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_migration_path",
                Description = "Get step-by-step migration path for legacy/deprecated patterns. Returns detailed migration instructions, code examples, and effort estimate.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern_id = new { type = "string", description = "Pattern ID to get migration path for" },
                        include_code_example = new { type = "boolean", description = "Include before/after code example", @default = true }
                    },
                    required = new[] { "pattern_id" }
                }
            },
            new McpTool
            {
                Name = "validate_project",
                Description = "Comprehensive project validation. Returns overall quality/security scores, all pattern validations, vulnerabilities, legacy patterns, and top recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to validate" }
                    },
                    required = new[] { "context" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "search_patterns" => await SearchPatternsToolAsync(args, cancellationToken),
            "validate_best_practices" => await ValidateBestPracticesToolAsync(args, cancellationToken),
            "get_recommendations" => await GetRecommendationsToolAsync(args, cancellationToken),
            "get_available_best_practices" => await GetAvailableBestPracticesToolAsync(args, cancellationToken),
            "validate_pattern_quality" => await ValidatePatternQualityToolAsync(args, cancellationToken),
            "find_anti_patterns" => await FindAntiPatternsToolAsync(args, cancellationToken),
            "validate_security" => await ValidateSecurityToolAsync(args, cancellationToken),
            "get_migration_path" => await GetMigrationPathToolAsync(args, cancellationToken),
            "validate_project" => await ValidateProjectToolAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    // Implementation of all 8 tool methods follows...
    
    private async Task<McpToolResult> SearchPatternsToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var query = args?.GetValueOrDefault("query")?.ToString() ?? "";
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 20);

        if (string.IsNullOrWhiteSpace(query))
            return ErrorResult("Query is required");

        // 🧠 Intent classification for pattern suggestions
        try
        {
            var intent = await _intentClassifier.ClassifyIntentAsync(query, context, cancellationToken);
            var suggestedCategories = await _intentClassifier.SuggestPatternCategoriesAsync(intent, cancellationToken);
            _logger.LogInformation("🎯 Auto-detected pattern categories from query: {Categories}", 
                string.Join(", ", suggestedCategories));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent classification failed for pattern search, using query as-is");
        }

        var patterns = await _patternService.SearchPatternsAsync(query, context, limit, cancellationToken);

        if (!patterns.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"No patterns found for query: '{query}'" }
                }
            };
        }

        var text = $"🔍 Found {patterns.Count} pattern(s) for '{query}':\n\n";
        
        foreach (var pattern in patterns)
        {
            text += $"📊 {pattern.Name}\n";
            text += $"   Type: {pattern.Type} ({pattern.Category})\n";
            text += $"   Implementation: {pattern.Implementation}\n";
            text += $"   Language: {pattern.Language}\n";
            text += $"   File: {pattern.FilePath}:{pattern.LineNumber}\n";
            text += $"   Confidence: {pattern.Confidence:P0}\n";
            text += $"   Best Practice: {pattern.BestPractice}\n";
            if (!string.IsNullOrEmpty(pattern.AzureBestPracticeUrl))
                text += $"   📚 Azure Docs: {pattern.AzureBestPracticeUrl}\n";
            text += $"\n   Code:\n   {TruncateCode(pattern.Content, 200)}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateBestPracticesToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("Context is required");

        var includeExamples = SafeParseBool(args?.GetValueOrDefault("includeExamples"), true);
        var maxExamples = SafeParseInt(args?.GetValueOrDefault("maxExamplesPerPractice"), 5);

        var bestPractices = new List<string>();
        if (args?.TryGetValue("bestPractices", out var bpObj) == true && bpObj is IEnumerable<object> bpList)
        {
            bestPractices = bpList.Select(bp => bp.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        var request = new BestPracticeValidationRequest
        {
            Context = context,
            BestPractices = bestPractices.Any() ? bestPractices : null,
            IncludeExamples = includeExamples,
            MaxExamplesPerPractice = maxExamples
        };

        var result = await _bestPracticeValidation.ValidateBestPracticesAsync(request, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"📋 Best Practice Validation for '{context}'\n");
        sb.AppendLine($"Overall Score: {result.OverallScore:P0} ({result.PracticesImplemented}/{result.TotalPracticesChecked} practices)");
        sb.AppendLine($"✅ Implemented: {result.PracticesImplemented}");
        sb.AppendLine($"❌ Missing: {result.PracticesMissing}\n");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        var implemented = result.Results.Where(r => r.Implemented).ToList();
        var missing = result.Results.Where(r => !r.Implemented).ToList();

        if (implemented.Any())
        {
            sb.AppendLine("✅ IMPLEMENTED PRACTICES:\n");
            foreach (var practice in implemented.OrderByDescending(p => p.Count))
            {
                sb.AppendLine($"• {practice.Practice} ({practice.PatternType})");
                sb.AppendLine($"  Count: {practice.Count} instances");
                sb.AppendLine($"  Avg Confidence: {practice.AverageConfidence:P0}");
                if (includeExamples && practice.Examples.Any())
                {
                    sb.AppendLine("  Examples:");
                    foreach (var example in practice.Examples.Take(3))
                        sb.AppendLine($"    - {example.FilePath}:{example.LineNumber} ({example.Implementation})");
                }
                sb.AppendLine();
            }
        }

        if (missing.Any())
        {
            sb.AppendLine("\n❌ MISSING PRACTICES:\n");
            foreach (var practice in missing)
            {
                sb.AppendLine($"• {practice.Practice} ({practice.PatternType})");
                sb.AppendLine($"  Recommendation: {practice.Recommendation}");
                if (!string.IsNullOrEmpty(practice.AzureUrl))
                    sb.AppendLine($"  📚 Learn more: {practice.AzureUrl}");
                sb.AppendLine();
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = sb.ToString() }
            }
        };
    }

    private async Task<McpToolResult> GetRecommendationsToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("Context is required");

        var includeLowPriority = SafeParseBool(args?.GetValueOrDefault("includeLowPriority"), false);
        var maxRecommendations = SafeParseInt(args?.GetValueOrDefault("maxRecommendations"), 10);

        var categories = new List<PatternCategory>();
        if (args?.TryGetValue("categories", out var catObj) == true && catObj is IEnumerable<object> catList)
        {
            foreach (var cat in catList)
            {
                if (Enum.TryParse<PatternCategory>(cat.ToString(), out var category))
                    categories.Add(category);
            }
        }

        var request = new RecommendationRequest
        {
            Context = context,
            Categories = categories.Any() ? categories : null,
            IncludeLowPriority = includeLowPriority,
            MaxRecommendations = maxRecommendations
        };

        var result = await _recommendationService.AnalyzeAndRecommendAsync(request, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"🎯 Architecture Recommendations for '{context}'\n");
        sb.AppendLine($"Overall Health: {result.OverallHealth:P0}");
        sb.AppendLine($"Patterns Detected: {result.TotalPatternsDetected}");
        sb.AppendLine($"Recommendations: {result.Recommendations.Count}\n");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        if (!result.Recommendations.Any())
        {
            sb.AppendLine("✅ No critical recommendations! Your project looks good.");
        }
        else
        {
            var groups = new[]
            {
                ("🚨 CRITICAL PRIORITY:\n", result.Recommendations.Where(r => r.Priority == "CRITICAL")),
                ("\n⚠️  HIGH PRIORITY:\n", result.Recommendations.Where(r => r.Priority == "HIGH")),
                ("\n📌 MEDIUM PRIORITY:\n", result.Recommendations.Where(r => r.Priority == "MEDIUM")),
                ("\n💡 LOW PRIORITY:\n", result.Recommendations.Where(r => r.Priority == "LOW"))
            };

            foreach (var (header, recs) in groups)
            {
                if (!recs.Any() || (header.Contains("LOW") && !includeLowPriority))
                    continue;

                sb.AppendLine(header);
                foreach (var rec in recs)
                    sb.AppendLine(FormatRecommendation(rec));
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = sb.ToString() }
            }
        };
    }

    private async Task<McpToolResult> GetAvailableBestPracticesToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var practices = await _bestPracticeValidation.GetAvailableBestPracticesAsync(cancellationToken);

        var text = "📚 Available Azure Best Practices:\n\n";
        
        var grouped = practices.GroupBy(p => GetCategoryFromPracticeName(p));
        
        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            text += $"{group.Key}:\n";
            foreach (var practice in group.OrderBy(p => p))
                text += $"  • {practice}\n";
            text += "\n";
        }

        text += "\nUsage: Call validate_best_practices with specific practice names, or omit to check all practices.\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidatePatternQualityToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var patternId = args?.GetValueOrDefault("pattern_id")?.ToString() ?? "";
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var includeAutoFix = SafeParseBool(args?.GetValueOrDefault("include_auto_fix"), true);
        var minSeverityStr = args?.GetValueOrDefault("min_severity")?.ToString() ?? "low";
        
        var minSeverity = minSeverityStr.ToLower() switch
        {
            "critical" => IssueSeverity.Critical,
            "high" => IssueSeverity.High,
            "medium" => IssueSeverity.Medium,
            _ => IssueSeverity.Low
        };

        var result = await _patternValidationService.ValidatePatternQualityAsync(patternId, context, includeAutoFix, cancellationToken);

        var text = $"🔍 Pattern Quality Validation\n\n" +
                   $"Pattern: {result.Pattern.Name}\n" +
                   $"Quality Score: {result.Score}/10 (Grade: {result.Grade})\n" +
                   $"Security Score: {result.SecurityScore}/10\n\n";

        if (result.Issues.Any())
        {
            text += "❌ Issues Found:\n\n";
            foreach (var issue in result.Issues.Where(i => i.Severity >= minSeverity))
            {
                var icon = issue.Severity switch
                {
                    IssueSeverity.Critical => "🚨",
                    IssueSeverity.High => "❌",
                    IssueSeverity.Medium => "⚠️",
                    _ => "ℹ️"
                };
                text += $"{icon} {issue.Severity}: {issue.Message}\n";
                if (issue.FixGuidance != null)
                    text += $"   💡 Fix: {issue.FixGuidance}\n";
                text += "\n";
            }
        }

        if (result.Recommendations.Any())
        {
            text += "📋 Recommendations:\n";
            foreach (var rec in result.Recommendations)
                text += $"• {rec}\n";
            text += "\n";
        }

        if (!string.IsNullOrEmpty(result.AutoFixCode))
            text += $"🔧 Auto-Fix Code:\n\n```\n{result.AutoFixCode}\n```\n\n";

        text += $"Summary: {result.Summary}\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> FindAntiPatternsToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "";
        var minSeverityStr = args?.GetValueOrDefault("min_severity")?.ToString() ?? "medium";
        var includeLegacy = SafeParseBool(args?.GetValueOrDefault("include_legacy"), true);

        var minSeverity = minSeverityStr.ToLower() switch
        {
            "critical" => IssueSeverity.Critical,
            "high" => IssueSeverity.High,
            "low" => IssueSeverity.Low,
            _ => IssueSeverity.Medium
        };

        var result = await _patternValidationService.FindAntiPatternsAsync(context, minSeverity, includeLegacy, cancellationToken);

        var text = $"🚨 Anti-Pattern Analysis for {context}\n\n" +
                   $"Total Anti-Patterns Found: {result.TotalCount}\n" +
                   $"Critical Issues: {result.CriticalCount}\n" +
                   $"Overall Security Score: {result.OverallSecurityScore}/10\n\n";

        if (result.AntiPatterns.Any())
        {
            text += "📋 Anti-Patterns Detected:\n\n";
            foreach (var antiPattern in result.AntiPatterns.Take(10))
            {
                text += $"• {antiPattern.Pattern.Name} (Score: {antiPattern.Score}/10)\n" +
                        $"  File: {antiPattern.Pattern.FilePath}\n";
                if (antiPattern.Issues.Any())
                {
                    var topIssue = antiPattern.Issues.OrderByDescending(i => i.Severity).First();
                    text += $"  🚨 {topIssue.Severity}: {topIssue.Message}\n";
                }
                text += "\n";
            }

            if (result.AntiPatterns.Count > 10)
                text += $"... and {result.AntiPatterns.Count - 10} more\n\n";
        }

        text += $"Summary: {result.Summary}\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateSecurityToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "";

        var result = await _patternValidationService.ValidateSecurityAsync(context, null, cancellationToken);

        var text = $"🔒 Security Validation for {context}\n\n" +
                   $"Security Score: {result.SecurityScore}/10 ({result.Grade})\n" +
                   $"Vulnerabilities Found: {result.Vulnerabilities.Count}\n\n";

        if (result.Vulnerabilities.Any())
        {
            text += "🚨 Security Vulnerabilities:\n\n";
            foreach (var vuln in result.Vulnerabilities.Take(10))
            {
                var icon = vuln.Severity switch
                {
                    IssueSeverity.Critical => "🚨",
                    IssueSeverity.High => "❗",
                    IssueSeverity.Medium => "⚠️",
                    _ => "ℹ️"
                };
                text += $"{icon} {vuln.Severity} - {vuln.PatternName}\n" +
                        $"  Description: {vuln.Description}\n" +
                        $"  File: {vuln.FilePath}\n";
                if (!string.IsNullOrEmpty(vuln.Reference))
                    text += $"  Reference: {vuln.Reference}\n";
                text += $"  🔧 Remediation: {vuln.Remediation}\n\n";
            }
        }

        if (result.RemediationSteps.Any())
        {
            text += "📋 Priority Remediation Steps:\n";
            foreach (var step in result.RemediationSteps.Take(5))
                text += $"• {step}\n";
            text += "\n";
        }

        text += $"Summary: {result.Summary}\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> GetMigrationPathToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var patternId = args?.GetValueOrDefault("pattern_id")?.ToString() ?? "";
        var includeCodeExample = SafeParseBool(args?.GetValueOrDefault("include_code_example"), true);

        var result = await _patternValidationService.GetMigrationPathAsync(patternId, includeCodeExample, cancellationToken);

        if (result == null)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"No migration path available for pattern {patternId}" }
                }
            };
        }

        var text = $"🔄 Migration Path\n\n" +
                   $"Current Pattern: {result.CurrentPattern}\n" +
                   $"Target Pattern: {result.TargetPattern}\n" +
                   $"Status: {result.Status}\n" +
                   $"Effort Estimate: {result.EffortEstimate}\n" +
                   $"Complexity: {result.Complexity}\n\n" +
                   "📋 Migration Steps:\n\n";

        foreach (var step in result.Steps)
        {
            text += $"{step.StepNumber}. {step.Title}\n   {step.Instructions}\n";
            if (step.FilesToModify.Any())
                text += $"   Files: {string.Join(", ", step.FilesToModify)}\n";
            text += "\n";
        }

        if (result.CodeExample != null)
        {
            text += $"💡 Code Example:\n\n{result.CodeExample.Description}\n\n" +
                    $"Before:\n```\n{result.CodeExample.Before}\n```\n\n" +
                    $"After:\n```\n{result.CodeExample.After}\n```\n\n";
        }

        if (result.Benefits.Any())
        {
            text += "✅ Benefits:\n";
            foreach (var benefit in result.Benefits)
                text += $"• {benefit}\n";
            text += "\n";
        }

        if (result.Risks.Any())
        {
            text += "⚠️ Risks of NOT Migrating:\n";
            foreach (var risk in result.Risks)
                text += $"• {risk}\n";
            text += "\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateProjectToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "";

        var result = await _patternValidationService.ValidateProjectAsync(context, cancellationToken);

        var text = $"📊 Project Validation Report - {context}\n\n" +
                   $"Overall Quality Score: {result.OverallScore}/10\n" +
                   $"Security Score: {result.SecurityScore}/10\n" +
                   $"Total Patterns: {result.TotalPatterns}\n\n" +
                   "📈 Patterns by Grade:\n";

        foreach (var grade in result.PatternsByGrade.OrderBy(g => g.Key))
            text += $"  Grade {grade.Key}: {grade.Value} patterns\n";
        text += "\n";

        if (result.CriticalIssues.Any())
        {
            text += $"🚨 Critical Issues ({result.CriticalIssues.Count}):\n";
            foreach (var issue in result.CriticalIssues.Take(5))
                text += $"  • {issue.Message}\n";
            if (result.CriticalIssues.Count > 5)
                text += $"  ... and {result.CriticalIssues.Count - 5} more\n";
            text += "\n";
        }

        if (result.SecurityVulnerabilities.Any())
        {
            text += $"🔒 Security Vulnerabilities ({result.SecurityVulnerabilities.Count}):\n";
            foreach (var vuln in result.SecurityVulnerabilities.Take(5))
                text += $"  {vuln.Severity}: {vuln.Description}\n";
            if (result.SecurityVulnerabilities.Count > 5)
                text += $"  ... and {result.SecurityVulnerabilities.Count - 5} more\n";
            text += "\n";
        }

        if (result.LegacyPatterns.Any())
        {
            text += $"⚠️ Legacy Patterns Needing Migration ({result.LegacyPatterns.Count}):\n";
            foreach (var legacy in result.LegacyPatterns.Take(5))
                text += $"  • {legacy.CurrentPattern} → {legacy.TargetPattern} ({legacy.EffortEstimate})\n";
            if (result.LegacyPatterns.Count > 5)
                text += $"  ... and {result.LegacyPatterns.Count - 5} more\n";
            text += "\n";
        }

        if (result.TopRecommendations.Any())
        {
            text += "📋 Top Recommendations:\n";
            foreach (var rec in result.TopRecommendations)
                text += $"  {rec}\n";
            text += "\n";
        }

        text += $"Summary: {result.Summary}\n" +
                $"Generated: {result.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    // Helper methods
    private string FormatRecommendation(PatternRecommendation rec)
    {
        var text = $"• [{rec.Category}] {rec.Issue}\n" +
                   $"  💡 {rec.Recommendation}\n" +
                   $"  Impact: {rec.Impact}\n";
        if (!string.IsNullOrEmpty(rec.AzureUrl))
            text += $"  📚 {rec.AzureUrl}\n";
        if (rec.CodeExample != null)
            text += $"  \n  Example:\n  ```\n  {TruncateCode(rec.CodeExample, 150)}\n  ```\n";
        return text + "\n";
    }

    private string GetCategoryFromPracticeName(string practice)
    {
        if (practice.Contains("Retry") || practice.Contains("Circuit") || practice.Contains("Timeout"))
            return "Resilience";
        if (practice.Contains("Cache") || practice.Contains("Caching"))
            return "Performance";
        if (practice.Contains("Security") || practice.Contains("Authentication") || practice.Contains("Authorization"))
            return "Security";
        if (practice.Contains("Validation") || practice.Contains("Input"))
            return "Validation";
        if (practice.Contains("Log") || practice.Contains("Monitoring") || practice.Contains("Telemetry"))
            return "Observability";
        return "General";
    }

    private string TruncateCode(string code, int maxLength)
    {
        if (string.IsNullOrEmpty(code) || code.Length <= maxLength)
            return code;
        return code.Substring(0, maxLength) + "...";
    }

    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            _ => defaultValue
        };

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var i) => i,
            _ => defaultValue
        };

    private McpToolResult ErrorResult(string error) => new McpToolResult
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };
}

