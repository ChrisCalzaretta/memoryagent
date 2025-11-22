using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for intelligent search combining graph and semantic approaches
/// </summary>
public interface ISmartSearchService
{
    /// <summary>
    /// Execute smart search with auto-detection of strategy
    /// </summary>
    Task<SmartSearchResponse> SearchAsync(SmartSearchRequest request, CancellationToken cancellationToken = default);
}

