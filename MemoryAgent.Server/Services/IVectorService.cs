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
    Task DeleteByFilePathAsync(string filePath, string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all file paths indexed for a specific context
    /// </summary>
    Task<List<string>> GetFilePathsForContextAsync(string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the last indexed time for a specific file
    /// </summary>
    Task<DateTime?> GetFileLastIndexedTimeAsync(string filePath, string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check for Qdrant
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
    
    #region Agent Lightning (Learning) Operations
    
    /// <summary>
    /// Store a Q&A mapping with its question embedding in the lightning collection
    /// </summary>
    Task StoreLightningQAAsync(
        string id,
        string question,
        float[] questionEmbedding,
        string answer,
        List<string> relevantFiles,
        string context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search for similar questions using vector similarity in the lightning collection
    /// </summary>
    Task<List<LightningQAResult>> SearchSimilarQuestionsAsync(
        float[] questionEmbedding,
        string context,
        int limit = 5,
        float minimumScore = 0.7f,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Store a session snapshot in the lightning collection
    /// </summary>
    Task StoreLightningSessionAsync(
        string sessionId,
        float[] sessionEmbedding,
        string summary,
        List<string> filesDiscussed,
        List<string> filesEdited,
        string context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Store importance data point in the lightning collection
    /// </summary>
    Task StoreLightningImportanceAsync(
        string id,
        string filePath,
        float[] fileEmbedding,
        float importanceScore,
        int accessCount,
        int editCount,
        string context,
        CancellationToken cancellationToken = default);
    
    #endregion
}

/// <summary>
/// Result from searching similar questions in lightning collection
/// </summary>
public class LightningQAResult
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<string> RelevantFiles { get; set; } = new();
    public float Score { get; set; }
    public DateTime StoredAt { get; set; }
}

