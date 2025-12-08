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
                // Parse the response to extract prompt content
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                // For now, return a default - actual implementation would parse MCP response
                return new PromptInfo
                {
                    Name = promptName,
                    Content = GetDefaultPrompt(promptName),
                    Version = 1,
                    IsActive = true
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting prompt from MemoryAgent");
            return null;
        }
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
- Include CancellationToken support for async methods",

        "validation_agent_system" => @"You are an expert code reviewer. Your task is to review code for quality, security, and best practices.

VALIDATION RULES:
1. Check for null reference vulnerabilities
2. Check for proper error handling
3. Check for security issues (SQL injection, hardcoded secrets, etc.)
4. Check for proper async patterns
5. Check for proper resource disposal
6. Check for code maintainability
7. Check for proper naming conventions

SCORING:
- 10: Perfect, no issues
- 8-9: Good, minor suggestions only
- 6-7: Acceptable, needs some fixes
- 4-5: Poor, significant issues
- 0-3: Critical, major problems

Be strict but fair. Focus on real issues, not style preferences.",

        _ => "You are a helpful AI assistant."
    };
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

