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

