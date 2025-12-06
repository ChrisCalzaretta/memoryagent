using System.Text;
using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for code analysis operations.
/// Tools: impact_analysis, dependency_chain, analyze_complexity, validate
/// </summary>
public class AnalysisToolHandler : IMcpToolHandler
{
    private readonly IGraphService _graphService;
    private readonly ICodeComplexityService _complexityService;
    private readonly IBestPracticeValidationService _bestPracticeService;
    private readonly IPatternValidationService _patternValidationService;
    private readonly ILogger<AnalysisToolHandler> _logger;

    public AnalysisToolHandler(
        IGraphService graphService,
        ICodeComplexityService complexityService,
        IBestPracticeValidationService bestPracticeService,
        IPatternValidationService patternValidationService,
        ILogger<AnalysisToolHandler> logger)
    {
        _graphService = graphService;
        _complexityService = complexityService;
        _bestPracticeService = bestPracticeService;
        _patternValidationService = patternValidationService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "impact_analysis",
                Description = "Analyze what code would be impacted if a class changes. Shows all dependents.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name to analyze" }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "dependency_chain",
                Description = "Get the dependency chain for a class. Shows all dependencies and optionally detects circular dependencies.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name" },
                        maxDepth = new { type = "number", description = "Maximum depth to traverse (default: 5)", @default = 5 },
                        includeCircular = new { type = "boolean", description = "Also check for circular dependencies (default: false)", @default = false },
                        context = new { type = "string", description = "Project context (required if includeCircular is true)" }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "analyze_complexity",
                Description = "Analyze code complexity metrics (cyclomatic, cognitive, LOC, nesting, code smells). Returns grades and recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the file to analyze" },
                        methodName = new { type = "string", description = "Optional: specific method to analyze (if omitted, analyzes all)" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new McpTool
            {
                Name = "validate",
                Description = "Unified validation tool. Use scope to choose what to validate: 'best_practices', 'security', 'pattern_quality', 'anti_patterns', 'project' (comprehensive), 'list_best_practices' (list available), or 'all'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to validate (not required for list_best_practices)" },
                        scope = new { type = "string", description = "Validation scope", @default = "best_practices", @enum = new[] { "best_practices", "security", "pattern_quality", "anti_patterns", "project", "list_best_practices", "all" } },
                        patternId = new { type = "string", description = "For scope='pattern_quality': specific pattern ID to validate" },
                        minSeverity = new { type = "string", description = "Minimum severity to report (default: medium)", @default = "medium", @enum = new[] { "low", "medium", "high", "critical" } },
                        includeAutoFix = new { type = "boolean", description = "Include auto-fix code suggestions (default: true)", @default = true },
                        includeLegacy = new { type = "boolean", description = "For scope='anti_patterns': include legacy patterns (default: true)", @default = true }
                    },
                    required = Array.Empty<string>()
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
            "impact_analysis" => await ImpactAnalysisAsync(args, cancellationToken),
            "dependency_chain" => await DependencyChainAsync(args, cancellationToken),
            "analyze_complexity" => await AnalyzeComplexityAsync(args, cancellationToken),
            "validate" => await ValidateAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Graph Analysis

    private async Task<McpToolResult> ImpactAnalysisAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();

        if (string.IsNullOrWhiteSpace(className))
            return ErrorResult("className is required");

        var impacted = await _graphService.GetImpactAnalysisAsync(className, ct);
        
        var output = $"🎯 Impact Analysis for {className}\n\n";
        
        if (!impacted.Any())
        {
            output += "No dependents found. This class is either:\n";
            output += "• A leaf node (no other code depends on it)\n";
            output += "• Not indexed yet\n";
        }
        else
        {
            output += $"⚠️ {impacted.Count} classes would be affected by changes:\n\n";
            foreach (var c in impacted.Take(50))
            {
                output += $"  • {c}\n";
            }
            if (impacted.Count > 50)
                output += $"\n  ... and {impacted.Count - 50} more\n";
            
            output += "\n💡 Consider these dependents when making changes!";
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> DependencyChainAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();
        var maxDepth = SafeParseInt(args?.GetValueOrDefault("maxDepth"), 5);
        var includeCircular = SafeParseBool(args?.GetValueOrDefault("includeCircular"), false);
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(className))
            return ErrorResult("className is required");

        var dependencies = await _graphService.GetDependencyChainAsync(className, maxDepth, ct);
        
        var output = $"🔗 Dependency Chain for {className}\n";
        output += $"Max Depth: {maxDepth}\n\n";
        
        if (!dependencies.Any())
        {
            output += "No dependencies found. This class is either:\n";
            output += "• Self-contained (no external dependencies)\n";
            output += "• Not indexed yet\n";
        }
        else
        {
            output += $"📦 {dependencies.Count} dependencies:\n\n";
            foreach (var d in dependencies.Take(50))
            {
                output += $"  → {d}\n";
            }
            if (dependencies.Count > 50)
                output += $"\n  ... and {dependencies.Count - 50} more\n";
        }

        // Check for circular dependencies if requested
        if (includeCircular)
        {
            output += "\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
            output += "🔄 Circular Dependency Check:\n\n";
            
            var cycles = await _graphService.FindCircularDependenciesAsync(context, ct);
            
            if (!cycles.Any())
            {
                output += "✅ No circular dependencies found!\n";
            }
            else
            {
                output += $"⚠️ Found {cycles.Count} circular dependency cycles:\n\n";
                var i = 1;
                foreach (var cycle in cycles.Take(10))
                {
                    output += $"  Cycle {i++}: {string.Join(" → ", cycle)}\n";
                }
                if (cycles.Count > 10)
                    output += $"\n  ... and {cycles.Count - 10} more cycles\n";
            }
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Complexity Analysis

    private async Task<McpToolResult> AnalyzeComplexityAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var filePath = args?.GetValueOrDefault("filePath")?.ToString() ?? "";
        var methodName = args?.GetValueOrDefault("methodName")?.ToString();

        var result = await _complexityService.AnalyzeFileAsync(filePath, methodName, ct);

        if (!result.Success)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Error analyzing complexity:\n{string.Join("\n", result.Errors)}" }
                }
            };
        }

        var text = $"📊 Code Complexity Analysis\n";
        text += $"File: {result.FilePath}\n";
        if (!string.IsNullOrEmpty(result.MethodName))
            text += $"Method: {result.MethodName}\n";
        text += "\n";

        // Summary
        text += $"📈 Summary (Overall Grade: {result.Summary.OverallGrade})\n" +
                $"  Total Methods: {result.Summary.TotalMethods}\n" +
                $"  Avg Cyclomatic: {result.Summary.AverageCyclomaticComplexity:F1}\n" +
                $"  Avg Cognitive: {result.Summary.AverageCognitiveComplexity:F1}\n" +
                $"  Max Cyclomatic: {result.Summary.MaxCyclomaticComplexity}\n" +
                $"  High Complexity Methods: {result.Summary.MethodsWithHighComplexity}\n" +
                $"  Methods with Code Smells: {result.Summary.MethodsWithCodeSmells}\n\n";

        if (result.Summary.FileRecommendations.Any())
        {
            text += "📋 File-Level Recommendations:\n";
            foreach (var rec in result.Summary.FileRecommendations)
                text += $"  • {rec}\n";
            text += "\n";
        }

        // Method details (sorted by grade, worst first)
        if (result.Methods.Any())
        {
            text += "🔍 Method Details:\n\n";
            
            var sortedMethods = result.Methods
                .OrderBy(m => m.Grade switch { "F" => 1, "D" => 2, "C" => 3, "B" => 4, "A" => 5, _ => 6 })
                .ThenByDescending(m => m.CyclomaticComplexity)
                .Take(20)
                .ToList();

            foreach (var method in sortedMethods)
            {
                var gradeEmoji = method.Grade switch
                {
                    "A" => "✅",
                    "B" => "✅",
                    "C" => "⚠️",
                    "D" => "❌",
                    "F" => "🔴",
                    _ => "❓"
                };

                text += $"{gradeEmoji} {method.ClassName}.{method.MethodName} (Grade: {method.Grade})\n";
                text += $"   Cyclomatic: {method.CyclomaticComplexity} | Cognitive: {method.CognitiveComplexity} | LOC: {method.LinesOfCode}\n";
                
                if (method.CodeSmells.Any())
                    text += $"   🔴 Smells: {string.Join(", ", method.CodeSmells)}\n";
                
                if (method.Recommendations.Any())
                {
                    text += "   💡 ";
                    text += string.Join(" | ", method.Recommendations.Take(2));
                    text += "\n";
                }
                
                text += "\n";
            }

            if (result.Methods.Count > 20)
                text += $"... and {result.Methods.Count - 20} more methods\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    #endregion

    #region Validation

    private async Task<McpToolResult> ValidateAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var scope = args?.GetValueOrDefault("scope")?.ToString()?.ToLowerInvariant() ?? "best_practices";

        // list_best_practices doesn't require context
        if (scope == "list_best_practices")
            return await ListBestPracticesAsync(ct);

        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        return scope switch
        {
            "best_practices" => await ValidateBestPracticesAsync(context, args, ct),
            "security" => await ValidateSecurityAsync(context, args, ct),
            "pattern_quality" => await ValidatePatternQualityAsync(context, args, ct),
            "anti_patterns" => await FindAntiPatternsAsync(context, args, ct),
            "project" => await ValidateProjectAsync(context, ct),
            "all" => await ValidateAllAsync(context, args, ct),
            _ => ErrorResult($"Unknown validation scope: {scope}")
        };
    }

    private async Task<McpToolResult> ListBestPracticesAsync(CancellationToken ct)
    {
        var practices = await _bestPracticeService.GetAvailableBestPracticesAsync(ct);

        var output = $"📋 Available Best Practices ({practices.Count})\n\n";

        // Group by category extracted from practice name (e.g., "CacheAside" -> "Caching")
        var byCategory = practices.GroupBy(p => GetCategoryFromPracticeName(p)).OrderBy(g => g.Key);
        
        foreach (var group in byCategory)
        {
            output += $"📁 {group.Key}\n";
            foreach (var practice in group.OrderBy(p => p))
            {
                output += $"  • {practice}\n";
            }
            output += "\n";
        }

        output += "\n✨ Use validate with scope='best_practices' and a context to check your project against these practices.";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private static string GetCategoryFromPracticeName(string practiceName)
    {
        if (practiceName.Contains("Cache", StringComparison.OrdinalIgnoreCase)) return "Caching";
        if (practiceName.Contains("Retry", StringComparison.OrdinalIgnoreCase) || 
            practiceName.Contains("Circuit", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Resilience", StringComparison.OrdinalIgnoreCase)) return "Resilience";
        if (practiceName.Contains("Queue", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Message", StringComparison.OrdinalIgnoreCase)) return "Messaging";
        if (practiceName.Contains("Database", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Repository", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Connection", StringComparison.OrdinalIgnoreCase)) return "Data";
        if (practiceName.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Auth", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Validation", StringComparison.OrdinalIgnoreCase)) return "Security";
        if (practiceName.Contains("Config", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Setting", StringComparison.OrdinalIgnoreCase)) return "Configuration";
        if (practiceName.Contains("Log", StringComparison.OrdinalIgnoreCase) ||
            practiceName.Contains("Telemetry", StringComparison.OrdinalIgnoreCase)) return "Observability";
        if (practiceName.Contains("Health", StringComparison.OrdinalIgnoreCase)) return "Health Checks";
        return "General";
    }

    private async Task<McpToolResult> ValidateBestPracticesAsync(string context, Dictionary<string, object>? args, CancellationToken ct)
    {
        var request = new BestPracticeValidationRequest
        {
            Context = context,
            IncludeExamples = true,
            MaxExamplesPerPractice = 3
        };

        var result = await _bestPracticeService.ValidateBestPracticesAsync(request, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"📋 Best Practice Validation for '{context}'\n");
        sb.AppendLine($"Overall Score: {result.OverallScore:P0} ({result.PracticesImplemented}/{result.TotalPracticesChecked} practices)");
        sb.AppendLine($"✅ Implemented: {result.PracticesImplemented}");
        sb.AppendLine($"❌ Missing: {result.PracticesMissing}\n");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        var implemented = result.Results.Where(r => r.Implemented).OrderByDescending(p => p.Count).Take(10).ToList();
        var missing = result.Results.Where(r => !r.Implemented).Take(10).ToList();

        if (implemented.Any())
        {
            sb.AppendLine("✅ TOP IMPLEMENTED:\n");
            foreach (var p in implemented)
            {
                sb.AppendLine($"• {p.Practice} ({p.Count} instances, {p.AverageConfidence:P0} confidence)");
            }
            sb.AppendLine();
        }

        if (missing.Any())
        {
            sb.AppendLine("❌ MISSING/RECOMMENDED:\n");
            foreach (var p in missing)
            {
                sb.AppendLine($"• {p.Practice}");
                sb.AppendLine($"  💡 {p.Recommendation}");
                if (!string.IsNullOrEmpty(p.AzureUrl))
                    sb.AppendLine($"  📚 {p.AzureUrl}");
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

    private async Task<McpToolResult> ValidateSecurityAsync(string context, Dictionary<string, object>? args, CancellationToken ct)
    {
        var result = await _patternValidationService.ValidateSecurityAsync(context, null, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"🔒 Security Validation for '{context}'\n");
        sb.AppendLine($"Security Score: {result.SecurityScore}/10 ({result.Grade})");
        sb.AppendLine($"Vulnerabilities: {result.Vulnerabilities.Count}\n");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        if (result.Vulnerabilities.Any())
        {
            sb.AppendLine("🚨 Vulnerabilities:\n");
            foreach (var vuln in result.Vulnerabilities.Take(10))
            {
                var icon = vuln.Severity switch
                {
                    IssueSeverity.Critical => "🚨",
                    IssueSeverity.High => "❗",
                    IssueSeverity.Medium => "⚠️",
                    _ => "ℹ️"
                };
                sb.AppendLine($"{icon} {vuln.Severity}: {vuln.PatternName}");
                sb.AppendLine($"   {vuln.Description}");
                sb.AppendLine($"   📁 {vuln.FilePath}");
                sb.AppendLine($"   🔧 Fix: {vuln.Remediation}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("✅ No security vulnerabilities found!");
        }

        if (result.RemediationSteps.Any())
        {
            sb.AppendLine("📋 Priority Actions:\n");
            foreach (var step in result.RemediationSteps.Take(5))
                sb.AppendLine($"• {step}");
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = sb.ToString() }
            }
        };
    }

    private async Task<McpToolResult> ValidatePatternQualityAsync(string context, Dictionary<string, object>? args, CancellationToken ct)
    {
        var patternId = args?.GetValueOrDefault("patternId")?.ToString() ?? "";
        var includeAutoFix = SafeParseBool(args?.GetValueOrDefault("includeAutoFix"), true);

        if (string.IsNullOrWhiteSpace(patternId))
            return ErrorResult("patternId is required for scope='pattern_quality'");

        var result = await _patternValidationService.ValidatePatternQualityAsync(patternId, context, includeAutoFix, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"🔍 Pattern Quality Validation\n");
        sb.AppendLine($"Pattern: {result.Pattern.Name}");
        sb.AppendLine($"Quality Score: {result.Score}/10 (Grade: {result.Grade})");
        sb.AppendLine($"Security Score: {result.SecurityScore}/10\n");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        if (result.Issues.Any())
        {
            sb.AppendLine("❌ Issues:\n");
            foreach (var issue in result.Issues.Take(10))
            {
                var icon = issue.Severity switch
                {
                    IssueSeverity.Critical => "🚨",
                    IssueSeverity.High => "❌",
                    IssueSeverity.Medium => "⚠️",
                    _ => "ℹ️"
                };
                sb.AppendLine($"{icon} {issue.Severity}: {issue.Message}");
                if (issue.FixGuidance != null)
                    sb.AppendLine($"   💡 {issue.FixGuidance}");
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrEmpty(result.AutoFixCode))
        {
            sb.AppendLine("🔧 Auto-Fix Code:\n");
            sb.AppendLine("```");
            sb.AppendLine(result.AutoFixCode);
            sb.AppendLine("```\n");
        }

        sb.AppendLine($"Summary: {result.Summary}");

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = sb.ToString() }
            }
        };
    }

    private async Task<McpToolResult> FindAntiPatternsAsync(string context, Dictionary<string, object>? args, CancellationToken ct)
    {
        var minSeverityStr = args?.GetValueOrDefault("minSeverity")?.ToString() ?? "medium";
        var includeLegacy = SafeParseBool(args?.GetValueOrDefault("includeLegacy"), true);

        var minSeverity = minSeverityStr.ToLower() switch
        {
            "critical" => IssueSeverity.Critical,
            "high" => IssueSeverity.High,
            "low" => IssueSeverity.Low,
            _ => IssueSeverity.Medium
        };

        var result = await _patternValidationService.FindAntiPatternsAsync(context, minSeverity, includeLegacy, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"🚨 Anti-Pattern Analysis for '{context}'\n");
        sb.AppendLine($"Total Found: {result.TotalCount}");
        sb.AppendLine($"Critical Issues: {result.CriticalCount}");
        sb.AppendLine($"Security Score: {result.OverallSecurityScore}/10\n");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        if (result.AntiPatterns.Any())
        {
            foreach (var ap in result.AntiPatterns.Take(15))
            {
                sb.AppendLine($"• {ap.Pattern.Name} (Score: {ap.Score}/10)");
                sb.AppendLine($"  📁 {ap.Pattern.FilePath}");
                if (ap.Issues.Any())
                {
                    var topIssue = ap.Issues.OrderByDescending(i => i.Severity).First();
                    sb.AppendLine($"  🚨 {topIssue.Severity}: {topIssue.Message}");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("✅ No anti-patterns found!");
        }

        sb.AppendLine($"Summary: {result.Summary}");

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = sb.ToString() }
            }
        };
    }

    private async Task<McpToolResult> ValidateProjectAsync(string context, CancellationToken ct)
    {
        var result = await _patternValidationService.ValidateProjectAsync(context, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"📊 Comprehensive Project Validation - {context}\n");
        sb.AppendLine($"Overall Quality: {result.OverallScore}/10");
        sb.AppendLine($"Security Score: {result.SecurityScore}/10");
        sb.AppendLine($"Total Patterns: {result.TotalPatterns}\n");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

        sb.AppendLine("📈 Patterns by Grade:\n");
        foreach (var grade in result.PatternsByGrade.OrderBy(g => g.Key))
            sb.AppendLine($"  Grade {grade.Key}: {grade.Value} patterns");
        sb.AppendLine();

        if (result.CriticalIssues.Any())
        {
            sb.AppendLine($"🚨 Critical Issues ({result.CriticalIssues.Count}):\n");
            foreach (var issue in result.CriticalIssues.Take(5))
                sb.AppendLine($"  • {issue.Message}");
            sb.AppendLine();
        }

        if (result.SecurityVulnerabilities.Any())
        {
            sb.AppendLine($"🔒 Security Vulnerabilities ({result.SecurityVulnerabilities.Count}):\n");
            foreach (var vuln in result.SecurityVulnerabilities.Take(5))
                sb.AppendLine($"  • {vuln.Severity}: {vuln.Description}");
            sb.AppendLine();
        }

        if (result.LegacyPatterns.Any())
        {
            sb.AppendLine($"⚠️ Legacy Patterns ({result.LegacyPatterns.Count}):\n");
            foreach (var legacy in result.LegacyPatterns.Take(5))
                sb.AppendLine($"  • {legacy.CurrentPattern} → {legacy.TargetPattern}");
            sb.AppendLine();
        }

        if (result.TopRecommendations.Any())
        {
            sb.AppendLine("📋 Top Recommendations:\n");
            foreach (var rec in result.TopRecommendations.Take(5))
                sb.AppendLine($"  • {rec}");
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = sb.ToString() }
            }
        };
    }

    private async Task<McpToolResult> ValidateAllAsync(string context, Dictionary<string, object>? args, CancellationToken ct)
    {
        // Run project validation which includes everything
        return await ValidateProjectAsync(context, ct);
    }

    #endregion

    #region Helpers

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var i) => i,
            JsonElement je when je.TryGetInt32(out var i) => i,
            _ => defaultValue
        };

    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            _ => defaultValue
        };

    private static McpToolResult ErrorResult(string error) => new()
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };

    #endregion
}

