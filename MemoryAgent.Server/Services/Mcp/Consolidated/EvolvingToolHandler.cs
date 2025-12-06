using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for evolving prompts and patterns.
/// Tools: manage_prompts, manage_patterns, feedback
/// </summary>
public class EvolvingToolHandler : IMcpToolHandler
{
    private readonly IPromptService _promptService;
    private readonly IEvolvingPatternCatalogService _patternCatalogService;
    private readonly ILogger<EvolvingToolHandler> _logger;

    public EvolvingToolHandler(
        IPromptService promptService,
        IEvolvingPatternCatalogService patternCatalogService,
        ILogger<EvolvingToolHandler> logger)
    {
        _promptService = promptService;
        _patternCatalogService = patternCatalogService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "manage_prompts",
                Description = "Manage LLM prompts. Actions: 'list', 'get_history', 'get_metrics', 'create_version', 'activate', 'rollback', 'start_ab_test', 'get_ab_results', 'promote', 'suggest_improvements'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        action = new { type = "string", description = "Action to perform", @enum = new[] { "list", "get_history", "get_metrics", "create_version", "activate", "rollback", "start_ab_test", "get_ab_results", "promote", "suggest_improvements" } },
                        name = new { type = "string", description = "Prompt name (required for most actions)" },
                        content = new { type = "string", description = "New prompt content (for create_version)" },
                        reason = new { type = "string", description = "Reason for version change (for create_version)" },
                        activateImmediately = new { type = "boolean", description = "Activate new version immediately (default: false)", @default = false },
                        testVersion = new { type = "number", description = "Version to test (for start_ab_test)" },
                        trafficPercent = new { type = "number", description = "Traffic percent for test (default: 10)", @default = 10 },
                        activeOnly = new { type = "boolean", description = "List only active prompts (default: true)", @default = true }
                    },
                    required = new[] { "action" }
                }
            },
            new McpTool
            {
                Name = "manage_patterns",
                Description = "Manage evolving patterns. Actions: 'list', 'get_metrics', 'get_useful', 'get_needing_improvement', 'suggest_new', 'evolve', 'deprecate', 'get_catalog_metrics'.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        action = new { type = "string", description = "Action to perform", @enum = new[] { "list", "get_metrics", "get_useful", "get_needing_improvement", "suggest_new", "evolve", "deprecate", "get_catalog_metrics" } },
                        name = new { type = "string", description = "Pattern name (for get_metrics, evolve, deprecate)" },
                        limit = new { type = "number", description = "Max results (default: 20)", @default = 20 },
                        threshold = new { type = "number", description = "Usefulness threshold for get_needing_improvement (default: 0.5)", @default = 0.5 },
                        
                        // For suggest_new
                        codeExample = new { type = "string", description = "Code example for suggest_new" },
                        description = new { type = "string", description = "Description for suggest_new" },
                        suggestedName = new { type = "string", description = "Suggested name for new pattern" },
                        rationale = new { type = "string", description = "Why this should be a pattern" },
                        context = new { type = "string", description = "Project context" },
                        
                        // For evolve
                        reason = new { type = "string", description = "Reason for evolution" },
                        newRecommendation = new { type = "string", description = "Updated recommendation text" },
                        newExamples = new { type = "array", items = new { type = "string" }, description = "New code examples" },
                        
                        // For deprecate
                        supersededBy = new { type = "string", description = "Pattern that supersedes this one" }
                    },
                    required = new[] { "action" }
                }
            },
            new McpTool
            {
                Name = "feedback",
                Description = "Record feedback on prompts or patterns. Use type='prompt' for prompt outcomes, type='pattern' for pattern detection feedback.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        type = new { type = "string", description = "Feedback type", @enum = new[] { "prompt", "pattern" } },
                        
                        // For prompt feedback
                        executionId = new { type = "string", description = "For type=prompt: execution ID" },
                        wasSuccessful = new { type = "boolean", description = "Was the output helpful/successful?" },
                        rating = new { type = "number", description = "Optional rating 1-5" },
                        comments = new { type = "string", description = "Optional feedback comments" },
                        
                        // For pattern feedback
                        patternName = new { type = "string", description = "For type=pattern: pattern name" },
                        wasUseful = new { type = "boolean", description = "Was the pattern detection useful?" },
                        feedbackType = new { type = "string", description = "Feedback type: Correct, CorrectButNotUseful, FalsePositive, FalseNegative" }
                    },
                    required = new[] { "type" }
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
            "manage_prompts" => await ManagePromptsAsync(args, cancellationToken),
            "manage_patterns" => await ManagePatternsAsync(args, cancellationToken),
            "feedback" => await FeedbackAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Prompt Management

    private async Task<McpToolResult> ManagePromptsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var action = args?.GetValueOrDefault("action")?.ToString()?.ToLowerInvariant();

        return action switch
        {
            "list" => await ListPromptsAsync(args, ct),
            "get_history" => await GetPromptHistoryAsync(args, ct),
            "get_metrics" => await GetPromptMetricsAsync(args, ct),
            "create_version" => await CreatePromptVersionAsync(args, ct),
            "activate" => await ActivatePromptAsync(args, ct),
            "rollback" => await RollbackPromptAsync(args, ct),
            "start_ab_test" => await StartABTestAsync(args, ct),
            "get_ab_results" => await GetABResultsAsync(args, ct),
            "promote" => await PromotePromptAsync(args, ct),
            "suggest_improvements" => await SuggestImprovementsAsync(args, ct),
            _ => ErrorResult($"Unknown prompt action: {action}")
        };
    }

    private async Task<McpToolResult> ListPromptsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var activeOnly = SafeParseBool(args?.GetValueOrDefault("activeOnly"), true);
        var prompts = await _promptService.ListPromptsAsync(activeOnly, ct);

        var output = $"📝 Prompts ({prompts.Count}):\n\n";
        foreach (var p in prompts)
        {
            var statusIcon = p.IsActive ? "✅" : p.IsTestVariant ? "🧪" : "📦";
            output += $"{statusIcon} {p.Name} v{p.Version}\n";
            output += $"   Uses: {p.TimesUsed} | Success: {p.SuccessRate:P0} | Confidence: {p.AvgConfidence:P0}\n";
            output += $"   {p.Description}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent> { new() { Type = "text", Text = output } }
        };
    }

    private async Task<McpToolResult> GetPromptHistoryAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for get_history");

        var history = await _promptService.GetPromptHistoryAsync(name, ct);

        var output = $"📜 Prompt History: {name}\n\n";
        foreach (var p in history)
        {
            var statusIcon = p.IsActive ? "✅ Active" : p.IsTestVariant ? "🧪 Testing" : "📦 Archived";
            output += $"v{p.Version} - {statusIcon}\n";
            output += $"  Created: {p.CreatedAt:yyyy-MM-dd} | Uses: {p.TimesUsed} | Success: {p.SuccessRate:P0}\n";
            if (!string.IsNullOrEmpty(p.EvolutionReason))
                output += $"  Reason: {p.EvolutionReason}\n";
            output += "\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent> { new() { Type = "text", Text = output } }
        };
    }

    private async Task<McpToolResult> GetPromptMetricsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for get_metrics");

        var metrics = await _promptService.GetPromptMetricsAsync(name, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> CreatePromptVersionAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        var content = args?.GetValueOrDefault("content")?.ToString();
        var reason = args?.GetValueOrDefault("reason")?.ToString();
        var activateImmediately = SafeParseBool(args?.GetValueOrDefault("activateImmediately"), false);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(reason))
            return ErrorResult("name, content, and reason are required for create_version");

        var newVersion = await _promptService.CreateVersionAsync(name, content, reason, activateImmediately, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Created prompt '{name}' v{newVersion.Version}\n\nActive: {newVersion.IsActive}" }
            }
        };
    }

    private async Task<McpToolResult> ActivatePromptAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for activate");

        // Get latest version and activate it
        var history = await _promptService.GetPromptHistoryAsync(name, ct);
        var latest = history.OrderByDescending(h => h.Version).FirstOrDefault();
        if (latest == null)
            return ErrorResult($"Prompt not found: {name}");

        await _promptService.ActivateVersionAsync(name, latest.Version, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Activated prompt '{name}' v{latest.Version}" }
            }
        };
    }

    private async Task<McpToolResult> RollbackPromptAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for rollback");

        await _promptService.RollbackAsync(name, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Rolled back prompt '{name}' to previous version" }
            }
        };
    }

    private async Task<McpToolResult> StartABTestAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        var testVersion = SafeParseInt(args?.GetValueOrDefault("testVersion"), 0);
        var trafficPercent = SafeParseInt(args?.GetValueOrDefault("trafficPercent"), 10);

        if (string.IsNullOrWhiteSpace(name) || testVersion == 0)
            return ErrorResult("name and testVersion are required for start_ab_test");

        await _promptService.StartABTestAsync(name, testVersion, trafficPercent, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"🧪 Started A/B test for '{name}' v{testVersion} at {trafficPercent}% traffic" }
            }
        };
    }

    private async Task<McpToolResult> GetABResultsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for get_ab_results");

        var results = await _promptService.GetABTestResultsAsync(name, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> PromotePromptAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for promote");

        await _promptService.StopABTestAsync(name, true, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Promoted test version of '{name}' to active" }
            }
        };
    }

    private async Task<McpToolResult> SuggestImprovementsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for suggest_improvements");

        var suggestions = await _promptService.SuggestImprovementsAsync(name, ct);

        var output = $"💡 Improvement Suggestions for '{name}':\n\n";
        foreach (var s in suggestions)
            output += $"• {s}\n";

        return new McpToolResult
        {
            Content = new List<McpContent> { new() { Type = "text", Text = output } }
        };
    }

    #endregion

    #region Pattern Management

    private async Task<McpToolResult> ManagePatternsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var action = args?.GetValueOrDefault("action")?.ToString()?.ToLowerInvariant();

        return action switch
        {
            "list" or "get_useful" => await GetUsefulPatternsAsync(args, ct),
            "get_metrics" => await GetPatternMetricsAsync(args, ct),
            "get_needing_improvement" => await GetPatternsNeedingImprovementAsync(args, ct),
            "suggest_new" => await SuggestNewPatternAsync(args, ct),
            "evolve" => await EvolvePatternAsync(args, ct),
            "deprecate" => await DeprecatePatternAsync(args, ct),
            "get_catalog_metrics" => await GetCatalogMetricsAsync(ct),
            _ => ErrorResult($"Unknown pattern action: {action}")
        };
    }

    private async Task<McpToolResult> GetUsefulPatternsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 20);
        var patterns = await _patternCatalogService.GetMostUsefulPatternsAsync(limit, ct);

        var output = $"🎨 Top {patterns.Count} Useful Patterns:\n\n";
        foreach (var p in patterns)
        {
            output += $"• {p.Name} ({p.UsefulnessScore:P0})\n";
            output += $"  Type: {p.Type} | Category: {p.Category}\n";
            output += $"  Detections: {p.TimesDetected} | Useful: {p.TimesUseful}\n";
            output += $"  {p.Recommendation}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent> { new() { Type = "text", Text = output } }
        };
    }

    private async Task<McpToolResult> GetPatternMetricsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return ErrorResult("name is required for get_metrics");

        var metrics = await _patternCatalogService.GetPatternMetricsAsync(name, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> GetPatternsNeedingImprovementAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var threshold = SafeParseFloat(args?.GetValueOrDefault("threshold"), 0.5f);
        var patterns = await _patternCatalogService.GetPatternsNeedingImprovementAsync(threshold, ct);

        var output = $"⚠️ Patterns Needing Improvement (< {threshold:P0} usefulness):\n\n";
        foreach (var p in patterns)
        {
            output += $"• {p.Name} ({p.UsefulnessScore:P0})\n";
            output += $"  Detections: {p.TimesDetected} | Not Useful: {p.TimesNotUseful}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent> { new() { Type = "text", Text = output } }
        };
    }

    private async Task<McpToolResult> SuggestNewPatternAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var codeExample = args?.GetValueOrDefault("codeExample")?.ToString();
        var description = args?.GetValueOrDefault("description")?.ToString();

        if (string.IsNullOrWhiteSpace(codeExample) || string.IsNullOrWhiteSpace(description))
            return ErrorResult("codeExample and description are required for suggest_new");

        var request = new PatternSuggestionRequest
        {
            CodeExample = codeExample,
            Description = description,
            SuggestedName = args?.GetValueOrDefault("suggestedName")?.ToString(),
            Rationale = args?.GetValueOrDefault("rationale")?.ToString(),
            Context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant()
        };

        var suggestion = await _patternCatalogService.SuggestPatternAsync(request, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(suggestion, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> EvolvePatternAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        var reason = args?.GetValueOrDefault("reason")?.ToString();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(reason))
            return ErrorResult("name and reason are required for evolve");

        var newRecommendation = args?.GetValueOrDefault("newRecommendation")?.ToString();
        List<string>? newExamples = null;
        
        if (args?.TryGetValue("newExamples", out var examplesObj) == true && examplesObj is JsonElement je)
        {
            newExamples = je.EnumerateArray().Select(e => e.GetString()!).ToList();
        }

        var newVersion = await _patternCatalogService.CreateVersionAsync(name, reason, newRecommendation, null, newExamples, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Evolved pattern '{name}' to v{newVersion.Version}" }
            }
        };
    }

    private async Task<McpToolResult> DeprecatePatternAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var name = args?.GetValueOrDefault("name")?.ToString();
        var reason = args?.GetValueOrDefault("reason")?.ToString();
        var supersededBy = args?.GetValueOrDefault("supersededBy")?.ToString();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(reason))
            return ErrorResult("name and reason are required for deprecate");

        await _patternCatalogService.DeprecatePatternAsync(name, reason, supersededBy, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"⚠️ Deprecated pattern '{name}': {reason}" }
            }
        };
    }

    private async Task<McpToolResult> GetCatalogMetricsAsync(CancellationToken ct)
    {
        var metrics = await _patternCatalogService.GetCatalogMetricsAsync(ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    #endregion

    #region Feedback

    private async Task<McpToolResult> FeedbackAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var type = args?.GetValueOrDefault("type")?.ToString()?.ToLowerInvariant();

        return type switch
        {
            "prompt" => await RecordPromptFeedbackAsync(args, ct),
            "pattern" => await RecordPatternFeedbackAsync(args, ct),
            _ => ErrorResult($"Unknown feedback type: {type}. Valid: prompt, pattern")
        };
    }

    private async Task<McpToolResult> RecordPromptFeedbackAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var executionId = args?.GetValueOrDefault("executionId")?.ToString();
        var wasSuccessful = SafeParseBool(args?.GetValueOrDefault("wasSuccessful"), false);
        var rating = SafeParseInt(args?.GetValueOrDefault("rating"), 0);
        var comments = args?.GetValueOrDefault("comments")?.ToString();

        if (string.IsNullOrWhiteSpace(executionId))
            return ErrorResult("executionId is required for prompt feedback");

        await _promptService.RecordOutcomeAsync(executionId, wasSuccessful, rating > 0 ? rating : null, comments, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Recorded {(wasSuccessful ? "positive" : "negative")} feedback for prompt execution" }
            }
        };
    }

    private async Task<McpToolResult> RecordPatternFeedbackAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var patternName = args?.GetValueOrDefault("patternName")?.ToString();
        var wasUseful = SafeParseBool(args?.GetValueOrDefault("wasUseful"), false);

        if (string.IsNullOrWhiteSpace(patternName))
            return ErrorResult("patternName is required for pattern feedback");

        if (wasUseful)
            await _patternCatalogService.RecordUsefulAsync(patternName, ct);
        else
            await _patternCatalogService.RecordNotUsefulAsync(patternName, ct);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Recorded {(wasUseful ? "positive" : "negative")} feedback for pattern '{patternName}'" }
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

    private static float SafeParseFloat(object? value, float defaultValue) =>
        value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            string s when float.TryParse(s, out var f) => f,
            JsonElement je when je.TryGetSingle(out var f) => f,
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

