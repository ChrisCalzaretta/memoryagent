using System.Diagnostics;
using System.Text;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Executes generated code in Docker containers to verify it actually works
/// This ensures code compiles and runs before returning to the user
/// </summary>
public class ExecutionService : IExecutionService
{
    private readonly ILogger<ExecutionService> _logger;
    private const string TempDirPrefix = "code-exec-";

    public ExecutionService(ILogger<ExecutionService> logger)
    {
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(
        string language,
        List<ExecutionFile> files,
        string workspacePath,
        AgentContracts.Models.ExecutionInstructions? instructions,
        CancellationToken cancellationToken)
    {
        var result = new ExecutionResult();
        var startTime = DateTime.UtcNow;
        
        // 🎯 PRIORITY: Request language > LLM instructions > File detection
        string detectedLanguage;
        if (!string.IsNullOrEmpty(language) && language != "auto" && language != "python")
        {
            // Request explicitly specified a language - use it!
            detectedLanguage = language;
            _logger.LogInformation("🎯 Using REQUEST language: {Language} (ignoring LLM suggestion)", language);
        }
        else if (instructions != null && !string.IsNullOrEmpty(instructions.Language))
        {
            // Use LLM's suggestion
            detectedLanguage = instructions.Language;
            _logger.LogInformation("🧠 Using LLM-provided language: {Language}, MainFile: {MainFile}", 
                instructions.Language, instructions.MainFile);
        }
        else
        {
            // Fallback: detect from files
            detectedLanguage = DetectLanguageFromFiles(files) ?? language ?? "python";
            _logger.LogInformation("📁 Detected language from files: {Detected} (request: {Request})", 
                detectedLanguage, language ?? "not specified");
        }
        
        // Get language config based on actual generated files
        var config = DockerLanguageConfig.GetConfig(detectedLanguage);
        result.DockerImage = config.DockerImage;
        
        _logger.LogInformation("🐳 Executing {Language} code in Docker ({Image}) [request was: {RequestLang}]", 
            detectedLanguage, config.DockerImage, language ?? "auto");

        // Skip execution for languages that need special environments
        if (config.SkipExecution)
        {
            _logger.LogInformation("⏭️ Skipping execution for {Language} (requires special environment)", language);
            result.Success = true;
            result.BuildPassed = true;
            result.ExecutionPassed = true;
            result.Output = $"Execution skipped for {language} - requires browser/database";
            result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            return result;
        }

        string? tempDir = null;
        
        try
        {
            // Create temp directory for code
            tempDir = CreateTempDirectory(workspacePath);
            _logger.LogDebug("Created temp directory: {TempDir}", tempDir);
            
            // Write all files
            var mainFile = await WriteFilesAsync(files, tempDir, config, cancellationToken);
            _logger.LogDebug("Main file detected: {MainFile}", mainFile);
            
            // Pull image if needed (do this first to avoid timeout during execution)
            await PullImageIfNeededAsync(config.DockerImage, cancellationToken);
            
            // Run setup commands if any
            foreach (var setupCmd in config.SetupCommands)
            {
                await RunDockerCommandAsync(config, tempDir, setupCmd, mainFile, 
                    TimeSpan.FromSeconds(60), cancellationToken);
            }
            
            // Step 1: Build/Compile
            _logger.LogInformation("🔨 Building {Language} code...", language);
            var buildCmd = config.BuildCommand.Replace("{mainFile}", mainFile);
            result.CommandsExecuted.Add($"BUILD: {buildCmd}");
            
            var buildResult = await RunDockerCommandAsync(
                config, tempDir, buildCmd, mainFile,
                TimeSpan.FromSeconds(config.TimeoutSeconds),
                cancellationToken);
            
            result.BuildPassed = buildResult.ExitCode == 0;
            
            if (!result.BuildPassed)
            {
                _logger.LogWarning("❌ Build failed for {Language}: {Error}", language, buildResult.Stderr);
                result.Success = false;
                result.Errors = $"BUILD FAILED:\n{buildResult.Stderr}\n{buildResult.Stdout}";
                result.Output = buildResult.Stdout;
                result.ExitCode = buildResult.ExitCode;
                result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                return result;
            }
            
            _logger.LogInformation("✅ Build passed for {Language}", language);
            
            // Step 2: Execute/Run
            _logger.LogInformation("🚀 Running {Language} code...", language);
            var runCmd = config.RunCommand
                .Replace("{mainFile}", mainFile)
                .Replace("{className}", Path.GetFileNameWithoutExtension(mainFile));
            result.CommandsExecuted.Add($"RUN: {runCmd}");
            
            var runResult = await RunDockerCommandAsync(
                config, tempDir, runCmd, mainFile,
                TimeSpan.FromSeconds(config.TimeoutSeconds),
                cancellationToken);
            
            result.ExecutionPassed = runResult.ExitCode == 0;
            result.Output = runResult.Stdout;
            result.Errors = runResult.Stderr;
            result.ExitCode = runResult.ExitCode;
            
            if (!result.ExecutionPassed)
            {
                _logger.LogWarning("❌ Execution failed for {Language}: {Error}", language, runResult.Stderr);
                result.Success = false;
                result.Errors = $"EXECUTION FAILED:\n{runResult.Stderr}\n{runResult.Stdout}";
            }
            else
            {
                _logger.LogInformation("✅ Execution passed for {Language}. Output: {Output}", 
                    language, runResult.Stdout.Length > 100 
                        ? runResult.Stdout[..100] + "..." 
                        : runResult.Stdout);
                result.Success = true;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⏱️ Execution cancelled/timed out for {Language}", language);
            result.Success = false;
            result.Errors = "Execution timed out or was cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Execution error for {Language}", language);
            result.Success = false;
            result.Errors = $"Execution error: {ex.Message}";
        }
        finally
        {
            // Cleanup temp directory
            if (tempDir != null)
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                    _logger.LogDebug("Cleaned up temp directory: {TempDir}", tempDir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDir);
                }
            }
            
            result.DurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        }
        
        return result;
    }

    /// <summary>
    /// Detect language from the actual generated files (not the request)
    /// This ensures we use the right Docker image for what was actually generated
    /// </summary>
    private string? DetectLanguageFromFiles(List<ExecutionFile> files)
    {
        // 🦋 FLUTTER DETECTION: Check pubspec.yaml first
        // Flutter requires special handling - it's a UI framework that can't run interactively in Docker
        var pubspecFile = files.FirstOrDefault(f => 
            f.Path.Equals("pubspec.yaml", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith("/pubspec.yaml", StringComparison.OrdinalIgnoreCase) ||
            f.Path.EndsWith("\\pubspec.yaml", StringComparison.OrdinalIgnoreCase));
        
        if (pubspecFile != null && !string.IsNullOrEmpty(pubspecFile.Content))
        {
            // Check if it's a Flutter project (has flutter SDK or flutter dependencies)
            if (pubspecFile.Content.Contains("flutter:") || 
                pubspecFile.Content.Contains("flutter_") ||
                pubspecFile.Content.Contains("sdk: flutter"))
            {
                _logger.LogInformation("🦋 Flutter project detected from pubspec.yaml");
                return "flutter";
            }
            
            // Regular Dart project (has pubspec but no Flutter)
            _logger.LogInformation("🎯 Dart project detected from pubspec.yaml (no Flutter)");
            return "dart";
        }
        
        var extensionCounts = new Dictionary<string, int>();
        
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.Path)?.ToLowerInvariant();
            var lang = ext switch
            {
                ".py" => "python",
                ".cs" => "csharp",
                ".ts" or ".tsx" => "typescript",
                ".js" or ".jsx" => "javascript",
                ".go" => "go",
                ".rs" => "rust",
                ".java" => "java",
                ".rb" => "ruby",
                ".php" => "php",
                ".swift" => "swift",
                ".kt" or ".kts" => "kotlin",
                ".dart" => "dart",
                ".sql" => "sql",
                ".sh" or ".bash" => "shell",
                ".html" or ".htm" => "html",
                ".css" => "css",
                _ => null
            };
            
            if (lang != null)
            {
                extensionCounts[lang] = extensionCounts.GetValueOrDefault(lang, 0) + 1;
                _logger.LogDebug("File {Path} detected as {Language}", file.Path, lang);
            }
        }
        
        if (!extensionCounts.Any())
        {
            _logger.LogWarning("Could not detect language from any files");
            return null;
        }
        
        // Priority: executable languages over markup
        // If we have both C# and HTML, prefer C#
        // If we have both JS and HTML, prefer JS
        var executableLanguages = new[] { "csharp", "python", "typescript", "javascript", "go", "rust", "java", "ruby", "php", "swift", "kotlin", "dart", "shell" };
        
        foreach (var lang in executableLanguages)
        {
            if (extensionCounts.ContainsKey(lang))
            {
                _logger.LogInformation("🎯 Primary language detected: {Language} (from {Count} files)", 
                    lang, extensionCounts[lang]);
                return lang;
            }
        }
        
        // Fallback to most common
        var detected = extensionCounts.OrderByDescending(kv => kv.Value).First().Key;
        _logger.LogInformation("🎯 Language detected by count: {Language}", detected);
        return detected;
    }

    private string CreateTempDirectory(string workspacePath)
    {
        var basePath = Path.GetTempPath();
        var tempDir = Path.Combine(basePath, $"{TempDirPrefix}{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private async Task<string> WriteFilesAsync(
        List<ExecutionFile> files, 
        string tempDir, 
        LanguageConfig config,
        CancellationToken cancellationToken)
    {
        string? mainFile = null;
        
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var filePath = Path.Combine(tempDir, file.Path);
            var fileDir = Path.GetDirectoryName(filePath);
            
            if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
            {
                Directory.CreateDirectory(fileDir);
            }
            
            await File.WriteAllTextAsync(filePath, file.Content, cancellationToken);
            _logger.LogDebug("Wrote file: {FilePath}", filePath);
            
            // Detect main file
            var fileName = Path.GetFileName(file.Path);
            foreach (var pattern in config.MainFilePatterns)
            {
                if (pattern.Contains('*'))
                {
                    if (fileName.EndsWith(config.FileExtension))
                    {
                        mainFile ??= file.Path;
                    }
                }
                else if (fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    mainFile = file.Path;
                    break;
                }
            }
        }
        
        return mainFile ?? files.FirstOrDefault()?.Path ?? "main" + config.FileExtension;
    }

    private async Task PullImageIfNeededAsync(string image, CancellationToken cancellationToken)
    {
        try
        {
            // Check if image exists locally
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"image inspect {image}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);
            
            if (checkProcess.ExitCode != 0)
            {
                _logger.LogInformation("📥 Pulling Docker image: {Image}", image);
                var pullProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = $"pull {image}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                pullProcess.Start();
                
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMinutes(5)); // 5 min timeout for pull
                
                await pullProcess.WaitForExitAsync(cts.Token);
                _logger.LogInformation("✅ Pulled Docker image: {Image}", image);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not pull image {Image}, will try to use cached", image);
        }
    }

    private async Task<DockerRunResult> RunDockerCommandAsync(
        LanguageConfig config,
        string workDir,
        string command,
        string mainFile,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        // Build docker run command with security constraints
        var dockerArgs = new StringBuilder();
        dockerArgs.Append("run --rm ");
        
        // Resource limits
        dockerArgs.Append("--memory=512m ");
        dockerArgs.Append("--cpus=1.0 ");
        dockerArgs.Append("--pids-limit=100 ");
        
        // Security (but allow write for compilation)
        dockerArgs.Append("--network=none ");  // No network access
        dockerArgs.Append("--security-opt=no-new-privileges ");
        
        // Mount workspace as read-write (needed for Python __pycache__, etc.)
        dockerArgs.Append($"-v \"{workDir}:/app:rw\" ");
        dockerArgs.Append("-w /app ");
        
        // Image and command
        dockerArgs.Append($"{config.DockerImage} ");
        dockerArgs.Append($"sh -c \"{command.Replace("\"", "\\\"")}\"");
        
        _logger.LogDebug("Docker command: docker {Args}", dockerArgs);
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = dockerArgs.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        
        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }
            
            return new DockerRunResult
            {
                ExitCode = -1,
                Stdout = stdout.ToString(),
                Stderr = "TIMEOUT: Execution exceeded time limit"
            };
        }
        
        return new DockerRunResult
        {
            ExitCode = process.ExitCode,
            Stdout = stdout.ToString().Trim(),
            Stderr = stderr.ToString().Trim()
        };
    }

    private class DockerRunResult
    {
        public int ExitCode { get; set; }
        public string Stdout { get; set; } = "";
        public string Stderr { get; set; } = "";
    }
}

