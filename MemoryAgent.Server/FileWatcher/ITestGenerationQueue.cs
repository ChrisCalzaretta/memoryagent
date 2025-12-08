namespace MemoryAgent.Server.FileWatcher;

/// <summary>
/// Queue for auto-generating tests for files that don't have them
/// </summary>
public interface ITestGenerationQueue
{
    /// <summary>
    /// Add a file to the test generation queue
    /// </summary>
    void Enqueue(TestGenerationRequest request);
    
    /// <summary>
    /// Try to get the next item from the queue
    /// </summary>
    bool TryDequeue(out TestGenerationRequest? request);
    
    /// <summary>
    /// Check if a file is already queued or being processed
    /// </summary>
    bool IsQueued(string filePath);
    
    /// <summary>
    /// Get current queue count
    /// </summary>
    int Count { get; }
    
    /// <summary>
    /// Check if currently processing a test generation
    /// </summary>
    bool IsProcessing { get; }
    
    /// <summary>
    /// Mark processing started
    /// </summary>
    void StartProcessing();
    
    /// <summary>
    /// Mark processing completed
    /// </summary>
    void CompleteProcessing();
}

public class TestGenerationRequest
{
    /// <summary>
    /// Full path to source file (container path)
    /// </summary>
    public string SourceFilePath { get; set; } = "";
    
    /// <summary>
    /// Project context name
    /// </summary>
    public string Context { get; set; } = "";
    
    /// <summary>
    /// Expected test file path
    /// </summary>
    public string TestFilePath { get; set; } = "";
    
    /// <summary>
    /// Project name (e.g., "MyProject")
    /// </summary>
    public string ProjectName { get; set; } = "";
    
    /// <summary>
    /// Class name to generate tests for
    /// </summary>
    public string ClassName { get; set; } = "";
    
    /// <summary>
    /// When the request was queued
    /// </summary>
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}

