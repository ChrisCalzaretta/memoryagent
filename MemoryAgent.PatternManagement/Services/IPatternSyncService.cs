using MemoryAgent.PatternManagement.Models;

namespace MemoryAgent.PatternManagement.Services;

/// <summary>
/// Service for syncing patterns to workspace collections
/// </summary>
public interface IPatternSyncService
{
    /// <summary>
    /// Push a pattern to all workspace collections
    /// </summary>
    Task SyncPatternToAllWorkspacesAsync(GlobalPattern pattern, CancellationToken ct = default);
    
    /// <summary>
    /// Remove a pattern from all workspace collections
    /// </summary>
    Task RemovePatternFromAllWorkspacesAsync(string patternId, CancellationToken ct = default);
    
    /// <summary>
    /// Sync all patterns to a specific workspace
    /// </summary>
    Task SyncAllPatternsToWorkspaceAsync(string workspaceName, CancellationToken ct = default);
    
    /// <summary>
    /// Force sync all patterns to all workspaces
    /// </summary>
    Task SyncAllPatternsToAllWorkspacesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get list of all workspace collections
    /// </summary>
    Task<List<string>> GetWorkspacesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Get sync status for each workspace
    /// </summary>
    Task<Dictionary<string, SyncStatus>> GetSyncStatusAsync(CancellationToken ct = default);
}

/// <summary>
/// Sync status for a workspace
/// </summary>
public class SyncStatus
{
    public required string Workspace { get; set; }
    public int PatternCount { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsInSync { get; set; }
}




