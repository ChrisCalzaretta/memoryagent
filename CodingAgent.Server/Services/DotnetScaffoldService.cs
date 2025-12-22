using System.Diagnostics;

namespace CodingAgent.Server.Services;

/// <summary>
/// Scaffolds .NET projects using `dotnet new` templates IN DOCKER.
/// Runs dotnet new inside memoryagent-dotnet-multi container with full SDK.
/// </summary>
public interface IDotnetScaffoldService
{
    /// <summary>
    /// Detect the best project template based on task description
    /// </summary>
    DotnetProjectType DetectProjectType(string taskDescription);
    
    /// <summary>
    /// Scaffold a project using dotnet new and return generated files
    /// </summary>
    Task<DotnetScaffoldResult> ScaffoldProjectAsync(DotnetProjectType projectType, string projectName, CancellationToken cancellationToken);
}

public class DotnetScaffoldService : IDotnetScaffoldService
{
    private readonly ILogger<DotnetScaffoldService> _logger;
    
    // Map of keywords to project templates
    private static readonly Dictionary<string[], DotnetProjectType> KeywordMappings = new()
    {
        // Blazor (highest priority for web UI)
        [new[] { "blazor server", "blazor app", "blazor web" }] = DotnetProjectType.BlazorServer,
        [new[] { "blazor wasm", "webassembly", "blazor standalone" }] = DotnetProjectType.BlazorWasm,
        [new[] { "blazor" }] = DotnetProjectType.BlazorServer, // Default to server
        
        // Web APIs
        [new[] { "web api", "webapi", "rest api", "api endpoint" }] = DotnetProjectType.WebApi,
        [new[] { "minimal api" }] = DotnetProjectType.WebApiMinimal,
        
        // Console (default fallback)
        [new[] { "console", "command line", "cli" }] = DotnetProjectType.Console,
    };
    
    // Template configurations
    private static readonly Dictionary<DotnetProjectType, DotnetTemplate> Templates = new()
    {
        [DotnetProjectType.Console] = new("console", "Console App"),
        [DotnetProjectType.WebApi] = new("webapi", "ASP.NET Core Web API", new[] { "--use-controllers" }),
        [DotnetProjectType.WebApiMinimal] = new("webapi", "ASP.NET Core Web API (Minimal)"),
        [DotnetProjectType.BlazorServer] = new("blazor", "Blazor Server App"),  // .NET 9 uses 'blazor' not 'blazorserver'
        [DotnetProjectType.BlazorWasm] = new("blazor", "Blazor WebAssembly", new[] { "--interactivity", "WebAssembly", "--empty" }),
    };

    public DotnetScaffoldService(ILogger<DotnetScaffoldService> logger)
    {
        _logger = logger;
    }

    public DotnetProjectType DetectProjectType(string taskDescription)
    {
        var lowerTask = taskDescription.ToLowerInvariant();
        
        foreach (var mapping in KeywordMappings)
        {
            if (mapping.Key.Any(keyword => lowerTask.Contains(keyword)))
            {
                _logger.LogInformation("🎯 Detected project type: {Type} from keywords", mapping.Value);
                return mapping.Value;
            }
        }
        
        // Default to console
        _logger.LogInformation("🎯 Defaulting to Console app");
        return DotnetProjectType.Console;
    }

    public async Task<DotnetScaffoldResult> ScaffoldProjectAsync(
        DotnetProjectType projectType, 
        string projectName, 
        CancellationToken cancellationToken)
    {
        if (!Templates.TryGetValue(projectType, out var template))
        {
            return new DotnetScaffoldResult
            {
                Success = false,
                Error = $"Unknown project type: {projectType}"
            };
        }
        
        // Create temp directory for scaffolding
        // Use /data/scaffolds which is mounted from host (Z:\Memory\shared\memory)
        var scaffoldBase = "/data/scaffolds";
        Directory.CreateDirectory(scaffoldBase);
        var tempDir = Path.Combine(scaffoldBase, $"dotnet_scaffold_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            _logger.LogInformation("📦 Scaffolding {Template} project '{Name}' in {Dir}", 
                template.ShortName, projectName, tempDir);
            
            // Build dotnet new command - runs INSIDE Docker container with SDK
            var templateArgs = $"new {template.ShortName} -n {projectName} -o /scaffold --force";  // --force to overwrite existing files
            if (template.AdditionalArgs?.Any() == true)
            {
                templateArgs += " " + string.Join(" ", template.AdditionalArgs);
            }
            
            // Run dotnet new INSIDE Docker container that has the SDK
            var dockerArgs = $"run --rm -v \"{tempDir}:/scaffold\" codingagent-dotnet-multi:latest dotnet {templateArgs}";
            
            _logger.LogInformation("🐳 Running: docker {Args}", dockerArgs);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = dockerArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            
            await process.WaitForExitAsync(cancellationToken);
            
            // Log output even on success for debugging
            if (!string.IsNullOrWhiteSpace(output))
            {
                _logger.LogInformation("📄 Docker stdout: {Output}", output);
            }
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogWarning("⚠️ Docker stderr: {Error}", error);
            }
            
            if (process.ExitCode != 0)
            {
                _logger.LogError("❌ dotnet new failed with exit code {ExitCode}", process.ExitCode);
                return new DotnetScaffoldResult
                {
                    Success = false,
                    Error = $"dotnet new failed: {error}"
                };
            }
            
            // Collect generated files
            var files = new List<ScaffoldedFile>();
            
            // DEBUG: List what was actually created
            _logger.LogInformation("🔍 Checking for scaffolded files in: {TempDir}", tempDir);
            if (Directory.Exists(tempDir))
            {
                var allItems = Directory.GetFileSystemEntries(tempDir, "*", SearchOption.AllDirectories);
                _logger.LogInformation("🔍 Found {Count} items total:", allItems.Length);
                foreach (var item in allItems)
                {
                    var relativePath = Path.GetRelativePath(tempDir, item);
                    var type = Directory.Exists(item) ? "DIR " : "FILE";
                    _logger.LogInformation("  {Type}: {Path}", type, relativePath);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ Temp directory doesn't exist: {TempDir}", tempDir);
            }
            
            // dotnet new creates files in /scaffold which maps to tempDir
            // Some templates put files in a subdirectory named after the project
            var projectDir = tempDir;
            var subDir = Path.Combine(tempDir, projectName);
            if (Directory.Exists(subDir) && Directory.GetFiles(subDir).Length > 0)
            {
                _logger.LogInformation("✅ Using subdirectory: {SubDir}", subDir);
                projectDir = subDir;
            }
            else
            {
                _logger.LogInformation("ℹ️ No subdirectory, using root: {ProjectDir}", projectDir);
            }
            
            foreach (var file in Directory.GetFiles(projectDir, "*", SearchOption.AllDirectories))
            {
                // Skip bin/obj directories
                var relativePath = Path.GetRelativePath(projectDir, file);
                if (relativePath.Contains("bin") || relativePath.Contains("obj"))
                {
                    continue;
                }
                
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                
                files.Add(new ScaffoldedFile
                {
                    Path = relativePath.Replace('\\', '/'),
                    Content = content,
                    IsGenerated = false
                });
            }
            
            _logger.LogInformation("✅ Scaffolded {Count} files for {Template}", files.Count, template.Name);
            
            return new DotnetScaffoldResult
            {
                Success = true,
                ProjectType = projectType.ToString(),
                TemplateName = template.Name,
                ProjectName = projectName,
                Files = files,
                Command = $"dotnet {templateArgs}"
            };
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp directory: {Dir}", tempDir);
            }
        }
    }
}

public enum DotnetProjectType
{
    Console,
    WebApi,
    WebApiMinimal,
    BlazorServer,
    BlazorWasm
}

public record DotnetTemplate(
    string ShortName, 
    string Name, 
    string[]? AdditionalArgs = null);

public class DotnetScaffoldResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string ProjectType { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public List<ScaffoldedFile> Files { get; set; } = new();
    public string Command { get; set; } = "";
}

