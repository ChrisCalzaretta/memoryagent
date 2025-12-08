using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for intelligence and insights operations.
/// Tools: get_recommendations, get_important_files, get_coedited_files, get_insights
/// </summary>
public class IntelligenceToolHandler : IMcpToolHandler
{
    private readonly IRecommendationService _recommendationService;
    private readonly ILearningService _learningService;
    private readonly IPromptService _promptService;
    private readonly IEvolvingPatternCatalogService _patternCatalogService;
    private readonly IPathTranslationService _pathTranslation;
    private readonly ILogger<IntelligenceToolHandler> _logger;

    public IntelligenceToolHandler(
        IRecommendationService recommendationService,
        ILearningService learningService,
        IPromptService promptService,
        IEvolvingPatternCatalogService patternCatalogService,
        IPathTranslationService pathTranslation,
        ILogger<IntelligenceToolHandler> logger)
    {
        _recommendationService = recommendationService;
        _learningService = learningService;
        _promptService = promptService;
        _patternCatalogService = patternCatalogService;
        _pathTranslation = pathTranslation;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "get_recommendations",
                Description = "Get prioritized architecture recommendations based on detected patterns. Returns health score and actionable improvements.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to analyze" },
                        categories = new { type = "array", items = new { type = "string" }, description = "Focus on specific categories (e.g., 'Security', 'Performance')" },
                        includeLowPriority = new { type = "boolean", description = "Include low-priority recommendations (default: false)", @default = false },
                        maxRecommendations = new { type = "number", description = "Maximum recommendations to return (default: 10)", @default = 10 }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_important_files",
                Description = "Get the most important files based on access patterns, edit frequency, and discussion history.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Maximum number of files (default: 20)", @default = 20 }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_coedited_files",
                Description = "Get files frequently edited together. Helps understand dependencies and find related files to update.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "File to find co-edit partners for" },
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Maximum results (default: 10)", @default = 10 },
                        includeClusters = new { type = "boolean", description = "Also show file clusters (modules) (default: false)", @default = false }
                    },
                    required = new[] { "filePath", "context" }
                }
            },
            new McpTool
            {
                Name = "get_insights",
                Description = "Get insights and metrics. Use category: 'patterns', 'prompts', 'tools', 'sessions', 'domains' (detect from file), 'recalculate' (refresh importance), or 'all'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context" },
                        category = new { type = "string", description = "Insight category", @default = "all", @enum = new[] { "patterns", "prompts", "tools", "sessions", "domains", "recalculate", "all" } },
                        limit = new { type = "number", description = "Maximum items per category (default: 10)", @default = 10 },
                        filePath = new { type = "string", description = "For category='domains': file to analyze" },
                        content = new { type = "string", description = "For category='domains': file content (optional)" }
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
            "get_recommendations" => await GetRecommendationsAsync(args, cancellationToken),
            "get_important_files" => await GetImportantFilesAsync(args, cancellationToken),
            "get_coedited_files" => await GetCoEditedFilesAsync(args, cancellationToken),
            "get_insights" => await GetInsightsAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Recommendations

    private async Task<McpToolResult> GetRecommendationsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var includeLowPriority = SafeParseBool(args?.GetValueOrDefault("includeLowPriority"), false);
        var maxRecommendations = SafeParseInt(args?.GetValueOrDefault("maxRecommendations"), 10);

        var categories = new List<PatternCategory>();
        if (args?.TryGetValue("categories", out var catObj) == true)
        {
            categories = ParseEnumList<PatternCategory>(catObj);
        }

        var request = new RecommendationRequest
        {
            Context = context,
            Categories = categories.Any() ? categories : null,
            IncludeLowPriority = includeLowPriority,
            MaxRecommendations = maxRecommendations
        };

        var result = await _recommendationService.AnalyzeAndRecommendAsync(request, ct);

        var output = $"🎯 Architecture Recommendations for '{context}'\n\n";
        output += $"Overall Health: {result.OverallHealth:P0}\n";
        output += $"Patterns Detected: {result.TotalPatternsDetected}\n";
        output += $"Recommendations: {result.Recommendations.Count}\n\n";
        output += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

        if (!result.Recommendations.Any())
        {
            output += "✅ No critical recommendations! Your project looks good.\n";
        }
        else
        {
            var priorityGroups = new[]
            {
                ("🚨 CRITICAL:", result.Recommendations.Where(r => r.Priority == "CRITICAL")),
                ("⚠️ HIGH:", result.Recommendations.Where(r => r.Priority == "HIGH")),
                ("📌 MEDIUM:", result.Recommendations.Where(r => r.Priority == "MEDIUM")),
                ("💡 LOW:", result.Recommendations.Where(r => r.Priority == "LOW" && includeLowPriority))
            };

            foreach (var (header, recs) in priorityGroups)
            {
                if (!recs.Any()) continue;
                
                output += $"\n{header}\n\n";
                foreach (var rec in recs.Take(5))
                {
                    output += $"• [{rec.Category}] {rec.Issue}\n";
                    output += $"  💡 {rec.Recommendation}\n";
                    output += $"  Impact: {rec.Impact}\n";
                    if (!string.IsNullOrEmpty(rec.AzureUrl))
                        output += $"  📚 {rec.AzureUrl}\n";
                    output += "\n";
                }
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

    #region File Intelligence

    private async Task<McpToolResult> GetImportantFilesAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 20);
        
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var metrics = await _learningService.GetMostImportantFilesAsync(context, limit, ct);
        
        if (!metrics.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"📊 No importance metrics found for '{context}'.\n\nImportance builds over time as you:\n• Discuss files (record_file_discussed)\n• Edit files (record_file_edited)\n• Search for code (smartsearch)" }
                }
            };
        }

        var output = $"⭐ Most Important Files in '{context}'\n\n";
        var rank = 1;
        foreach (var m in metrics)
        {
            var stars = m.ImportanceScore switch
            {
                >= 0.8f => "⭐⭐⭐",
                >= 0.5f => "⭐⭐",
                _ => "⭐"
            };
            output += $"{rank}. {stars} {Path.GetFileName(m.FilePath)}\n";
            output += $"   Importance: {m.ImportanceScore:P0} | Accesses: {m.AccessCount} | Edits: {m.EditCount}\n";
            output += $"   📁 {m.FilePath}\n\n";
            rank++;
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> GetCoEditedFilesAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 10);
        var includeClusters = SafeParseBool(args?.GetValueOrDefault("includeClusters"), false);
        
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("filePath and context are required");

        var coEdits = await _learningService.GetCoEditedFilesAsync(filePath, context, limit, ct);
        
        var output = $"🔗 Files Frequently Edited With {Path.GetFileName(filePath)}\n\n";
        
        if (!coEdits.Any())
        {
            output += "No co-edit patterns found yet.\n\n";
            output += "Co-edit tracking builds over time as files are edited together in sessions.\n";
            output += "Use record_file_edited to track edits!\n";
        }
        else
        {
            foreach (var co in coEdits)
            {
                var strength = co.CoEditStrength switch
                {
                    >= 0.7f => "🔴 Strong",
                    >= 0.4f => "🟡 Medium",
                    _ => "🟢 Light"
                };
                output += $"• {Path.GetFileName(co.FilePath2)}\n";
                output += $"  {strength} ({co.CoEditCount} co-edits)\n";
                output += $"  📁 {co.FilePath2}\n\n";
            }
        }

        // Include clusters if requested
        if (includeClusters)
        {
            output += "\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
            output += $"📦 File Clusters in '{context}':\n\n";
            
            var clusters = await _learningService.GetFileClusterssAsync(context, ct);
            
            if (!clusters.Any())
            {
                output += "No clusters detected yet. Clusters emerge from 3+ consistent co-edits.\n";
            }
            else
            {
                var clusterNum = 1;
                foreach (var cluster in clusters.Take(5))
                {
                    output += $"Cluster #{clusterNum} ({cluster.Count} files):\n";
                    foreach (var file in cluster.Take(5))
                    {
                        output += $"  • {Path.GetFileName(file)}\n";
                    }
                    if (cluster.Count > 5)
                        output += $"  ... and {cluster.Count - 5} more\n";
                    output += "\n";
                    clusterNum++;
                }
            }
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Insights

    private async Task<McpToolResult> GetInsightsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var category = args?.GetValueOrDefault("category")?.ToString()?.ToLowerInvariant() ?? "all";
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 10);

        var output = "📊 System Insights\n\n";

        var showAll = category == "all";

        // Pattern Catalog Metrics
        if (showAll || category == "patterns")
        {
            output += "🎨 Pattern Catalog:\n\n";
            try
            {
                var catalogMetrics = await _patternCatalogService.GetCatalogMetricsAsync(ct);
                output += $"  Total Patterns: {catalogMetrics.TotalPatterns}\n";
                output += $"  Active: {catalogMetrics.ActivePatterns}\n";
                output += $"  Deprecated: {catalogMetrics.DeprecatedPatterns}\n";
                output += $"  Avg Usefulness: {catalogMetrics.AvgUsefulnessScore:P0}\n";
                
                var topPatterns = await _patternCatalogService.GetMostUsefulPatternsAsync(3, ct);
                if (topPatterns.Any())
                {
                    output += "  Top Patterns:\n";
                    foreach (var p in topPatterns)
                        output += $"    • {p.Name} ({p.UsefulnessScore:P0})\n";
                }
            }
            catch (Exception ex)
            {
                output += $"  Error: {ex.Message}\n";
            }
            output += "\n";
        }

        // Prompt Metrics
        if (showAll || category == "prompts")
        {
            output += "📝 Prompt System:\n\n";
            try
            {
                var prompts = await _promptService.ListPromptsAsync(true, ct);
                output += $"  Active Prompts: {prompts.Count}\n";
                
                var withMetrics = prompts.Where(p => p.TimesUsed > 0).OrderByDescending(p => p.SuccessRate).Take(3).ToList();
                if (withMetrics.Any())
                {
                    output += "  Top Performing:\n";
                    foreach (var p in withMetrics)
                        output += $"    • {p.Name} v{p.Version} ({p.SuccessRate:P0} success, {p.TimesUsed} uses)\n";
                }
            }
            catch (Exception ex)
            {
                output += $"  Error: {ex.Message}\n";
            }
            output += "\n";
        }

        // Tool Usage
        if (showAll || category == "tools")
        {
            output += "🔧 Tool Usage:\n\n";
            try
            {
                var toolMetrics = await _learningService.GetToolUsageMetricsAsync(context, ct);
                output += $"  Tools Used: {toolMetrics.Count}\n";
                output += $"  Total Calls: {toolMetrics.Sum(m => m.CallCount):N0}\n";
                
                var topTools = toolMetrics.OrderByDescending(m => m.CallCount).Take(5).ToList();
                if (topTools.Any())
                {
                    output += "  Most Used:\n";
                    foreach (var t in topTools)
                    {
                        var successRate = t.CallCount > 0 ? (t.SuccessCount * 100.0 / t.CallCount) : 100;
                        output += $"    • {t.ToolName} ({t.CallCount} calls, {successRate:F0}% success)\n";
                    }
                }
            }
            catch (Exception ex)
            {
                output += $"  Error: {ex.Message}\n";
            }
            output += "\n";
        }

        // Recent Sessions
        if ((showAll || category == "sessions") && !string.IsNullOrWhiteSpace(context))
        {
            output += $"📜 Recent Sessions ({context}):\n\n";
            try
            {
                var sessions = await _learningService.GetRecentSessionsAsync(context, limit, ct);
                if (!sessions.Any())
                {
                    output += "  No sessions found.\n";
                }
                else
                {
                    foreach (var s in sessions.Take(5))
                    {
                        var status = s.EndedAt.HasValue ? "✅" : "🟢";
                        output += $"  {status} {s.StartedAt:g}\n";
                        output += $"     Files: {s.FilesDiscussed.Count} discussed, {s.FilesEdited.Count} edited\n";
                        if (!string.IsNullOrEmpty(s.Summary))
                            output += $"     Summary: {s.Summary[..Math.Min(50, s.Summary.Length)]}...\n";
                    }
                }
            }
            catch (Exception ex)
            {
                output += $"  Error: {ex.Message}\n";
            }
        }

        // Domain Detection (category-specific, not part of 'all')
        if (category == "domains")
        {
            return await HandleDomainsAsync(args, ct);
        }

        // Recalculate Importance (category-specific, not part of 'all')
        if (category == "recalculate")
        {
            return await HandleRecalculateAsync(context, ct);
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> HandleRecalculateAsync(string? context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required for category='recalculate'");

        await _learningService.RecalculateImportanceScoresAsync(context, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Importance scores recalculated for context: {context}\n\nFile rankings have been updated based on:\n• Access patterns\n• Edit frequency\n• Discussion history\n• Co-edit relationships\n\nUse get_important_files to see the updated rankings." }
            }
        };
    }

    private async Task<McpToolResult> HandleDomainsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        var content = args?.GetValueOrDefault("content")?.ToString();

        if (string.IsNullOrWhiteSpace(filePath))
            return ErrorResult("filePath is required");

        // If content not provided, try to read from filePath
        if (string.IsNullOrWhiteSpace(content))
        {
            try
            {
                // Translate Windows path to container path
                var containerPath = _pathTranslation.TranslateToContainerPath(filePath);
                if (File.Exists(containerPath))
                    content = await File.ReadAllTextAsync(containerPath, ct);
            }
            catch
            {
                return ErrorResult($"Could not read file: {filePath}");
            }
        }

        if (string.IsNullOrWhiteSpace(content))
            return ErrorResult("content is required (file empty or not found)");

        var domains = await _learningService.DetectDomainsAsync(filePath, content, ct);

        var output = $"🏷️ Domain Detection for {Path.GetFileName(filePath)}\n\n";
        
        if (!domains.Any())
        {
            output += "No specific domains detected. File may be infrastructure/utility code.\n";
        }
        else
        {
            output += "Detected Domains:\n\n";
            foreach (var domain in domains.OrderByDescending(d => d.Confidence))
            {
                output += $"  • {domain.Name} ({domain.Confidence:P0} confidence)\n";
                if (!string.IsNullOrEmpty(domain.Description))
                    output += $"    {domain.Description}\n";
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
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

    private static List<T> ParseEnumList<T>(object? value) where T : struct, Enum
    {
        var result = new List<T>();
        if (value == null) return result;

        IEnumerable<object>? items = value switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Array => je.EnumerateArray().Select(e => (object)e.GetString()!),
            IEnumerable<object> enumerable => enumerable,
            _ => null
        };

        if (items != null)
        {
            foreach (var item in items)
            {
                if (Enum.TryParse<T>(item.ToString(), true, out var parsed))
                    result.Add(parsed);
            }
        }

        return result;
    }

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

