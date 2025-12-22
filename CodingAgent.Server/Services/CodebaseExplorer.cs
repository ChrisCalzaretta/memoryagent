using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CodingAgent.Server.Services;

/// <summary>
/// Explores workspace to provide rich context for LLM code generation
/// Scans files, extracts patterns, builds context summary
/// </summary>
public class CodebaseExplorer : ICodebaseExplorer
{
    private readonly ILogger<CodebaseExplorer> _logger;
    
    // Key files that should be read in full for context
    private static readonly string[] KeyFiles = new[]
    {
        "Program.cs",
        "Startup.cs",
        "_Imports.razor",
        "appsettings.json",
        "App.razor",
        "MainLayout.razor"
    };
    
    public CodebaseExplorer(ILogger<CodebaseExplorer> logger)
    {
        _logger = logger;
    }
    
    public async Task<CodebaseContext> ExploreAsync(string workspacePath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔍 Exploring workspace: {Path}", workspacePath);
        
        var context = new CodebaseContext
        {
            RootPath = workspacePath
        };
        
        // Check if workspace exists and has content
        if (!Directory.Exists(workspacePath))
        {
            _logger.LogWarning("⚠️ Workspace does not exist: {Path}", workspacePath);
            context.IsEmpty = true;
            return context;
        }
        
        try
        {
            // 1. Scan directory structure
            await ScanDirectoryStructureAsync(workspacePath, context, cancellationToken);
            
            // 2. Find and read .csproj files
            await AnalyzeCsprojFilesAsync(workspacePath, context, cancellationToken);
            
            // 3. Read key files for pattern matching
            await ReadKeyFilesAsync(workspacePath, context, cancellationToken);
            
            // 4. Extract patterns from code files
            await ExtractPatternsAsync(workspacePath, context, cancellationToken);
            
            _logger.LogInformation("✅ Workspace exploration complete: {Dirs} dirs, {Files} files, {Namespaces} namespaces",
                context.Directories.Count, context.Files.Count, context.Namespaces.Count);
            
            context.IsEmpty = context.Files.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to explore workspace");
            context.IsEmpty = true;
        }
        
        return context;
    }
    
    /// <summary>
    /// Scan directory structure (skip bin, obj, node_modules, etc.)
    /// </summary>
    private async Task ScanDirectoryStructureAsync(string path, CodebaseContext context, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try
            {
                ScanDirectoryRecursive(path, path, context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan directory structure");
            }
        }, cancellationToken);
    }
    
    private void ScanDirectoryRecursive(string currentPath, string basePath, CodebaseContext context)
    {
        var dirName = Path.GetFileName(currentPath);
        
        // Skip ignored directories
        if (ShouldIgnoreDirectory(dirName))
        {
            return;
        }
        
        // Add relative path
        var relativePath = Path.GetRelativePath(basePath, currentPath).Replace("\\", "/");
        if (relativePath != ".")
        {
            context.Directories.Add(relativePath);
        }
        
        // Add files
        try
        {
            foreach (var file in Directory.GetFiles(currentPath))
            {
                var fileName = Path.GetFileName(file);
                if (!ShouldIgnoreFile(fileName))
                {
                    var relativeFile = Path.GetRelativePath(basePath, file).Replace("\\", "/");
                    context.Files.Add(relativeFile);
                }
            }
            
            // Recurse subdirectories
            foreach (var subDir in Directory.GetDirectories(currentPath))
            {
                ScanDirectoryRecursive(subDir, basePath, context);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
    }
    
    /// <summary>
    /// Analyze .csproj files to extract project type, framework, dependencies
    /// </summary>
    private async Task AnalyzeCsprojFilesAsync(string path, CodebaseContext context, CancellationToken cancellationToken)
    {
        var csprojFiles = Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories);
        
        foreach (var csprojFile in csprojFiles)
        {
            if (ShouldIgnoreDirectory(Path.GetDirectoryName(csprojFile) ?? ""))
            {
                continue;
            }
            
            try
            {
                var content = await File.ReadAllTextAsync(csprojFile, cancellationToken);
                var xml = XDocument.Parse(content);
                
                // Extract target framework
                var targetFramework = xml.Descendants("TargetFramework").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(targetFramework))
                {
                    context.TargetFramework = targetFramework;
                }
                
                // Detect project type from SDK
                var sdk = xml.Root?.Attribute("Sdk")?.Value ?? "";
                if (sdk.Contains("Microsoft.NET.Sdk.Web"))
                {
                    if (content.Contains("Microsoft.AspNetCore.Components.WebAssembly"))
                    {
                        context.ProjectType = "blazor-wasm";
                    }
                    else if (content.Contains("Microsoft.AspNetCore.Components"))
                    {
                        context.ProjectType = "blazor-server";
                    }
                    else
                    {
                        context.ProjectType = "webapi";
                    }
                }
                else if (sdk.Contains("Microsoft.NET.Sdk"))
                {
                    context.ProjectType = "console";
                }
                
                // Extract NuGet packages
                foreach (var packageRef in xml.Descendants("PackageReference"))
                {
                    var packageName = packageRef.Attribute("Include")?.Value;
                    var version = packageRef.Attribute("Version")?.Value;
                    if (!string.IsNullOrEmpty(packageName))
                    {
                        context.Dependencies.Add(version != null 
                            ? $"{packageName} ({version})" 
                            : packageName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse .csproj file: {File}", csprojFile);
            }
        }
    }
    
    /// <summary>
    /// Read key files in full for pattern matching
    /// </summary>
    private async Task ReadKeyFilesAsync(string path, CodebaseContext context, CancellationToken cancellationToken)
    {
        foreach (var keyFile in KeyFiles)
        {
            var files = Directory.GetFiles(path, keyFile, SearchOption.AllDirectories);
            
            foreach (var file in files)
            {
                if (ShouldIgnoreDirectory(Path.GetDirectoryName(file) ?? ""))
                {
                    continue;
                }
                
                try
                {
                    var content = await File.ReadAllTextAsync(file, cancellationToken);
                    var relativePath = Path.GetRelativePath(path, file).Replace("\\", "/");
                    context.KeyFileContents[relativePath] = content;
                    
                    _logger.LogDebug("📄 Read key file: {File} ({Length} chars)", relativePath, content.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read key file: {File}", file);
                }
            }
        }
    }
    
    /// <summary>
    /// Extract patterns from C# files: namespaces, service registrations, imports
    /// </summary>
    private async Task ExtractPatternsAsync(string path, CodebaseContext context, CancellationToken cancellationToken)
    {
        var csFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ShouldIgnoreDirectory(Path.GetDirectoryName(f) ?? ""))
            .Take(100); // Limit to avoid reading too many files
        
        foreach (var csFile in csFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(csFile, cancellationToken);
                
                // Extract namespaces
                var namespaceMatches = Regex.Matches(content, @"namespace\s+([\w\.]+)");
                foreach (Match match in namespaceMatches)
                {
                    context.Namespaces.Add(match.Groups[1].Value);
                }
                
                // Extract service registrations from Program.cs/Startup.cs
                if (csFile.EndsWith("Program.cs") || csFile.EndsWith("Startup.cs"))
                {
                    var serviceMatches = Regex.Matches(content, 
                        @"builder\.Services\.Add\w+<[^>]+>\([^)]*\)|services\.Add\w+<[^>]+>\([^)]*\)");
                    foreach (Match match in serviceMatches)
                    {
                        context.ServiceRegistrations.Add(match.Value.Trim());
                    }
                }
                
                // Extract using statements
                var usingMatches = Regex.Matches(content, @"using\s+([\w\.]+);");
                foreach (Match match in usingMatches)
                {
                    context.CommonImports.Add(match.Groups[1].Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract patterns from: {File}", csFile);
            }
        }
        
        // Extract imports from _Imports.razor
        var importsFiles = Directory.GetFiles(path, "_Imports.razor", SearchOption.AllDirectories);
        foreach (var importsFile in importsFiles)
        {
            if (ShouldIgnoreDirectory(Path.GetDirectoryName(importsFile) ?? ""))
            {
                continue;
            }
            
            try
            {
                var content = await File.ReadAllTextAsync(importsFile, cancellationToken);
                var usingMatches = Regex.Matches(content, @"@using\s+([\w\.]+)");
                foreach (Match match in usingMatches)
                {
                    context.CommonImports.Add(match.Groups[1].Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read _Imports.razor: {File}", importsFile);
            }
        }
    }
    
    private static bool ShouldIgnoreDirectory(string dirName)
    {
        var ignoredDirs = new[] { "bin", "obj", "node_modules", ".git", ".vs", ".idea", "packages", "TestResults" };
        return ignoredDirs.Contains(Path.GetFileName(dirName), StringComparer.OrdinalIgnoreCase);
    }
    
    private static bool ShouldIgnoreFile(string fileName)
    {
        var ignoredExtensions = new[] { ".dll", ".exe", ".pdb", ".cache", ".suo", ".user" };
        return ignoredExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);
    }
}


