namespace MemoryAgent.Server.Models;

/// <summary>
/// Request to reindex a context (directory)
/// </summary>
public class ReindexRequest
{
    public string? Context { get; set; }
    public string? Path { get; set; }
    public bool RemoveStale { get; set; } = true;
}

public class ReindexResult
{
    public bool Success { get; set; }
    public int FilesAdded { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesRemoved { get; set; }
    public int TotalProcessed { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}


