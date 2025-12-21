using System.Text;
using System.Text.Json;
using AgentContracts.Requests;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// HTTP client for MemoryAgent.Server
/// </summary>
public class MemoryAgentClient : IMemoryAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MemoryAgentClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MemoryAgentClient(HttpClient httpClient, ILogger<MemoryAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CodeContext?> GetContextAsync(string task, string context, CancellationToken cancellationToken)
    {
        try
        {
            // Call get_context MCP tool
            var request = new
            {
                name = "get_context",
                arguments = new
                {
                    task,
                    context,
                    includePatterns = true,
                    includeQA = true
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                // Parse the response and extract context
                // For now, return empty context - actual implementation would parse MCP response
                return new CodeContext();
            }

            _logger.LogWarning("Failed to get context from MemoryAgent: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting context from MemoryAgent");
            return null;
        }
    }

    public async Task StoreQaAsync(string question, string answer, List<string> relevantFiles, string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "store_qa",
                arguments = new
                {
                    question,
                    answer,
                    relevantFiles,
                    context
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to store Q&A in MemoryAgent: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing Q&A in MemoryAgent");
        }
    }

    public async Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "manage_prompts",
                arguments = new
                {
                    action = "list",
                    name = promptName,
                    activeOnly = true
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var extractedPrompt = ExtractPromptFromResponse(content, promptName);
                if (extractedPrompt != null)
                {
                    _logger.LogInformation("✅ Got prompt {PromptName} v{Version} from Lightning", 
                        promptName, extractedPrompt.Version);
                    return extractedPrompt;
                }
            }

            _logger.LogError("❌ CRITICAL: Prompt '{PromptName}' not found in Lightning. Ensure prompts are seeded.", promptName);
            throw new InvalidOperationException($"Required prompt '{promptName}' not found in Lightning. Run PromptSeedService or check Neo4j connection.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "❌ CRITICAL: Cannot connect to Lightning to get prompt '{PromptName}'", promptName);
            throw new InvalidOperationException($"Cannot connect to Lightning to get required prompt '{promptName}'. Ensure MemoryAgent is running.", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "❌ CRITICAL: Error getting prompt '{PromptName}' from Lightning", promptName);
            throw new InvalidOperationException($"Failed to get required prompt '{promptName}' from Lightning: {ex.Message}", ex);
        }
    }

    private PromptInfo? ExtractPromptFromResponse(string jsonResponse, string promptName)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonResponse);
            
            if (doc.RootElement.TryGetProperty("result", out var result) &&
                result.TryGetProperty("content", out var contentArray))
            {
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textElement))
                    {
                        var text = textElement.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            try
                            {
                                var promptDoc = JsonDocument.Parse(text);
                                if (promptDoc.RootElement.TryGetProperty("prompts", out var prompts))
                                {
                                    foreach (var p in prompts.EnumerateArray())
                                    {
                                        var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                                        if (name == promptName)
                                        {
                                            return new PromptInfo
                                            {
                                                Name = promptName,
                                                Content = p.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                                                Version = p.TryGetProperty("version", out var v) ? v.GetInt32() : 1,
                                                IsActive = true
                                            };
                                        }
                                    }
                                }
                                else if (promptDoc.RootElement.TryGetProperty("content", out var directContent))
                                {
                                    return new PromptInfo
                                    {
                                        Name = promptName,
                                        Content = directContent.GetString() ?? "",
                                        Version = promptDoc.RootElement.TryGetProperty("version", out var v) ? v.GetInt32() : 1,
                                        IsActive = true
                                    };
                                }
                            }
                            catch
                            {
                                return new PromptInfo
                {
                    Name = promptName,
                                    Content = text,
                    Version = 1,
                    IsActive = true
                };
            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract prompt from response");
        }
        return null;
    }

    public async Task RecordPromptFeedbackAsync(string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "feedback",
                arguments = new
                {
                    type = "prompt",
                    promptName = promptName,
                    wasSuccessful = wasSuccessful,
                    rating = rating
                }
            };

            await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            _logger.LogDebug("Recorded prompt feedback for {PromptName}: success={Success}, rating={Rating}", 
                promptName, wasSuccessful, rating);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error recording prompt feedback");
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 🧠 TASK LEARNING: Record detailed task failure for future avoidance
    /// </summary>
    public async Task RecordTaskFailureAsync(TaskFailureRecord failure, CancellationToken cancellationToken)
    {
        try
        {
            // Use proper JSONRPC format
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "store_task_failure",
                    arguments = new
                    {
                        taskDescription = failure.TaskDescription,
                        taskKeywords = failure.TaskKeywords,
                        language = failure.Language,
                        failurePhase = failure.FailurePhase,
                        errorMessage = failure.ErrorMessage,
                        errorPattern = failure.ErrorPattern,
                        approachesTried = failure.ApproachesTried,
                        modelsUsed = failure.ModelsUsed,
                        iterationsAttempted = failure.IterationsAttempted,
                        lessonsLearned = failure.LessonsLearned,
                        context = failure.Context
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "📝 Recorded task failure: {Phase} in {Language} - {Pattern}", 
                    failure.FailurePhase, failure.Language, failure.ErrorPattern);
            }
            else
            {
                _logger.LogWarning("Failed to record task failure: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error recording task failure (non-critical)");
        }
    }

    /// <summary>
    /// 🧠 TASK LEARNING: Query lessons learned from similar failed tasks
    /// </summary>
    public async Task<TaskLessonsResult> QueryTaskLessonsAsync(
        string taskDescription, 
        List<string> keywords, 
        string language, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Use proper JSONRPC format
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "query_task_lessons",
                    arguments = new
                    {
                        taskDescription,
                        taskKeywords = keywords,
                        language,
                        limit = 5
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Parse JSONRPC response structure
                var jsonrpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(content, JsonOptions);
                if (jsonrpcResponse?.Result?.Content?.FirstOrDefault()?.Text != null)
                {
                    var result = JsonSerializer.Deserialize<TaskLessonsResult>(
                        jsonrpcResponse.Result.Content.First().Text, JsonOptions);
                    
                    if (result != null)
                    {
                        _logger.LogInformation(
                            "🧠 Found {Count} lessons learned for similar tasks", 
                            result.FoundLessons);
                        return result;
                    }
                }
            }
            
            _logger.LogDebug("No lessons found for task");
            return new TaskLessonsResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying task lessons (non-critical)");
            return new TaskLessonsResult();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 🚀 SMART CODE GENERATION - Phase 1: Foundation
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 📋 Generate a plan with checklist before code generation
    /// </summary>
    public async Task<TaskPlan> GeneratePlanAsync(string task, string language, string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "generate_task_plan",
                    arguments = new { task, language, context }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonrpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(content, JsonOptions);
                
                if (jsonrpcResponse?.Result?.Content?.FirstOrDefault()?.Text != null)
                {
                    var plan = JsonSerializer.Deserialize<TaskPlan>(
                        jsonrpcResponse.Result.Content.First().Text, JsonOptions);
                    
                    if (plan != null)
                    {
                        _logger.LogInformation("📋 Generated plan with {Count} steps for task", plan.Steps.Count);
                        return plan;
                    }
                }
            }
            
            // Return a default plan if MCP call fails
            _logger.LogWarning("Failed to generate plan, using default");
            return CreateDefaultPlan(task, language, context);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating plan (using default)");
            return CreateDefaultPlan(task, language, context);
        }
    }

    /// <summary>
    /// 📋 Update plan checklist status
    /// </summary>
    public async Task UpdatePlanStatusAsync(string planId, string stepId, string status, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "update_plan_status",
                    arguments = new { planId, stepId, status }
                }
            };

            await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            _logger.LogDebug("📋 Updated plan step {StepId} to {Status}", stepId, status);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error updating plan status (non-critical)");
        }
    }

    /// <summary>
    /// 📁 Index a generated file immediately (for context awareness)
    /// </summary>
    public async Task IndexFileAsync(string filePath, string content, string language, string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "index",
                    arguments = new
                    {
                        path = filePath,
                        context,
                        scope = "file",
                        // Pass content directly so we don't need the file on disk
                        content,
                        language
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("📁 Indexed file: {Path} in context {Context}", filePath, context);
            }
            else
            {
                _logger.LogWarning("Failed to index file: {Path} - {StatusCode}", filePath, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error indexing file (non-critical): {Path}", filePath);
        }
    }

    /// <summary>
    /// 🔍 Smart search for relevant code/context (searches Qdrant + Neo4j)
    /// </summary>
    public async Task<List<SmartSearchResult>> SmartSearchAsync(string query, string context, int limit, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "smartsearch",
                    arguments = new
                    {
                        query,
                        context,
                        limit,
                        includeRelationships = true
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonrpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(content, JsonOptions);
                
                if (jsonrpcResponse?.Result?.Content?.FirstOrDefault()?.Text != null)
                {
                    // Parse the response - smartsearch returns structured results
                    var results = new List<SmartSearchResult>();
                    var text = jsonrpcResponse.Result.Content.First().Text;
                    
                    // Try to parse as JSON first, fallback to text parsing
                    try
                    {
                        var searchResponse = JsonSerializer.Deserialize<SmartSearchJsonResponse>(text, JsonOptions);
                        if (searchResponse?.Results != null)
                        {
                            results = searchResponse.Results.Select(r => new SmartSearchResult
                            {
                                Name = r.Name ?? "",
                                FilePath = r.FilePath ?? "",
                                Type = r.Type ?? "code",
                                Content = r.Content ?? "",
                                Score = r.Score
                            }).ToList();
                        }
                    }
                    catch
                    {
                        // Fallback: parse as text output
                        _logger.LogDebug("SmartSearch returned text format, parsing manually");
                    }
                    
                    _logger.LogInformation("🔍 SmartSearch found {Count} results for '{Query}' in {Context}", 
                        results.Count, query.Length > 50 ? query[..50] + "..." : query, context);
                    return results;
                }
            }
            
            _logger.LogWarning("SmartSearch failed: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SmartSearch error (non-critical)");
        }
        
        return new List<SmartSearchResult>();
    }

    /// <summary>
    /// 🔍 Get all symbols (classes, methods) in the project context
    /// </summary>
    public async Task<ProjectSymbols> GetProjectSymbolsAsync(string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "get_project_symbols",
                    arguments = new { context }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonrpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(content, JsonOptions);
                
                if (jsonrpcResponse?.Result?.Content?.FirstOrDefault()?.Text != null)
                {
                    var symbols = JsonSerializer.Deserialize<ProjectSymbols>(
                        jsonrpcResponse.Result.Content.First().Text, JsonOptions);
                    
                    if (symbols != null)
                    {
                        _logger.LogInformation("🔍 Retrieved {ClassCount} classes, {FuncCount} functions for context {Context}",
                            symbols.Classes.Count, symbols.Functions.Count, context);
                        return symbols;
                    }
                }
            }
            
            return new ProjectSymbols { Context = context };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting project symbols");
            return new ProjectSymbols { Context = context };
        }
    }

    /// <summary>
    /// ✅ Validate imports before Docker execution
    /// </summary>
    public async Task<ImportValidationResult> ValidateImportsAsync(string code, string language, string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "validate_imports",
                    arguments = new { code, language, context }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonrpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(content, JsonOptions);
                
                if (jsonrpcResponse?.Result?.Content?.FirstOrDefault()?.Text != null)
                {
                    var result = JsonSerializer.Deserialize<ImportValidationResult>(
                        jsonrpcResponse.Result.Content.First().Text, JsonOptions);
                    
                    if (result != null)
                    {
                        if (!result.IsValid)
                        {
                            var invalidImports = result.Imports.Where(i => !i.IsValid).Select(i => i.Module);
                            _logger.LogWarning("⚠️ Invalid imports found: {Imports}", string.Join(", ", invalidImports));
                        }
                        return result;
                    }
                }
            }
            
            // Default to valid if we can't check (graceful degradation)
            return new ImportValidationResult { IsValid = true };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating imports (assuming valid)");
            return new ImportValidationResult { IsValid = true };
        }
    }

    /// <summary>
    /// 🎉 Store successful task approach for future learning
    /// </summary>
    public async Task StoreSuccessfulTaskAsync(TaskSuccessRecord success, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "store_successful_task",
                    arguments = new
                    {
                        taskDescription = success.TaskDescription,
                        language = success.Language,
                        context = success.Context,
                        approachUsed = success.ApproachUsed,
                        patternsUsed = success.PatternsUsed,
                        filesGenerated = success.FilesGenerated,
                        usefulSnippets = success.UsefulSnippets,
                        keywords = success.Keywords,
                        iterationsNeeded = success.IterationsNeeded,
                        finalScore = success.FinalScore,
                        modelUsed = success.ModelUsed,
                        semanticStructure = success.SemanticStructure
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("🎉 Stored successful task: {Task} ({Language}) - {Files} files",
                    success.TaskDescription.Length > 50 ? success.TaskDescription[..50] + "..." : success.TaskDescription,
                    success.Language,
                    success.FilesGenerated.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing successful task (non-critical)");
        }
    }

    /// <summary>
    /// 🔎 Query similar successful tasks for guidance
    /// </summary>
    public async Task<SimilarTasksResult> QuerySimilarSuccessfulTasksAsync(string task, string language, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "query_similar_tasks",
                    arguments = new { task, language, limit = 3 }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonrpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(content, JsonOptions);
                
                if (jsonrpcResponse?.Result?.Content?.FirstOrDefault()?.Text != null)
                {
                    var result = JsonSerializer.Deserialize<SimilarTasksResult>(
                        jsonrpcResponse.Result.Content.First().Text, JsonOptions);
                    
                    if (result != null && result.FoundTasks > 0)
                    {
                        _logger.LogInformation("🔎 Found {Count} similar successful tasks", result.FoundTasks);
                        return result;
                    }
                }
            }
            
            return new SimilarTasksResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying similar tasks");
            return new SimilarTasksResult();
        }
    }

    /// <summary>
    /// 🎨 Query design system for UI tasks
    /// </summary>
    public async Task<DesignContext?> GetDesignContextAsync(string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "design_get_brand",
                    arguments = new { context }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonrpcResponse = JsonSerializer.Deserialize<JsonRpcResponse>(content, JsonOptions);
                
                // Check if response has an error (method not found, etc.)
                if (jsonrpcResponse?.Error != null)
                {
                    _logger.LogDebug("Design context not available: {Error}", jsonrpcResponse.Error.Message);
                    return null;
                }
                
                if (jsonrpcResponse?.Result?.Content?.FirstOrDefault()?.Text != null)
                {
                    var text = jsonrpcResponse.Result.Content.First().Text;
                    
                    // Check if text is an error message or valid JSON
                    if (text.StartsWith("Error", StringComparison.OrdinalIgnoreCase) || 
                        !text.TrimStart().StartsWith("{"))
                    {
                        _logger.LogDebug("Design context returned error or non-JSON: {Text}", 
                            text.Length > 100 ? text.Substring(0, 100) + "..." : text);
                        return null;
                    }
                    
                    var design = JsonSerializer.Deserialize<DesignContext>(text, JsonOptions);
                    
                    if (design != null)
                    {
                        _logger.LogInformation("🎨 Retrieved design context for {Context}", context);
                        return design;
                    }
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting design context");
            return null;
        }
    }

    /// <summary>
    /// Create a default plan when MCP tool is unavailable
    /// </summary>
    private static TaskPlan CreateDefaultPlan(string task, string language, string context)
    {
        return new TaskPlan
        {
            Task = task,
            Language = language,
            Context = context,
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    StepId = "1",
                    Order = 1,
                    Description = "Generate main implementation",
                    Status = "pending"
                }
            }
        };
    }

    // NO FALLBACK PROMPTS - All prompts MUST come from Lightning
    // If a prompt is missing, the system will throw an error
    // Run PromptSeedService to seed required prompts into Neo4j
}

/// <summary>
/// JSONRPC response wrapper
/// </summary>
internal class JsonRpcResponse
{
    public string? JsonRpc { get; set; }
    public int? Id { get; set; }
    public McpCallResult? Result { get; set; }
    public JsonRpcError? Error { get; set; }
}

internal class McpCallResult
{
    public List<McpContent>? Content { get; set; }
    public bool IsError { get; set; }
}

internal class McpContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

internal class JsonRpcError
{
    public int Code { get; set; }
    public string? Message { get; set; }
}

internal class SmartSearchJsonResponse
{
    public List<SmartSearchJsonResult>? Results { get; set; }
}

internal class SmartSearchJsonResult
{
    public string? Name { get; set; }
    public string? FilePath { get; set; }
    public string? Type { get; set; }
    public string? Content { get; set; }
    public float Score { get; set; }
}

