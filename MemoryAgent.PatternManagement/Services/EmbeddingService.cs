using System.Text;
using System.Text.Json;

namespace MemoryAgent.PatternManagement.Services;

/// <summary>
/// Generates embeddings via Ollama API
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _model;
    private readonly string _baseUrl;
    
    public EmbeddingService(HttpClient httpClient, ILogger<EmbeddingService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = config["Ollama:EmbeddingModel"] ?? "mxbai-embed-large:latest";
        _baseUrl = config["Ollama:Url"] ?? "http://localhost:11434";
    }
    
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var request = new { model = _model, prompt = text };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/embeddings", content, ct);
            response.EnsureSuccessStatusCode();
            
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<EmbeddingResponse>(responseBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result?.Embedding ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text (length: {Length})", text.Length);
            throw;
        }
    }
    
    public async Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken ct = default)
    {
        var results = new List<float[]>();
        
        foreach (var text in texts)
        {
            var embedding = await EmbedAsync(text, ct);
            results.Add(embedding);
        }
        
        return results;
    }
    
    private class EmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}



