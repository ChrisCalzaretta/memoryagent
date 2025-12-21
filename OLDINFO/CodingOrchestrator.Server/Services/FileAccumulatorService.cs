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
    /// Get accumulated files filtered to exclude build artifacts (bin/, obj/, .vs/)
    /// Use this when sending files to agents to avoid massive payloads
    /// </summary>
    IReadOnlyDictionary<string, FileChange> GetAccumulatedFilesFiltered();
    
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
    /// 🧹 Clean accumulated files for C# build - remove junk and consolidate duplicates
    /// </summary>
    void CleanForCSharpBuild();
    
    /// <summary>
    /// Get count of accumulated files
    /// </summary>
    int Count { get; }
}

public class FileAccumulatorService : IFileAccumulatorService
{
    private readonly Dictionary<string, FileChange> _accumulatedFiles = new(StringComparer.OrdinalIgnoreCase);
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
            // 🔧 NORMALIZE PATH: Handle different path formats from LLMs
            var normalizedPath = NormalizePath(file.Path);
            
            // Check for existing file with same base name (e.g., "Services/Calculator.cs" vs "Calculator.cs")
            var existingKey = FindExistingFileKey(normalizedPath);
            
            if (existingKey != null && existingKey != normalizedPath)
            {
                // Same file with different path - update existing, keep canonical path
                _logger.LogInformation("📁 Updating existing file: {OldPath} → {NewPath}", existingKey, normalizedPath);
                _accumulatedFiles.Remove(existingKey);
            }
            
            _accumulatedFiles[normalizedPath] = new FileChange
            {
                Path = normalizedPath,  // Use normalized path
                Content = file.Content,
                Type = file.Type,
                Reason = file.Reason
            };
            _logger.LogDebug("📁 Accumulated file: {Path} (total: {Count})", normalizedPath, _accumulatedFiles.Count);
        }
    }
    
    /// <summary>
    /// Normalize file path to prevent duplicates from different LLM path formats
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        
        // Normalize separators
        var normalized = path.Replace('\\', '/').Trim('/');
        
        // Remove redundant segments like ./
        while (normalized.Contains("./"))
        {
            normalized = normalized.Replace("./", "");
        }
        
        // For C# files: If the path is just a filename like "Calculator.cs", keep it simple
        // If it has a directory, keep the structure: "Services/Calculator.cs"
        
        return normalized;
    }
    
    /// <summary>
    /// Find if we already have a file with the same base name
    /// This handles cases like "Services/Calculator.cs" vs "Calculator.cs"
    /// </summary>
    private string? FindExistingFileKey(string newPath)
    {
        var newFileName = Path.GetFileName(newPath);
        
        // Exact match first
        if (_accumulatedFiles.ContainsKey(newPath))
            return newPath;
        
        // Check for same filename with different path
        foreach (var existingKey in _accumulatedFiles.Keys)
        {
            var existingFileName = Path.GetFileName(existingKey);
            
            // Same filename - this is likely the same class
            if (existingFileName.Equals(newFileName, StringComparison.OrdinalIgnoreCase))
            {
                // For C# files, always treat same filename as same file
                if (newFileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    return existingKey;
                }
                // For .csproj files, always consolidate to one
                if (newFileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    return existingKey;
                }
            }
        }
        
        // 🔧 SPECIAL: Check for duplicate .csproj files by ANY name
        if (newPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var existingCsproj = _accumulatedFiles.Keys.FirstOrDefault(k => 
                k.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
            if (existingCsproj != null)
            {
                _logger.LogWarning("🔧 Replacing existing .csproj ({Old}) with new one ({New})", existingCsproj, newPath);
                return existingCsproj;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 🧹 Clean accumulated files for C# - remove junk and consolidate duplicates
    /// Call this before final build
    /// </summary>
    public void CleanForCSharpBuild()
    {
        var toRemove = new List<string>();
        var csprojFiles = new List<string>();
        var csFilesByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var key in _accumulatedFiles.Keys)
        {
            var fileName = Path.GetFileName(key);
            
            // Track .csproj files
            if (key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                csprojFiles.Add(key);
                continue;
            }
            
            // Track .cs files by base name
            if (key.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (!csFilesByName.ContainsKey(fileName))
                    csFilesByName[fileName] = new List<string>();
                csFilesByName[fileName].Add(key);
                continue;
            }
            
            // 🧹 Remove junk files that shouldn't be in a C# project
            var ext = Path.GetExtension(key).ToLowerInvariant();
            if (ext is ".json" or ".xml" or ".txt" or ".yaml" or ".yml")
            {
                // Keep only specific config files
                if (!fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.Equals("appsettings.Development.json", StringComparison.OrdinalIgnoreCase))
                {
                    toRemove.Add(key);
                    _logger.LogWarning("🧹 Removing junk file from C# build: {Path}", key);
                }
            }
        }
        
        // Keep only ONE .csproj file (prefer the one with actual project name)
        if (csprojFiles.Count > 1)
        {
            // Sort: prefer files without "Generated" in name, then shorter paths
            var preferred = csprojFiles
                .OrderBy(f => f.Contains("Generated", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(f => f.Length)
                .First();
            
            foreach (var csproj in csprojFiles.Where(f => f != preferred))
            {
                toRemove.Add(csproj);
                _logger.LogWarning("🧹 Removing duplicate .csproj: {Path} (keeping {Preferred})", csproj, preferred);
            }
        }
        
        // For duplicate .cs files, keep the one with content (or the shorter path)
        foreach (var kvp in csFilesByName.Where(k => k.Value.Count > 1))
        {
            var preferredCs = kvp.Value
                .OrderByDescending(f => _accumulatedFiles[f].Content?.Length ?? 0)
                .ThenBy(f => f.Length)
                .First();
            
            foreach (var dup in kvp.Value.Where(f => f != preferredCs))
            {
                toRemove.Add(dup);
                _logger.LogWarning("🧹 Removing duplicate C# file: {Path} (keeping {Preferred})", dup, preferredCs);
            }
        }
        
        // Remove the junk
        foreach (var key in toRemove)
        {
            _accumulatedFiles.Remove(key);
        }
        
        if (toRemove.Any())
        {
            _logger.LogInformation("🧹 Cleaned {Count} junk/duplicate files, {Remaining} files remain", 
                toRemove.Count, _accumulatedFiles.Count);
        }
    }

    public IReadOnlyDictionary<string, FileChange> GetAccumulatedFiles() => _accumulatedFiles;
    
    public IReadOnlyDictionary<string, FileChange> GetAccumulatedFilesFiltered()
    {
        // Filter out build artifacts that bloat payloads (bin/, obj/, .vs/, node_modules/)
        var filtered = _accumulatedFiles
            .Where(kvp => !IsBuildArtifact(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        
        var excludedCount = _accumulatedFiles.Count - filtered.Count;
        if (excludedCount > 0)
        {
            _logger.LogInformation("🚫 Filtered out {Count} build artifacts (bin/obj/.vs) from accumulated files", excludedCount);
        }
        
        return filtered;
    }
    
    /// <summary>
    /// Check if a file path is a build artifact that should be excluded
    /// </summary>
    private static bool IsBuildArtifact(string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        
        // Exclude folders
        if (normalizedPath.Contains("/bin/") || normalizedPath.StartsWith("bin/") ||
            normalizedPath.Contains("/obj/") || normalizedPath.StartsWith("obj/") ||
            normalizedPath.Contains("/.vs/") || normalizedPath.StartsWith(".vs/") ||
            normalizedPath.Contains("/node_modules/") || normalizedPath.StartsWith("node_modules/") ||
            normalizedPath.Contains("/.git/") || normalizedPath.StartsWith(".git/"))
        {
            return true;
        }
        
        // Exclude common build artifacts by file pattern
        var fileName = Path.GetFileName(normalizedPath);
        if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".cache", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains(".nuget.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        return false;
    }

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

