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
            var response = await client.GetAsync($"/api/prompts/{promptName}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Prompt '{Name}' not found in Lightning (using default)", promptName);
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var prompt = JsonSerializer.Deserialize<PromptTemplate>(content, JsonOptions);
            
            _logger.LogDebug("Got prompt '{Name}' v{Version} from Lightning", promptName, prompt?.Version);
            return prompt;
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
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(language)) queryParams.Add($"language={language}");
            if (!string.IsNullOrEmpty(taskType)) queryParams.Add($"taskType={taskType}");
            
            var query = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await client.GetAsync($"/api/model-stats{query}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new List<ModelStats>();
            }
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<List<ModelStats>>(content, JsonOptions) ?? new List<ModelStats>();
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
            var payload = new
            {
                model,
                taskType,
                outcome = succeeded ? "success" : "failure",
                score,
                language = language ?? "design",
                complexity = complexity ?? "moderate",
                iterations,
                durationMs,
                errorType,
                context = "design-agent"
            };
            
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            await client.PostAsync("/api/model-performance", content, cancellationToken);
            _logger.LogDebug("Recorded performance for {Model}: {Outcome}", model, succeeded ? "success" : "failure");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record model performance");
        }
    }
}

