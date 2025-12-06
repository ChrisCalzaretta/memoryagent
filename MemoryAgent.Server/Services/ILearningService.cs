using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for learning from user interactions and improving memory recall.
/// Implements Agent Lightning patterns for self-improving AI assistance.
/// </summary>
public interface ILearningService
{
    #region Session Tracking
    
    /// <summary>
    /// Start a new session for tracking context
    /// </summary>
    Task<Session> StartSessionAsync(string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// End a session and generate summary
    /// </summary>
    Task EndSessionAsync(string sessionId, string? summary = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the current active session for a context
    /// </summary>
    Task<Session?> GetActiveSessionAsync(string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a file was discussed in a session
    /// </summary>
    Task RecordFileDiscussedAsync(string sessionId, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a file was edited in a session
    /// </summary>
    Task RecordFileEditedAsync(string sessionId, string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record a question asked in a session
    /// </summary>
    Task RecordQuestionAskedAsync(string sessionId, string question, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recent sessions for a context
    /// </summary>
    Task<List<Session>> GetRecentSessionsAsync(string context, int limit = 10, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Q&A Learning
    
    /// <summary>
    /// Store a question-answer mapping for future recall
    /// </summary>
    Task StoreQuestionMappingAsync(
        string question, 
        string answer, 
        List<string> relevantFiles, 
        string context,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find similar questions that have been asked before
    /// </summary>
    Task<List<QuestionMapping>> FindSimilarQuestionsAsync(
        string question, 
        string context, 
        int limit = 5,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Mark a question answer as helpful or not
    /// </summary>
    Task RecordAnswerFeedbackAsync(string questionId, bool wasHelpful, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Importance Scoring
    
    /// <summary>
    /// Record that a code element was accessed
    /// </summary>
    Task RecordAccessAsync(string filePath, string? elementName, CodeMemoryType type, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a code element was edited
    /// </summary>
    Task RecordEditAsync(string filePath, string? elementName, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a code element was returned in search results
    /// </summary>
    Task RecordSearchResultAsync(string filePath, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a code element was selected from search results
    /// </summary>
    Task RecordSelectionAsync(string filePath, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get importance score for a code element
    /// </summary>
    Task<ImportanceMetric?> GetImportanceAsync(string filePath, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get most important files in a context
    /// </summary>
    Task<List<ImportanceMetric>> GetMostImportantFilesAsync(string context, int limit = 20, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Recalculate importance scores (e.g., decay recency over time)
    /// </summary>
    Task RecalculateImportanceScoresAsync(string context, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Co-Edit Tracking
    
    /// <summary>
    /// Record that files were edited together in a session
    /// </summary>
    Task RecordCoEditAsync(List<string> filePaths, string sessionId, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get files that are frequently edited with a given file
    /// </summary>
    Task<List<CoEditMetric>> GetCoEditedFilesAsync(string filePath, string context, int limit = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file clusters based on co-edit patterns
    /// </summary>
    Task<List<List<string>>> GetFileClusterssAsync(string context, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Reward Signals
    
    /// <summary>
    /// Record a reward signal for learning
    /// </summary>
    Task RecordRewardSignalAsync(RewardSignal signal, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get accumulated reward for a file/query combination
    /// </summary>
    Task<float> GetAccumulatedRewardAsync(string query, string filePath, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Domain Tagging
    
    /// <summary>
    /// Detect and assign business domain tags to a file
    /// </summary>
    Task<List<DomainTag>> DetectDomainsAsync(string filePath, string content, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get files by business domain
    /// </summary>
    Task<List<string>> GetFilesByDomainAsync(string domain, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all domains in a context
    /// </summary>
    Task<List<string>> GetDomainsAsync(string context, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Smart Search Integration
    
    /// <summary>
    /// Enhance search results using learned importance and patterns
    /// </summary>
    Task<List<CodeExample>> EnhanceSearchResultsAsync(
        List<CodeExample> results, 
        string query, 
        string context,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Tool Usage Tracking
    
    /// <summary>
    /// Record a tool invocation for learning and analytics
    /// </summary>
    Task RecordToolInvocationAsync(
        string toolName,
        string? context,
        string? sessionId,
        string? query,
        Dictionary<string, object>? arguments,
        bool success,
        string? errorMessage,
        long durationMs,
        string? resultSummary,
        int? resultCount,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get aggregated usage metrics for all tools
    /// </summary>
    Task<List<ToolUsageMetric>> GetToolUsageMetricsAsync(
        string? context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get most popular tools by call count
    /// </summary>
    Task<List<ToolUsageMetric>> GetPopularToolsAsync(
        string? context = null,
        int limit = 10,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recent tool invocations for analysis
    /// </summary>
    Task<List<ToolInvocation>> GetRecentToolInvocationsAsync(
        string? context = null,
        string? toolName = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get tool usage patterns (which tools are used together)
    /// </summary>
    Task<Dictionary<string, List<string>>> GetToolUsagePatternsAsync(
        string? context = null,
        CancellationToken cancellationToken = default);
    
    #endregion
}

