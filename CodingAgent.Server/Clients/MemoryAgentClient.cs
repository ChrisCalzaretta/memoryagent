using System.Net.Http.Json;
using System.Text.Json;

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



