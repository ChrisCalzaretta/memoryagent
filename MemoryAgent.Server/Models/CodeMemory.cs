namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a code element in memory (file, class, method, etc.)
/// </summary>
public class CodeMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public CodeMemoryType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public float[]? Embedding { get; set; }
}

public enum CodeMemoryType
{
    File,
    Class,
    Method,
    Property,
    Interface,
    Pattern
}

