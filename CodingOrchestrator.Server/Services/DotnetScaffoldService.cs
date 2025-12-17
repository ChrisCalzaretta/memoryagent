using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Scaffolds .NET projects using `dotnet new` templates.
/// Provides perfect project structure for ALL .NET project types.
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
    Task<ScaffoldResult> ScaffoldProjectAsync(DotnetProjectType projectType, string projectName, CancellationToken cancellationToken);
    
    /// <summary>
    /// Get list of available templates
    /// </summary>
    IReadOnlyList<DotnetTemplate> GetAvailableTemplates();
}

public class DotnetScaffoldService : IDotnetScaffoldService
{
    private readonly ILogger<DotnetScaffoldService> _logger;
    
    // Map of keywords to project templates
    private static readonly Dictionary<string[], DotnetProjectType> KeywordMappings = new()
    {
        // Web APIs (highest priority for enterprise)
        [new[] { "web api", "webapi", "rest api", "api endpoint", "api service", "http api" }] = DotnetProjectType.WebApi,
        [new[] { "minimal api" }] = DotnetProjectType.WebApiMinimal,
        [new[] { "native aot", "aot api" }] = DotnetProjectType.WebApiAot,
        
        // Web Apps
        [new[] { "blazor", "blazor app", "blazor web" }] = DotnetProjectType.Blazor,
        [new[] { "blazor wasm", "webassembly", "blazor standalone" }] = DotnetProjectType.BlazorWasm,
        [new[] { "mvc", "model view controller", "asp.net mvc" }] = DotnetProjectType.Mvc,
        [new[] { "razor pages", "razor page" }] = DotnetProjectType.RazorPages,
        
        // Services
        [new[] { "grpc", "grpc service", "protocol buffer" }] = DotnetProjectType.Grpc,
        [new[] { "worker", "worker service", "background service", "hosted service" }] = DotnetProjectType.Worker,
        
        // Libraries
        [new[] { "class library", "library", "nuget package", "shared library" }] = DotnetProjectType.ClassLib,
        [new[] { "razor class library", "razor library" }] = DotnetProjectType.RazorClassLib,
        
        // Testing
        [new[] { "xunit test", "xunit", "unit test" }] = DotnetProjectType.XUnit,
        [new[] { "nunit test", "nunit" }] = DotnetProjectType.NUnit,
        [new[] { "mstest", "ms test" }] = DotnetProjectType.MSTest,
        
        // Desktop
        [new[] { "winforms", "windows forms" }] = DotnetProjectType.WinForms,
        [new[] { "wpf", "windows presentation" }] = DotnetProjectType.Wpf,
        [new[] { "maui", ".net maui", "cross platform" }] = DotnetProjectType.Maui,
        
        // Mobile (MAUI-based)
        [new[] { "mobile app", "ios app", "android app" }] = DotnetProjectType.Maui,
        
        // Console (default fallback)
        [new[] { "console", "command line", "cli" }] = DotnetProjectType.Console,
    };
    
    // Template configurations
    private static readonly Dictionary<DotnetProjectType, DotnetTemplate> Templates = new()
    {
        [DotnetProjectType.Console] = new("console", "Console App", "Common/Console"),
        [DotnetProjectType.WebApi] = new("webapi", "ASP.NET Core Web API", "Web/API", new[] { "--use-controllers" }, new[] { "Microsoft.OpenApi" }),
        [DotnetProjectType.WebApiMinimal] = new("webapi", "ASP.NET Core Web API (Minimal)", "Web/API"),
        [DotnetProjectType.WebApiAot] = new("webapiaot", "ASP.NET Core Web API (Native AOT)", "Web/API"),
        [DotnetProjectType.Mvc] = new("mvc", "ASP.NET Core MVC", "Web/MVC"),
        [DotnetProjectType.RazorPages] = new("webapp", "ASP.NET Core Razor Pages", "Web/Razor"),
        [DotnetProjectType.Blazor] = new("blazor", "Blazor Web App", "Web/Blazor"),
        [DotnetProjectType.BlazorWasm] = new("blazorwasm", "Blazor WebAssembly Standalone", "Web/Blazor"),
        [DotnetProjectType.Grpc] = new("grpc", "ASP.NET Core gRPC Service", "Web/gRPC"),
        [DotnetProjectType.Worker] = new("worker", "Worker Service", "Common/Worker"),
        [DotnetProjectType.ClassLib] = new("classlib", "Class Library", "Common/Library"),
        [DotnetProjectType.RazorClassLib] = new("razorclasslib", "Razor Class Library", "Web/Library"),
        [DotnetProjectType.XUnit] = new("xunit", "xUnit Test Project", "Test/xUnit"),
        [DotnetProjectType.NUnit] = new("nunit", "NUnit Test Project", "Test/NUnit"),
        [DotnetProjectType.MSTest] = new("mstest", "MSTest Test Project", "Test/MSTest"),
        [DotnetProjectType.WinForms] = new("winforms", "Windows Forms App", "Desktop/WinForms"),
        [DotnetProjectType.Wpf] = new("wpf", "WPF Application", "Desktop/WPF"),
        [DotnetProjectType.Maui] = new("maui", ".NET MAUI App", "Mobile/MAUI"),
        [DotnetProjectType.Solution] = new("sln", "Solution File", "Solution"),
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
        
        // Default to console for simple tasks, WebApi for anything mentioning "service" or "application"
        if (lowerTask.Contains("service") || lowerTask.Contains("application") || lowerTask.Contains("app"))
        {
            _logger.LogInformation("🎯 Defaulting to WebApi for service/application task");
            return DotnetProjectType.WebApi;
        }
        
        _logger.LogInformation("🎯 Defaulting to Console app");
        return DotnetProjectType.Console;
    }

    public async Task<ScaffoldResult> ScaffoldProjectAsync(
        DotnetProjectType projectType, 
        string projectName, 
        CancellationToken cancellationToken)
    {
        if (!Templates.TryGetValue(projectType, out var template))
        {
            return new ScaffoldResult
            {
                Success = false,
                Error = $"Unknown project type: {projectType}"
            };
        }
        
        // Create temp directory for scaffolding
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet_scaffold_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        
        try
        {
            _logger.LogInformation("📦 Scaffolding {Template} project '{Name}' in {Dir}", 
                template.ShortName, projectName, tempDir);
            
            // Build dotnet new command - runs INSIDE Docker container with SDK
            var templateArgs = $"new {template.ShortName} -n {projectName} -o /scaffold";
            if (template.AdditionalArgs?.Any() == true)
            {
                templateArgs += " " + string.Join(" ", template.AdditionalArgs);
            }
            
            // Run dotnet new INSIDE Docker container that has the SDK
            var dockerArgs = $"run --rm -v \"{tempDir}:/scaffold\" memoryagent-dotnet-multi:latest dotnet {templateArgs}";
            
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
            
            if (process.ExitCode != 0)
            {
                _logger.LogError("❌ dotnet new failed: {Error}", error);
                return new ScaffoldResult
                {
                    Success = false,
                    Error = $"dotnet new failed: {error}"
                };
            }
            
            // Add any additional packages required for this template
            if (template.AdditionalPackages?.Any() == true)
            {
                foreach (var package in template.AdditionalPackages)
                {
                    _logger.LogInformation("📦 Adding package: {Package}", package);
                    var addPackageArgs = $"run --rm -v \"{tempDir}:/scaffold\" -w /scaffold/{projectName} memoryagent-dotnet-multi:latest dotnet add package {package}";
                    
                    var addStartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = addPackageArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    using var addProcess = new Process { StartInfo = addStartInfo };
                    addProcess.Start();
                    await addProcess.WaitForExitAsync(cancellationToken);
                    
                    if (addProcess.ExitCode != 0)
                    {
                        _logger.LogWarning("⚠️ Failed to add package {Package}, continuing anyway", package);
                    }
                }
            }
            
            // Collect generated files
            var files = new List<ScaffoldedFile>();
            
            // dotnet new creates files in /scaffold which maps to tempDir
            // Some templates put files in a subdirectory named after the project
            var projectDir = tempDir;
            var subDir = Path.Combine(tempDir, projectName);
            if (Directory.Exists(subDir) && Directory.GetFiles(subDir).Length > 0)
            {
                projectDir = subDir;
            }
            
            foreach (var file in Directory.GetFiles(projectDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(projectDir, file);
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                
                files.Add(new ScaffoldedFile
                {
                    Path = relativePath.Replace('\\', '/'),
                    Content = content,
                    IsGenerated = true
                });
            }
            
            _logger.LogInformation("✅ Scaffolded {Count} files for {Template}", files.Count, template.Name);
            
            return new ScaffoldResult
            {
                Success = true,
                ProjectType = projectType,
                TemplateName = template.Name,
                ProjectName = projectName,
                Files = files
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

    public IReadOnlyList<DotnetTemplate> GetAvailableTemplates()
    {
        return Templates.Values.ToList();
    }
}

public enum DotnetProjectType
{
    Console,
    WebApi,
    WebApiMinimal,
    WebApiAot,
    Mvc,
    RazorPages,
    Blazor,
    BlazorWasm,
    Grpc,
    Worker,
    ClassLib,
    RazorClassLib,
    XUnit,
    NUnit,
    MSTest,
    WinForms,
    Wpf,
    Maui,
    Solution
}

public record DotnetTemplate(
    string ShortName, 
    string Name, 
    string Category, 
    string[]? AdditionalArgs = null,
    string[]? AdditionalPackages = null);

public class ScaffoldResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DotnetProjectType ProjectType { get; set; }
    public string TemplateName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public List<ScaffoldedFile> Files { get; set; } = new();
}

public class ScaffoldedFile
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsGenerated { get; set; }
}

