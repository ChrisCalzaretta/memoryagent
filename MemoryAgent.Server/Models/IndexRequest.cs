namespace MemoryAgent.Server.Models;

/// <summary>
/// Request to index a file or directory
/// </summary>
public class IndexFileRequest
{
    public string Path { get; set; } = string.Empty;
    public string? Context { get; set; }
}

public class IndexDirectoryRequest
{
    public string Path { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
    public string? Context { get; set; }
}

public class IndexResult
{
    public bool Success { get; set; }
    public int FilesIndexed { get; set; }
    public int ClassesFound { get; set; }
    public int MethodsFound { get; set; }
    public int PatternsDetected { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

