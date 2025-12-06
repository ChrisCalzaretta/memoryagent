using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContracts.Services;

namespace ValidationAgent.Server.Services;

/// <summary>
/// HTTP client for Ollama API - supports multiple ports for multi-GPU setup
/// </summary>
public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;
    private readonly int _defaultPort;
    private readonly string _baseHost;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaClient(HttpClient httpClient, ILogger<OllamaClient> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _defaultPort = config.GetValue<int>("Ollama:Port", 11434);
        
        // Support full URL (e.g., http://10.0.0.20:11434) or default to localhost
        var configuredUrl = config.GetValue<string>("Ollama:Url");
        if (!string.IsNullOrEmpty(configuredUrl) && Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri))
        {
            _baseHost = $"{uri.Scheme}://{uri.Host}";
            _defaultPort = uri.Port > 0 ? uri.Port : _defaultPort;
        }
        else
        {
            _baseHost = "http://localhost";
        }
        
        _logger.LogInformation("OllamaClient configured with base host: {BaseHost}:{Port}", _baseHost, _defaultPort);
    }

    public async Task<OllamaResponse> GenerateAsync(
        string model, 
        string prompt, 
        string? systemPrompt = null,
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        var actualPort = port ?? _defaultPort;
        var url = $"{_baseHost}:{actualPort}/api/generate";
        
        _logger.LogInformation("Calling Ollama generate on port {Port} with model {Model}", actualPort, model);
        
        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            System = systemPrompt,
            Stream = false,
            KeepAlive = -1 // Keep model loaded
        };

        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Ollama error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return new OllamaResponse 
                { 
                    Response = "", 
                    Success = false, 
                    Error = $"HTTP {response.StatusCode}: {responseBody}" 
                };
            }

            var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseBody, JsonOptions);
            
            return new OllamaResponse
            {
                Response = ollamaResponse?.Response ?? "",
                Success = true,
                TotalDurationMs = (int)((ollamaResponse?.TotalDuration ?? 0) / 1_000_000), // ns to ms
                PromptTokens = ollamaResponse?.PromptEvalCount ?? 0,
                ResponseTokens = ollamaResponse?.EvalCount ?? 0
            };
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Ollama on port {Port}", actualPort);
            return new OllamaResponse 
            { 
                Response = "", 
                Success = false, 
                Error = ex.Message 
            };
        }
    }

    public async Task<OllamaResponse> ChatAsync(
        string model,
        List<ChatMessage> messages,
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        var actualPort = port ?? _defaultPort;
        var url = $"{_baseHost}:{actualPort}/api/chat";
        
        _logger.LogInformation("Calling Ollama chat on port {Port} with model {Model}", actualPort, model);

        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages.Select(m => new OllamaChatMessage 
            { 
                Role = m.Role, 
                Content = m.Content 
            }).ToList(),
            Stream = false,
            KeepAlive = -1
        };

        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return new OllamaResponse 
                { 
                    Response = "", 
                    Success = false, 
                    Error = $"HTTP {response.StatusCode}" 
                };
            }

            var ollamaResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody, JsonOptions);
            
            return new OllamaResponse
            {
                Response = ollamaResponse?.Message?.Content ?? "",
                Success = true,
                TotalDurationMs = (int)((ollamaResponse?.TotalDuration ?? 0) / 1_000_000),
                PromptTokens = ollamaResponse?.PromptEvalCount ?? 0,
                ResponseTokens = ollamaResponse?.EvalCount ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Ollama chat");
            return new OllamaResponse 
            { 
                Response = "", 
                Success = false, 
                Error = ex.Message 
            };
        }
    }

    public async Task<bool> IsModelLoadedAsync(string model, int port, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseHost}:{port}/api/ps";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode) return false;
            
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Contains(model, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetModelsAsync(int? port = null, CancellationToken cancellationToken = default)
    {
        var actualPort = port ?? _defaultPort;
        
        try
        {
            var url = $"{_baseHost}:{actualPort}/api/tags";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode) return new List<string>();
            
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(body, JsonOptions);
            
            return tagsResponse?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}

#region Ollama API DTOs

internal class OllamaGenerateRequest
{
    public required string Model { get; set; }
    public required string Prompt { get; set; }
    public string? System { get; set; }
    public bool Stream { get; set; }
    [JsonPropertyName("keep_alive")]
    public int KeepAlive { get; set; }
}

internal class OllamaGenerateResponse
{
    public string? Response { get; set; }
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}

internal class OllamaChatRequest
{
    public required string Model { get; set; }
    public required List<OllamaChatMessage> Messages { get; set; }
    public bool Stream { get; set; }
    [JsonPropertyName("keep_alive")]
    public int KeepAlive { get; set; }
}

internal class OllamaChatMessage
{
    public required string Role { get; set; }
    public required string Content { get; set; }
}

internal class OllamaChatResponse
{
    public OllamaChatMessage? Message { get; set; }
    [JsonPropertyName("total_duration")]
    public long TotalDuration { get; set; }
    [JsonPropertyName("prompt_eval_count")]
    public int PromptEvalCount { get; set; }
    [JsonPropertyName("eval_count")]
    public int EvalCount { get; set; }
}

internal class OllamaTagsResponse
{
    public List<OllamaModelInfo>? Models { get; set; }
}

internal class OllamaModelInfo
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
}

#endregion
