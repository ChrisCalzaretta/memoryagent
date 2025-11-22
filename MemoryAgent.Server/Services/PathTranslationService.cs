using System.Runtime.InteropServices;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Translates between host paths and container paths
/// </summary>
public interface IPathTranslationService
{
    string TranslateToContainerPath(string hostPath);
    string TranslateToHostPath(string containerPath);
}

public class PathTranslationService : IPathTranslationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PathTranslationService> _logger;
    private readonly Dictionary<string, string> _pathMappings;

    public PathTranslationService(IConfiguration configuration, ILogger<PathTranslationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        // Load path mappings from configuration
        _pathMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Default mapping for the standard setup
        // Windows: E:\GitHub -> Container: /workspace
        var hostRoot = _configuration["PathMapping:HostRoot"] ?? @"E:\GitHub";
        var containerRoot = _configuration["PathMapping:ContainerRoot"] ?? "/workspace";
        
        _pathMappings[hostRoot] = containerRoot;
        
        _logger.LogInformation("Path mapping configured: {HostRoot} -> {ContainerRoot}", hostRoot, containerRoot);
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
                    
                var containerPath = $"{containerRoot}/{relativePath}";
                
                _logger.LogDebug("Translated path: {HostPath} -> {ContainerPath}", hostPath, containerPath);
                
                return containerPath;
            }
        }

        // If no mapping found, log warning and return original
        _logger.LogWarning("No path mapping found for: {HostPath}", hostPath);
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
                
                _logger.LogDebug("Translated path: {ContainerPath} -> {HostPath}", containerPath, hostPath);
                
                return hostPath;
            }
        }

        // If no mapping found, return original
        return containerPath;
    }
}

