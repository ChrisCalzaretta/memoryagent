using DesignAgent.Server.Models.DesignIntelligence;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Storage interface for Design Intelligence System (Neo4j + Qdrant)
/// </summary>
public interface IDesignIntelligenceStorage
{
    // ===== DESIGN SOURCES =====
    
    /// <summary>
    /// Store a new design source
    /// </summary>
    Task<DesignSource> StoreSourceAsync(DesignSource source, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get source by URL (check if already evaluated)
    /// </summary>
    Task<DesignSource?> GetSourceByUrlAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get pending sources (not yet crawled)
    /// </summary>
    Task<List<DesignSource>> GetPendingSourcesAsync(int limit = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update source status
    /// </summary>
    Task UpdateSourceStatusAsync(string sourceId, string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset stuck "processing" sources back to "pending" (cleanup on restart)
    /// </summary>
    Task ResetStuckProcessingSourcesAsync(CancellationToken cancellationToken = default);
    
    // ===== CAPTURED DESIGNS =====
    
    /// <summary>
    /// Store a captured design
    /// </summary>
    Task<CapturedDesign> StoreDesignAsync(CapturedDesign design, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get design by ID
    /// </summary>
    Task<CapturedDesign?> GetDesignAsync(string designId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get design by URL
    /// </summary>
    Task<CapturedDesign?> GetDesignByUrlAsync(string url, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get leaderboard designs (top N by score)
    /// </summary>
    Task<List<CapturedDesign>> GetLeaderboardAsync(int limit = 100, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get leaderboard floor (lowest score in top 100)
    /// </summary>
    Task<double?> GetLeaderboardFloorAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update leaderboard ranks after new design insertion
    /// </summary>
    Task UpdateLeaderboardRanksAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Evict design from leaderboard (when new design pushes it out)
    /// </summary>
    Task EvictFromLeaderboardAsync(string designId, CancellationToken cancellationToken = default);
    
    // ===== PAGE ANALYSIS =====
    
    /// <summary>
    /// Store page analysis
    /// </summary>
    Task<PageAnalysis> StorePageAnalysisAsync(PageAnalysis page, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get page analysis by ID
    /// </summary>
    Task<PageAnalysis?> GetPageAnalysisAsync(string pageId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all pages for a design
    /// </summary>
    Task<List<PageAnalysis>> GetDesignPagesAsync(string designId, CancellationToken cancellationToken = default);
    
    // ===== DESIGN PATTERNS =====
    
    /// <summary>
    /// Store or update a design pattern
    /// </summary>
    Task<DesignPattern> StorePatternAsync(DesignPattern pattern, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get pattern by ID
    /// </summary>
    Task<DesignPattern?> GetPatternAsync(string patternId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find similar patterns (by name/category/tags)
    /// </summary>
    Task<List<DesignPattern>> FindSimilarPatternsAsync(string category, List<string> tags, int limit = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get top patterns by quality score
    /// </summary>
    Task<List<DesignPattern>> GetTopPatternsAsync(int limit = 50, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Increment pattern observation count
    /// </summary>
    Task IncrementPatternObservationAsync(string patternId, string sourceDesignId, CancellationToken cancellationToken = default);
    
    // ===== FEEDBACK =====
    
    /// <summary>
    /// Store user feedback
    /// </summary>
    Task<DesignFeedback> StoreFeedbackAsync(DesignFeedback feedback, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recent feedback for prompt evolution
    /// </summary>
    Task<List<DesignFeedback>> GetRecentFeedbackAsync(int limit = 20, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get feedback with significant mismatch
    /// </summary>
    Task<List<DesignFeedback>> GetMismatchedFeedbackAsync(double threshold = 2.0, CancellationToken cancellationToken = default);
    
    // ===== MODEL PERFORMANCE =====
    
    /// <summary>
    /// Store or update model performance calibration
    /// </summary>
    Task<ModelPerformance> StoreModelPerformanceAsync(ModelPerformance performance, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get model performance calibration
    /// </summary>
    Task<ModelPerformance?> GetModelPerformanceAsync(string model, string pageType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all model performance data for a model
    /// </summary>
    Task<List<ModelPerformance>> GetModelPerformanceHistoryAsync(string model, CancellationToken cancellationToken = default);
    
    // ===== PROMPTS (Integration with Lightning) =====
    
    /// <summary>
    /// Get prompt from Lightning (Neo4j)
    /// </summary>
    Task<string?> GetPromptAsync(string promptName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update prompt version in Lightning
    /// </summary>
    Task UpdatePromptAsync(string promptName, string newContent, int version, CancellationToken cancellationToken = default);
    
    // ===== SEARCH & VECTOR =====
    
    /// <summary>
    /// Store design embeddings in Qdrant
    /// </summary>
    Task StoreDesignEmbeddingAsync(string designId, float[] embedding, Dictionary<string, object> metadata, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find similar designs by embedding
    /// </summary>
    Task<List<string>> FindSimilarDesignsAsync(float[] queryEmbedding, int limit = 10, CancellationToken cancellationToken = default);
}

