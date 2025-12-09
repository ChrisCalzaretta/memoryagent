using System.Net.Http.Json;
using System.Text.Json;
using AgentContracts.Models;

namespace CodingAgent.Server.Clients;

/// <summary>
/// HTTP client for MemoryAgent.Server (Lightning)
/// </summary>
public class MemoryAgentClient : IMemoryAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MemoryAgentClient> _logger;
    
    // Prompt cache to reduce Lightning calls
    private readonly Dictionary<string, (PromptInfo Prompt, DateTime FetchedAt)> _promptCache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    
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

    public async Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken)
    {
        // Check cache first
        if (_promptCache.TryGetValue(promptName, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < _cacheDuration)
        {
            _logger.LogDebug("Using cached prompt for {PromptName}", promptName);
            return cached.Prompt;
        }

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
                    name = "manage_prompts",
                    arguments = new
                    {
                        action = "list",
                        name = promptName,
                        activeOnly = true
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Try to parse prompt from Lightning response
                var extractedPrompt = ExtractPromptFromResponse(content, promptName);
                
                if (extractedPrompt != null)
                {
                    _logger.LogInformation("✅ Got prompt {PromptName} v{Version} from Lightning", 
                        promptName, extractedPrompt.Version);
                    _promptCache[promptName] = (extractedPrompt, DateTime.UtcNow);
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
    
    /// <summary>
    /// Extract prompt content from JSONRPC response
    /// </summary>
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
                            // Try to parse as JSON containing prompts
                            try
                            {
                                var promptDoc = JsonDocument.Parse(text);
                                
                                // Look for prompts array or direct prompt object
                                if (promptDoc.RootElement.TryGetProperty("prompts", out var prompts))
                                {
                                    foreach (var p in prompts.EnumerateArray())
                                    {
                                        var name = p.TryGetProperty("name", out var n) ? n.GetString() : "";
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
                                
                                // Maybe it's a direct content string
                                if (promptDoc.RootElement.TryGetProperty("content", out var directContent))
                                {
                                    return new PromptInfo
                                    {
                                        Name = promptName,
                                        Content = directContent.GetString() ?? "",
                                        Version = 1,
                                        IsActive = true
                                    };
                                }
                            }
                            catch
                            {
                                // Text might not be JSON, could be the prompt itself
                                if (text.Length > 50 && !text.StartsWith("{"))
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
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not parse prompt response");
        }
        
        return null;
    }

    public async Task<List<SimilarSolution>> FindSimilarSolutionsAsync(
        string task, string context, CancellationToken cancellationToken)
    {
        var solutions = new List<SimilarSolution>();
        
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
                    name = "find_similar_questions",
                    arguments = new
                    {
                        question = task,
                        context = context,
                        limit = 5
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                solutions.AddRange(ExtractSolutionsFromResponse(content));
                
                if (solutions.Any())
                {
                    _logger.LogInformation("🔍 Found {Count} similar solutions from Lightning", solutions.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding similar solutions from Lightning");
        }

        return solutions;
    }
    
    /// <summary>
    /// Extract solutions from JSONRPC response
    /// </summary>
    private List<SimilarSolution> ExtractSolutionsFromResponse(string jsonResponse)
    {
        var solutions = new List<SimilarSolution>();
        
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
                                var qaDoc = JsonDocument.Parse(text);
                                
                                // Look for matches array
                                if (qaDoc.RootElement.TryGetProperty("matches", out var matches))
                                {
                                    foreach (var match in matches.EnumerateArray())
                                    {
                                        var solution = new SimilarSolution
                                        {
                                            Question = match.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
                                            Answer = match.TryGetProperty("answer", out var a) ? a.GetString() ?? "" : "",
                                            Similarity = match.TryGetProperty("similarity", out var s) ? s.GetDouble() : 0.5
                                        };
                                        
                                        if (!string.IsNullOrEmpty(solution.Question))
                                            solutions.Add(solution);
                                    }
                                }
                            }
                            catch
                            {
                                // Not valid JSON
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not parse solutions response");
        }
        
        return solutions;
    }

    public async Task<List<PatternInfo>> GetPatternsAsync(
        string task, string context, CancellationToken cancellationToken)
    {
        var patterns = new List<PatternInfo>();
        
        try
        {
            // Use proper JSONRPC format to call get_context
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "get_context",
                    arguments = new
                    {
                        task = task,
                        context = context,
                        includePatterns = true,
                        includeQA = false,
                        limit = 5
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                
                // Parse JSONRPC response
                var jsonDoc = JsonDocument.Parse(content);
                
                if (jsonDoc.RootElement.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("content", out var contentArray))
                {
                    foreach (var item in contentArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var textElement))
                        {
                            var text = textElement.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                // Try to parse patterns from the response text
                                patterns.AddRange(ExtractPatternsFromResponse(text));
                            }
                        }
                    }
                }
                
                _logger.LogInformation("🎯 Got {Count} patterns from Lightning for task", patterns.Count);
            }
            
            // If no patterns from get_context, try manage_patterns
            if (!patterns.Any())
            {
                patterns.AddRange(await GetManagedPatternsAsync(task, context, cancellationToken));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting patterns from Lightning");
        }

        return patterns;
    }
    
    /// <summary>
    /// Extract patterns from the MCP response text
    /// </summary>
    private List<PatternInfo> ExtractPatternsFromResponse(string responseText)
    {
        var patterns = new List<PatternInfo>();
        
        try
        {
            // Try to parse as JSON
            var doc = JsonDocument.Parse(responseText);
            
            // Look for patterns array
            if (doc.RootElement.TryGetProperty("patterns", out var patternsArray))
            {
                foreach (var p in patternsArray.EnumerateArray())
                {
                    var pattern = new PatternInfo
                    {
                        Name = p.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                        Description = p.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        BestPractice = p.TryGetProperty("recommendation", out var rec) ? rec.GetString() ?? "" : "",
                        CodeExample = p.TryGetProperty("codeExample", out var code) ? code.GetString() ?? "" : ""
                    };
                    
                    if (!string.IsNullOrEmpty(pattern.Name))
                        patterns.Add(pattern);
                }
            }
        }
        catch
        {
            // Not JSON or doesn't have patterns structure
        }
        
        return patterns;
    }
    
    /// <summary>
    /// Get patterns from manage_patterns tool
    /// </summary>
    private async Task<List<PatternInfo>> GetManagedPatternsAsync(string task, string context, CancellationToken cancellationToken)
    {
        var patterns = new List<PatternInfo>();
        
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(),
                method = "tools/call",
                @params = new
                {
                    name = "manage_patterns",
                    arguments = new
                    {
                        action = "get_useful",
                        context = context,
                        limit = 5
                    }
                }
            };
            
            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(content);
                
                if (jsonDoc.RootElement.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("content", out var contentArray))
                {
                    foreach (var item in contentArray.EnumerateArray())
                    {
                        if (item.TryGetProperty("text", out var textElement))
                        {
                            var text = textElement.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                patterns.AddRange(ExtractPatternsFromResponse(text));
                            }
                        }
                    }
                }
                
                if (patterns.Any())
                {
                    _logger.LogInformation("🎯 Got {Count} useful patterns from manage_patterns", patterns.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get managed patterns");
        }
        
        return patterns;
    }

    public async Task RecordPromptFeedbackAsync(
        string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "feedback",
                arguments = new
                {
                    type = "prompt",
                    name = promptName,
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
            _logger.LogWarning(ex, "Error recording prompt feedback for {PromptName}", promptName);
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
    /// 🔍 SEARCH BEFORE WRITE: Find existing code that might already solve the task
    /// </summary>
    public async Task<ExistingCodeContext> SearchExistingCodeAsync(
        string task, string context, string? workspacePath, CancellationToken cancellationToken)
    {
        var result = new ExistingCodeContext();
        
        _logger.LogInformation("🔍 Searching existing code for task: {Task}", task);
        
        try
        {
            // 1. Use smartsearch to find relevant code
            var searchResults = await SmartSearchAsync(task, context, cancellationToken);
            
            // 2. Extract services/interfaces from search results
            foreach (var searchResult in searchResults.Where(r => r.Type == "class" || r.Type == "interface"))
            {
                var service = new ExistingService
                {
                    Name = searchResult.Name,
                    FilePath = searchResult.FilePath,
                    Description = searchResult.Description,
                    IsInterface = searchResult.Type == "interface",
                    Methods = searchResult.Methods ?? new List<string>()
                };
                result.ExistingServices.Add(service);
            }
            
            // 3. Extract relevant methods
            foreach (var searchResult in searchResults.Where(r => r.Type == "method"))
            {
                var method = new ExistingMethod
                {
                    Name = searchResult.Name,
                    ClassName = searchResult.ClassName ?? "Unknown",
                    FilePath = searchResult.FilePath,
                    FullSignature = searchResult.Signature ?? searchResult.Name,
                    Description = searchResult.Description,
                    Relevance = searchResult.Score
                };
                result.ExistingMethods.Add(method);
            }
            
            // 4. Find similar implementations via Q&A
            var similarSolutions = await FindSimilarSolutionsAsync(task, context, cancellationToken);
            foreach (var solution in similarSolutions.Take(3))
            {
                result.SimilarImplementations.Add(new SimilarImplementation
                {
                    FilePath = solution.RelevantFiles.FirstOrDefault() ?? "Unknown",
                    Description = solution.Question,
                    Similarity = solution.Similarity,
                    CodeSnippet = solution.Answer.Length > 500 ? solution.Answer[..500] + "..." : solution.Answer
                });
            }
            
            // 5. Find implemented patterns
            var patterns = await GetPatternsAsync(task, context, cancellationToken);
            result.ImplementedPatterns = patterns
                .Where(p => !string.IsNullOrEmpty(p.CodeExample))
                .Select(p => $"{p.Name}: {p.Description}")
                .ToList();
            
            // 6. Identify files that should be modified (not created new)
            result.FilesToModify = searchResults
                .Where(r => r.Score > 0.7) // High relevance
                .Select(r => r.FilePath)
                .Distinct()
                .Take(5)
                .ToList();
            
            _logger.LogInformation(
                "🔍 Search complete: {Services} services, {Methods} methods, {Similar} similar implementations found",
                result.ExistingServices.Count,
                result.ExistingMethods.Count,
                result.SimilarImplementations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error searching existing code, continuing without context");
        }
        
        return result;
    }
    
    /// <summary>
    /// Call Lightning's smartsearch to find relevant code
    /// </summary>
    private async Task<List<CodeSearchResult>> SmartSearchAsync(
        string query, string context, CancellationToken cancellationToken)
    {
        var results = new List<CodeSearchResult>();
        
        try
        {
            var request = new
            {
                name = "smartsearch",
                arguments = new
                {
                    query = query,
                    context = context,
                    includeRelationships = true,
                    limit = 20,
                    minimumScore = 0.3
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("SmartSearch response: {Content}", content);
                
                // Parse the response - it returns markdown formatted results
                // Extract structured data from the response
                results = ParseSmartSearchResults(content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calling smartsearch");
        }
        
        return results;
    }
    
    /// <summary>
    /// Parse smartsearch results from Lightning's markdown response
    /// </summary>
    private List<CodeSearchResult> ParseSmartSearchResults(string content)
    {
        var results = new List<CodeSearchResult>();
        
        try
        {
            // Try to parse as JSON first (if MCP returns structured data)
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            
            // Check if it's a tool result with content
            if (root.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contentArray.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textElement))
                    {
                        var text = textElement.GetString() ?? "";
                        // Parse the text content for code references
                        results.AddRange(ExtractCodeReferencesFromText(text));
                    }
                }
            }
            // Try direct result parsing
            else if (root.TryGetProperty("result", out var resultElement))
            {
                var text = resultElement.GetString() ?? "";
                results.AddRange(ExtractCodeReferencesFromText(text));
            }
        }
        catch (JsonException)
        {
            // Not JSON, try to parse as plain text/markdown
            results.AddRange(ExtractCodeReferencesFromText(content));
        }
        
        return results;
    }
    
    /// <summary>
    /// Extract code references from text (file paths, class names, method names)
    /// </summary>
    private static List<CodeSearchResult> ExtractCodeReferencesFromText(string text)
    {
        var results = new List<CodeSearchResult>();
        var lines = text.Split('\n');
        
        foreach (var line in lines)
        {
            // Look for file paths (e.g., Services/UserService.cs)
            var filePathMatch = System.Text.RegularExpressions.Regex.Match(
                line, @"[\w/\\]+\.(?:cs|ts|py|js|java|go)\b");
            
            if (filePathMatch.Success)
            {
                var filePath = filePathMatch.Value;
                var name = System.IO.Path.GetFileNameWithoutExtension(filePath);
                
                // Determine type from name
                var type = name.StartsWith("I") && char.IsUpper(name[1]) ? "interface" :
                           name.EndsWith("Service") || name.EndsWith("Repository") ? "class" :
                           "file";
                
                results.Add(new CodeSearchResult
                {
                    Name = name,
                    FilePath = filePath,
                    Type = type,
                    Score = 0.5,
                    Description = line.Trim()
                });
            }
            
            // Look for method signatures
            var methodMatch = System.Text.RegularExpressions.Regex.Match(
                line, @"(?:public|private|protected|internal)\s+(?:async\s+)?[\w<>]+\s+(\w+)\s*\(");
            
            if (methodMatch.Success)
            {
                results.Add(new CodeSearchResult
                {
                    Name = methodMatch.Groups[1].Value,
                    FilePath = "Unknown",
                    Type = "method",
                    Signature = line.Trim(),
                    Score = 0.6
                });
            }
        }
        
        return results.DistinctBy(r => r.Name + r.FilePath).ToList();
    }

    /// <summary>
    /// 🧠 MODEL LEARNING: Record model performance for future selection
    /// </summary>
    public async Task RecordModelPerformanceAsync(ModelPerformanceRecord record, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "store_model_performance",
                arguments = new
                {
                    model = record.Model,
                    taskType = record.TaskType,
                    language = record.Language,
                    complexity = record.Complexity,
                    outcome = record.Outcome,
                    score = record.Score,
                    durationMs = record.DurationMs,
                    iterations = record.Iterations,
                    taskKeywords = record.TaskKeywords,
                    errorType = record.ErrorType,
                    context = record.Context
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "📊 Recorded model performance: {Model} on {TaskType}/{Language} = {Outcome} ({Score}/10)",
                    record.Model, record.TaskType, record.Language, record.Outcome, record.Score);
            }
            else
            {
                _logger.LogWarning("Failed to record model performance: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error recording model performance for {Model}", record.Model);
        }
    }
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Query the best model for a task based on historical performance
    /// </summary>
    public async Task<BestModelResponse> QueryBestModelAsync(BestModelRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var mcpRequest = new
            {
                name = "query_best_model",
                arguments = new
                {
                    taskType = request.TaskType,
                    language = request.Language,
                    complexity = request.Complexity,
                    taskKeywords = request.TaskKeywords,
                    context = request.Context,
                    excludeModels = request.ExcludeModels,
                    maxVramGb = request.MaxVramGb
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", mcpRequest, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("QueryBestModel response: {Content}", content);
                
                // Try to parse the response
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;
                    
                    // Check for result in MCP response format
                    if (root.TryGetProperty("result", out var resultElement))
                    {
                        var result = JsonSerializer.Deserialize<BestModelResponse>(resultElement.GetRawText(), JsonOptions);
                        if (result != null) return result;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug(ex, "Could not parse QueryBestModel JSON response, using default");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying best model from Lightning");
        }
        
        // Return empty response (no historical data)
        return new BestModelResponse
        {
            RecommendedModel = "",
            Reasoning = "No historical data available",
            IsHistorical = false
        };
    }
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Get aggregated stats for all models
    /// </summary>
    public async Task<List<ModelStats>> GetModelStatsAsync(string? language, string? taskType, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "get_model_stats",
                arguments = new
                {
                    language = language,
                    taskType = taskType
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("result", out var resultElement))
                    {
                        var stats = JsonSerializer.Deserialize<List<ModelStats>>(resultElement.GetRawText(), JsonOptions);
                        if (stats != null) return stats;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogDebug(ex, "Could not parse model stats JSON response");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting model stats from Lightning");
        }
        
        return new List<ModelStats>();
    }

    // NO FALLBACK PROMPTS - All prompts MUST come from Lightning
    // If a prompt is missing, the system will throw an error
    // Run PromptSeedService to seed required prompts into Neo4j
}

/// <summary>
/// Internal class for parsing smartsearch results
/// </summary>
internal class CodeSearchResult
{
    public required string Name { get; set; }
    public required string FilePath { get; set; }
    public required string Type { get; set; } // class, interface, method, file
    public string? Description { get; set; }
    public string? Signature { get; set; }
    public string? ClassName { get; set; }
    public List<string>? Methods { get; set; }
    public double Score { get; set; }
}



