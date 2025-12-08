namespace AgentContracts.Services;

/// <summary>
/// Translates between host paths and container paths.
/// Used by all agents to properly access files when running in Docker containers.
/// </summary>
public interface IPathTranslationService
{
    /// <summary>
    /// Translate a Windows host path to a container path.
    /// E.g., E:\GitHub\MyProject -> /workspace/MyProject
    /// </summary>
    string TranslateToContainerPath(string hostPath);
    
    /// <summary>
    /// Translate a container path back to a Windows host path.
    /// E.g., /workspace/MyProject -> E:\GitHub\MyProject
    /// </summary>
    string TranslateToHostPath(string containerPath);
    
    /// <summary>
    /// Check if a file exists (using translated path)
    /// </summary>
    bool FileExists(string path);
    
    /// <summary>
    /// Read file contents (using translated path)
    /// </summary>
    Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>
/// Path translation service that reads mappings from configuration.
/// Configuration keys: PathMapping:HostRoot, PathMapping:ContainerRoot
/// </summary>
public class PathTranslationService : IPathTranslationService
{
    private readonly Dictionary<string, string> _pathMappings;
    private readonly string _hostRoot;
    private readonly string _containerRoot;

    public PathTranslationService(string? hostRoot = null, string? containerRoot = null)
    {
        _hostRoot = hostRoot ?? Environment.GetEnvironmentVariable("PathMapping__HostRoot") ?? @"E:\GitHub";
        _containerRoot = containerRoot ?? Environment.GetEnvironmentVariable("PathMapping__ContainerRoot") ?? "/workspace";
        
        _pathMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [_hostRoot] = _containerRoot
        };
    }

    public string TranslateToContainerPath(string hostPath)
    {
        if (string.IsNullOrWhiteSpace(hostPath))
            return hostPath;

        // If already a container path (starts with /), return as-is
        if (hostPath.StartsWith('/'))
            return hostPath;

        // Normalize path separators
        var normalizedPath = hostPath.Replace('\\', '/');
        
        // Try each mapping
        foreach (var (hostRoot, containerRoot) in _pathMappings)
        {
            var normalizedHostRoot = hostRoot.Replace('\\', '/');
            
            if (normalizedPath.StartsWith(normalizedHostRoot, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = normalizedPath.Substring(normalizedHostRoot.Length);
                if (relativePath.StartsWith('/'))
                    relativePath = relativePath.Substring(1);
                    
                var containerPath = string.IsNullOrEmpty(relativePath) 
                    ? containerRoot 
                    : $"{containerRoot}/{relativePath}";
                
                return containerPath;
            }
        }

        // If no mapping found, return original
        return hostPath;
    }

    public string TranslateToHostPath(string containerPath)
    {
        if (string.IsNullOrWhiteSpace(containerPath))
            return containerPath;

        // If not a container path, return as-is
        if (!containerPath.StartsWith('/'))
            return containerPath;

        // Try each mapping in reverse
        foreach (var (hostRoot, containerRoot) in _pathMappings)
        {
            if (containerPath.StartsWith(containerRoot, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = containerPath.Substring(containerRoot.Length);
                if (relativePath.StartsWith('/'))
                    relativePath = relativePath.Substring(1);
                    
                var hostPath = Path.Combine(hostRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                return hostPath;
            }
        }

        // If no mapping found, return original
        return containerPath;
    }
    
    public bool FileExists(string path)
    {
        var translatedPath = TranslateToContainerPath(path);
        return File.Exists(translatedPath);
    }
    
    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var translatedPath = TranslateToContainerPath(path);
        return await File.ReadAllTextAsync(translatedPath, cancellationToken);
    }
}

