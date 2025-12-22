using System.Collections.Generic;

namespace CodingAgent.Server.Configuration;

/// <summary>
/// GPU-aware model configuration for distributed model loading
/// Optimized for 60GB VRAM setup (2x RTX 3090 + 1x RTX 5070 Ti)
/// </summary>
public class GPUModelConfiguration
{
    /// <summary>
    /// GPU assignments for different model types
    /// </summary>
    public Dictionary<string, GPUAssignment> Models { get; set; } = new();

    /// <summary>
    /// Available GPUs in the system
    /// </summary>
    public List<GPUInfo> GPUs { get; set; } = new()
    {
        new GPUInfo { DeviceId = 0, Name = "RTX 3090 #1", VRAMTotal = 24, VRAMReserved = 1 },
        new GPUInfo { DeviceId = 1, Name = "RTX 3090 #2", VRAMTotal = 24, VRAMReserved = 1 },
        new GPUInfo { DeviceId = 2, Name = "RTX 5070 Ti", VRAMTotal = 12, VRAMReserved = 1 }
    };

    /// <summary>
    /// Default configuration optimized for 60GB setup
    /// </summary>
    public static GPUModelConfiguration Default => new()
    {
        Models = new Dictionary<string, GPUAssignment>
        {
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
            // GPU 0 (RTX 3090 #1): THINKING MODELS
            // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
               // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
               // ALL MODELS ON DEFAULT PORT 11434 (Single Ollama instance)
               // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
               ["phi4:latest"] = new GPUAssignment
               {
                   GPU = 0,
                   VRAMEstimate = 14,
                   Purpose = "Fast strategic thinking",
                   Priority = 1 // Always loaded
               },
               ["gemma3:latest"] = new GPUAssignment
               {
                   GPU = 0,
                   VRAMEstimate = 10,
                   Purpose = "Deep reasoning and critique",
                   Priority = 1 // Always loaded
               },
               ["qwen2.5-coder:14b"] = new GPUAssignment
               {
                   GPU = 0,
                   VRAMEstimate = 7.5,
                   Purpose = "Code generation + thinking",
                   Priority = 1 // Always loaded
               },
               ["deepseek-coder-v2:16b"] = new GPUAssignment
               {
                   GPU = 0,
                   VRAMEstimate = 16,
                   Purpose = "Fast code generation",
                   Priority = 1 // Always loaded
               },
               ["llama3:latest"] = new GPUAssignment
               {
                   GPU = 0,
                   VRAMEstimate = 9,
                   Purpose = "Validation + backup thinking",
                   Priority = 1 // Always loaded
               },
               ["codestral:latest"] = new GPUAssignment
               {
                   GPU = 0,
                   VRAMEstimate = 22,
                   Purpose = "Premium code generation",
                   Priority = 1 // Always loaded
               }
        }
    };

    /// <summary>
    /// Get available VRAM for a GPU
    /// </summary>
    public double GetAvailableVRAM(int gpu)
    {
        var gpuInfo = GPUs.Find(g => g.DeviceId == gpu);
        if (gpuInfo == null) return 0;

        var usedVRAM = Models.Values
            .Where(m => m.GPU == gpu && m.IsLoaded)
            .Sum(m => m.VRAMEstimate);

        return gpuInfo.VRAMTotal - gpuInfo.VRAMReserved - usedVRAM;
    }

    /// <summary>
    /// Check if a model can be loaded
    /// </summary>
    public bool CanLoadModel(string modelName)
    {
        if (!Models.TryGetValue(modelName, out var assignment))
            return false;

        var available = GetAvailableVRAM(assignment.GPU);
        return available >= assignment.VRAMEstimate;
    }

    /// <summary>
    /// Mark model as loaded/unloaded
    /// </summary>
    public void SetModelLoaded(string modelName, bool loaded)
    {
        if (Models.TryGetValue(modelName, out var assignment))
        {
            assignment.IsLoaded = loaded;
        }
    }

    /// <summary>
    /// Get model configuration (name + GPU assignment)
    /// </summary>
    public ModelGpuInfo GetModel(string modelName)
    {
        if (Models.TryGetValue(modelName, out var assignment))
        {
            return new ModelGpuInfo
            {
                Name = modelName,
                GpuDevice = assignment.GPU
            };
        }

        // Fallback: Use default GPU 0
        return new ModelGpuInfo
        {
            Name = modelName,
            GpuDevice = 0
        };
    }
}

/// <summary>
/// Model with GPU device assignment
/// </summary>
public class ModelGpuInfo
{
    public string Name { get; set; } = "";
    public int GpuDevice { get; set; }
}

/// <summary>
/// GPU assignment for a model
/// </summary>
public class GPUAssignment
{
    /// <summary>
    /// GPU device ID (0, 1, 2)
    /// </summary>
    public int GPU { get; set; }

    /// <summary>
    /// Estimated VRAM usage in GB
    /// </summary>
    public double VRAMEstimate { get; set; }

    /// <summary>
    /// Purpose/role of this model
    /// </summary>
    public string Purpose { get; set; } = "";

    /// <summary>
    /// Loading priority (1=always loaded, 2=on-demand, 3=swap-in)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Model to swap with (for large models)
    /// </summary>
    public string? SwapWith { get; set; }

    /// <summary>
    /// Is model currently loaded?
    /// </summary>
    public bool IsLoaded { get; set; }
}

/// <summary>
/// GPU information
/// </summary>
public class GPUInfo
{
    /// <summary>
    /// GPU device ID
    /// </summary>
    public int DeviceId { get; set; }

    /// <summary>
    /// GPU name
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Total VRAM in GB
    /// </summary>
    public double VRAMTotal { get; set; }

    /// <summary>
    /// Reserved VRAM for system (in GB)
    /// </summary>
    public double VRAMReserved { get; set; }
}

