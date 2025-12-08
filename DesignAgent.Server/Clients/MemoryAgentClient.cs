using System.Text.Json;
using AgentContracts.Models;

namespace DesignAgent.Server.Clients;

/// <summary>
/// Client for MemoryAgent - used for Lightning prompt management and model performance tracking
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Get a prompt from Lightning
    /// </summary>
    Task<PromptTemplate?> GetPromptAsync(string promptName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get model performance stats for smart model selection
    /// </summary>
    Task<List<ModelStats>> GetModelStatsAsync(string? language, string? taskType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record model performance for learning
    /// </summary>
    Task RecordModelPerformanceAsync(
        string model, string taskType, bool succeeded, double score,
        string? language = null, string? complexity = null, int iterations = 1,
        long durationMs = 0, string? errorType = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simple prompt template DTO
/// </summary>
public class PromptTemplate
{
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public int Version { get; set; }
}

public class MemoryAgentClient : IMemoryAgentClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MemoryAgentClient> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MemoryAgentClient(IHttpClientFactory httpClientFactory, ILogger<MemoryAgentClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PromptTemplate?> GetPromptAsync(string promptName, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MemoryAgent");
            
            // Use MCP call to get prompt from Lightning
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
            
            var response = await client.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Got prompt from Lightning: {Prompt}", promptName);
                
                // Parse MCP response to extract prompt
                var mcpResponse = JsonSerializer.Deserialize<McpResponse>(content, JsonOptions);
                if (mcpResponse?.Content?.FirstOrDefault()?.Text != null)
                {
                    // Extract prompt content from MCP response
                    // The MCP response contains the prompt data
                    return new PromptTemplate
                    {
                        Name = promptName,
                        Content = mcpResponse.Content.First().Text ?? "",
                        Version = 1
                    };
                }
            }
            
            _logger.LogDebug("Prompt '{Name}' not found in Lightning (using default)", promptName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get prompt from Lightning");
            return null;
        }
    }

    public async Task<List<ModelStats>> GetModelStatsAsync(string? language, string? taskType, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MemoryAgent");
            
            // Use MCP call to query best model
            var request = new
            {
                name = "query_best_model",
                arguments = new
                {
                    taskDescription = $"{taskType ?? "design"} task",
                    language = language ?? "blazor",
                    complexity = "moderate",
                    taskType = taskType ?? "design",
                    context = "design-agent"
                }
            };
            
            var response = await client.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("MCP query_best_model returned {Status}", response.StatusCode);
                return new List<ModelStats>();
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<McpResponse>(content, JsonOptions);
            
            // Convert MCP response to model stats
            if (result?.Content != null)
            {
                var stats = new List<ModelStats>();
                foreach (var item in result.Content)
                {
                    if (item.Text != null && item.Text.Contains("success_rate"))
                    {
                        try
                        {
                            var modelData = JsonSerializer.Deserialize<ModelStats>(item.Text, JsonOptions);
                            if (modelData != null) stats.Add(modelData);
                        }
                        catch { /* ignore parse errors */ }
                    }
                }
                return stats;
            }
            
            return new List<ModelStats>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get model stats");
            return new List<ModelStats>();
        }
    }

    public async Task RecordModelPerformanceAsync(
        string model, string taskType, bool succeeded, double score,
        string? language = null, string? complexity = null, int iterations = 1,
        long durationMs = 0, string? errorType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MemoryAgent");
            
            // Use MCP call to store model performance
            var request = new
            {
                name = "store_model_performance",
                arguments = new
                {
                    model,
                    taskType,
                    language = language ?? "blazor",
                    complexity = complexity ?? "moderate",
                    outcome = succeeded ? "success" : "failure",
                    score,
                    durationMs,
                    iterations,
                    taskKeywords = new[] { "design", taskType },
                    errorType,
                    context = "design-agent"
                }
            };
            
            var response = await client.PostAsJsonAsync("/api/mcp/call", request, JsonOptions, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("📊 Recorded performance for {Model}: {Outcome}", model, succeeded ? "success" : "failure");
            }
            else
            {
                _logger.LogDebug("Failed to record performance: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record model performance");
        }
    }
}

/// <summary>
/// MCP response wrapper
/// </summary>
public class McpResponse
{
    public List<McpContent>? Content { get; set; }
    public bool IsError { get; set; }
}

public class McpContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

