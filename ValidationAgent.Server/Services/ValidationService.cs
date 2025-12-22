using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;
using ValidationAgent.Server.Clients;

namespace ValidationAgent.Server.Services;

/// <summary>
/// Validates code quality using rules + LLM analysis - with smart model selection
/// NOW WITH: LLM-based model selection + historical learning + exploration!
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IValidationPromptBuilder _promptBuilder;
    private readonly IOllamaClient _ollamaClient;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly IValidationModelSelector _modelSelector;
    private readonly ILogger<ValidationService> _logger;
    private readonly IConfiguration _config;
    
    // Model configuration
    private readonly string _defaultModel;
    private readonly int _ollamaPort;
    
    // Track which model was selected for this validation
    private string _selectedModel = "";
    private bool _isExploration = false;

    public ValidationService(
        IValidationPromptBuilder promptBuilder,
        IOllamaClient ollamaClient,
        IMemoryAgentClient memoryAgent,
        IValidationModelSelector modelSelector,
        ILogger<ValidationService> logger,
        IConfiguration config)
    {
        _promptBuilder = promptBuilder;
        _ollamaClient = ollamaClient;
        _memoryAgent = memoryAgent;
        _modelSelector = modelSelector;
        _logger = logger;
        _config = config;
        
        // Load model configuration (fallback only)
        _defaultModel = config.GetValue<string>("Gpu:ValidationModel") ?? "phi4:latest";
        _ollamaPort = config.GetValue<int>("Ollama:Port", 11434);
    }

    public async Task<ValidateCodeResponse> ValidateAsync(ValidateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating {FileCount} files with rules: {Rules}", 
            request.Files.Count, string.Join(", ", request.Rules));
        
        var startTime = DateTime.UtcNow;
        
        // 🧠 SMART MODEL SELECTION: Use LLM to pick best model based on task + history
        var language = DetectLanguage(request.Files);
        var taskDescription = $"Validate {request.Files.Count} {language} files. Rules: {string.Join(", ", request.Rules)}";
        
        try
        {
            var selection = await _modelSelector.SelectModelAsync(
                taskDescription, language, request.Files.Count, cancellationToken);
            
            _selectedModel = selection.Model;
            _isExploration = selection.IsExploration;
            
            _logger.LogInformation(
                "🧠 Selected model for validation: {Model} (exploration={IsExploration})",
                _selectedModel, _isExploration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model selection failed, using default: {Model}", _defaultModel);
            _selectedModel = _defaultModel;
            _isExploration = false;
        }
        
        var response = new ValidateCodeResponse
        {
            Passed = true,
            Score = 10,
            Issues = new List<ValidationIssue>(),
            Suggestions = new List<string>()
        };

        // Phase 0: 🔨 COMPILATION CHECK (CRITICAL - must compile before anything else!)
        _logger.LogInformation("🔨 Phase 0: Attempting to compile code...");
        var buildErrors = await TryCompileCodeAsync(request, cancellationToken);
        if (!string.IsNullOrEmpty(buildErrors))
        {
            _logger.LogError("❌ Code does not compile! Build errors:\n{BuildErrors}", buildErrors);
            response.BuildErrors = buildErrors;
            response.Score = 0; // CRITICAL: Code that doesn't compile gets score 0!
            response.Passed = false;
            response.Summary = "Code does not compile. Fix compilation errors first.";
            response.Issues.Add(new ValidationIssue
            {
                Severity = "critical",
                Message = "Code does not compile",
                File = "Build",
                Line = 0,
                Suggestion = "Fix compilation errors before proceeding:\n" + buildErrors
            });
            
            _logger.LogWarning("⚠️ Skipping rule-based and LLM validation due to compilation failure");
            return response; // Return immediately - no point validating code that doesn't compile!
        }
        _logger.LogInformation("✅ Phase 0 complete: Code compiles successfully!");

        // Phase 0.5: 🎯 TASK ALIGNMENT CHECK (CRITICAL - does code match the task?)
        _logger.LogInformation("🎯 Phase 0.5: Checking task alignment...");
        var alignmentIssues = await ValidateTaskAlignmentAsync(request, cancellationToken);
        if (alignmentIssues.Any())
        {
            _logger.LogError("❌ Code does not match task requirements! Alignment issues:\n{Issues}", 
                string.Join("\n", alignmentIssues.Select(i => $"- {i.Message}")));
            
            response.Issues.AddRange(alignmentIssues);
            response.Score = 0; // CRITICAL: Wrong code gets score 0!
            response.Passed = false;
            response.Summary = "Generated code does not match task requirements. Critical alignment failure.";
            
            _logger.LogWarning("⚠️ Skipping rule-based and LLM validation due to task alignment failure");
            return response; // Return immediately - no point validating wrong code!
        }
        _logger.LogInformation("✅ Phase 0.5 complete: Code aligns with task!");

        // Phase 1: Rule-based validation (fast, deterministic)
        // ValidationMode: "standard" (default) = relaxed, "enterprise" = strict
        _logger.LogInformation("Using validation mode: {Mode}", request.ValidationMode);
        
        foreach (var file in request.Files)
        {
            var issues = await ValidateFileWithRulesAsync(file, request.Rules, request.ValidationMode, cancellationToken);
            response.Issues.AddRange(issues);
        }

        // Phase 2: LLM validation (deep analysis, quality assessment) - using selected model
        var llmIssues = await ValidateWithLlmAsync(request, cancellationToken);
        response.Issues.AddRange(llmIssues);

        // Calculate score based on all issues
        response.Score = CalculateScore(response.Issues);
        response.Passed = response.Score >= 8; // Require 8/10 to pass

        // Generate summary
        response.Summary = GenerateSummary(response);

        // Add suggestions
        response.Suggestions = GenerateSuggestions(response.Issues);

        _logger.LogInformation("Validation complete: Score={Score}/10, Passed={Passed}, Issues={IssueCount}",
            response.Score, response.Passed, response.Issues.Count);

        // 🧠 Record model performance for learning
        await RecordModelPerformanceAsync(request, response, startTime, cancellationToken);

        return response;
    }
    
    /// <summary>
    /// 🧠 Record validation model performance for future smart selection
    /// </summary>
    private async Task RecordModelPerformanceAsync(
        ValidateCodeRequest request,
        ValidateCodeResponse response,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        try
        {
            // Detect language from files
            var language = DetectLanguage(request.Files);
            var durationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            
            // Determine outcome based on whether validation produced useful results
            var outcome = response.Score >= 8 ? "success" : 
                         response.Score >= 5 ? "partial" : "failure";
            
            // Use the selected model (not the default) for accurate tracking
            var modelUsed = !string.IsNullOrEmpty(_selectedModel) ? _selectedModel : _defaultModel;
            
            await _memoryAgent.RecordModelPerformanceAsync(
                model: modelUsed,
                taskType: "validation",
                succeeded: response.Score >= 5, // Validation "succeeded" if it could analyze the code
                score: response.Score,
                language: language,
                complexity: EstimateComplexity(request.Files),
                iterations: 1,
                durationMs: durationMs,
                errorType: response.Issues.Any(i => i.Severity == "critical") ? "critical_issues" : null,
                taskKeywords: ExtractKeywords(request),
                context: request.Context ?? "default",
                cancellationToken: cancellationToken);
            
            _logger.LogDebug("📊 Recorded validation performance: {Model} = {Outcome} ({Score}/10)",
                modelUsed, outcome, response.Score);
        }
        catch (Exception ex)
        {
            // Non-critical - don't fail validation if recording fails
            _logger.LogWarning(ex, "Failed to record validation model performance (non-critical)");
        }
    }
    
    /// <summary>
    /// 🎯 CRITICAL: Validate that generated code matches the original task requirements
    /// Prevents model hallucination from producing 10/10 code for the WRONG task
    /// </summary>
    private async Task<List<ValidationIssue>> ValidateTaskAlignmentAsync(
        ValidateCodeRequest request, 
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        
        // Skip if no task provided
        if (string.IsNullOrEmpty(request.OriginalTask))
        {
            _logger.LogDebug("No OriginalTask provided, skipping task alignment check");
            return issues;
        }
        
        await Task.CompletedTask; // For async pattern
        
        var task = request.OriginalTask.ToLowerInvariant();
        var detectedLanguage = DetectLanguage(request.Files);
        var requestedLanguage = request.Language?.ToLowerInvariant() ?? "unknown";
        
        _logger.LogInformation("🔍 Checking task alignment: Task mentions '{TaskSnippet}...', detected language: {DetectedLang}, requested: {RequestedLang}",
            request.OriginalTask.Length > 100 ? request.OriginalTask.Substring(0, 100) : request.OriginalTask,
            detectedLanguage, requestedLanguage);
        
        // ════════════════════════════════════════════════════════════════════
        // CHECK 1: LANGUAGE MISMATCH (CRITICAL)
        // If task asks for C# but we got Python, that's a 100% failure
        // ════════════════════════════════════════════════════════════════════
        var languageMismatch = false;
        
        if ((task.Contains("c#") || task.Contains("csharp") || task.Contains(".net") || task.Contains("blazor")) 
            && detectedLanguage != "csharp")
        {
            languageMismatch = true;
            issues.Add(new ValidationIssue
            {
                Severity = "critical",
                File = "TASK_ALIGNMENT",
                Message = $"CRITICAL: Task requested C#/Blazor code, but generated {detectedLanguage} code instead!",
                Suggestion = "Regenerate code using the correct language (C#)",
                Rule = "task_alignment"
            });
        }
        else if (task.Contains("python") && detectedLanguage != "python")
        {
            languageMismatch = true;
            issues.Add(new ValidationIssue
            {
                Severity = "critical",
                File = "TASK_ALIGNMENT",
                Message = $"CRITICAL: Task requested Python code, but generated {detectedLanguage} code instead!",
                Suggestion = "Regenerate code using the correct language (Python)",
                Rule = "task_alignment"
            });
        }
        else if (task.Contains("typescript") && detectedLanguage != "typescript")
        {
            languageMismatch = true;
            issues.Add(new ValidationIssue
            {
                Severity = "critical",
                File = "TASK_ALIGNMENT",
                Message = $"CRITICAL: Task requested TypeScript code, but generated {detectedLanguage} code instead!",
                Suggestion = "Regenerate code using the correct language (TypeScript)",
                Rule = "task_alignment"
            });
        }
        else if (task.Contains("java") && !task.Contains("javascript") && detectedLanguage != "java")
        {
            languageMismatch = true;
            issues.Add(new ValidationIssue
            {
                Severity = "critical",
                File = "TASK_ALIGNMENT",
                Message = $"CRITICAL: Task requested Java code, but generated {detectedLanguage} code instead!",
                Suggestion = "Regenerate code using the correct language (Java)",
                Rule = "task_alignment"
            });
        }
        
        if (languageMismatch)
        {
            _logger.LogError("❌ LANGUAGE MISMATCH: Task requested one language but got another!");
            return issues; // Don't bother with other checks if language is wrong
        }
        
        // ════════════════════════════════════════════════════════════════════
        // CHECK 2: KEY REQUIREMENTS MISSING (HIGH SEVERITY)
        // Extract expected types/classes from task and verify they exist
        // Now checks FILE EXTENSIONS and STRUCTURE, not just keywords!
        // ════════════════════════════════════════════════════════════════════
        var allContent = string.Join("\n", request.Files.Select(f => f.Content)).ToLowerInvariant();
        var allFilePaths = string.Join("\n", request.Files.Select(f => f.Path)).ToLowerInvariant();
        
        // Extract key entities that should exist (case-insensitive search)
        var keyRequirements = new List<(string keyword, string description)>();
        
        // Detect project type and expected components
        if (task.Contains("chess") || task.Contains("game"))
        {
            // For chess games, check for domain concepts (less strict)
            var hasGameLogic = allContent.Contains("board") || allContent.Contains("piece") || 
                              allContent.Contains("move") || allContent.Contains("square");
            
            if (!hasGameLogic)
            {
                keyRequirements.Add(("board/piece/move", "chess game logic"));
            }
        }
        
        // ════════════════════════════════════════════════════════════════════
        // CHECK 3: COMPLETE PROJECT STRUCTURE (CRITICAL!)
        // If task asks for "complete", "full", or "project", validate ALL necessary files exist
        // This applies to ALL technologies: .NET, Flutter, React, Python, Java, etc.
        // ════════════════════════════════════════════════════════════════════
        var requiresCompleteProject = task.Contains("complete") || task.Contains("full") || 
                                     task.Contains("fully functional") || task.Contains("project");
        
        if (requiresCompleteProject)
        {
            // .NET / C# Projects
            if (task.Contains("c#") || task.Contains("csharp") || task.Contains(".net") || 
                task.Contains("blazor") || task.Contains("asp.net"))
            {
                var hasCsproj = request.Files.Any(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
                var hasProgramCs = request.Files.Any(f => f.Path.EndsWith("Program.cs", StringComparison.OrdinalIgnoreCase));
                
                if (!hasCsproj)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = "TASK_ALIGNMENT",
                        Message = "Task requires complete .NET project but .csproj file is missing",
                        Suggestion = "Generate a complete .csproj file with necessary NuGet packages and project configuration",
                        Rule = "task_alignment"
                    });
                }
                
                if (!hasProgramCs)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = "TASK_ALIGNMENT",
                        Message = "Task requires complete .NET project but Program.cs is missing",
                        Suggestion = "Generate Program.cs with necessary service registration and application startup code",
                        Rule = "task_alignment"
                    });
                }
                
                // Blazor-specific checks
                if (task.Contains("blazor"))
                {
                    var hasRazorFiles = request.Files.Any(f => f.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));
                    var hasAppRazor = request.Files.Any(f => f.Path.EndsWith("App.razor", StringComparison.OrdinalIgnoreCase));
                    
                    if (!hasRazorFiles)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "critical",
                            File = "TASK_ALIGNMENT",
                            Message = "Task requires Blazor project but no .razor files found",
                            Suggestion = "Create Blazor Razor components (.razor files) for the UI",
                            Rule = "task_alignment"
                        });
                    }
                    
                    if (!hasAppRazor)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "high",
                            File = "TASK_ALIGNMENT",
                            Message = "Task requires complete Blazor project but App.razor is missing",
                            Suggestion = "Generate App.razor with Router configuration",
                            Rule = "task_alignment"
                        });
                    }
                }
            }
            
            // Flutter Projects
            if (task.Contains("flutter") || task.Contains("dart"))
            {
                var hasPubspec = request.Files.Any(f => f.Path.EndsWith("pubspec.yaml", StringComparison.OrdinalIgnoreCase));
                var hasMainDart = request.Files.Any(f => f.Path.EndsWith("main.dart", StringComparison.OrdinalIgnoreCase));
                
                if (!hasPubspec)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = "TASK_ALIGNMENT",
                        Message = "Task requires complete Flutter project but pubspec.yaml is missing",
                        Suggestion = "Generate pubspec.yaml with dependencies and project metadata",
                        Rule = "task_alignment"
                    });
                }
                
                if (!hasMainDart)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = "TASK_ALIGNMENT",
                        Message = "Task requires complete Flutter project but main.dart is missing",
                        Suggestion = "Generate main.dart with MaterialApp and runApp() entry point",
                        Rule = "task_alignment"
                    });
                }
            }
            
            // React/Node Projects
            if (task.Contains("react") || task.Contains("node") || task.Contains("typescript") || task.Contains("javascript"))
            {
                var hasPackageJson = request.Files.Any(f => f.Path.EndsWith("package.json", StringComparison.OrdinalIgnoreCase));
                
                if (!hasPackageJson)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = "TASK_ALIGNMENT",
                        Message = "Task requires complete React/Node project but package.json is missing",
                        Suggestion = "Generate package.json with dependencies, scripts, and project metadata",
                        Rule = "task_alignment"
                    });
                }
            }
            
            // Python Projects
            if (task.Contains("python"))
            {
                var hasRequirements = request.Files.Any(f => 
                    f.Path.EndsWith("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                    f.Path.EndsWith("pyproject.toml", StringComparison.OrdinalIgnoreCase));
                
                if (!hasRequirements)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "high",
                        File = "TASK_ALIGNMENT",
                        Message = "Task requires complete Python project but requirements.txt/pyproject.toml is missing",
                        Suggestion = "Generate requirements.txt or pyproject.toml with project dependencies",
                        Rule = "task_alignment"
                    });
                }
            }
            
            // Java Projects
            if (task.Contains("java") && !task.Contains("javascript"))
            {
                var hasBuildFile = request.Files.Any(f => 
                    f.Path.EndsWith("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                    f.Path.EndsWith("build.gradle", StringComparison.OrdinalIgnoreCase));
                
                if (!hasBuildFile)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = "TASK_ALIGNMENT",
                        Message = "Task requires complete Java project but pom.xml/build.gradle is missing",
                        Suggestion = "Generate pom.xml (Maven) or build.gradle (Gradle) with dependencies",
                        Rule = "task_alignment"
                    });
                }
            }
        }
        
        // Technology-specific checks (even if not "complete" project)
        if (task.Contains("blazor"))
        {
            var hasRazorFiles = request.Files.Any(f => f.Path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase));
            
            if (!hasRazorFiles)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "critical",
                    File = "TASK_ALIGNMENT",
                    Message = "Task requires Blazor components but no .razor files found",
                    Suggestion = "Create Blazor Razor components (.razor files) for the UI",
                    Rule = "task_alignment"
                });
            }
        }
        
        if (task.Contains("api") || task.Contains("rest") || task.Contains("endpoint"))
        {
            keyRequirements.Add(("controller", "API controllers or endpoints"));
        }
        
        if (task.Contains("database") || task.Contains("crud"))
        {
            keyRequirements.Add(("repository", "database repository or data access"));
        }
        
        // Check for missing key requirements (keyword-based, only for non-Blazor)
        var missingRequirements = keyRequirements
            .Where(req => !allContent.Contains(req.keyword.Split('/')[0])) // Check first keyword in compound
            .ToList();
        
        if (missingRequirements.Any())
        {
            foreach (var (keyword, description) in missingRequirements)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "high",
                    File = "TASK_ALIGNMENT",
                    Message = $"Missing expected component: {description}",
                    Suggestion = $"Add {description} to fulfill task requirements",
                    Rule = "task_alignment"
                });
            }
        }
        
        // ════════════════════════════════════════════════════════════════════
        // CHECK 3: UNRELATED CODE DETECTED (CRITICAL)
        // If task is about chess but code mentions "image editor", that's wrong!
        // ════════════════════════════════════════════════════════════════════
        var unrelatedKeywords = new List<(string keyword, string category)>();
        
        // Define unrelated keywords based on task context
        if (task.Contains("chess") || task.Contains("game"))
        {
            // If task is about chess/game, these are suspicious
            if (allContent.Contains("image") && allContent.Contains("resize"))
                unrelatedKeywords.Add(("image processing", "image manipulation"));
            if (allContent.Contains("opencv") || allContent.Contains("cv2"))
                unrelatedKeywords.Add(("OpenCV/computer vision", "image processing library"));
        }
        
        if (task.Contains("api") || task.Contains("service"))
        {
            // If task is about API/service, GUI stuff is suspicious
            if (allContent.Contains("tkinter") || allContent.Contains("pygame") || allContent.Contains("pygame"))
                unrelatedKeywords.Add(("GUI framework", "graphical user interface"));
        }
        
        if (unrelatedKeywords.Any())
        {
            foreach (var (keyword, category) in unrelatedKeywords)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "critical",
                    File = "TASK_ALIGNMENT",
                    Message = $"CRITICAL: Code contains unrelated functionality: {category}",
                    Suggestion = "Regenerate code to match the actual task requirements, not unrelated functionality",
                    Rule = "task_alignment"
                });
            }
        }
        
        if (issues.Any())
        {
            _logger.LogWarning("⚠️ Task alignment issues found: {Count} issues", issues.Count);
        }
        else
        {
            _logger.LogInformation("✅ Task alignment check passed!");
        }
        
        return issues;
    }
    
    /// <summary>
    /// 🔨 CRITICAL: Try to compile the code and return build errors if any
    /// </summary>
    private async Task<string?> TryCompileCodeAsync(ValidateCodeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var language = DetectLanguage(request.Files);
            
            // Only compile for languages that support compilation
            if (language != "csharp" && language != "java" && language != "go" && language != "rust")
            {
                _logger.LogDebug("Skipping compilation for {Language} (interpreted language or not supported)", language);
                return null; // Interpreted languages (Python, JS, TS) don't need compilation
            }
            
            _logger.LogInformation("🔨 Attempting to compile {FileCount} {Language} files...", request.Files.Count, language);
            
            // Create temporary directory for compilation
            var tempDir = Path.Combine(Path.GetTempPath(), $"validation_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            
            try
            {
                // Write all files to temp directory
                foreach (var file in request.Files)
                {
                    var filePath = Path.Combine(tempDir, file.Path);
                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }
                    await File.WriteAllTextAsync(filePath, file.Content, cancellationToken);
                }
                
                // Compile based on language
                string buildOutput;
                switch (language)
                {
                    case "csharp":
                        buildOutput = await CompileCSharpAsync(tempDir, request.Files, cancellationToken);
                        break;
                    case "java":
                        buildOutput = await CompileJavaAsync(tempDir, cancellationToken);
                        break;
                    case "go":
                        buildOutput = await CompileGoAsync(tempDir, cancellationToken);
                        break;
                    case "rust":
                        buildOutput = await CompileRustAsync(tempDir, cancellationToken);
                        break;
                    default:
                        return null; // Unsupported language
                }
                
                // If build output contains errors, return them
                if (!string.IsNullOrEmpty(buildOutput) && 
                    (buildOutput.Contains("error CS") || buildOutput.Contains("error:") || buildOutput.Contains("Build FAILED")))
                {
                    _logger.LogWarning("❌ Compilation failed:\n{BuildOutput}", buildOutput);
                    return buildOutput;
                }
                
                _logger.LogInformation("✅ Compilation successful!");
                return null; // No errors
            }
            finally
            {
                // Cleanup temp directory
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compilation check");
            return $"Compilation check failed: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Compile C# code using dotnet build
    /// </summary>
    private async Task<string> CompileCSharpAsync(string tempDir, List<CodeFile> files, CancellationToken cancellationToken)
    {
        // Check if there's a .csproj file
        var csprojFile = files.FirstOrDefault(f => f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        
        if (csprojFile == null)
        {
            _logger.LogWarning("No .csproj file found - creating a default one");
            // Create a minimal .csproj file
            var projectName = "ValidationProject";
            var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";
            var csprojPath = Path.Combine(tempDir, $"{projectName}.csproj");
            await File.WriteAllTextAsync(csprojPath, csprojContent, cancellationToken);
        }
        
        // Run dotnet build
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build --verbosity quiet --nologo",
            WorkingDirectory = tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            return "Failed to start dotnet build process";
        }
        
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        
        var combinedOutput = output + "\n" + error;
        
        if (process.ExitCode != 0)
        {
            _logger.LogWarning("dotnet build exited with code {ExitCode}", process.ExitCode);
            return combinedOutput;
        }
        
        return string.Empty; // Success
    }
    
    /// <summary>
    /// Compile Java code using javac
    /// </summary>
    private async Task<string> CompileJavaAsync(string tempDir, CancellationToken cancellationToken)
    {
        // Find all .java files
        var javaFiles = Directory.GetFiles(tempDir, "*.java", SearchOption.AllDirectories);
        
        if (javaFiles.Length == 0)
        {
            return "No .java files found";
        }
        
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "javac",
            Arguments = string.Join(" ", javaFiles.Select(f => $"\"{f}\"")),
            WorkingDirectory = tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            return "Failed to start javac process";
        }
        
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        
        return process.ExitCode != 0 ? (output + "\n" + error) : string.Empty;
    }
    
    /// <summary>
    /// Compile Go code using go build
    /// </summary>
    private async Task<string> CompileGoAsync(string tempDir, CancellationToken cancellationToken)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "go",
            Arguments = "build",
            WorkingDirectory = tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            return "Failed to start go build process";
        }
        
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        
        return process.ExitCode != 0 ? (output + "\n" + error) : string.Empty;
    }
    
    /// <summary>
    /// Compile Rust code using cargo build
    /// </summary>
    private async Task<string> CompileRustAsync(string tempDir, CancellationToken cancellationToken)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cargo",
            Arguments = "build",
            WorkingDirectory = tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process == null)
        {
            return "Failed to start cargo build process";
        }
        
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        
        return process.ExitCode != 0 ? (output + "\n" + error) : string.Empty;
    }

    private string DetectLanguage(List<CodeFile> files)
    {
        var extensions = files.Select(f => Path.GetExtension(f.Path).ToLowerInvariant()).ToList();
        
        if (extensions.Any(e => e == ".cs")) return "csharp";
        if (extensions.Any(e => e == ".py")) return "python";
        if (extensions.Any(e => e is ".ts" or ".tsx")) return "typescript";
        if (extensions.Any(e => e is ".js" or ".jsx")) return "javascript";
        if (extensions.Any(e => e == ".dart")) return "dart";
        if (extensions.Any(e => e == ".java")) return "java";
        if (extensions.Any(e => e == ".go")) return "go";
        if (extensions.Any(e => e == ".rs")) return "rust";
        if (extensions.Any(e => e == ".swift")) return "swift";
        if (extensions.Any(e => e == ".kt")) return "kotlin";
        
        return "unknown";
    }
    
    private string EstimateComplexity(List<CodeFile> files)
    {
        var totalLines = files.Sum(f => f.Content.Split('\n').Length);
        var fileCount = files.Count;
        
        if (totalLines < 100 && fileCount <= 2) return "simple";
        if (totalLines < 500 && fileCount <= 5) return "moderate";
        if (totalLines < 1500 && fileCount <= 10) return "complex";
        return "very_complex";
    }
    
    private List<string> ExtractKeywords(ValidateCodeRequest request)
    {
        var keywords = new List<string>();
        
        // Add rule names as keywords
        keywords.AddRange(request.Rules);
        
        // Extract from file paths
        foreach (var file in request.Files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file.Path);
            if (fileName.Contains("Service")) keywords.Add("service");
            if (fileName.Contains("Controller")) keywords.Add("controller");
            if (fileName.Contains("Repository")) keywords.Add("repository");
            if (fileName.Contains("Model")) keywords.Add("model");
            if (fileName.Contains("Test")) keywords.Add("test");
            if (fileName.Contains("Component")) keywords.Add("component");
        }
        
        return keywords.Distinct().ToList();
    }

    /// <summary>
    /// Validate code using LLM for deep analysis
    /// </summary>
    private async Task<List<ValidationIssue>> ValidateWithLlmAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        
        // Build the validation prompt
        var prompt = await _promptBuilder.BuildValidationPromptAsync(request, cancellationToken);
        
        var systemPrompt = @"You are an expert code reviewer. Analyze the provided code and identify issues.

OUTPUT FORMAT - Respond with JSON only:
{
    ""issues"": [
        {
            ""severity"": ""critical|high|warning|info"",
            ""file"": ""path/to/file.cs"",
            ""line"": 42,
            ""message"": ""Description of the issue"",
            ""suggestion"": ""How to fix it"",
            ""rule"": ""category_name""
        }
    ],
    ""summary"": ""Brief overall assessment""
}

RULES:
- critical: Security vulnerabilities, data loss risks, crashes
- high: Bugs, logic errors, missing error handling
- warning: Code smells, performance issues, maintainability
- info: Style issues, minor improvements

Be thorough but fair. Only report real issues.";

        try
        {
            // Use the dynamically selected model
            var modelToUse = !string.IsNullOrEmpty(_selectedModel) ? _selectedModel : _defaultModel;
            _logger.LogInformation("Calling LLM validation with model {Model} (exploration={IsExploration})", 
                modelToUse, _isExploration);
            
            var response = await _ollamaClient.GenerateAsync(
                modelToUse,
                prompt,
                systemPrompt,
                _ollamaPort,
                cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("LLM validation failed: {Error}. Continuing with rule-based only.", response.Error);
                return issues;
            }

            // Parse LLM response
            var parsedIssues = ParseLlmResponse(response.Response);
            issues.AddRange(parsedIssues);
            
            _logger.LogInformation("LLM found {Count} additional issues in {Duration}ms",
                parsedIssues.Count, response.TotalDurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM validation error. Continuing with rule-based only.");
        }

        return issues;
    }

    /// <summary>
    /// Parse LLM JSON response to extract issues
    /// </summary>
    private List<ValidationIssue> ParseLlmResponse(string response)
    {
        var issues = new List<ValidationIssue>();
        
        try
        {
            // Extract JSON from response (may have markdown formatting)
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}", RegexOptions.Singleline);
            if (!jsonMatch.Success)
            {
                _logger.LogWarning("No JSON found in LLM response");
                return issues;
            }
            
            var json = jsonMatch.Value;
            var parsed = JsonSerializer.Deserialize<LlmValidationResponse>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (parsed?.Issues != null)
            {
                foreach (var issue in parsed.Issues)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = issue.Severity ?? "info",
                        File = issue.File,
                        Line = issue.Line,
                        Message = issue.Message ?? "Unknown issue",
                        Suggestion = issue.Suggestion,
                        Rule = $"llm_{issue.Rule ?? "analysis"}"
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM JSON response");
        }
        
        return issues;
    }

    private async Task<List<ValidationIssue>> ValidateFileWithRulesAsync(
        CodeFile file, 
        List<string> rules,
        string validationMode,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        var content = file.Content;
        var lines = content.Split('\n');

        await Task.Delay(10, cancellationToken); // Simulate processing

        // 🌐 LANGUAGE-AWARE VALIDATION: Only run language-specific rules
        var extension = Path.GetExtension(file.Path).ToLowerInvariant();
        var isCSharp = extension == ".cs";
        var isPython = extension == ".py";
        var isJavaScript = extension is ".js" or ".jsx";
        var isTypeScript = extension is ".ts" or ".tsx";
        
        // Skip C# rules for non-C# files - let LLM handle language-specific validation
        if (!isCSharp)
        {
            _logger.LogDebug("Skipping C# rule-based validation for {Extension} file: {Path}", extension, file.Path);
            
            // Add basic universal checks that apply to ALL languages
            issues.AddRange(ValidateUniversalRules(file, content, lines, rules));
            
            return issues;
        }
        
        _logger.LogDebug("Running C# rule-based validation for: {Path} (mode={Mode})", file.Path, validationMode);

        // ============================================
        // C# SPECIFIC RULES (only for .cs files)
        // ============================================
        // TIERED VALIDATION:
        // - "standard" (default): Only critical issues - bugs, security, syntax errors
        // - "enterprise": Full strict mode with all best practices
        var isEnterprise = validationMode == "enterprise";
        
        // ════════════════════════════════════════════════════════════════════
        // STANDARD MODE: Critical issues only (bugs, security, syntax errors)
        // These checks run in BOTH modes
        // ════════════════════════════════════════════════════════════════════
        
        // Security checks (ALWAYS run - critical)
        if (rules.Contains("security"))
        {
            // Check for SQL injection vulnerabilities
            if (content.Contains("SELECT") && content.Contains("+ "))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "critical",
                    File = file.Path,
                    Message = "Potential SQL injection vulnerability",
                    Suggestion = "Use parameterized queries instead of string concatenation",
                    Rule = "security"
                });
            }

            // Check for hardcoded secrets
            if (content.Contains("password") && content.Contains("\"") && content.Contains("="))
            {
                var hasLiteralPassword = lines.Any(l => 
                    l.Contains("password", StringComparison.OrdinalIgnoreCase) && 
                    l.Contains("\"") && 
                    !l.TrimStart().StartsWith("//") &&
                    !l.Contains("Configuration") &&
                    !l.Contains("Options"));
                    
                if (hasLiteralPassword)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = file.Path,
                        Message = "Potential hardcoded password detected",
                        Suggestion = "Use configuration or secrets management",
                        Rule = "security"
                    });
                }
            }
            
            // Check for Process.Start with user input (command injection)
            if (content.Contains("Process.Start") && (content.Contains("+ ") || content.Contains("$\"")))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "critical",
                    File = file.Path,
                    Message = "Potential command injection vulnerability",
                    Suggestion = "Sanitize user input before passing to Process.Start",
                    Rule = "security"
                });
            }
        }
        
        // Syntax/Bug checks (ALWAYS run - these catch real bugs)
        if (rules.Contains("best_practices"))
        {
            // Check for proper using statements (resource leaks are bugs)
            if (content.Contains("IDisposable") && !content.Contains("using ") && !content.Contains("await using") && !content.Contains("Dispose()"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    File = file.Path,
                    Message = "IDisposable resources should be properly disposed",
                    Suggestion = "Use 'using' or 'await using' statements, or implement IDisposable pattern",
                    Rule = "best_practices"
                });
            }
            
            // Check for empty catch blocks (swallowed exceptions are bugs)
            if (System.Text.RegularExpressions.Regex.IsMatch(content, @"catch\s*\([^)]*\)\s*\{\s*\}"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    File = file.Path,
                    Message = "Empty catch block swallows exceptions",
                    Suggestion = "Log the exception or rethrow it. Avoid empty catch blocks.",
                    Rule = "best_practices"
                });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ENTERPRISE MODE ONLY: Best practice checks (strict)
        // These only run when ValidationMode = "enterprise"
        // ════════════════════════════════════════════════════════════════════
        if (isEnterprise && rules.Contains("best_practices"))
        {
            _logger.LogDebug("Running ENTERPRISE validation rules for: {Path}", file.Path);
            
            // Check for null checks (enterprise only)
            if (content.Contains("public") && !content.Contains("null"))
            {
                if (content.Contains("string ") || content.Contains("object ") || content.Contains("?"))
                {
                    if (!content.Contains("ArgumentNullException") && !content.Contains("?? throw"))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "warning",
                            File = file.Path,
                            Message = "[Enterprise] Consider adding null checks for nullable parameters",
                            Suggestion = "Use 'ArgumentNullException.ThrowIfNull()' or null-coalescing operators",
                            Rule = "best_practices"
                        });
                    }
                }
            }

            // Check for XML documentation (enterprise only)
            if (content.Contains("public class") || content.Contains("public interface"))
            {
                if (!content.Contains("/// <summary>"))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "info",
                        File = file.Path,
                        Message = "[Enterprise] Public types should have XML documentation",
                        Suggestion = "Add /// <summary> comments to public classes and methods",
                        Rule = "best_practices"
                    });
                }
            }

            // Check for async without CancellationToken (enterprise only)
            if (content.Contains("async Task") && !content.Contains("CancellationToken"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    File = file.Path,
                    Message = "[Enterprise] Async methods should accept CancellationToken",
                    Suggestion = "Add 'CancellationToken cancellationToken = default' parameter",
                    Rule = "best_practices"
                });
            }
        }

        // ENTERPRISE MODE ONLY: Pattern checks
        if (isEnterprise && rules.Contains("patterns"))
        {
            // Check for proper DI (enterprise only)
            if (content.Contains("new ") && content.Contains("Service(") && !content.Contains("Test"))
            {
                if (!file.Path.Contains("Test") && !file.Path.Contains("Program.cs"))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "info",
                        File = file.Path,
                        Message = "[Enterprise] Consider using dependency injection instead of direct instantiation",
                        Suggestion = "Inject dependencies through constructor",
                        Rule = "patterns"
                    });
                }
            }
        }

        return issues;
    }

    private int CalculateScore(List<ValidationIssue> issues)
    {
        var score = 10;

        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                "critical" => 3,
                "high" => 2,
                "warning" => 1,
                "info" => 0,
                _ => 0
            };
        }

        return Math.Max(0, Math.Min(10, score));
    }

    private string GenerateSummary(ValidateCodeResponse response)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header with score
        if (response.Score == 10)
        {
            sb.AppendLine("Code passes all validation checks. Excellent quality!");
        }
        else if (response.Score >= 8)
        {
            sb.AppendLine($"Code quality is good (Score: {response.Score}/10). Minor improvements suggested.");
        }
        else if (response.Score >= 5)
        {
            sb.AppendLine($"Code needs improvement (Score: {response.Score}/10). Please address the issues below:");
        }
        else
        {
            sb.AppendLine($"Code has significant issues (Score: {response.Score}/10). Critical fixes required:");
        }
        
        // Include specific issues in summary
        if (response.Issues.Any())
        {
            sb.AppendLine();
            sb.AppendLine("ISSUES FOUND:");
            
            // Group by severity
            var criticalIssues = response.Issues.Where(i => i.Severity == "critical").ToList();
            var highIssues = response.Issues.Where(i => i.Severity == "high").ToList();
            var warningIssues = response.Issues.Where(i => i.Severity == "warning").ToList();
            var infoIssues = response.Issues.Where(i => i.Severity == "info").ToList();
            
            if (criticalIssues.Any())
            {
                sb.AppendLine($"  CRITICAL ({criticalIssues.Count}):");
                foreach (var issue in criticalIssues.Take(3))
                {
                    sb.AppendLine($"    - {issue.Message}");
                    if (!string.IsNullOrEmpty(issue.Suggestion))
                        sb.AppendLine($"      Fix: {issue.Suggestion}");
                }
            }
            
            if (highIssues.Any())
            {
                sb.AppendLine($"  HIGH ({highIssues.Count}):");
                foreach (var issue in highIssues.Take(3))
                {
                    sb.AppendLine($"    - {issue.Message}");
                    if (!string.IsNullOrEmpty(issue.Suggestion))
                        sb.AppendLine($"      Fix: {issue.Suggestion}");
                }
            }
            
            if (warningIssues.Any())
            {
                sb.AppendLine($"  WARNING ({warningIssues.Count}):");
                foreach (var issue in warningIssues.Take(3))
                {
                    sb.AppendLine($"    - {issue.Message}");
                }
            }
            
            if (infoIssues.Any())
            {
                sb.AppendLine($"  INFO ({infoIssues.Count}):");
                foreach (var issue in infoIssues.Take(2))
                {
                    sb.AppendLine($"    - {issue.Message}");
                }
            }
        }
        
        return sb.ToString().TrimEnd();
    }

    private List<string> GenerateSuggestions(List<ValidationIssue> issues)
    {
        var suggestions = new List<string>();
        var groupedIssues = issues.GroupBy(i => i.Rule);

        foreach (var group in groupedIssues)
        {
            if (group.Key == "best_practices" && group.Count() > 2)
            {
                suggestions.Add("Review best practices guidelines for common patterns");
            }
            if (group.Key == "security" && group.Any(i => i.Severity == "critical"))
            {
                suggestions.Add("CRITICAL: Address security vulnerabilities before proceeding");
            }
            if (group.Key?.StartsWith("llm_") == true)
            {
                suggestions.Add("LLM analysis identified additional improvements - review suggestions");
            }
        }

        if (!issues.Any())
        {
            suggestions.Add("Code looks great! Consider adding unit tests if not already present.");
        }

        return suggestions;
    }
    
    /// <summary>
    /// 🌐 Universal validation rules that apply to ALL languages
    /// These are language-agnostic security and quality checks
    /// </summary>
    private List<ValidationIssue> ValidateUniversalRules(
        CodeFile file, 
        string content, 
        string[] lines,
        List<string> rules)
    {
        var issues = new List<ValidationIssue>();
        
        // Security checks (universal)
        if (rules.Contains("security"))
        {
            // Check for hardcoded secrets (applies to all languages)
            var secretPatterns = new[]
            {
                (@"(?i)(password|passwd|pwd)\s*[=:]\s*[""'][^""']+[""']", "Potential hardcoded password"),
                (@"(?i)(api[_-]?key|apikey)\s*[=:]\s*[""'][^""']+[""']", "Potential hardcoded API key"),
                (@"(?i)(secret|token)\s*[=:]\s*[""'][^""']+[""']", "Potential hardcoded secret/token"),
                (@"(?i)(aws_access_key|aws_secret)", "Potential AWS credentials"),
            };
            
            foreach (var (pattern, message) in secretPatterns)
            {
                if (Regex.IsMatch(content, pattern))
                {
                    // Skip if it's in a comment or looks like a placeholder
                    var match = Regex.Match(content, pattern);
                    if (match.Success && 
                        !match.Value.Contains("your_") && 
                        !match.Value.Contains("xxx") &&
                        !match.Value.Contains("***") &&
                        !match.Value.Contains("PLACEHOLDER"))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "warning",
                            File = file.Path,
                            Message = message,
                            Suggestion = "Use environment variables or a secrets manager",
                            Rule = "security"
                        });
                    }
                }
            }
            
            // SQL injection check (universal - applies to any language using SQL)
            if (content.Contains("SELECT") || content.Contains("INSERT") || content.Contains("UPDATE") || content.Contains("DELETE"))
            {
                // Check for string concatenation in SQL
                if (Regex.IsMatch(content, @"(SELECT|INSERT|UPDATE|DELETE).*\+\s*[""']?\w+[""']?\s*\+"))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = file.Path,
                        Message = "Potential SQL injection vulnerability - string concatenation in SQL query",
                        Suggestion = "Use parameterized queries or an ORM",
                        Rule = "security"
                    });
                }
            }
        }
        
        // Basic quality checks (universal)
        if (rules.Contains("best_practices"))
        {
            // Check for very long lines (applies to all languages)
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length > 200)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "info",
                        File = file.Path,
                        Line = i + 1,
                        Message = "Line exceeds 200 characters",
                        Suggestion = "Consider breaking into multiple lines for readability",
                        Rule = "best_practices"
                    });
                    break; // Only report once per file
                }
            }
            
            // Check for TODO/FIXME comments (informational)
            if (Regex.IsMatch(content, @"(?i)(TODO|FIXME|HACK|XXX):?\s+"))
            {
                var todoCount = Regex.Matches(content, @"(?i)(TODO|FIXME|HACK|XXX):?\s+").Count;
                if (todoCount > 3)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "info",
                        File = file.Path,
                        Message = $"Found {todoCount} TODO/FIXME comments",
                        Suggestion = "Consider addressing these before finalizing",
                        Rule = "best_practices"
                    });
                }
            }
        }
        
        return issues;
    }
}

/// <summary>
/// LLM response structure for validation
/// </summary>
internal class LlmValidationResponse
{
    public List<LlmValidationIssue>? Issues { get; set; }
    public string? Summary { get; set; }
}

internal class LlmValidationIssue
{
    public string? Severity { get; set; }
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Message { get; set; }
    public string? Suggestion { get; set; }
    public string? Rule { get; set; }
}
