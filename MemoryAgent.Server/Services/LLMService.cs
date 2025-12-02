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
    
    public LLMService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LLMService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _logger = logger;
        _model = configuration["Ollama:LLMModel"] ?? "deepseek-coder-v2:16b";
    }
    
    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating text with {Model} (prompt length: {Length} chars)", 
                _model, prompt.Length);
            
            var request = new
            {
                model = _model,
                prompt = prompt,
                stream = false,
                options = new
                {
                    temperature = 0.1,  // Low temperature for consistent code generation
                    top_p = 0.9,
                    num_predict = 4096  // Max tokens to generate
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

