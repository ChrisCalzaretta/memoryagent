using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for orchestrating code indexing across all storage systems
/// </summary>
public interface IIndexingService
{
    /// <summary>
    /// Index a single file
    /// </summary>
    Task<IndexResult> IndexFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Index a directory recursively
    /// </summary>
    Task<IndexResult> IndexDirectoryAsync(string directoryPath, bool recursive = true, string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Query code memory
    /// </summary>
    Task<QueryResult> QueryAsync(string query, string? context = null, int limit = 5, float minimumScore = 0.7f, CancellationToken cancellationToken = default);
}

