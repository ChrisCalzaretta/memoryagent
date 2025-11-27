namespace MemoryAgent.Server.FileWatcher;

/// <summary>
/// Interface for auto-reindex service with multi-workspace support
/// </summary>
public interface IAutoReindexService
{
    /// <summary>
    /// Register a workspace directory for file watching
    /// </summary>
    Task RegisterWorkspaceAsync(string workspacePath, string context);

    /// <summary>
    /// Unregister a workspace directory from file watching
    /// </summary>
    Task UnregisterWorkspaceAsync(string workspacePath);
    
    /// <summary>
    /// Get list of currently registered workspaces
    /// </summary>
    List<string> GetRegisteredWorkspaces();
}

