using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Service for accumulating files across iterations
/// Tracks all generated files and merges updates from each iteration
/// </summary>
public interface IFileAccumulatorService
{
    /// <summary>
    /// Add or update files from a coding iteration
    /// </summary>
    void AccumulateFiles(IEnumerable<FileChange> files);
    
    /// <summary>
    /// Get all accumulated files
    /// </summary>
    IReadOnlyDictionary<string, FileChange> GetAccumulatedFiles();
    
    /// <summary>
    /// Get files as ExecutionFile list for Docker execution
    /// </summary>
    List<ExecutionFile> GetExecutionFiles();
    
    /// <summary>
    /// Get files as GeneratedFile list for final result
    /// </summary>
    List<GeneratedFile> GetGeneratedFiles();
    
    /// <summary>
    /// Get file summary for logging
    /// </summary>
    string GetFileSummary();
    
    /// <summary>
    /// Clear all accumulated files
    /// </summary>
    void Clear();
    
    /// <summary>
    /// Get count of accumulated files
    /// </summary>
    int Count { get; }
}

public class FileAccumulatorService : IFileAccumulatorService
{
    private readonly Dictionary<string, FileChange> _accumulatedFiles = new();
    private readonly ILogger<FileAccumulatorService> _logger;

    public FileAccumulatorService(ILogger<FileAccumulatorService> logger)
    {
        _logger = logger;
    }

    public int Count => _accumulatedFiles.Count;

    public void AccumulateFiles(IEnumerable<FileChange> files)
    {
        foreach (var file in files)
        {
            _accumulatedFiles[file.Path] = file;
            _logger.LogDebug("📁 Accumulated file: {Path} (total: {Count})", file.Path, _accumulatedFiles.Count);
        }
    }

    public IReadOnlyDictionary<string, FileChange> GetAccumulatedFiles() => _accumulatedFiles;

    public List<ExecutionFile> GetExecutionFiles()
    {
        return _accumulatedFiles.Values.Select(f => new ExecutionFile
        {
            Path = f.Path,
            Content = f.Content,
            ChangeType = (int)f.Type,
            Reason = f.Reason
        }).ToList();
    }

    public List<GeneratedFile> GetGeneratedFiles()
    {
        return _accumulatedFiles.Values.Select(f => new GeneratedFile
        {
            Path = f.Path,
            Content = f.Content,
            ChangeType = f.Type,
            Reason = f.Reason
        }).ToList();
    }

    public string GetFileSummary()
    {
        if (!_accumulatedFiles.Any())
            return "No files generated";

        return $"📁 FILES ALREADY GENERATED ({_accumulatedFiles.Count}):\n" + 
               string.Join("\n", _accumulatedFiles.Keys.Select(f => $"- {f}")) +
               "\n\n⚠️ Generate the MISSING files. You may also update existing files if needed.";
    }

    public void Clear()
    {
        _accumulatedFiles.Clear();
        _logger.LogDebug("📁 Cleared accumulated files");
    }
}

