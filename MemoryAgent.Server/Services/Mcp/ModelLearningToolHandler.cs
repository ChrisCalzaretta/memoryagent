using System.Text.Json;
using MemoryAgent.Server.Models;
using Neo4j.Driver;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// MCP Tool Handler for Model Learning - tracks model performance for smart selection
/// Stores data in Neo4j for relationship-based queries
/// NOTE: These tools are INTERNAL - used by CodingAgent, NOT exposed to Cursor
/// </summary>
public class ModelLearningToolHandler : IMcpToolHandler
{
    private readonly IGraphService _graphService;
    private readonly IDriver _neo4jDriver;
    private readonly ILogger<ModelLearningToolHandler> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ModelLearningToolHandler(
        IGraphService graphService,
        IDriver neo4jDriver,
        ILogger<ModelLearningToolHandler> logger)
    {
        _graphService = graphService;
        _neo4jDriver = neo4jDriver;
        _logger = logger;
    }

    /// <summary>
    /// Get tools - implements IMcpToolHandler
    /// These tools are INTERNAL only - filtered out in McpService.GetToolsAsync()
    /// </summary>
    public IEnumerable<McpTool> GetTools()
    {
        return new List<McpTool>
        {
            new McpTool
            {
                Name = "store_model_performance",
                Description = "Record model performance for learning. Called after each code generation task to track which models succeed on which task types.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        model = new { type = "string", description = "Model name (e.g., qwen2.5-coder:14b)" },
                        taskType = new { type = "string", description = "Task type: code_generation, fix, validation" },
                        language = new { type = "string", description = "Programming language" },
                        complexity = new { type = "string", description = "Task complexity: simple, moderate, complex, very_complex" },
                        outcome = new { type = "string", description = "Outcome: success, partial, failure" },
                        score = new { type = "integer", description = "Validation score (0-10)" },
                        durationMs = new { type = "integer", description = "Duration in milliseconds" },
                        iterations = new { type = "integer", description = "Number of iterations needed" },
                        errorType = new { type = "string", description = "Error type if failed" },
                        context = new { type = "string", description = "Project context" }
                    },
                    required = new[] { "model", "taskType", "outcome" }
                }
            },
            new McpTool
            {
                Name = "query_best_model",
                Description = "Query the best model for a task based on historical performance data. Returns ranked recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        taskType = new { type = "string", description = "Task type: code_generation, fix, validation" },
                        language = new { type = "string", description = "Programming language" },
                        complexity = new { type = "string", description = "Task complexity" },
                        context = new { type = "string", description = "Project context" },
                        maxVramGb = new { type = "number", description = "Maximum VRAM available (GB)" }
                    },
                    required = new[] { "taskType" }
                }
            },
            new McpTool
            {
                Name = "get_model_stats",
                Description = "Get aggregated performance statistics for all models, optionally filtered by language or task type.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        language = new { type = "string", description = "Filter by language (optional)" },
                        taskType = new { type = "string", description = "Filter by task type (optional)" }
                    },
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "get_loaded_models",
                Description = "Get list of models currently loaded in Ollama (warm/ready for instant use). Prefer these models to avoid cold start latency.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        ollamaUrl = new { type = "string", description = "Ollama URL (default: http://localhost:11434)" }
                    },
                    required = Array.Empty<string>()
                }
            }
        };
    }

    /// <summary>
    /// Handle MCP tool calls - implements IMcpToolHandler
    /// </summary>
    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        var arguments = args ?? new Dictionary<string, object>();
        
        var result = toolName switch
        {
            "store_model_performance" => await HandleStorePerformanceAsync(arguments, cancellationToken),
            "query_best_model" => await HandleQueryBestModelAsync(arguments, cancellationToken),
            "get_model_stats" => await HandleGetModelStatsAsync(arguments, cancellationToken),
            "get_loaded_models" => await HandleGetLoadedModelsAsync(arguments, cancellationToken),
            _ => $"Unknown tool: {toolName}"
        };
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = result }
            }
        };
    }

    private async Task<string> HandleStorePerformanceAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var model = GetStringArg(arguments, "model");
        var taskType = GetStringArg(arguments, "taskType");
        var language = GetStringArg(arguments, "language", "unknown");
        var complexity = GetStringArg(arguments, "complexity", "unknown");
        var outcome = GetStringArg(arguments, "outcome");
        var score = GetIntArg(arguments, "score", 0);
        var durationMs = GetLongArg(arguments, "durationMs", 0);
        var iterations = GetIntArg(arguments, "iterations", 1);
        var errorType = GetStringArg(arguments, "errorType", null);
        var context = GetStringArg(arguments, "context", "default");
        var taskKeywords = GetListArg(arguments, "taskKeywords");
        
        try
        {
            await using var session = _neo4jDriver.AsyncSession();
            
            await session.ExecuteWriteAsync(async tx =>
            {
                // Store in Neo4j with relationships
                var query = @"
                    MERGE (m:Model {name: $model})
                    MERGE (t:TaskType {name: $taskType})
                    MERGE (l:Language {name: $language})
                    MERGE (c:Complexity {level: $complexity})
                    
                    CREATE (p:Performance {
                        outcome: $outcome,
                        score: $score,
                        durationMs: $durationMs,
                        iterations: $iterations,
                        errorType: $errorType,
                        context: $context,
                        keywords: $keywords,
                        recordedAt: datetime()
                    })
                    
                    CREATE (m)-[:PERFORMED]->(p)
                    CREATE (p)-[:ON_TASK_TYPE]->(t)
                    CREATE (p)-[:IN_LANGUAGE]->(l)
                    CREATE (p)-[:WITH_COMPLEXITY]->(c)
                    
                    RETURN id(p) as performanceId";
                
                await tx.RunAsync(query, new
                {
                    model,
                    taskType,
                    language,
                    complexity,
                    outcome,
                    score,
                    durationMs,
                    iterations,
                    errorType = errorType ?? "",
                    context,
                    keywords = taskKeywords
                });
            });
            
            _logger.LogInformation(
                "📊 Stored model performance: {Model} on {TaskType}/{Language} = {Outcome} ({Score}/10)",
                model, taskType, language, outcome, score);
            
            return $"✅ Recorded: {model} on {taskType}/{language} = {outcome} (score: {score}/10)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing model performance");
            return $"❌ Error storing performance: {ex.Message}";
        }
    }

    private async Task<string> HandleQueryBestModelAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var taskType = GetStringArg(arguments, "taskType");
        var language = GetStringArg(arguments, "language", null);
        var complexity = GetStringArg(arguments, "complexity", null);
        var context = GetStringArg(arguments, "context", null);
        var excludeModels = GetListArg(arguments, "excludeModels");
        var maxVramGb = GetDoubleArg(arguments, "maxVramGb", 24);
        var includeRelatedLanguages = GetBoolArg(arguments, "includeRelatedLanguages", true);
        
        try
        {
            await using var session = _neo4jDriver.AsyncSession();
            
            // Get related languages for cross-language learning
            var relatedLanguages = includeRelatedLanguages && !string.IsNullOrEmpty(language)
                ? GetRelatedLanguages(language)
                : new List<string>();
            
            var results = await session.ExecuteReadAsync(async tx =>
            {
                // Build query with TIME-DECAY WEIGHTING and optional filters
                // Recent performance (24h) = 3x weight, last week = 2x, older = 1x
                var languageFilter = !string.IsNullOrEmpty(language) 
                    ? "MATCH (p)-[:IN_LANGUAGE]->(l:Language) WHERE l.name = $language OR l.name IN $relatedLanguages" 
                    : "";
                var complexityFilter = !string.IsNullOrEmpty(complexity) 
                    ? "MATCH (p)-[:WITH_COMPLEXITY]->(c:Complexity {level: $complexity})" 
                    : "";
                
                // 🕐 TIME-DECAY WEIGHTED QUERY
                // Recent results count more than older ones
                var query = $@"
                    MATCH (m:Model)-[:PERFORMED]->(p:Performance)-[:ON_TASK_TYPE]->(t:TaskType {{name: $taskType}})
                    WHERE NOT m.name IN $excludeModels
                    {languageFilter}
                    {complexityFilter}
                    
                    WITH m.name AS model, p,
                         CASE 
                             WHEN p.recordedAt > datetime() - duration('P1D') THEN 3.0  // Last 24 hours = 3x weight
                             WHEN p.recordedAt > datetime() - duration('P7D') THEN 2.0  // Last week = 2x weight
                             ELSE 1.0  // Older = 1x weight
                         END AS timeWeight
                    
                    WITH model,
                         SUM(timeWeight) AS totalWeight,
                         SUM(CASE WHEN p.outcome = 'success' THEN timeWeight ELSE 0 END) AS weightedSuccesses,
                         SUM(p.score * timeWeight) / SUM(timeWeight) AS weightedAvgScore,
                         AVG(p.iterations) AS avgIterations,
                         AVG(p.durationMs) AS avgDuration,
                         COUNT(p) AS attempts
                    WHERE attempts >= 1
                    
                    RETURN model, 
                           toInteger(totalWeight) AS weightedAttempts,
                           toInteger(weightedSuccesses) AS weightedSuccessCount,
                           CASE WHEN totalWeight > 0 THEN weightedSuccesses / totalWeight * 100 ELSE 0 END AS successRate,
                           weightedAvgScore AS avgScore, 
                           avgIterations, 
                           avgDuration,
                           attempts AS rawAttempts
                    ORDER BY successRate DESC, avgScore DESC, weightedAttempts DESC
                    LIMIT 5";
                
                var cursor = await tx.RunAsync(query, new
                {
                    taskType,
                    language = language ?? "",
                    relatedLanguages,
                    complexity = complexity ?? "",
                    excludeModels
                });
                
                var list = new List<ModelQueryResult>();
                await foreach (var record in cursor)
                {
                    list.Add(new ModelQueryResult
                    {
                        Model = record["model"].As<string>(),
                        Attempts = record["rawAttempts"].As<int>(),
                        Successes = record["weightedSuccessCount"].As<int>(),
                        SuccessRate = record["successRate"].As<double>(),
                        AvgScore = record["avgScore"].As<double>(),
                        AvgIterations = record["avgIterations"].As<double>(),
                        AvgDuration = record["avgDuration"].As<double>()
                    });
                }
                return list;
            });
            
            if (!results.Any())
            {
                return JsonSerializer.Serialize(new BestModelResponse
                {
                    RecommendedModel = "",
                    Reasoning = $"No historical data for {taskType}" + (language != null ? $"/{language}" : ""),
                    IsHistorical = false
                }, JsonOptions);
            }
            
            var best = results.First();
            var alternatives = results.Skip(1).Select(r => new ModelRecommendation
            {
                Model = r.Model,
                SuccessRate = r.SuccessRate,
                AverageScore = r.AvgScore,
                SampleCount = r.Attempts,
                Reasoning = $"{r.Successes}/{r.Attempts} successes, avg score {r.AvgScore:F1}"
            }).ToList();
            
            var response = new BestModelResponse
            {
                RecommendedModel = best.Model,
                SuccessRate = best.SuccessRate,
                AverageScore = best.AvgScore,
                SampleCount = best.Attempts,
                Reasoning = $"Best performer for {taskType}" + (language != null ? $"/{language}" : "") +
                           $": {best.Successes}/{best.Attempts} successes ({best.SuccessRate:F0}%), avg score {best.AvgScore:F1}",
                Alternatives = alternatives,
                IsHistorical = true
            };
            
            _logger.LogInformation(
                "🧠 Recommended {Model} for {TaskType}/{Language}: {SuccessRate:F0}% success rate",
                best.Model, taskType, language ?? "any", best.SuccessRate);
            
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying best model");
            return JsonSerializer.Serialize(new BestModelResponse
            {
                RecommendedModel = "",
                Reasoning = $"Error: {ex.Message}",
                IsHistorical = false
            }, JsonOptions);
        }
    }

    private async Task<string> HandleGetModelStatsAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var language = GetStringArg(arguments, "language", null);
        var taskType = GetStringArg(arguments, "taskType", null);
        
        try
        {
            await using var session = _neo4jDriver.AsyncSession();
            
            var stats = await session.ExecuteReadAsync(async tx =>
            {
                var languageFilter = !string.IsNullOrEmpty(language) 
                    ? "MATCH (p)-[:IN_LANGUAGE]->(l:Language {name: $language})" 
                    : "";
                var taskTypeFilter = !string.IsNullOrEmpty(taskType) 
                    ? "WHERE t.name = $taskType" 
                    : "";
                
                var query = $@"
                    MATCH (m:Model)-[:PERFORMED]->(p:Performance)-[:ON_TASK_TYPE]->(t:TaskType)
                    {languageFilter}
                    {taskTypeFilter}
                    
                    WITH m.name AS model, t.name AS taskType,
                         COUNT(p) AS attempts,
                         SUM(CASE WHEN p.outcome = 'success' THEN 1 ELSE 0 END) AS successes,
                         SUM(CASE WHEN p.outcome = 'failure' THEN 1 ELSE 0 END) AS failures,
                         AVG(p.score) AS avgScore,
                         AVG(p.durationMs) AS avgDuration,
                         AVG(p.iterations) AS avgIterations
                    
                    RETURN model, taskType, attempts, successes, failures, avgScore, avgDuration, avgIterations
                    ORDER BY model, taskType";
                
                var cursor = await tx.RunAsync(query, new
                {
                    language = language ?? "",
                    taskType = taskType ?? ""
                });
                
                var list = new List<ModelStats>();
                await foreach (var record in cursor)
                {
                    list.Add(new ModelStats
                    {
                        Model = record["model"].As<string>(),
                        TaskType = record["taskType"].As<string>(),
                        Language = language ?? "all",
                        TotalAttempts = record["attempts"].As<int>(),
                        Successes = record["successes"].As<int>(),
                        Failures = record["failures"].As<int>(),
                        AverageScore = record["avgScore"].As<double>(),
                        AverageDurationMs = record["avgDuration"].As<double>(),
                        AverageIterations = record["avgIterations"].As<double>()
                    });
                }
                return list;
            });
            
            // Format as readable text
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("## 📊 Model Performance Statistics\n");
            
            if (!stats.Any())
            {
                sb.AppendLine("No performance data recorded yet.");
            }
            else
            {
                sb.AppendLine("| Model | Task Type | Success Rate | Avg Score | Samples |");
                sb.AppendLine("|-------|-----------|--------------|-----------|---------|");
                
                foreach (var stat in stats)
                {
                    sb.AppendLine($"| {stat.Model} | {stat.TaskType} | {stat.SuccessRate:F0}% | {stat.AverageScore:F1} | {stat.TotalAttempts} |");
                }
            }
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model stats");
            return $"❌ Error getting stats: {ex.Message}";
        }
    }

    private async Task<string> HandleGetLoadedModelsAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var ollamaUrl = GetStringArg(arguments, "ollamaUrl", "http://localhost:11434");
        
        try
        {
            // Query Ollama's /api/ps endpoint to see what's currently loaded
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            
            var response = await client.GetAsync($"{ollamaUrl}/api/ps", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new LoadedModelsResponse
                {
                    LoadedModels = new List<LoadedModel>(),
                    Message = $"Failed to query Ollama: {response.StatusCode}"
                }, JsonOptions);
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResponse = JsonSerializer.Deserialize<OllamaPsResponse>(content, JsonOptions);
            
            var loadedModels = ollamaResponse?.Models?.Select(m => new LoadedModel
            {
                Name = m.Name ?? "unknown",
                Size = m.Size,
                SizeGb = m.Size / (1024.0 * 1024 * 1024),
                VramUsageMb = m.SizeVram / (1024.0 * 1024),
                ExpiresAt = m.ExpiresAt ?? DateTime.UtcNow.AddMinutes(5)
            }).ToList() ?? new List<LoadedModel>();
            
            _logger.LogInformation("🔥 Found {Count} models loaded in Ollama", loadedModels.Count);
            
            return JsonSerializer.Serialize(new LoadedModelsResponse
            {
                LoadedModels = loadedModels,
                Message = loadedModels.Count > 0 
                    ? $"Found {loadedModels.Count} loaded models (prefer these for instant response)"
                    : "No models currently loaded - any model will have cold start latency"
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Ollama for loaded models");
            return JsonSerializer.Serialize(new LoadedModelsResponse
            {
                LoadedModels = new List<LoadedModel>(),
                Message = $"Error querying Ollama: {ex.Message}"
            }, JsonOptions);
        }
    }

    #region Cross-Language Learning
    
    /// <summary>
    /// Get related languages for cross-language learning
    /// If a model is good at Python, it's likely good at TypeScript (similar paradigms)
    /// </summary>
    private static List<string> GetRelatedLanguages(string language)
    {
        var languageFamilies = new Dictionary<string, List<string>>
        {
            // C-family languages (similar syntax, static typing)
            ["csharp"] = new() { "java", "typescript", "kotlin", "go" },
            ["java"] = new() { "csharp", "kotlin", "typescript", "go" },
            ["kotlin"] = new() { "java", "csharp", "typescript", "swift" },
            
            // Dynamic scripting languages
            ["python"] = new() { "javascript", "typescript", "ruby" },
            ["javascript"] = new() { "typescript", "python" },
            ["typescript"] = new() { "javascript", "python", "csharp" },
            ["ruby"] = new() { "python", "javascript" },
            
            // Systems languages
            ["go"] = new() { "csharp", "java", "rust" },
            ["rust"] = new() { "go", "csharp" },
            
            // Mobile languages
            ["swift"] = new() { "kotlin", "typescript" },
            ["dart"] = new() { "typescript", "kotlin", "javascript" },
            
            // Functional languages
            ["fsharp"] = new() { "csharp", "haskell" }
        };
        
        var normalized = language.ToLowerInvariant();
        return languageFamilies.TryGetValue(normalized, out var related) ? related : new List<string>();
    }
    
    #endregion
    
    #region Helper Methods
    
    private static string GetStringArg(Dictionary<string, object> args, string key, string? defaultValue = null)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
                return jsonElement.GetString() ?? defaultValue ?? "";
            return value?.ToString() ?? defaultValue ?? "";
        }
        return defaultValue ?? "";
    }
    
    private static int GetIntArg(Dictionary<string, object> args, string key, int defaultValue = 0)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.TryGetInt32(out var intVal))
                return intVal;
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (int.TryParse(value?.ToString(), out var parsed)) return parsed;
        }
        return defaultValue;
    }
    
    private static long GetLongArg(Dictionary<string, object> args, string key, long defaultValue = 0)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.TryGetInt64(out var longVal))
                return longVal;
            if (value is long l) return l;
            if (value is int i) return i;
            if (long.TryParse(value?.ToString(), out var parsed)) return parsed;
        }
        return defaultValue;
    }
    
    private static double GetDoubleArg(Dictionary<string, object> args, string key, double defaultValue = 0)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.TryGetDouble(out var dblVal))
                return dblVal;
            if (value is double d) return d;
            if (value is float f) return f;
            if (double.TryParse(value?.ToString(), out var parsed)) return parsed;
        }
        return defaultValue;
    }
    
    private static bool GetBoolArg(Dictionary<string, object> args, string key, bool defaultValue = false)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.True) return true;
            if (value is JsonElement jsonElement2 && jsonElement2.ValueKind == JsonValueKind.False) return false;
            if (value is bool b) return b;
            if (bool.TryParse(value?.ToString(), out var parsed)) return parsed;
        }
        return defaultValue;
    }
    
    private static List<string> GetListArg(Dictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
            {
                return jsonElement.EnumerateArray()
                    .Select(e => e.GetString() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            if (value is List<string> list) return list;
            if (value is IEnumerable<object> enumerable)
                return enumerable.Select(o => o.ToString() ?? "").ToList();
        }
        return new List<string>();
    }
    
    #endregion
}

#region Response Types (local copies to avoid cross-project dependency)

/// <summary>
/// Response with recommended models
/// </summary>
internal class BestModelResponse
{
    public string RecommendedModel { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public double SuccessRate { get; set; }
    public double AverageScore { get; set; }
    public int SampleCount { get; set; }
    public List<ModelRecommendation> Alternatives { get; set; } = new();
    public bool IsHistorical { get; set; }
}

/// <summary>
/// A model recommendation with stats
/// </summary>
internal class ModelRecommendation
{
    public string Model { get; set; } = "";
    public double SuccessRate { get; set; }
    public double AverageScore { get; set; }
    public int SampleCount { get; set; }
    public double SizeGb { get; set; }
    public string Reasoning { get; set; } = "";
}

/// <summary>
/// Aggregated stats for a model on a task type
/// </summary>
internal class ModelStats
{
    public string Model { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string Language { get; set; } = "";
    public int TotalAttempts { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
    public double SuccessRate => TotalAttempts > 0 ? (double)Successes / TotalAttempts * 100 : 0;
    public double AverageScore { get; set; }
    public double AverageDurationMs { get; set; }
    public double AverageIterations { get; set; }
}

internal class ModelQueryResult
{
    public string Model { get; set; } = "";
    public int Attempts { get; set; }
    public int Successes { get; set; }
    public double SuccessRate { get; set; }
    public double AvgScore { get; set; }
    public double AvgIterations { get; set; }
    public double AvgDuration { get; set; }
}

internal class LoadedModelsResponse
{
    public List<LoadedModel> LoadedModels { get; set; } = new();
    public string Message { get; set; } = "";
}

internal class LoadedModel
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public double SizeGb { get; set; }
    public double VramUsageMb { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal class OllamaPsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("models")]
    public List<OllamaPsModel>? Models { get; set; }
}

internal class OllamaPsModel
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public long Size { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("size_vram")]
    public long SizeVram { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}

#endregion

