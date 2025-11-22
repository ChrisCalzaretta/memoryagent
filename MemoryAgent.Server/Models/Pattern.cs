namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a detected code pattern
/// </summary>
public class Pattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<string> Examples { get; set; } = new();
    public int UsageCount { get; set; }
    public float Confidence { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public float[]? Embedding { get; set; }
}

