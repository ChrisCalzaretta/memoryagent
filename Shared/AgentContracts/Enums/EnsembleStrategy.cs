namespace AgentContracts.Enums;

/// <summary>
/// Strategy for ensemble validation (using multiple models)
/// </summary>
public enum EnsembleStrategy
{
    /// <summary>
    /// Single model validation (fastest, lowest confidence)
    /// </summary>
    Single = 0,
    
    /// <summary>
    /// Sequential validation - starts with fast model, adds more if needed
    /// Cost-effective: Only uses ensemble on borderline cases (score 4-8)
    /// Recommended for most use cases
    /// </summary>
    Sequential = 1,
    
    /// <summary>
    /// Parallel voting - runs 3 models in parallel and averages results
    /// High confidence but expensive. Use for final validation or critical code.
    /// </summary>
    ParallelVoting = 2,
    
    /// <summary>
    /// Specialized ensemble - uses different models for different validation aspects
    /// Security model + Pattern model + General quality model
    /// Good balance of speed and thoroughness.
    /// </summary>
    Specialized = 3,
    
    /// <summary>
    /// Adaptive strategy - chooses ensemble mode based on iteration number
    /// Early iterations: Single model (fast)
    /// Late iterations: Sequential (thorough)
    /// Final iteration: Full voting (maximum confidence)
    /// </summary>
    Adaptive = 4,
    
    /// <summary>
    /// Pessimistic ensemble - takes the LOWEST score from multiple models
    /// Safest approach - prevents false positives
    /// Use when code quality is critical
    /// </summary>
    Pessimistic = 5,
    
    /// <summary>
    /// Optimistic ensemble - takes the HIGHEST score from multiple models
    /// Fastest iteration - prevents false negatives
    /// Use when you want to move forward quickly
    /// </summary>
    Optimistic = 6
}



