using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for managing versioned, evolving prompts with outcome tracking.
/// Enables A/B testing and automatic prompt improvement.
/// </summary>
public interface IPromptService
{
    #region Prompt Retrieval
    
    /// <summary>
    /// Get the active prompt for a given name, optionally selecting A/B test variant
    /// </summary>
    Task<PromptTemplate> GetPromptAsync(string name, bool allowTestVariant = true, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a specific version of a prompt
    /// </summary>
    Task<PromptTemplate?> GetPromptVersionAsync(string name, int version, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all versions of a prompt
    /// </summary>
    Task<List<PromptTemplate>> GetPromptHistoryAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List all available prompts
    /// </summary>
    Task<List<PromptTemplate>> ListPromptsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Render a prompt with variables substituted
    /// </summary>
    Task<string> RenderPromptAsync(string name, Dictionary<string, string> variables, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Prompt Management
    
    /// <summary>
    /// Create a new prompt (version 1)
    /// </summary>
    Task<PromptTemplate> CreatePromptAsync(
        string name,
        string content,
        string description,
        List<PromptVariable>? variables = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a new version of an existing prompt
    /// </summary>
    Task<PromptTemplate> CreateVersionAsync(
        string name,
        string content,
        string evolutionReason,
        bool activateImmediately = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Activate a specific prompt version
    /// </summary>
    Task ActivateVersionAsync(string name, int version, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rollback to previous version
    /// </summary>
    Task RollbackAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Start A/B testing a new version
    /// </summary>
    Task StartABTestAsync(string name, int testVersion, int trafficPercent = 10, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop A/B testing (optionally promote the test version)
    /// </summary>
    Task StopABTestAsync(string name, bool promoteTestVersion = false, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Execution Tracking
    
    /// <summary>
    /// Record a prompt execution for tracking
    /// </summary>
    Task<PromptExecution> RecordExecutionAsync(
        string promptId,
        string renderedPrompt,
        Dictionary<string, string> inputVariables,
        string response,
        long responseTimeMs,
        float? confidence = null,
        bool parseSuccess = true,
        string? parseError = null,
        string? sessionId = null,
        string? context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record outcome/feedback for an execution
    /// </summary>
    Task RecordOutcomeAsync(
        string executionId,
        bool wasSuccessful,
        int? userRating = null,
        string? comments = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record implicit success signal (e.g., user took action based on response)
    /// </summary>
    Task RecordImplicitSuccessAsync(string executionId, string signal, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record implicit failure signal (e.g., user asked follow-up clarification)
    /// </summary>
    Task RecordImplicitFailureAsync(string executionId, string signal, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recent executions for a prompt
    /// </summary>
    Task<List<PromptExecution>> GetRecentExecutionsAsync(
        string promptName,
        int limit = 50,
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Analytics & Learning
    
    /// <summary>
    /// Get metrics for a prompt (success rate, confidence, etc.)
    /// </summary>
    Task<PromptMetrics> GetPromptMetricsAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get A/B test results comparing versions
    /// </summary>
    Task<ABTestResult> GetABTestResultsAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Suggest prompt improvements based on execution data
    /// </summary>
    Task<List<PromptImprovement>> SuggestImprovementsAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Automatically evolve prompt based on successful executions
    /// </summary>
    Task<PromptTemplate?> AutoEvolveAsync(string name, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Initialization
    
    /// <summary>
    /// Initialize default prompts from code (migration from hardcoded prompts)
    /// </summary>
    Task InitializeDefaultPromptsAsync(CancellationToken cancellationToken = default);
    
    #endregion
}

/// <summary>
/// Metrics for a prompt
/// </summary>
public class PromptMetrics
{
    public string PromptName { get; set; } = string.Empty;
    public int ActiveVersion { get; set; }
    public int TotalVersions { get; set; }
    public int TotalExecutions { get; set; }
    public float SuccessRate { get; set; }
    public float AvgConfidence { get; set; }
    public double AvgResponseTimeMs { get; set; }
    public int ExecutionsLast24h { get; set; }
    public int ExecutionsLast7d { get; set; }
    public float SuccessRateTrend { get; set; } // +/- change from previous period
    public bool IsABTesting { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime? LastImproved { get; set; }
}

/// <summary>
/// Results of an A/B test
/// </summary>
public class ABTestResult
{
    public string PromptName { get; set; } = string.Empty;
    public int ControlVersion { get; set; }
    public int TestVersion { get; set; }
    public int ControlExecutions { get; set; }
    public int TestExecutions { get; set; }
    public float ControlSuccessRate { get; set; }
    public float TestSuccessRate { get; set; }
    public float ControlAvgConfidence { get; set; }
    public float TestAvgConfidence { get; set; }
    public double ControlAvgResponseMs { get; set; }
    public double TestAvgResponseMs { get; set; }
    public float SuccessRateDifference { get; set; }
    public bool IsStatisticallySignificant { get; set; }
    public string Recommendation { get; set; } = string.Empty; // "promote", "reject", "continue"
    public DateTime TestStartedAt { get; set; }
    public int TestDurationDays { get; set; }
}

/// <summary>
/// Suggested improvement for a prompt
/// </summary>
public class PromptImprovement
{
    public string Type { get; set; } = string.Empty; // "clarity", "examples", "structure", "constraints"
    public string Description { get; set; } = string.Empty;
    public string SuggestedChange { get; set; } = string.Empty;
    public float ExpectedImpact { get; set; } // Estimated improvement 0.0-1.0
    public string Evidence { get; set; } = string.Empty; // Why this is suggested
}

