using AgentContracts.Services;
using System.Text.Json;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Lightweight Ollama client for code summarization
/// </summary>
public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;
    private readonly string _baseUrl;

    public OllamaClient(HttpClient httpClient, ILogger<OllamaClient> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["Ollama:Url"] ?? "http://10.0.2.20";
        _httpClient.Timeout = TimeSpan.FromSeconds(60); // Summarization can take time
    }

    public async Task<OllamaResponse> GenerateAsync(
        string model, 
        string prompt, 
        string? systemPrompt = null, 
        int? port = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}:{port ?? 11434}/api/generate";
        
        var requestBody = new
        {
            model,
            prompt,
            system = systemPrompt,
            stream = false,
            options = new
            {
                temperature = 0.1, // Low temperature for consistent summaries
                num_predict = 500  // Limit output length
            }
        };

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResult = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseText);

            return new OllamaResponse
            {
                Response = ollamaResult?.Response ?? "",
                Success = true,
                TotalDurationMs = (int)(ollamaResult?.TotalDuration ?? 0) / 1_000_000,
                PromptTokens = ollamaResult?.PromptEvalCount ?? 0,
                ResponseTokens = ollamaResult?.EvalCount ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama generate failed for model {Model}", model);
            return new OllamaResponse
            {
                Response = "",
                Success = false,
                Error = ex.Message
            };
        }
    }

    public Task<OllamaResponse> GenerateWithVisionAsync(
        string model, 
        string prompt, 
        List<string> images, 
        string? systemPrompt = null, 
        int? port = null, 
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Vision not needed for orchestrator");
    }

    public Task<OllamaResponse> ChatAsync(
        string model, 
        List<ChatMessage> messages, 
        int? port = null, 
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Chat not needed for orchestrator");
    }

    public Task<bool> IsModelLoadedAsync(string model, int port, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Not needed for orchestrator");
    }

    public Task<List<string>> GetModelsAsync(int? port = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Not needed for orchestrator");
    }

    private class OllamaGenerateResponse
    {
        public string? Response { get; set; }
        public long? TotalDuration { get; set; }
        public int? PromptEvalCount { get; set; }
        public int? EvalCount { get; set; }
    }
}
