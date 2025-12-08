namespace AgentContracts.Models;

/// <summary>
/// GPU configuration for model orchestration
/// </summary>
public class GpuConfig
{
    /// <summary>
    /// Whether we have dual GPUs available
    /// </summary>
    public bool DualGpu { get; set; } = false;
    
    /// <summary>
    /// Port for pinned models (primary GPU)
    /// </summary>
    public int PinnedPort { get; set; } = 11434;
    
    /// <summary>
    /// Port for swap models (secondary GPU, only if DualGpu=true)
    /// </summary>
    public int SwapPort { get; set; } = 11435;
    
    /// <summary>
    /// Model pinned for code generation (never unloaded)
    /// </summary>
    public string PrimaryModel { get; set; } = "deepseek-v2:16b";
    
    /// <summary>
    /// Model pinned for embeddings (never unloaded)
    /// </summary>
    public string EmbeddingModel { get; set; } = "mxbai-embed-large:latest";
    
    /// <summary>
    /// Total VRAM on pinned GPU (GB)
    /// </summary>
    public int PinnedGpuVram { get; set; } = 16;
    
    /// <summary>
    /// Total VRAM on swap GPU (GB), only if DualGpu=true
    /// </summary>
    public int SwapGpuVram { get; set; } = 24;
    
    /// <summary>
    /// Estimated VRAM used by pinned models (GB)
    /// </summary>
    public int PinnedModelsVram { get; set; } = 11; // ~10GB deepseek + ~1GB embedding
    
    /// <summary>
    /// Whether to use LLM-based smart model selection.
    /// Set to false on single GPU to avoid model thrashing.
    /// When false, always uses PrimaryModel without LLM analysis.
    /// </summary>
    public bool UseSmartModelSelection { get; set; } = true;
}

/// <summary>
/// Model information with sizing
/// </summary>
public class ModelInfo
{
    public required string Name { get; set; }
    public double SizeGb { get; set; }
    public ModelPurpose Purpose { get; set; }
    public int Priority { get; set; } = 10;
}

public enum ModelPurpose
{
    CodeGeneration,
    Validation,
    General
}

/// <summary>
/// Information about a currently loaded model (from /api/ps)
/// </summary>
public class LoadedModelInfo
{
    public required string Name { get; set; }
    public double SizeGb { get; set; }
    public double VramGb { get; set; }
}

