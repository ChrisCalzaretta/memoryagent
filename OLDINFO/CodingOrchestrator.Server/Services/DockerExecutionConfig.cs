namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Configuration for Docker-based code execution
/// Loaded from appsettings.json "DockerExecution" section
/// </summary>
public class DockerExecutionConfig
{
    /// <summary>
    /// Memory limit for containers (e.g., "512m", "1g")
    /// </summary>
    public string MemoryLimit { get; set; } = "512m";

    /// <summary>
    /// CPU limit (e.g., "1.0", "0.5")
    /// </summary>
    public string CpuLimit { get; set; } = "1.0";

    /// <summary>
    /// Maximum number of PIDs in container
    /// </summary>
    public int PidsLimit { get; set; } = 100;

    /// <summary>
    /// Execution timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to allow network access in containers
    /// </summary>
    public bool NetworkEnabled { get; set; } = false;

    /// <summary>
    /// Whether to pre-pull common images on startup
    /// </summary>
    public bool EnableWarmup { get; set; } = true;

    /// <summary>
    /// Docker images to pre-pull for warmup
    /// </summary>
    public List<string> WarmupImages { get; set; } = new()
    {
        "python:3.12-slim",
        "mcr.microsoft.com/dotnet/sdk:8.0",
        "node:20-slim"
    };

    /// <summary>
    /// Timeout for pulling Docker images (minutes)
    /// </summary>
    public int ImagePullTimeoutMinutes { get; set; } = 5;
}

