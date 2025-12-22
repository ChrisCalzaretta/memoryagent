using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContracts.Services;

namespace ValidationAgent.Server.Services;

/// <summary>
/// HTTP client for Ollama API - supports multiple ports for multi-GPU setup
/// Auto-detects and uses optimal context size for each model
/// </summary>
public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;
    private readonly int _defaultPort;
    private readonly string _baseHost;
    
    // Cache model context sizes to avoid repeated API calls
    private static readonly Dictionary<string, int> _modelContextCache = new();
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);
    
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
        ["gemma2"] = 32768,              // Bumped from 8k to 32k
        ["gemma3"] = 131072,             // 131k works (already running this)
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
    
    /// <summary>
    /// Get optimal context size for a model based on task type
    /// Validation needs more prompt room (80% prompt, 20% response) since it includes full code
    /// </summary>
    private async Task<int> GetOptimalContextSizeAsync(string model, int port, CancellationToken ct, string taskType = "validation")
    {
        var cacheKey = $"{model}:{taskType}";
        
        // Check cache first
        if (_modelContextCache.TryGetValue(cacheKey, out var cached))
            return cached;
        
        await _cacheLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_modelContextCache.TryGetValue(cacheKey, out cached))
                return cached;
            
            // Try to query Ollama for model info
            var maxContextSize = await QueryModelContextSizeAsync(model, port, ct);
            
            // If query failed, use known defaults
            if (maxContextSize <= 0)
            {
                maxContextSize = GetKnownContextSize(model);
            }
            
            // Task-aware context ratio:
            // - validation: 80% (needs full code in prompt, small JSON response)
            // - model_selection: 90% (tiny JSON response)
            var promptRatio = taskType switch
            {
                "validation" => 0.80,
                "model_selection" => 0.90,
                _ => 0.80
            };
            
            var optimalSize = (int)(maxContextSize * promptRatio);
            
            // Ensure minimum of 4096
            optimalSize = Math.Max(optimalSize, 4096);
            
            _modelContextCache[cacheKey] = optimalSize;
            _logger.LogInformation("📐 Model {Model} ({TaskType}): {Max} max → using {Optimal} ({Ratio:P0} for prompt, {ResponseRatio:P0} for response)", 
                model, taskType, maxContextSize, optimalSize, promptRatio, 1 - promptRatio);
            
            return optimalSize;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    
    /// <summary>
    /// Query Ollama API for model's context size
    /// </summary>
    private async Task<int> QueryModelContextSizeAsync(string model, int port, CancellationToken ct)
    {
        try
        {
            var url = $"{_baseHost}:{port}/api/show";
            var request = new { name = model };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, ct);
            if (!response.IsSuccessStatusCode)
                return 0;
            
            var body = await response.Content.ReadAsStringAsync(ct);
            
            // Parse the modelfile to find num_ctx parameter
            // Look for patterns like "num_ctx 4096" or context_length in parameters
            if (body.Contains("num_ctx"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(body, @"num_ctx["":\s]+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var ctx))
                    return ctx;
            }
            
            // Try to find context_length in model_info
            if (body.Contains("context_length"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(body, @"context_length["":\s]+(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var ctx))
                    return ctx;
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query context size for {Model}", model);
            return 0;
        }
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
        var actualPort = port ?? _defaultPort;
        var url = $"{_baseHost}:{actualPort}/api/generate";
        
        // 📐 Smart context sizing: 80% for prompt (validation needs full code), 20% for response (small JSON)
        var optimalContext = await GetOptimalContextSizeAsync(model, actualPort, cancellationToken, "validation");
        
        _logger.LogInformation("Calling Ollama generate on port {Port} with model {Model} (context={Context})", 
            actualPort, model, optimalContext);
        
        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = prompt,
            System = systemPrompt,
            Stream = false,
            KeepAlive = -1, // Keep model loaded
            Options = new OllamaOptions
            {
                NumCtx = optimalContext // Auto-detected: 80% for validation prompts
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

    public async Task<OllamaResponse> GenerateWithVisionAsync(
        string model,
        string prompt,
        List<string> images,
        string? systemPrompt = null,
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        var actualPort = port ?? _defaultPort;
        var url = $"{_baseHost}:{actualPort}/api/generate";
        
        var optimalContext = await GetOptimalContextSizeAsync(model, actualPort, cancellationToken, "validation");
        
        _logger.LogInformation("Calling Ollama vision generate on port {Port} with model {Model} ({ImageCount} images)", 
            actualPort, model, images.Count);
        
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
                NumCtx = optimalContext
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
                _logger.LogError("Ollama vision error: {StatusCode} - {Body}", response.StatusCode, responseBody);
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
            _logger.LogError(ex, "Error calling Ollama vision on port {Port}", actualPort);
            return new OllamaResponse 
            { 
                Response = "", 
                Success = false, 
                Error = ex.Message 
            };
        }
    }
}

#region Ollama API DTOs

internal class OllamaGenerateRequest
{
    public required string Model { get; set; }
    public required string Prompt { get; set; }
    public string? System { get; set; }
    public List<string>? Images { get; set; }
    public bool Stream { get; set; }
    [JsonPropertyName("keep_alive")]
    public int KeepAlive { get; set; }
    /// <summary>
    /// Model options including context size
    /// </summary>
    public OllamaOptions? Options { get; set; }
}

/// <summary>
/// Ollama model options - controls context size, temperature, etc.
/// </summary>
internal class OllamaOptions
{
    /// <summary>
    /// Context window size in tokens. Default is 4096. 
    /// Set higher (8192, 16384, 32768) for longer prompts.
    /// Trade-off: More memory, slower responses.
    /// </summary>
    [JsonPropertyName("num_ctx")]
    public int NumCtx { get; set; } = 8192;
    
    /// <summary>
    /// Temperature for sampling (0.0 = deterministic, 1.0 = creative)
    /// </summary>
    public float? Temperature { get; set; }
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
