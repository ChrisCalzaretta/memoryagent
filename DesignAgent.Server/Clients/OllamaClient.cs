using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContracts.Services;

namespace DesignAgent.Server.Clients;

/// <summary>
/// HTTP client for Ollama API - for Design Agent LLM capabilities
/// </summary>
public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;
    private readonly int _defaultPort;
    private readonly string _baseHost;
    
    // Default context sizes by model family (fallback if API query fails)
    private static readonly Dictionary<string, int> _knownModelContexts = new()
    {
        ["deepseek-coder-v2"] = 32768,  // 32k (64k crashes - tested)
        ["deepseek-coder"] = 16384,
        ["qwen2.5-coder"] = 131072,     // 128k tested and works!
        ["qwen2.5"] = 131072,
        ["phi4"] = 131072,               // 128k tested and works!
        ["phi3.5"] = 32768,
        ["phi3"] = 4096,
        ["llama3.1"] = 32768,
        ["llama3.2"] = 32768,
        ["codellama"] = 16384,
        ["mistral"] = 32768,
        ["mixtral"] = 32768,
        ["gemma2"] = 32768,
        ["gemma3"] = 131072,             // 131k works
        ["llava"] = 32768,               // Vision model - 32k context
        ["starcoder2"] = 16384,
    };
    
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
        
        _logger.LogInformation("DesignAgent OllamaClient configured: {BaseHost}:{Port}", _baseHost, _defaultPort);
    }
    
    /// <summary>
    /// Get known context size for model family
    /// </summary>
    private int GetKnownContextSize(string model)
    {
        var lowerModel = model.ToLowerInvariant();
        
        foreach (var (prefix, contextSize) in _knownModelContexts)
        {
            if (lowerModel.Contains(prefix))
                return contextSize;
        }
        
        // Default fallback
        return 8192;
    }

    public async Task<OllamaResponse> GenerateAsync(
        string model, 
        string prompt, 
        string? systemPrompt = null,
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        return await GenerateInternalAsync(model, prompt, systemPrompt, null, port, cancellationToken);
    }

    public async Task<OllamaResponse> GenerateWithVisionAsync(
        string model, 
        string prompt, 
        List<string> images,
        string? systemPrompt = null,
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        return await GenerateInternalAsync(model, prompt, systemPrompt, images, port, cancellationToken);
    }

    private async Task<OllamaResponse> GenerateInternalAsync(
        string model, 
        string prompt, 
        string? systemPrompt,
        List<string>? images,
        int? port,
        CancellationToken cancellationToken)
    {
        var actualPort = port ?? _defaultPort;
        var url = $"{_baseHost}:{actualPort}/api/generate";
        
        // Get max context for this model
        var maxContext = GetKnownContextSize(model);
        
        // Cap ALL models at 128k max (VRAM safety)
        if (maxContext > 131072)
        {
            _logger.LogWarning("⚠️ Capping {Model} context from {Original} to 131072 (VRAM safety)", model, maxContext);
            maxContext = 131072;
        }
        
        if (images?.Any() == true)
        {
            _logger.LogInformation("🎨 DesignAgent calling {Model} with {ImageCount} image(s) (context={Context})", model, images.Count, maxContext);
        }
        else
        {
            _logger.LogInformation("🎨 DesignAgent calling {Model} for design generation (context={Context})", model, maxContext);
        }
        
        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            System = systemPrompt,
            Images = images,
            Stream = false,
            KeepAlive = -1,
            Options = new OllamaOptions
            {
                NumCtx = maxContext
            }
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
                TotalDurationMs = (int)((ollamaResponse?.TotalDuration ?? 0) / 1_000_000),
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
            _logger.LogError(ex, "Error calling Ollama at {Url}", url);
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
        
        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages.Select(m => new OllamaChatMessage 
            { 
                Role = m.Role, 
                Content = m.Content 
            }).ToList(),
            Stream = false
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
                    Error = $"HTTP {response.StatusCode}: {responseBody}" 
                };
            }

            var chatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody, JsonOptions);
            
            return new OllamaResponse
            {
                Response = chatResponse?.Message?.Content ?? "",
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat at {Url}", url);
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
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return content.Contains(model, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetModelsAsync(int? port = null, CancellationToken cancellationToken = default)
    {
        var actualPort = port ?? _defaultPort;
        var url = $"{_baseHost}:{actualPort}/api/tags";
        
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<string>();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<OllamaTagsResponse>(content, JsonOptions);
            
            return result?.Models?.Select(m => m.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() 
                ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get models from {Url}", url);
            return new List<string>();
        }
    }
}

#region Ollama DTOs

internal class OllamaGenerateRequest
{
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string? System { get; set; }
    public List<string>? Images { get; set; }
    public bool Stream { get; set; }
    
    [JsonPropertyName("keep_alive")]
    public int KeepAlive { get; set; }
    
    public OllamaOptions? Options { get; set; }
}

internal class OllamaOptions
{
    /// <summary>
    /// Context window size in tokens. Default is 4096. 
    /// Set higher (8192, 16384, 32768, 131072) for longer prompts.
    /// Trade-off: More memory, slower responses.
    /// </summary>
    [JsonPropertyName("num_ctx")]
    public int NumCtx { get; set; } = 8192;
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
    public string Model { get; set; } = "";
    public List<OllamaChatMessage> Messages { get; set; } = new();
    public bool Stream { get; set; }
}

internal class OllamaChatMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal class OllamaChatResponse
{
    public OllamaChatMessage? Message { get; set; }
}

internal class OllamaTagsResponse
{
    public List<OllamaModelInfo>? Models { get; set; }
}

internal class OllamaModelInfo
{
    public string? Name { get; set; }
    public long Size { get; set; }
}

internal class OllamaPsResponse
{
    public List<OllamaPsModel>? Models { get; set; }
}

internal class OllamaPsModel
{
    public string? Name { get; set; }
    public long Size { get; set; }
}

#endregion

