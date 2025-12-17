namespace MemoryAgent.PatternManagement.Services;

/// <summary>
/// Service for generating embeddings via Ollama
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding vector for text
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    
    /// <summary>
    /// Generate embeddings for multiple texts
    /// </summary>
    Task<List<float[]>> EmbedBatchAsync(List<string> texts, CancellationToken ct = default);
}







