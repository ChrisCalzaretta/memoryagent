using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Polly.Retry;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for generating embeddings using Ollama with GPU acceleration
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _model;
    private readonly AsyncRetryPolicy _retryPolicy;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _logger = logger;
        _model = configuration["Ollama:Model"] ?? "mxbai-embed-large:latest";

        // Retry policy for transient failures
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} after {Delay}s due to: {Exception}",
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                });
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new
                {
                    model = _model,
                    prompt = text
                };

                var response = await _httpClient.PostAsJsonAsync("/api/embeddings", request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken);
                
                if (result?.Embedding == null || result.Embedding.Length == 0)
                {
                    throw new InvalidOperationException("Empty embedding returned from Ollama");
                }

                return result.Embedding;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding for text (length: {Length})", text.Length);
            throw;
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var embeddings = new List<float[]>();
        
        // Process in batches of 32 for optimal GPU utilization
        const int batchSize = 32;
        
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();
            
            // Process batch in parallel (Ollama handles GPU batching internally)
            var tasks = batch.Select(text => GenerateEmbeddingAsync(text, cancellationToken));
            var batchEmbeddings = await Task.WhenAll(tasks);
            
            embeddings.AddRange(batchEmbeddings);
            
            _logger.LogInformation(
                "Generated embeddings for batch {Current}/{Total} ({Count} items)",
                Math.Min(i + batchSize, texts.Count), texts.Count, batch.Count);
        }

        return embeddings;
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if Ollama is running
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // Check if our model is available
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var models = JsonSerializer.Deserialize<OllamaModelsResponse>(content);
            
            var hasModel = models?.Models?.Any(m => m.Name.Contains("mxbai-embed-large")) ?? false;
            
            if (!hasModel)
            {
                _logger.LogWarning("Model {Model} not found. Available models: {Models}", 
                    _model, string.Join(", ", models?.Models?.Select(m => m.Name) ?? Array.Empty<string>()));
            }

            return hasModel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for Ollama");
            return false;
        }
    }

    private class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    private class OllamaModelsResponse
    {
        public List<OllamaModel>? Models { get; set; }
    }

    private class OllamaModel
    {
        public string Name { get; set; } = string.Empty;
    }
}

