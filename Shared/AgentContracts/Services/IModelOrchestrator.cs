using AgentContracts.Models;

namespace AgentContracts.Services;

/// <summary>
/// Orchestrates model selection across GPUs with smart rotation
/// </summary>
public interface IModelOrchestrator
{
    /// <summary>
    /// Get the pinned primary model (always loaded, instant response)
    /// </summary>
    (string Model, int Port) GetPrimaryModel();
    
    /// <summary>
    /// Select the best available model for a purpose, excluding already-tried models
    /// </summary>
    Task<(string Model, int Port)?> SelectModelAsync(
        ModelPurpose purpose, 
        HashSet<string> excludeModels,
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
    /// Record model performance for learning
    /// </summary>
    Task RecordModelPerformanceAsync(
        string model, 
        string taskType, 
        bool succeeded, 
        double score,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// GPU configuration
    /// </summary>
    GpuConfig Config { get; }
}


