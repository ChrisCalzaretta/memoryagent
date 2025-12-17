using DesignAgent.Server.Models.DesignIntelligence;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for discovering new design sources
/// </summary>
public interface IDesignDiscoveryService
{
    /// <summary>
    /// Generate search queries using LLM
    /// </summary>
    /// <param name="count">Number of queries to generate</param>
    /// <param name="category">Optional category focus (e.g., "saas", "ecommerce")</param>
    Task<List<string>> GenerateSearchQueriesAsync(int count = 5, string? category = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute search query and return URLs
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Max results to return</param>
    Task<List<string>> SearchDesignSourcesAsync(string query, int limit = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Evaluate a search result using LLM (is this a design-worthy site?)
    /// </summary>
    /// <param name="url">URL to evaluate</param>
    /// <param name="searchQuery">Original search query for context</param>
    /// <returns>DesignSource if worthy, null if not</returns>
    Task<DesignSource?> EvaluateSearchResultAsync(string url, string searchQuery, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Run full discovery cycle (generate queries, search, evaluate, store)
    /// </summary>
    /// <param name="targetCount">Target number of new sources to discover</param>
    /// <returns>Number of sources discovered</returns>
    Task<int> RunDiscoveryCycleAsync(int targetCount = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Seed curated sources from initial list
    /// </summary>
    Task<int> SeedCuratedSourcesAsync(List<DesignSource> sources, CancellationToken cancellationToken = default);
}

