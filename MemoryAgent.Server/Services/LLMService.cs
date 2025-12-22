using System.Net.Http.Json;
using System.Text.Json;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for LLM text generation using Ollama (DeepSeek Coder)
/// </summary>
public class LLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LLMService> _logger;
    private readonly string _model;
    
    // Default context sizes by model family
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
    
    public LLMService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LLMService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _logger = logger;
        _model = configuration["Ollama:LLMModel"] ?? "deepseek-coder-v2:16b";
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
    
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get max context for this model
            var maxContext = GetKnownContextSize(_model);
            
            // Cap ALL models at 128k max (VRAM safety)
            if (maxContext > 131072)
            {
                _logger.LogWarning("⚠️ Capping {Model} context from {Original} to 131072 (VRAM safety)", _model, maxContext);
                maxContext = 131072;
            }
            
            _logger.LogInformation("Generating text with {Model} (prompt length: {Length} chars, context: {Context})", 
                _model, prompt.Length, maxContext);
            
            var request = new
            {
                model = _model,
                prompt = prompt,
                stream = false,
                options = new
                {
                    num_ctx = maxContext,   // Context window size
                    temperature = 0.1,      // Low temperature for consistent code generation
                    top_p = 0.9,
                    num_predict = 4096      // Max tokens to generate
                }
            };
            
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            
            if (result?.Response == null)
            {
                throw new InvalidOperationException("Empty response from Ollama");
            }
            
            _logger.LogInformation("Generated {Length} chars in {Duration}ms", 
                result.Response.Length, result.TotalDuration / 1_000_000);
            
            return result.Response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text with LLM");
            throw;
        }
    }
}

/// <summary>
/// Response from Ollama generate API
/// </summary>
public class OllamaGenerateResponse
{
    public string Model { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Response { get; set; } = string.Empty;
    public bool Done { get; set; }
    public long TotalDuration { get; set; }
    public long LoadDuration { get; set; }
    public int PromptEvalCount { get; set; }
    public int EvalCount { get; set; }
}

