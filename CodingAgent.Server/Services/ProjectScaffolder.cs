using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CodingAgent.Server.Services;

/// <summary>
/// Auto-scaffolds project structures using official CLI tools
/// Smart enough to detect project type and use the right scaffolding command
/// </summary>
public class ProjectScaffolder : IProjectScaffolder
{
    private readonly ILogger<ProjectScaffolder> _logger;
    private readonly string _tempBasePath;

    public ProjectScaffolder(ILogger<ProjectScaffolder> logger)
    {
        _logger = logger;
        _tempBasePath = Path.Combine(Path.GetTempPath(), "scaffold");
        Directory.CreateDirectory(_tempBasePath);
    }

    public async Task<ScaffoldResult> ScaffoldProjectAsync(string task, string? language, CancellationToken cancellationToken)
    {
        var taskLower = task.ToLowerInvariant();
        language = language?.ToLowerInvariant() ?? "";

        _logger.LogInformation("🏗️ Detecting project type for scaffolding...");

        // ════════════════════════════════════════════════════════════════════
        // .NET PROJECTS
        // ════════════════════════════════════════════════════════════════════
        if (taskLower.Contains("blazor") || (taskLower.Contains(".net") && taskLower.Contains("web")))
        {
            if (taskLower.Contains("server"))
            {
                return await ScaffoldDotNetAsync("blazorserver", "BlazorApp", cancellationToken);
            }
            else if (taskLower.Contains("wasm") || taskLower.Contains("webassembly"))
            {
                return await ScaffoldDotNetAsync("blazorwasm", "BlazorApp", cancellationToken);
            }
            else
            {
                // Default to Blazor Server
                return await ScaffoldDotNetAsync("blazorserver", "BlazorApp", cancellationToken);
            }
        }
        else if (taskLower.Contains("asp.net") || taskLower.Contains("web api") || taskLower.Contains("rest api"))
        {
            return await ScaffoldDotNetAsync("webapi", "WebApi", cancellationToken);
        }
        else if ((taskLower.Contains("c#") || taskLower.Contains("csharp") || taskLower.Contains(".net")) && 
                 (taskLower.Contains("console") || taskLower.Contains("cli")))
        {
            return await ScaffoldDotNetAsync("console", "ConsoleApp", cancellationToken);
        }
        else if ((taskLower.Contains("c#") || taskLower.Contains("csharp") || language == "csharp") && 
                 taskLower.Contains("library"))
        {
            return await ScaffoldDotNetAsync("classlib", "Library", cancellationToken);
        }

        // ════════════════════════════════════════════════════════════════════
        // FLUTTER PROJECTS
        // ════════════════════════════════════════════════════════════════════
        else if (taskLower.Contains("flutter") || taskLower.Contains("dart"))
        {
            return await ScaffoldFlutterAsync("flutter_app", cancellationToken);
        }

        // ════════════════════════════════════════════════════════════════════
        // REACT PROJECTS
        // ════════════════════════════════════════════════════════════════════
        else if (taskLower.Contains("react"))
        {
            return await ScaffoldReactAsync("react-app", cancellationToken);
        }

        // ════════════════════════════════════════════════════════════════════
        // NODE PROJECTS
        // ════════════════════════════════════════════════════════════════════
        else if (taskLower.Contains("node") || taskLower.Contains("express"))
        {
            return await ScaffoldNodeAsync("node-app", cancellationToken);
        }

        // ════════════════════════════════════════════════════════════════════
        // NO SCAFFOLDING NEEDED
        // ════════════════════════════════════════════════════════════════════
        _logger.LogInformation("ℹ️ No scaffolding needed for this project type");
        return new ScaffoldResult
        {
            Success = true,
            ProjectType = "none",
            ProjectPath = "",
            Files = new List<ScaffoldedFile>()
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // .NET SCAFFOLDING
    // ════════════════════════════════════════════════════════════════════
    private async Task<ScaffoldResult> ScaffoldDotNetAsync(string template, string projectName, CancellationToken cancellationToken)
    {
        var scaffoldPath = Path.Combine(_tempBasePath, Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(scaffoldPath);

        var command = $"new {template} -n {projectName} -o {scaffoldPath}";
        
        _logger.LogInformation("🏗️ Scaffolding .NET project: dotnet {Command}", command);

        var result = await RunCommandAsync("dotnet", command, scaffoldPath, cancellationToken);

        if (!result.Success)
        {
            return new ScaffoldResult
            {
                Success = false,
                Error = result.Error,
                Command = $"dotnet {command}"
            };
        }

        // Read all generated files
        var files = new List<ScaffoldedFile>();
        await ReadFilesRecursivelyAsync(scaffoldPath, scaffoldPath, files, cancellationToken);

        _logger.LogInformation("✅ Scaffolded .NET {Template} project: {FileCount} files", template, files.Count);

        return new ScaffoldResult
        {
            Success = true,
            ProjectType = $"dotnet-{template}",
            ProjectPath = scaffoldPath,
            Files = files,
            Command = $"dotnet {command}"
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // FLUTTER SCAFFOLDING
    // ════════════════════════════════════════════════════════════════════
    private async Task<ScaffoldResult> ScaffoldFlutterAsync(string projectName, CancellationToken cancellationToken)
    {
        var scaffoldPath = Path.Combine(_tempBasePath, Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(scaffoldPath);

        var command = $"create {projectName}";
        
        _logger.LogInformation("🏗️ Scaffolding Flutter project: flutter {Command}", command);

        var result = await RunCommandAsync("flutter", command, scaffoldPath, cancellationToken);

        if (!result.Success)
        {
            return new ScaffoldResult
            {
                Success = false,
                Error = result.Error,
                Command = $"flutter {command}"
            };
        }

        var files = new List<ScaffoldedFile>();
        var projectPath = Path.Combine(scaffoldPath, projectName);
        await ReadFilesRecursivelyAsync(projectPath, projectPath, files, cancellationToken);

        _logger.LogInformation("✅ Scaffolded Flutter project: {FileCount} files", files.Count);

        return new ScaffoldResult
        {
            Success = true,
            ProjectType = "flutter",
            ProjectPath = projectPath,
            Files = files,
            Command = $"flutter {command}"
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // REACT SCAFFOLDING
    // ════════════════════════════════════════════════════════════════════
    private async Task<ScaffoldResult> ScaffoldReactAsync(string projectName, CancellationToken cancellationToken)
    {
        var scaffoldPath = Path.Combine(_tempBasePath, Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(scaffoldPath);

        var command = $"create-react-app {projectName}";
        
        _logger.LogInformation("🏗️ Scaffolding React project: npx {Command}", command);

        var result = await RunCommandAsync("npx", command, scaffoldPath, cancellationToken);

        if (!result.Success)
        {
            return new ScaffoldResult
            {
                Success = false,
                Error = result.Error,
                Command = $"npx {command}"
            };
        }

        var files = new List<ScaffoldedFile>();
        var projectPath = Path.Combine(scaffoldPath, projectName);
        await ReadFilesRecursivelyAsync(projectPath, projectPath, files, cancellationToken);

        _logger.LogInformation("✅ Scaffolded React project: {FileCount} files", files.Count);

        return new ScaffoldResult
        {
            Success = true,
            ProjectType = "react",
            ProjectPath = projectPath,
            Files = files,
            Command = $"npx {command}"
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // NODE SCAFFOLDING
    // ════════════════════════════════════════════════════════════════════
    private async Task<ScaffoldResult> ScaffoldNodeAsync(string projectName, CancellationToken cancellationToken)
    {
        var scaffoldPath = Path.Combine(_tempBasePath, Guid.NewGuid().ToString("N").Substring(0, 8), projectName);
        Directory.CreateDirectory(scaffoldPath);

        // Create basic package.json
        var packageJson = @"{
  ""name"": """ + projectName + @""",
  ""version"": ""1.0.0"",
  ""description"": """",
  ""main"": ""index.js"",
  ""scripts"": {
    ""start"": ""node index.js"",
    ""test"": ""echo \""Error: no test specified\"" && exit 1""
  },
  ""keywords"": [],
  ""author"": """",
  ""license"": ""ISC""
}";

        var packageJsonPath = Path.Combine(scaffoldPath, "package.json");
        await File.WriteAllTextAsync(packageJsonPath, packageJson, cancellationToken);

        var files = new List<ScaffoldedFile>
        {
            new ScaffoldedFile
            {
                Path = "package.json",
                Content = packageJson,
                IsGenerated = false
            }
        };

        _logger.LogInformation("✅ Scaffolded Node project: {FileCount} files", files.Count);

        return new ScaffoldResult
        {
            Success = true,
            ProjectType = "node",
            ProjectPath = scaffoldPath,
            Files = files,
            Command = "npm init -y"
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPER: RUN CLI COMMAND
    // ════════════════════════════════════════════════════════════════════
    private async Task<(bool Success, string Output, string Error)> RunCommandAsync(
        string executable, 
        string arguments, 
        string workingDirectory, 
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var success = process.ExitCode == 0;
            var outputStr = output.ToString();
            var errorStr = error.ToString();

            if (!success)
            {
                _logger.LogError("❌ Command failed: {Executable} {Args}\nError: {Error}", 
                    executable, arguments, errorStr);
            }

            return (success, outputStr, errorStr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception running command: {Executable} {Args}", executable, arguments);
            return (false, "", ex.Message);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPER: READ FILES RECURSIVELY
    // ════════════════════════════════════════════════════════════════════
    private async Task ReadFilesRecursivelyAsync(
        string directory, 
        string baseDirectory, 
        List<ScaffoldedFile> files, 
        CancellationToken cancellationToken)
    {
        // Skip common ignored directories
        var dirName = Path.GetFileName(directory);
        if (dirName == "bin" || dirName == "obj" || dirName == "node_modules" || 
            dirName == ".git" || dirName == ".vs" || dirName == ".idea")
        {
            return;
        }

        foreach (var file in Directory.GetFiles(directory))
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var relativePath = Path.GetRelativePath(baseDirectory, file);

                files.Add(new ScaffoldedFile
                {
                    Path = relativePath.Replace("\\", "/"),
                    Content = content,
                    IsGenerated = false
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to read file: {File}", file);
            }
        }

        foreach (var subDir in Directory.GetDirectories(directory))
        {
            await ReadFilesRecursivelyAsync(subDir, baseDirectory, files, cancellationToken);
        }
    }
}


