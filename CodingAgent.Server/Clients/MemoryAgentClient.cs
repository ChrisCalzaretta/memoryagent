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
                _logger.LogDebug("Got prompt from Lightning: {Content}", content);
                
                // Parse the MCP response to extract prompt content
                // For now, return a default - actual implementation would parse MCP response structure
                var prompt = new PromptInfo
                {
                    Name = promptName,
                    Content = GetDefaultPrompt(promptName),
                    Version = 1,
                    IsActive = true
                };
                
                // Cache the result
                _promptCache[promptName] = (prompt, DateTime.UtcNow);
                return prompt;
            }

            _logger.LogWarning("Failed to get prompt {PromptName} from Lightning: {Status}", 
                promptName, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting prompt {PromptName} from Lightning", promptName);
        }

        // Fallback to default
        return new PromptInfo
        {
            Name = promptName,
            Content = GetDefaultPrompt(promptName),
            Version = 0,
            IsActive = true
        };
    }

    public async Task<List<SimilarSolution>> FindSimilarSolutionsAsync(
        string task, string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                name = "find_similar_questions",
                arguments = new
                {
                    question = task,
                    context = context,
                    limit = 5
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Got similar solutions from Lightning: {Content}", content);
                
                // Parse MCP response - actual implementation would extract Q&A pairs
                // For now, return empty list
                return new List<SimilarSolution>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding similar solutions from Lightning");
        }

        return new List<SimilarSolution>();
    }

    public async Task<List<PatternInfo>> GetPatternsAsync(
        string task, string context, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
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
            };

            var response = await _httpClient.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Got patterns from Lightning: {Content}", content);
                
                // Parse MCP response - actual implementation would extract patterns
                // For now, return empty list
                return new List<PatternInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting patterns from Lightning");
        }

        return new List<PatternInfo>();
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

    private static string GetDefaultPrompt(string promptName) => promptName switch
    {
        "coding_agent_system" => @"You are an expert coding agent. Your task is to write production-quality code.

STRICT RULES:
1. ONLY create/modify files directly necessary for the requested task
2. Do NOT ""improve"" or refactor unrelated code
3. Do NOT add features that weren't requested
4. You MAY add package references if needed for your implementation
5. You MUST include proper error handling and null checks
6. You MUST include XML documentation on public methods
7. Follow C# naming conventions and best practices

REQUIREMENTS:
- Always check for null before accessing properties
- Use async/await for I/O operations
- Prefer IOptions<T> over raw configuration strings
- Include CancellationToken support for async methods
- Use dependency injection for services
- Log important operations",

        "coding_agent_fix" => @"You are an expert coding agent fixing validation issues.

FIX PRIORITY:
1. CRITICAL: Security vulnerabilities, null reference bugs
2. HIGH: Missing error handling, resource leaks
3. MEDIUM: Code style, naming conventions
4. LOW: Documentation, minor improvements

RULES:
- Fix ALL issues listed in the validation feedback
- Do NOT introduce new features while fixing
- Preserve existing functionality
- Add tests for fixed issues if appropriate",

        _ => "You are a helpful AI coding assistant."
    };
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



