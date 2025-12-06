using MemoryAgent.Server.Models;
using System.Text.Json;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// MCP tool handler for evolving prompts and patterns.
/// Exposes tools for managing versioned prompts and learnable patterns.
/// </summary>
public class EvolvingSystemToolHandler : IMcpToolHandler
{
    private readonly IPromptService _promptService;
    private readonly IEvolvingPatternCatalogService _patternCatalogService;
    private readonly ILogger<EvolvingSystemToolHandler> _logger;

    public EvolvingSystemToolHandler(
        IPromptService promptService,
        IEvolvingPatternCatalogService patternCatalogService,
        ILogger<EvolvingSystemToolHandler> logger)
    {
        _promptService = promptService;
        _patternCatalogService = patternCatalogService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            // Prompt Management Tools
            new McpTool
            {
                Name = "list_prompts",
                Description = "List all available prompts with their versions and metrics",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["activeOnly"] = new { type = "boolean", description = "Only return active prompts (default: true)" }
                    },
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "get_prompt_history",
                Description = "Get version history for a specific prompt",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" }
                    },
                    required = new[] { "name" }
                }
            },
            new McpTool
            {
                Name = "get_prompt_metrics",
                Description = "Get performance metrics for a prompt (success rate, confidence, etc.)",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" }
                    },
                    required = new[] { "name" }
                }
            },
            new McpTool
            {
                Name = "create_prompt_version",
                Description = "Create a new version of a prompt with improvements",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" },
                        ["content"] = new { type = "string", description = "New prompt content" },
                        ["reason"] = new { type = "string", description = "Reason for the new version" },
                        ["activateImmediately"] = new { type = "boolean", description = "Activate this version immediately (default: false)" }
                    },
                    required = new[] { "name", "content", "reason" }
                }
            },
            new McpTool
            {
                Name = "start_prompt_ab_test",
                Description = "Start A/B testing a prompt version against the current active version",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" },
                        ["testVersion"] = new { type = "number", description = "Version to test" },
                        ["trafficPercent"] = new { type = "number", description = "Percentage of traffic for test (default: 10)" }
                    },
                    required = new[] { "name", "testVersion" }
                }
            },
            new McpTool
            {
                Name = "get_ab_test_results",
                Description = "Get results of an ongoing A/B test",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" }
                    },
                    required = new[] { "name" }
                }
            },
            new McpTool
            {
                Name = "promote_prompt_version",
                Description = "Promote a test version to active (end A/B test with promotion)",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" }
                    },
                    required = new[] { "name" }
                }
            },
            new McpTool
            {
                Name = "rollback_prompt",
                Description = "Rollback a prompt to its previous version",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" }
                    },
                    required = new[] { "name" }
                }
            },
            new McpTool
            {
                Name = "suggest_prompt_improvements",
                Description = "Get AI-suggested improvements for a prompt based on execution data",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Prompt name" }
                    },
                    required = new[] { "name" }
                }
            },
            new McpTool
            {
                Name = "record_prompt_outcome",
                Description = "Record feedback on a prompt execution (was it helpful?)",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["executionId"] = new { type = "string", description = "Execution ID from prompt response" },
                        ["wasSuccessful"] = new { type = "boolean", description = "Was the prompt output helpful?" },
                        ["rating"] = new { type = "number", description = "Optional rating 1-5" },
                        ["comments"] = new { type = "string", description = "Optional feedback comments" }
                    },
                    required = new[] { "executionId", "wasSuccessful" }
                }
            },

            // Pattern Catalog Tools
            new McpTool
            {
                Name = "get_pattern_catalog_metrics",
                Description = "Get overall metrics for the evolving pattern catalog",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>(),
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "get_pattern_metrics",
                Description = "Get metrics for a specific pattern (usefulness, detections, etc.)",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Pattern name" }
                    },
                    required = new[] { "name" }
                }
            },
            new McpTool
            {
                Name = "get_most_useful_patterns",
                Description = "Get patterns with highest usefulness scores",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["limit"] = new { type = "number", description = "Number of patterns to return (default: 20)" }
                    },
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "get_patterns_needing_improvement",
                Description = "Get patterns with low usefulness scores that need improvement",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["threshold"] = new { type = "number", description = "Usefulness threshold (default: 0.5)" }
                    },
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "record_pattern_feedback",
                Description = "Record feedback on a pattern detection (was it correct/useful?)",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["patternName"] = new { type = "string", description = "Pattern name" },
                        ["wasUseful"] = new { type = "boolean", description = "Was the detection useful?" },
                        ["feedbackType"] = new { type = "string", description = "Feedback type: Correct, CorrectButNotUseful, FalsePositive, FalseNegative" },
                        ["comments"] = new { type = "string", description = "Optional comments" }
                    },
                    required = new[] { "patternName", "wasUseful" }
                }
            },
            new McpTool
            {
                Name = "suggest_new_pattern",
                Description = "Suggest a new pattern based on a code example",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["codeExample"] = new { type = "string", description = "Code example demonstrating the pattern" },
                        ["description"] = new { type = "string", description = "Description of what the pattern does" },
                        ["suggestedName"] = new { type = "string", description = "Optional suggested name" },
                        ["rationale"] = new { type = "string", description = "Why this should be a pattern" },
                        ["context"] = new { type = "string", description = "Project context" }
                    },
                    required = new[] { "codeExample", "description" }
                }
            },
            new McpTool
            {
                Name = "deprecate_pattern",
                Description = "Deprecate a pattern that is no longer useful",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Pattern name to deprecate" },
                        ["reason"] = new { type = "string", description = "Reason for deprecation" },
                        ["supersededBy"] = new { type = "string", description = "Pattern that supersedes this one (optional)" }
                    },
                    required = new[] { "name", "reason" }
                }
            },
            new McpTool
            {
                Name = "evolve_pattern",
                Description = "Create a new version of a pattern with improvements",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["name"] = new { type = "string", description = "Pattern name" },
                        ["reason"] = new { type = "string", description = "Reason for evolution" },
                        ["newRecommendation"] = new { type = "string", description = "Updated recommendation text (optional)" },
                        ["newExamples"] = new { type = "array", description = "New code examples (optional)", items = new { type = "string" } }
                    },
                    required = new[] { "name", "reason" }
                }
            },
            new McpTool
            {
                Name = "initialize_evolving_catalog",
                Description = "Initialize the evolving pattern catalog from static best practices (run once)",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>(),
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "initialize_prompts",
                Description = "Initialize default prompts in the database (run once)",
                InputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>(),
                    required = Array.Empty<string>()
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(string toolName, Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        args ??= new Dictionary<string, object>();

        try
        {
            var result = toolName switch
            {
                // Prompt tools
                "list_prompts" => await HandleListPromptsAsync(args, cancellationToken),
                "get_prompt_history" => await HandleGetPromptHistoryAsync(args, cancellationToken),
                "get_prompt_metrics" => await HandleGetPromptMetricsAsync(args, cancellationToken),
                "create_prompt_version" => await HandleCreatePromptVersionAsync(args, cancellationToken),
                "start_prompt_ab_test" => await HandleStartABTestAsync(args, cancellationToken),
                "get_ab_test_results" => await HandleGetABTestResultsAsync(args, cancellationToken),
                "promote_prompt_version" => await HandlePromotePromptAsync(args, cancellationToken),
                "rollback_prompt" => await HandleRollbackPromptAsync(args, cancellationToken),
                "suggest_prompt_improvements" => await HandleSuggestImprovementsAsync(args, cancellationToken),
                "record_prompt_outcome" => await HandleRecordPromptOutcomeAsync(args, cancellationToken),
                
                // Pattern tools
                "get_pattern_catalog_metrics" => await HandleGetCatalogMetricsAsync(cancellationToken),
                "get_pattern_metrics" => await HandleGetPatternMetricsAsync(args, cancellationToken),
                "get_most_useful_patterns" => await HandleGetMostUsefulPatternsAsync(args, cancellationToken),
                "get_patterns_needing_improvement" => await HandleGetPatternsNeedingImprovementAsync(args, cancellationToken),
                "record_pattern_feedback" => await HandleRecordPatternFeedbackAsync(args, cancellationToken),
                "suggest_new_pattern" => await HandleSuggestNewPatternAsync(args, cancellationToken),
                "deprecate_pattern" => await HandleDeprecatePatternAsync(args, cancellationToken),
                "evolve_pattern" => await HandleEvolvePatternAsync(args, cancellationToken),
                "initialize_evolving_catalog" => await HandleInitializeCatalogAsync(cancellationToken),
                "initialize_prompts" => await HandleInitializePromptsAsync(cancellationToken),
                
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = result }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool {Tool}", toolName);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = JsonSerializer.Serialize(new { error = ex.Message }) }
                }
            };
        }
    }

    #region Prompt Handlers

    private async Task<string> HandleListPromptsAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var activeOnly = !args.TryGetValue("activeOnly", out var activeOnlyObj) || (bool)activeOnlyObj;
        var prompts = await _promptService.ListPromptsAsync(activeOnly, cancellationToken);
        
        var result = prompts.Select(p => new
        {
            p.Name,
            p.Version,
            p.Description,
            p.IsActive,
            p.IsTestVariant,
            p.TimesUsed,
            p.SuccessRate,
            p.AvgConfidence
        });

        return JsonSerializer.Serialize(new { prompts = result, count = prompts.Count });
    }

    private async Task<string> HandleGetPromptHistoryAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var history = await _promptService.GetPromptHistoryAsync(name, cancellationToken);
        
        var result = history.Select(p => new
        {
            p.Version,
            p.IsActive,
            p.IsTestVariant,
            p.CreatedBy,
            p.EvolutionReason,
            p.TimesUsed,
            p.SuccessRate,
            p.AvgConfidence,
            p.CreatedAt
        });

        return JsonSerializer.Serialize(new { name, versions = result });
    }

    private async Task<string> HandleGetPromptMetricsAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var metrics = await _promptService.GetPromptMetricsAsync(name, cancellationToken);
        return JsonSerializer.Serialize(metrics);
    }

    private async Task<string> HandleCreatePromptVersionAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var content = args["content"].ToString()!;
        var reason = args["reason"].ToString()!;
        var activateImmediately = args.TryGetValue("activateImmediately", out var activate) && (bool)activate;

        var newVersion = await _promptService.CreateVersionAsync(name, content, reason, activateImmediately, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Created prompt '{name}' v{newVersion.Version}",
            version = newVersion.Version,
            isActive = newVersion.IsActive
        });
    }

    private async Task<string> HandleStartABTestAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var testVersion = Convert.ToInt32(args["testVersion"]);
        var trafficPercent = args.TryGetValue("trafficPercent", out var traffic) ? Convert.ToInt32(traffic) : 10;

        await _promptService.StartABTestAsync(name, testVersion, trafficPercent, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Started A/B test for '{name}' v{testVersion} at {trafficPercent}% traffic"
        });
    }

    private async Task<string> HandleGetABTestResultsAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var results = await _promptService.GetABTestResultsAsync(name, cancellationToken);
        return JsonSerializer.Serialize(results);
    }

    private async Task<string> HandlePromotePromptAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        await _promptService.StopABTestAsync(name, promoteTestVersion: true, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Promoted test version of '{name}' to active"
        });
    }

    private async Task<string> HandleRollbackPromptAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        await _promptService.RollbackAsync(name, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Rolled back prompt '{name}' to previous version"
        });
    }

    private async Task<string> HandleSuggestImprovementsAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var improvements = await _promptService.SuggestImprovementsAsync(name, cancellationToken);
        return JsonSerializer.Serialize(new { name, improvements });
    }

    private async Task<string> HandleRecordPromptOutcomeAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var executionId = args["executionId"].ToString()!;
        var wasSuccessful = (bool)args["wasSuccessful"];
        var rating = args.TryGetValue("rating", out var r) ? Convert.ToInt32(r) : (int?)null;
        var comments = args.TryGetValue("comments", out var c) ? c.ToString() : null;

        await _promptService.RecordOutcomeAsync(executionId, wasSuccessful, rating, comments, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Recorded {(wasSuccessful ? "positive" : "negative")} outcome for execution"
        });
    }

    private async Task<string> HandleInitializePromptsAsync(CancellationToken cancellationToken)
    {
        await _promptService.InitializeDefaultPromptsAsync(cancellationToken);
        return JsonSerializer.Serialize(new { success = true, message = "Default prompts initialized" });
    }

    #endregion

    #region Pattern Handlers

    private async Task<string> HandleGetCatalogMetricsAsync(CancellationToken cancellationToken)
    {
        var metrics = await _patternCatalogService.GetCatalogMetricsAsync(cancellationToken);
        return JsonSerializer.Serialize(metrics);
    }

    private async Task<string> HandleGetPatternMetricsAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var metrics = await _patternCatalogService.GetPatternMetricsAsync(name, cancellationToken);
        return JsonSerializer.Serialize(metrics);
    }

    private async Task<string> HandleGetMostUsefulPatternsAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var limit = args.TryGetValue("limit", out var l) ? Convert.ToInt32(l) : 20;
        var patterns = await _patternCatalogService.GetMostUsefulPatternsAsync(limit, cancellationToken);
        
        var result = patterns.Select(p => new
        {
            p.Name,
            p.Type,
            p.Category,
            p.UsefulnessScore,
            p.TimesDetected,
            p.TimesUseful,
            p.Recommendation
        });

        return JsonSerializer.Serialize(new { patterns = result, count = patterns.Count });
    }

    private async Task<string> HandleGetPatternsNeedingImprovementAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var threshold = args.TryGetValue("threshold", out var t) ? Convert.ToSingle(t) : 0.5f;
        var patterns = await _patternCatalogService.GetPatternsNeedingImprovementAsync(threshold, cancellationToken);
        
        var result = patterns.Select(p => new
        {
            p.Name,
            p.Type,
            p.Category,
            p.UsefulnessScore,
            p.TimesDetected,
            p.TimesNotUseful,
            p.Recommendation
        });

        return JsonSerializer.Serialize(new { patterns = result, count = patterns.Count, threshold });
    }

    private async Task<string> HandleRecordPatternFeedbackAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var patternName = args["patternName"].ToString()!;
        var wasUseful = (bool)args["wasUseful"];

        if (wasUseful)
        {
            await _patternCatalogService.RecordUsefulAsync(patternName, cancellationToken);
        }
        else
        {
            await _patternCatalogService.RecordNotUsefulAsync(patternName, cancellationToken);
        }
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Recorded {(wasUseful ? "positive" : "negative")} feedback for pattern '{patternName}'"
        });
    }

    private async Task<string> HandleSuggestNewPatternAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var request = new PatternSuggestionRequest
        {
            CodeExample = args["codeExample"].ToString()!,
            Description = args["description"].ToString()!,
            SuggestedName = args.TryGetValue("suggestedName", out var n) ? n.ToString() : null,
            Rationale = args.TryGetValue("rationale", out var r) ? r.ToString() : null,
            Context = args.TryGetValue("context", out var c) ? c.ToString()?.ToLowerInvariant() : null
        };

        var suggestion = await _patternCatalogService.SuggestPatternAsync(request, cancellationToken);
        return JsonSerializer.Serialize(suggestion);
    }

    private async Task<string> HandleDeprecatePatternAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var reason = args["reason"].ToString()!;
        var supersededBy = args.TryGetValue("supersededBy", out var s) ? s.ToString() : null;

        await _patternCatalogService.DeprecatePatternAsync(name, reason, supersededBy, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Deprecated pattern '{name}': {reason}"
        });
    }

    private async Task<string> HandleEvolvePatternAsync(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        var name = args["name"].ToString()!;
        var reason = args["reason"].ToString()!;
        var newRecommendation = args.TryGetValue("newRecommendation", out var rec) ? rec.ToString() : null;
        List<string>? newExamples = null;
        
        if (args.TryGetValue("newExamples", out var examples) && examples is JsonElement jsonArray)
        {
            newExamples = jsonArray.EnumerateArray().Select(e => e.GetString()!).ToList();
        }

        var newVersion = await _patternCatalogService.CreateVersionAsync(name, reason, newRecommendation, null, newExamples, cancellationToken);
        
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Created pattern '{name}' v{newVersion.Version}",
            version = newVersion.Version
        });
    }

    private async Task<string> HandleInitializeCatalogAsync(CancellationToken cancellationToken)
    {
        var isInitialized = await _patternCatalogService.IsInitializedAsync(cancellationToken);
        if (isInitialized)
        {
            return JsonSerializer.Serialize(new { success = true, message = "Catalog already initialized" });
        }

        await _patternCatalogService.InitializeFromStaticCatalogAsync(cancellationToken);
        return JsonSerializer.Serialize(new { success = true, message = "Migrated patterns from static catalog" });
    }

    #endregion
}

