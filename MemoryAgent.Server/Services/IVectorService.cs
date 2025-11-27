using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for interacting with Qdrant vector database
/// </summary>
public interface IVectorService
{
    /// <summary>
    /// Initialize collections in Qdrant
    /// </summary>
    Task InitializeCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize Qdrant collections for a specific workspace context
    /// </summary>
    Task InitializeCollectionsForContextAsync(string context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a code memory with its embedding
    /// </summary>
    Task StoreCodeMemoryAsync(CodeMemory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store multiple code memories in batch
    /// </summary>
    Task StoreCodeMemoriesAsync(List<CodeMemory> memories, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar code using vector similarity
    /// </summary>
    Task<List<CodeExample>> SearchSimilarCodeAsync(
        float[] queryEmbedding,
        CodeMemoryType? type = null,
        string? context = null,
        int limit = 5,
        float minimumScore = 0.7f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete code memories by file path
    /// </summary>
    Task DeleteByFilePathAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all file paths indexed for a specific context
    /// </summary>
    Task<List<string>> GetFilePathsForContextAsync(string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the last indexed time for a specific file
    /// </summary>
    Task<DateTime?> GetFileLastIndexedTimeAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check for Qdrant
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

