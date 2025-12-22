using AgentContracts.Responses;
using CodingAgent.Server.Clients;
using System.Text;

namespace CodingAgent.Server.Services;

/// <summary>
/// Manages hierarchical context loading to solve the "large project context" problem
/// Uses a pyramid approach: Overview → Summaries → Full Content (on-demand)
/// </summary>
public interface IHierarchicalContextManager
{
    Task<ProjectOverview> BuildProjectOverviewAsync(string workspacePath, CancellationToken cancellationToken);
    Task<string> BuildInitialContextAsync(string task, string workspacePath, CancellationToken cancellationToken);
    string GetContextGuidance(int availableTokens, int filesInProject);
}

public class HierarchicalContextManager : IHierarchicalContextManager
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ICodebaseExplorer _codebaseExplorer;
    private readonly ILogger<HierarchicalContextManager> _logger;
    
    public HierarchicalContextManager(
        IMemoryAgentClient memoryAgent,
        ICodebaseExplorer codebaseExplorer,
        ILogger<HierarchicalContextManager> logger)
    {
        _memoryAgent = memoryAgent;
        _codebaseExplorer = codebaseExplorer;
        _logger = logger;
    }
    
    /// <summary>
    /// Builds a high-level project overview (lightweight, always included)
    /// </summary>
    public async Task<ProjectOverview> BuildProjectOverviewAsync(
        string workspacePath, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("📊 Building project overview for {WorkspacePath}", workspacePath);
        
        var overview = new ProjectOverview { WorkspacePath = workspacePath };
        
        try
        {
            // Get codebase context (we already have this!)
            var codebaseContext = await _codebaseExplorer.ExploreAsync(workspacePath, cancellationToken);
            
            if (codebaseContext != null)
            {
                overview.TotalFiles = codebaseContext.Files.Count;
                overview.ProjectType = codebaseContext.ProjectType;
                overview.TargetFramework = codebaseContext.TargetFramework;
                overview.Dependencies = codebaseContext.Dependencies.Take(10).ToList(); // Top 10
                
                // Group files by directory
                overview.DirectoryStructure = codebaseContext.Files
                    .GroupBy(f => Path.GetDirectoryName(f) ?? "root")
                    .ToDictionary(
                        g => g.Key, 
                        g => new DirectoryInfo 
                        { 
                            FileCount = g.Count(),
                            TotalLines = g.Sum(f => EstimateLines(f))
                        });
                
                // Key patterns
                overview.Patterns = new List<string>(); // TODO: Extract patterns from codebase
                
                // Important files (by file size and connectivity)
                overview.KeyFiles = await IdentifyKeyFilesAsync(codebaseContext.Files, cancellationToken);
            }
            
            _logger.LogInformation("✅ Project overview: {FileCount} files, {DirCount} directories", 
                overview.TotalFiles, overview.DirectoryStructure.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to build project overview");
        }
        
        return overview;
    }
    
    /// <summary>
    /// Builds the initial context given to the LLM
    /// Uses hierarchical approach: Overview + Relevant file summaries + Guidance
    /// </summary>
    public async Task<string> BuildInitialContextAsync(
        string task, 
        string workspacePath, 
        CancellationToken cancellationToken)
    {
        var context = new StringBuilder();
        
        // ═══════════════════════════════════════════════════════════
        // LEVEL 1: PROJECT OVERVIEW (Always included)
        // ═══════════════════════════════════════════════════════════
        context.AppendLine("# 📁 PROJECT CONTEXT\n");
        
        var overview = await BuildProjectOverviewAsync(workspacePath, cancellationToken);
        context.AppendLine($"**Project Type:** {overview.ProjectType}");
        context.AppendLine($"**Framework:** {overview.TargetFramework}");
        context.AppendLine($"**Total Files:** {overview.TotalFiles}");
        context.AppendLine();
        
        // Directory structure
        context.AppendLine("## 📂 Directory Structure:");
        foreach (var (dir, info) in overview.DirectoryStructure.OrderByDescending(d => d.Value.FileCount).Take(10))
        {
            context.AppendLine($"  - `{dir}/` ({info.FileCount} files, ~{info.TotalLines} lines)");
        }
        context.AppendLine();
        
        // Key patterns
        if (overview.Patterns.Any())
        {
            context.AppendLine("## 🎯 Architectural Patterns:");
            foreach (var pattern in overview.Patterns.Take(5))
            {
                context.AppendLine($"  - {pattern}");
            }
            context.AppendLine();
        }
        
        // Dependencies
        if (overview.Dependencies.Any())
        {
            context.AppendLine("## 📦 Key Dependencies:");
            foreach (var dep in overview.Dependencies.Take(10))
            {
                context.AppendLine($"  - {dep}");
            }
            context.AppendLine();
        }
        
        // ═══════════════════════════════════════════════════════════
        // LEVEL 2: RELEVANT FILE SUMMARIES (Semantic search)
        // ═══════════════════════════════════════════════════════════
        context.AppendLine("## 🔍 Relevant Files (Based on Your Task):\n");
        
        try
        {
            // Use MemoryAgent's semantic search to find relevant files
            var smartSearchResults = await _memoryAgent.SmartSearchAsync(
                query: task,
                limit: 10,
                cancellationToken: cancellationToken);
            
            if (smartSearchResults?.Any() == true)
            {
                context.AppendLine("## 🔍 Relevant Code:");
                foreach (var result in smartSearchResults.Take(5))
                {
                    context.AppendLine($"  - {result.Path} (score: {result.Score:F2})");
                }
            }
            else
            {
                context.AppendLine("  *No existing files found related to this task*");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Smart search failed, skipping relevant files");
            context.AppendLine("  *Unable to search for relevant files*");
        }
        
        context.AppendLine();
        
        // ═══════════════════════════════════════════════════════════
        // LEVEL 3: CONTEXT GUIDANCE (Tell LLM how to get more info)
        // ═══════════════════════════════════════════════════════════
        context.AppendLine("## 💡 IMPORTANT - Context Management:\n");
        context.AppendLine("**You are working with a LARGE codebase.** To manage context effectively:");
        context.AppendLine();
        context.AppendLine("### 🎯 Before Generating Code:");
        context.AppendLine("1. **EXPLORE FIRST** - Use `list_files` to see what exists");
        context.AppendLine("2. **READ KEY FILES** - Use `read_file` on files related to your task");
        context.AppendLine("3. **SEARCH PATTERNS** - Use `search_codebase` to find existing implementations");
        context.AppendLine("4. **CHECK RELATIONSHIPS** - Use `get_file_relationships` to understand dependencies");
        context.AppendLine();
        context.AppendLine("### 📖 What You Have Access To:");
        context.AppendLine("- **Project Overview** ⬆️ (above) - High-level structure");
        context.AppendLine("- **File Summaries** ⬆️ (above) - Relevant files based on semantic search");
        context.AppendLine("- **On-Demand Reading** 🔧 (via tools) - Full content of ANY file");
        context.AppendLine();
        context.AppendLine("### ⚠️ DO NOT:");
        context.AppendLine("- Generate code without exploring existing patterns first");
        context.AppendLine("- Assume file locations - use `list_files` to check");
        context.AppendLine("- Guess at existing implementations - use `read_file` to verify");
        context.AppendLine();
        context.AppendLine("### ✅ RECOMMENDED WORKFLOW:");
        context.AppendLine("```");
        context.AppendLine("1. list_files(\"Services/\") → See existing services");
        context.AppendLine("2. read_file(\"ExistingService.cs\") → Understand pattern");
        context.AppendLine("3. search_codebase(\"dependency injection\") → Find DI patterns");
        context.AppendLine("4. read_file(\"Program.cs\") → See how services are registered");
        context.AppendLine("5. NOW generate code matching the project's patterns");
        context.AppendLine("6. compile_code → Verify it builds");
        context.AppendLine("7. FINALIZE → Submit working code");
        context.AppendLine("```");
        context.AppendLine();
        
        // Add key files that should definitely be read
        if (overview.KeyFiles.Any())
        {
            context.AppendLine("### 🔑 Key Files You Should Read:");
            foreach (var file in overview.KeyFiles.Take(5))
            {
                context.AppendLine($"- `{file.Path}` - {file.Reason}");
            }
            context.AppendLine();
        }
        
        return context.ToString();
    }
    
    /// <summary>
    /// Provides dynamic guidance based on context budget
    /// </summary>
    public string GetContextGuidance(int availableTokens, int filesInProject)
    {
        if (availableTokens < 4000)
        {
            return "⚠️ LOW CONTEXT BUDGET: Focus on reading ONLY the most critical 1-2 files. Use summaries from search results.";
        }
        else if (availableTokens < 8000)
        {
            return "📊 MEDIUM CONTEXT BUDGET: You can read 3-5 key files in full. Use `search_codebase` for others.";
        }
        else
        {
            return "✅ LARGE CONTEXT BUDGET: You can read multiple files. Still, prioritize the most relevant ones first.";
        }
    }
    
    private async Task<List<KeyFile>> IdentifyKeyFilesAsync(List<string> files, CancellationToken cancellationToken)
    {
        var keyFiles = new List<KeyFile>();
        
        // Program.cs is always key (DI registration, configuration)
        var programCs = files.FirstOrDefault(f => f.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase));
        if (programCs != null)
        {
            keyFiles.Add(new KeyFile 
            { 
                Path = programCs, 
                Reason = "Application entry point, DI configuration" 
            });
        }
        
        // appsettings.json is always key (configuration)
        var appSettings = files.FirstOrDefault(f => f.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase));
        if (appSettings != null)
        {
            keyFiles.Add(new KeyFile 
            { 
                Path = appSettings, 
                Reason = "Application configuration" 
            });
        }
        
        // .csproj file (dependencies)
        var csproj = files.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csproj != null)
        {
            keyFiles.Add(new KeyFile 
            { 
                Path = csproj, 
                Reason = "Project dependencies and settings" 
            });
        }
        
        // Startup.cs (if exists, older .NET projects)
        var startup = files.FirstOrDefault(f => f.EndsWith("Startup.cs", StringComparison.OrdinalIgnoreCase));
        if (startup != null)
        {
            keyFiles.Add(new KeyFile 
            { 
                Path = startup, 
                Reason = "Application startup configuration" 
            });
        }
        
        // Try to find files with high connectivity via Neo4j
        try
        {
            // TODO: Query Neo4j for files with highest number of relationships
            // var highConnectivity = await _memoryAgent.GetMostConnectedFilesAsync(limit: 5);
            // keyFiles.AddRange(highConnectivity);
        }
        catch
        {
            // Fallback: Just use file size as proxy for importance
            var largeFiles = files
                .Where(f => File.Exists(f))
                .Select(f => new { Path = f, Size = new FileInfo(f).Length })
                .OrderByDescending(f => f.Size)
                .Take(3)
                .Select(f => new KeyFile 
                { 
                    Path = f.Path, 
                    Reason = "Large file, likely core functionality" 
                });
            
            keyFiles.AddRange(largeFiles);
        }
        
        return keyFiles;
    }
    
    private int EstimateLines(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return 0;
            return File.ReadAllLines(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }
}

public class ProjectOverview
{
    public string WorkspacePath { get; set; } = "";
    public int TotalFiles { get; set; }
    public string ProjectType { get; set; } = "";
    public string TargetFramework { get; set; } = "";
    public Dictionary<string, DirectoryInfo> DirectoryStructure { get; set; } = new();
    public List<string> Patterns { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public List<KeyFile> KeyFiles { get; set; } = new();
}

public class DirectoryInfo
{
    public int FileCount { get; set; }
    public int TotalLines { get; set; }
}

public class KeyFile
{
    public string Path { get; set; } = "";
    public string Reason { get; set; } = "";
}


