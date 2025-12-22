using AgentContracts.Responses;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Builds and analyzes project dependency graph using Neo4j
/// Provides multi-file awareness and impact analysis
/// </summary>
public interface IProjectGraphService
{
    Task<ProjectGraph> BuildProjectGraphAsync(string workspacePath, CancellationToken cancellationToken);
    Task<ImpactAnalysis> AnalyzeImpactAsync(string filePath, string workspacePath, CancellationToken cancellationToken);
    Task<List<string>> GetFilesRelatedToTaskAsync(string task, string workspacePath, CancellationToken cancellationToken);
}

public class ProjectGraphService : IProjectGraphService
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<ProjectGraphService> _logger;
    
    public ProjectGraphService(
        IMemoryAgentClient memoryAgent,
        ILogger<ProjectGraphService> logger)
    {
        _memoryAgent = memoryAgent;
        _logger = logger;
    }
    
    public async Task<ProjectGraph> BuildProjectGraphAsync(string workspacePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🕸️ Building project graph for {WorkspacePath}", workspacePath);
        
        var graph = new ProjectGraph { WorkspacePath = workspacePath };
        
        try
        {
            // Get workspace status from Lightning (includes file relationships)
            var workspaceStatus = await _memoryAgent.GetWorkspaceStatusAsync(
                Path.GetFileName(workspacePath), 
                cancellationToken);
            
            if (workspaceStatus != null)
            {
                _logger.LogInformation("📊 Neo4j has {FileCount} files in graph", 
                    workspaceStatus.RecentFiles?.Count ?? 0);
                
                graph.Files = workspaceStatus.RecentFiles ?? new List<string>();
            }
            
            // Build dependency map
            foreach (var file in graph.Files.Take(100)) // Limit to avoid overload
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    // Get file relationships from Neo4j via MemoryAgent
                    var relationships = await GetFileRelationshipsAsync(file, cancellationToken);
                    
                    graph.Dependencies[file] = relationships.DependsOn;
                    graph.Dependents[file] = relationships.UsedBy;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to get relationships for {File}", file);
                }
            }
            
            // Identify critical files (high connectivity)
            graph.CriticalFiles = IdentifyCriticalFiles(graph);
            
            // Find clusters (related files)
            graph.Clusters = FindClusters(graph);
            
            _logger.LogInformation("✅ Project graph built: {Files} files, {Critical} critical, {Clusters} clusters",
                graph.Files.Count, graph.CriticalFiles.Count, graph.Clusters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to build project graph");
        }
        
        return graph;
    }
    
    public async Task<ImpactAnalysis> AnalyzeImpactAsync(
        string filePath, 
        string workspacePath, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔍 Analyzing impact of modifying {File}", filePath);
        
        var analysis = new ImpactAnalysis 
        { 
            ModifiedFile = filePath,
            WorkspacePath = workspacePath
        };
        
        try
        {
            // Get file relationships
            var relationships = await GetFileRelationshipsAsync(filePath, cancellationToken);
            
            // Files that depend on this one (will be affected)
            analysis.AffectedFiles.AddRange(relationships.UsedBy);
            
            // Files that this one depends on (might need to read)
            analysis.RequiredFiles.AddRange(relationships.DependsOn);
            
            // Get co-edited files (usually need updating together)
            try
            {
                var coedited = await _memoryAgent.GetCoEditedFilesAsync(filePath, cancellationToken);
                if (coedited?.Any() == true)
                {
                    analysis.LikelyNeedsUpdate.AddRange(coedited.Select(f => f.Path));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to get co-edited files");
            }
            
            // Calculate risk level
            analysis.RiskLevel = CalculateRiskLevel(analysis);
            
            _logger.LogInformation("📊 Impact analysis: {Affected} affected, {Required} required, {CoEdited} co-edited, Risk: {Risk}",
                analysis.AffectedFiles.Count, 
                analysis.RequiredFiles.Count, 
                analysis.LikelyNeedsUpdate.Count,
                analysis.RiskLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to analyze impact");
        }
        
        return analysis;
    }
    
    public async Task<List<string>> GetFilesRelatedToTaskAsync(
        string task, 
        string workspacePath, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔍 Finding files related to task: {Task}", task);
        
        var relatedFiles = new List<string>();
        
        try
        {
            // Use semantic search to find relevant files
            var searchResults = await _memoryAgent.SmartSearchAsync(
                query: task,
                limit: 15,
                cancellationToken: cancellationToken);
            
            // Extract file paths from search results
            if (searchResults?.Any() == true)
            {
                foreach (var result in searchResults)
                {
                    if (!string.IsNullOrEmpty(result.Path) && !relatedFiles.Contains(result.Path))
                    {
                        relatedFiles.Add(result.Path);
                    }
                }
            }
            
            _logger.LogInformation("✅ Found {Count} files related to task", relatedFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to find related files");
        }
        
        return relatedFiles;
    }
    
    // ═══════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════
    
    private async Task<FileRelationships> GetFileRelationshipsAsync(string filePath, CancellationToken cancellationToken)
    {
        var relationships = new FileRelationships { FilePath = filePath };
        
        try
        {
            // This would call MemoryAgent's Neo4j query
            // For now, we'll use a simplified approach
            var result = await _memoryAgent.GetFileDependenciesAsync(filePath, cancellationToken);
            
            if (result?.Any() == true)
            {
                relationships.DependsOn = result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get dependencies for {File}", filePath);
        }
        
        return relationships;
    }
    
    private List<string> IdentifyCriticalFiles(ProjectGraph graph)
    {
        var criticalFiles = new List<string>();
        
        // A file is "critical" if:
        // 1. Many files depend on it (high out-degree)
        // 2. It depends on many files (high in-degree)
        // 3. Total connectivity > threshold
        
        foreach (var file in graph.Files)
        {
            var dependentsCount = graph.Dependents.ContainsKey(file) ? graph.Dependents[file].Count : 0;
            var dependenciesCount = graph.Dependencies.ContainsKey(file) ? graph.Dependencies[file].Count : 0;
            var connectivity = dependentsCount + dependenciesCount;
            
            if (connectivity >= 5) // Threshold for "critical"
            {
                criticalFiles.Add(file);
            }
        }
        
        // Also add well-known critical files
        var wellKnownCritical = new[] { "Program.cs", "Startup.cs", "appsettings.json" };
        foreach (var file in graph.Files)
        {
            if (wellKnownCritical.Any(wk => file.EndsWith(wk, StringComparison.OrdinalIgnoreCase)))
            {
                if (!criticalFiles.Contains(file))
                {
                    criticalFiles.Add(file);
                }
            }
        }
        
        return criticalFiles.OrderByDescending(f => 
        {
            var dependentsCount = graph.Dependents.ContainsKey(f) ? graph.Dependents[f].Count : 0;
            var dependenciesCount = graph.Dependencies.ContainsKey(f) ? graph.Dependencies[f].Count : 0;
            return dependentsCount + dependenciesCount;
        }).ToList();
    }
    
    private List<FileCluster> FindClusters(ProjectGraph graph)
    {
        var clusters = new List<FileCluster>();
        
        // Simple clustering by directory
        var filesByDirectory = graph.Files
            .GroupBy(f => Path.GetDirectoryName(f) ?? "root")
            .Where(g => g.Count() >= 3); // At least 3 files to be a cluster
        
        foreach (var group in filesByDirectory)
        {
            clusters.Add(new FileCluster
            {
                Name = group.Key,
                Files = group.ToList(),
                Cohesion = CalculateCohesion(group.ToList(), graph)
            });
        }
        
        return clusters.OrderByDescending(c => c.Cohesion).ToList();
    }
    
    private double CalculateCohesion(List<string> files, ProjectGraph graph)
    {
        // Cohesion = ratio of internal dependencies to external dependencies
        var internalDeps = 0;
        var externalDeps = 0;
        
        foreach (var file in files)
        {
            if (graph.Dependencies.ContainsKey(file))
            {
                foreach (var dep in graph.Dependencies[file])
                {
                    if (files.Contains(dep))
                        internalDeps++;
                    else
                        externalDeps++;
                }
            }
        }
        
        var total = internalDeps + externalDeps;
        return total > 0 ? (double)internalDeps / total : 0;
    }
    
    private string CalculateRiskLevel(ImpactAnalysis analysis)
    {
        var affectedCount = analysis.AffectedFiles.Count;
        var requiredCount = analysis.RequiredFiles.Count;
        var totalImpact = affectedCount + requiredCount;
        
        if (totalImpact >= 10)
            return "High";
        else if (totalImpact >= 5)
            return "Medium";
        else
            return "Low";
    }
}

// ═══════════════════════════════════════════════════════════
// DATA STRUCTURES
// ═══════════════════════════════════════════════════════════

public class ProjectGraph
{
    public string WorkspacePath { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public Dictionary<string, List<string>> Dependencies { get; set; } = new(); // File -> Files it depends on
    public Dictionary<string, List<string>> Dependents { get; set; } = new(); // File -> Files that depend on it
    public List<string> CriticalFiles { get; set; } = new();
    public List<FileCluster> Clusters { get; set; } = new();
}

public class FileRelationships
{
    public string FilePath { get; set; } = "";
    public List<string> DependsOn { get; set; } = new();
    public List<string> UsedBy { get; set; } = new();
}

public class FileCluster
{
    public string Name { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public double Cohesion { get; set; } // 0-1, higher = more tightly coupled
}

public class ImpactAnalysis
{
    public string ModifiedFile { get; set; } = "";
    public string WorkspacePath { get; set; } = "";
    public List<string> AffectedFiles { get; set; } = new(); // Files that depend on modified file
    public List<string> RequiredFiles { get; set; } = new(); // Files that modified file depends on
    public List<string> LikelyNeedsUpdate { get; set; } = new(); // Co-edited files
    public string RiskLevel { get; set; } = "Low"; // Low, Medium, High
}


