namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for generating embeddings using Ollama
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding for a single text
    /// </summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in batch
    /// </summary>
    Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if Ollama is healthy and model is available
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

