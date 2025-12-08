using System.Collections.Concurrent;

namespace MemoryAgent.Server.FileWatcher;

/// <summary>
/// Thread-safe queue for test generation requests
/// Processes one at a time to avoid overloading Ollama
/// </summary>
public class TestGenerationQueue : ITestGenerationQueue
{
    private readonly ConcurrentQueue<TestGenerationRequest> _queue = new();
    private readonly ConcurrentDictionary<string, bool> _queuedFiles = new();
    private volatile bool _isProcessing;
    private readonly ILogger<TestGenerationQueue> _logger;

    public TestGenerationQueue(ILogger<TestGenerationQueue> logger)
    {
        _logger = logger;
    }

    public int Count => _queue.Count;
    public bool IsProcessing => _isProcessing;

    public void Enqueue(TestGenerationRequest request)
    {
        // Normalize path for comparison
        var normalizedPath = request.SourceFilePath.Replace("\\", "/").ToLowerInvariant();
        
        // Don't queue if already queued
        if (!_queuedFiles.TryAdd(normalizedPath, true))
        {
            _logger.LogDebug("File already queued for test generation: {File}", 
                Path.GetFileName(request.SourceFilePath));
            return;
        }

        _queue.Enqueue(request);
        _logger.LogInformation("📝 Queued test generation: {ClassName} ({QueueCount} in queue)",
            request.ClassName, _queue.Count);
    }

    public bool TryDequeue(out TestGenerationRequest? request)
    {
        if (_queue.TryDequeue(out request))
        {
            // Remove from queued set
            var normalizedPath = request.SourceFilePath.Replace("\\", "/").ToLowerInvariant();
            _queuedFiles.TryRemove(normalizedPath, out _);
            return true;
        }
        
        request = null;
        return false;
    }

    public bool IsQueued(string filePath)
    {
        var normalizedPath = filePath.Replace("\\", "/").ToLowerInvariant();
        return _queuedFiles.ContainsKey(normalizedPath);
    }

    public void StartProcessing()
    {
        _isProcessing = true;
    }

    public void CompleteProcessing()
    {
        _isProcessing = false;
    }
}

