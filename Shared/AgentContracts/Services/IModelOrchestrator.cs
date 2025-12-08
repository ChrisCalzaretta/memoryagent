using AgentContracts.Models;

namespace AgentContracts.Services;

/// <summary>
/// Orchestrates model selection across GPUs with smart rotation
/// NOW WITH LEARNING: Uses historical performance data for smart selection!
/// </summary>
public interface IModelOrchestrator
{
    /// <summary>
    /// Get the pinned primary model (always loaded, instant response)
    /// </summary>
    (string Model, int Port) GetPrimaryModel();
    
    /// <summary>
    /// Select the best available model for a purpose, excluding already-tried models
    /// Uses priority-based selection (no historical data)
    /// </summary>
    Task<(string Model, int Port)?> SelectModelAsync(
        ModelPurpose purpose, 
        HashSet<string> excludeModels,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 🧠 SMART MODEL SELECTION with LLM confirmation:
    /// 1. Query historical stats from MemoryAgent
    /// 2. Use LLM to analyze task + historical rates and confirm/adjust selection
    /// 3. Falls back to priority-based selection if no data available
    /// </summary>
    Task<(string Model, int Port)?> SelectBestModelAsync(
        ModelPurpose purpose,
        string? language,
        string? complexity,
        HashSet<string> excludeModels,
        List<string>? taskKeywords = null,
        string? context = null,
        string? taskDescription = null,
        object? llmSelector = null,  // ILlmModelSelector - use object to avoid circular dependency
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a model can be loaded without evicting pinned models
    /// </summary>
    bool CanLoadModel(string model, int port);
    
    /// <summary>
    /// Get all available models for a purpose
    /// </summary>
    List<ModelInfo> GetModelsForPurpose(ModelPurpose purpose);
    
    /// <summary>
    /// Record model performance for learning - stores in MemoryAgent
    /// </summary>
    Task RecordModelPerformanceAsync(
        string model, 
        string taskType, 
        bool succeeded, 
        double score,
        string? language = null,
        string? complexity = null,
        int iterations = 1,
        long durationMs = 0,
        string? errorType = null,
        List<string>? taskKeywords = null,
        string? context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// GPU configuration
    /// </summary>
    GpuConfig Config { get; }
}


