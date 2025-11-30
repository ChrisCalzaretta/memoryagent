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
    
    // mxbai-embed-large has a 512 token context window
    // Real-world testing shows: 1800 chars ≈ 548 tokens (too high!)
    // Using safer estimate: 1 token ≈ 3 chars, so 512 tokens ≈ 1536 chars
    // We'll use 1400 chars to stay well under 512 token limit
    private const int MaxCharacters = 1400;

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
            // Truncate text if it exceeds model's context window
            var originalLength = text.Length;
            var processedText = TruncateText(text, MaxCharacters);
            
            if (processedText.Length < originalLength)
            {
                _logger.LogWarning(
                    "Text truncated from {Original} to {Truncated} characters (model limit: ~512 tokens)",
                    originalLength, processedText.Length);
            }
            
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var request = new
                {
                    model = _model,
                    prompt = processedText
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
    
    /// <summary>
    /// Intelligently truncate text to fit within token limits while preserving important content
    /// </summary>
    private string TruncateText(string text, int maxChars)
    {
        if (text.Length <= maxChars)
            return text;
            
        // Strategy: Take the beginning (method signature, class declaration) 
        // and end (important logic) to preserve context
        var headSize = (int)(maxChars * 0.6); // 60% from start
        var tailSize = maxChars - headSize - 3; // 40% from end, -3 for "..."
        
        var head = text.Substring(0, headSize);
        var tail = text.Substring(text.Length - tailSize);
        
        return $"{head}...{tail}";
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var embeddings = new List<float[]>();
        
        // Process in batches of 32 for optimal GPU utilization
        const int batchSize = 32;
        
        for (int i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();
            
            // Process each item individually to prevent one failure from killing the whole batch
            foreach (var text in batch)
            {
                try
                {
                    var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
                    embeddings.Add(embedding);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Failed to generate embedding after {Retries} retries (text length: {Length}). Using zero vector to allow batch to continue.",
                        3, text.Length);
                    
                    // Use zero vector as placeholder - allows indexing to continue
                    // VectorService will skip storing items with zero vectors
                    embeddings.Add(new float[1024]); // mxbai-embed-large dimension
                }
            }
            
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

