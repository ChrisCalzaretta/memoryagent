using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for reindexing code and detecting changes
/// </summary>
public interface IReindexService
{
    /// <summary>
    /// Reindex a context, detecting changes and removing stale entries
    /// </summary>
    Task<ReindexResult> ReindexAsync(string? context = null, string? path = null, bool removeStale = true, CancellationToken cancellationToken = default);
}


