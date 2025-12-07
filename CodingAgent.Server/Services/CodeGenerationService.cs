using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Models;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;

namespace CodingAgent.Server.Services;

/// <summary>
/// Code generation service using Ollama LLM with smart model orchestration
/// - Uses pinned primary model for first attempt
/// - Rotates to different models on validation failure
/// - Dynamically discovers available models from Ollama
/// - Supports ALL programming languages (not just C#!)
/// </summary>
public class CodeGenerationService : ICodeGenerationService
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly IOllamaClient _ollamaClient;
    private readonly IModelOrchestrator _modelOrchestrator;
    private readonly ILogger<CodeGenerationService> _logger;
    
    /// <summary>
    /// Language tag → file extension mapping (supports all common languages)
    /// </summary>
    private static readonly Dictionary<string, string> LanguageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // C#
        ["csharp"] = ".cs", ["cs"] = ".cs", ["c#"] = ".cs",
        
        // JavaScript/TypeScript
        ["javascript"] = ".js", ["js"] = ".js",
        ["typescript"] = ".ts", ["ts"] = ".ts",
        ["tsx"] = ".tsx", ["jsx"] = ".jsx",
        
        // Python
        ["python"] = ".py", ["py"] = ".py", ["python3"] = ".py",
        
        // Web
        ["html"] = ".html", ["htm"] = ".html",
        ["css"] = ".css", ["scss"] = ".scss", ["sass"] = ".scss", ["less"] = ".less",
        
        // JVM
        ["java"] = ".java",
        ["kotlin"] = ".kt", ["kt"] = ".kt",
        ["scala"] = ".scala",
        ["groovy"] = ".groovy",
        
        // Systems
        ["go"] = ".go", ["golang"] = ".go",
        ["rust"] = ".rs", ["rs"] = ".rs",
        ["c"] = ".c", ["cpp"] = ".cpp", ["c++"] = ".cpp", ["cxx"] = ".cpp",
        ["h"] = ".h", ["hpp"] = ".hpp",
        
        // Mobile
        ["swift"] = ".swift",
        ["dart"] = ".dart", ["flutter"] = ".dart",
        ["objectivec"] = ".m", ["objc"] = ".m",
        
        // Scripting
        ["ruby"] = ".rb", ["rb"] = ".rb",
        ["php"] = ".php",
        ["perl"] = ".pl", ["pl"] = ".pl",
        ["lua"] = ".lua",
        ["r"] = ".r",
        
        // Shell
        ["shell"] = ".sh", ["bash"] = ".sh", ["sh"] = ".sh", ["zsh"] = ".sh",
        ["powershell"] = ".ps1", ["ps1"] = ".ps1", ["pwsh"] = ".ps1",
        ["batch"] = ".bat", ["bat"] = ".bat", ["cmd"] = ".cmd",
        
        // Data/Config
        ["sql"] = ".sql", ["mysql"] = ".sql", ["postgresql"] = ".sql", ["pgsql"] = ".sql",
        ["yaml"] = ".yaml", ["yml"] = ".yaml",
        ["json"] = ".json", ["jsonc"] = ".json",
        ["xml"] = ".xml", ["xsl"] = ".xsl", ["xslt"] = ".xslt",
        ["toml"] = ".toml",
        ["ini"] = ".ini",
        ["csv"] = ".csv",
        
        // DevOps/IaC
        ["dockerfile"] = "Dockerfile", ["docker"] = "Dockerfile",
        ["terraform"] = ".tf", ["tf"] = ".tf", ["hcl"] = ".tf",
        ["bicep"] = ".bicep",
        ["makefile"] = "Makefile", ["make"] = "Makefile",
        
        // Docs
        ["markdown"] = ".md", ["md"] = ".md",
        ["text"] = ".txt", ["txt"] = ".txt", ["plaintext"] = ".txt",
        
        // Other
        ["graphql"] = ".graphql", ["gql"] = ".graphql",
        ["proto"] = ".proto", ["protobuf"] = ".proto",
        ["razor"] = ".razor", ["cshtml"] = ".cshtml",
        ["vue"] = ".vue", ["svelte"] = ".svelte",
    };

    public CodeGenerationService(
        IPromptBuilder promptBuilder,
        IOllamaClient ollamaClient,
        IModelOrchestrator modelOrchestrator,
        ILogger<CodeGenerationService> logger)
    {
        _promptBuilder = promptBuilder;
        _ollamaClient = ollamaClient;
        _modelOrchestrator = modelOrchestrator;
        _logger = logger;
    }

    public async Task<GenerateCodeResponse> GenerateAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating code for task: {Task}, Language: {Language}", 
            request.Task, request.Language ?? "auto");

        // Build prompt from Lightning (includes context, patterns, similar solutions)
        var prompt = await _promptBuilder.BuildGeneratePromptAsync(request, cancellationToken);
        _logger.LogDebug("Built prompt with {Length} characters", prompt.Length);

        // 🧠 INTELLIGENT MODEL SELECTION based on task complexity and language
        var (model, port) = await SelectBestModelForTaskAsync(request, cancellationToken);
        
        return await GenerateWithModelAsync(model, port, prompt, request, cancellationToken);
    }

    /// <summary>
    /// Select the best model for the task based on language, complexity, and available models
    /// </summary>
    private async Task<(string Model, int Port)> SelectBestModelForTaskAsync(
        GenerateCodeRequest request, 
        CancellationToken cancellationToken)
    {
        var language = request.Language?.ToLowerInvariant() ?? "auto";
        var task = request.Task.ToLowerInvariant();
        
        // Determine if this is a complex task that needs a bigger model
        var isComplexTask = 
            language is "flutter" or "dart" or "swift" or "kotlin" ||  // Mobile/Desktop frameworks
            task.Contains("multi-file") || task.Contains("multiple file") ||
            task.Contains("full app") || task.Contains("complete app") ||
            task.Contains("project") || task.Contains("blackjack") ||
            (request.PreviousFeedback?.TriedModels?.Count ?? 0) > 0;  // Already tried before = complex
        
        if (isComplexTask)
        {
            _logger.LogInformation("🧠 Complex task detected ({Language}), selecting best available model", language);
            
            // Try to get a bigger/better model
            var selection = await _modelOrchestrator.SelectModelAsync(
                ModelPurpose.CodeGeneration,
                new HashSet<string>(),  // Don't exclude any models on first attempt
                cancellationToken);
            
            if (selection != null)
            {
                var (selectedModel, selectedPort) = selection.Value;
                _logger.LogInformation("✨ Selected {Model} for complex {Language} task", selectedModel, language);
                return (selectedModel, selectedPort);
            }
        }
        
        // Default: use primary model (always loaded, fastest response)
        var (primaryModel, primaryPort) = _modelOrchestrator.GetPrimaryModel();
        _logger.LogInformation("Using primary model {Model} for {Language} task", primaryModel, language);
        return (primaryModel, primaryPort);
    }

    public async Task<GenerateCodeResponse> FixAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fixing code for task: {Task}, Issues: {IssueCount}", 
            request.Task, request.PreviousFeedback?.Issues.Count ?? 0);

        // Build fix prompt from Lightning
        var prompt = await _promptBuilder.BuildFixPromptAsync(request, cancellationToken);

        // Get models that have already been tried (from previous feedback)
        var triedModels = request.PreviousFeedback?.TriedModels ?? new HashSet<string>();
        
        // Try to get a DIFFERENT model for fixing (fresh perspective!)
        var selection = await _modelOrchestrator.SelectModelAsync(
            ModelPurpose.CodeGeneration, 
            triedModels, 
            cancellationToken);
        
        if (selection == null)
        {
            // Fall back to primary model if no alternatives available
            _logger.LogWarning("No alternative models available, using primary model again");
            var (model, port) = _modelOrchestrator.GetPrimaryModel();
            return await GenerateWithModelAsync(model, port, prompt, request, cancellationToken);
        }
        
        var (selectedModel, selectedPort) = selection.Value;
        _logger.LogInformation("Using DIFFERENT model for fix: {Model} (previously tried: {Tried})",
            selectedModel, string.Join(", ", triedModels));
        
        return await GenerateWithModelAsync(selectedModel, selectedPort, prompt, request, cancellationToken);
    }

    private async Task<GenerateCodeResponse> GenerateWithModelAsync(
        string model,
        int port,
        string prompt, 
        GenerateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling {Model} on port {Port} for code generation", model, port);
        
        // Build language-aware system prompt
        var requestedLang = request.Language?.ToLowerInvariant() ?? "auto";
        var langExamples = GetLanguageExamples(requestedLang);
        
        var systemPrompt = $@"You are an expert code generator. Generate production-quality code.

🎯 TARGET LANGUAGE: {requestedLang.ToUpperInvariant()}
You MUST generate code in {requestedLang}. Do NOT use any other language.

OUTPUT FORMAT - You MUST respond with:
1. A brief explanation of your approach
2. One or more code blocks in this format:

```language:path/to/file.ext
// code here
```

{langExamples}

RULES:
- ALWAYS include the file path after the language tag
- ONLY generate {requestedLang} code - NO other languages
- Use proper conventions for {requestedLang}
- Include error handling appropriate for {requestedLang}
- Follow best practices for {requestedLang}";

        try
        {
            var response = await _ollamaClient.GenerateAsync(
                model, 
                prompt, 
                systemPrompt, 
                port, 
                cancellationToken);

            if (!response.Success)
            {
                _logger.LogError("Ollama call failed: {Error}", response.Error);
                return new GenerateCodeResponse
                {
                    Success = false,
                    Error = response.Error ?? "Unknown error from Ollama",
                    ModelUsed = model
                };
            }

            // Parse the LLM response to extract code files
            var fileChanges = ParseCodeBlocks(response.Response);
            var explanation = ExtractExplanation(response.Response);
            
            // 🐳 Parse execution instructions from LLM response
            var executionInstructions = ParseExecutionInstructions(response.Response, fileChanges);

            if (!fileChanges.Any())
            {
                _logger.LogWarning("No code blocks found in LLM response. Response length: {Len}, Preview: {Preview}", 
                    response.Response?.Length ?? 0,
                    response.Response?.Length > 200 ? response.Response[..200] + "..." : response.Response);
                
                // Create a generic file from the whole response if it contains code
                if (!string.IsNullOrWhiteSpace(response.Response) && response.Response.Length > 50)
                {
                    fileChanges.Add(new FileChange
                    {
                        Path = "Services/GeneratedCode.cs",
                        Content = response.Response,
                        Type = FileChangeType.Created,
                        Reason = "Generated from LLM response (no code blocks detected)"
                    });
                    _logger.LogInformation("Created fallback file from raw LLM response");
                }
                else
                {
                    _logger.LogError("LLM response too short or empty to create fallback file");
                }
            }

            _logger.LogInformation("Generated {Count} file(s) using {Model} in {Duration}ms",
                fileChanges.Count, model, response.TotalDurationMs);

            // Record performance for learning
            await _modelOrchestrator.RecordModelPerformanceAsync(
                model, 
                "code_generation", 
                true, 
                10.0, // Will be updated by validator
                cancellationToken);

            return new GenerateCodeResponse
            {
                Success = true,
                FileChanges = fileChanges,
                Explanation = explanation,
                TokensUsed = response.PromptTokens + response.ResponseTokens,
                ModelUsed = model,
                Execution = executionInstructions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating code with {Model}", model);
            return new GenerateCodeResponse
            {
                Success = false,
                Error = ex.Message,
                ModelUsed = model
            };
        }
    }

    /// <summary>
    /// Parse code blocks from LLM response - supports ALL programming languages
    /// Supports formats: ```language:path/to/file.ext or ```language
    /// </summary>
    private List<FileChange> ParseCodeBlocks(string response)
    {
        var files = new List<FileChange>();
        
        // Match code blocks with optional file path: ```language:path or ```language
        // Supports: 3-4 backticks, any language tag (letters, numbers, #, +, -)
        var codeBlockPattern = new Regex(
            @"`{3,4}([a-zA-Z0-9#_+.-]+)?(?::([^\n]+))?\n(.*?)`{3,4}",
            RegexOptions.Singleline);
        
        var matches = codeBlockPattern.Matches(response);
        
        _logger.LogInformation("Found {Count} code blocks in LLM response", matches.Count);
        
        // Track file counts per extension for auto-naming
        var extensionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        foreach (Match match in matches)
        {
            var language = match.Groups[1].Value.Trim();
            var explicitPath = match.Groups[2].Value.Trim();
            var code = match.Groups[3].Value.Trim();
            
            // Skip empty code blocks
            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogDebug("Skipping empty code block");
                continue;
            }
            
            // Determine file path
            string filePath;
            string extension;
            
            if (!string.IsNullOrEmpty(explicitPath))
            {
                // Use explicit path from LLM
                filePath = explicitPath;
                extension = Path.GetExtension(filePath);
                _logger.LogDebug("Using explicit path: {Path}", filePath);
            }
            else if (!string.IsNullOrEmpty(language) && LanguageExtensions.TryGetValue(language, out var ext))
            {
                // Auto-generate path based on known language
                extension = ext;
                extensionCounts.TryGetValue(ext, out var count);
                extensionCounts[ext] = count + 1;
                
                // Smart naming based on language type
                var baseName = GetDefaultFileName(language, count);
                filePath = ext == "Dockerfile" || ext == "Makefile" 
                    ? ext  // Special files without extension
                    : baseName + ext;
                    
                _logger.LogDebug("Auto-generated path for {Language}: {Path}", language, filePath);
            }
            else
            {
                // Unknown language - keep it anyway, use .txt
                extension = ".txt";
                extensionCounts.TryGetValue(extension, out var count);
                extensionCounts[extension] = count + 1;
                filePath = count == 0 ? "Generated.txt" : $"Generated_{count}.txt";
                
                _logger.LogDebug("Unknown language '{Language}', using: {Path}", language, filePath);
            }
            
            files.Add(new FileChange
            {
                Path = filePath,
                Content = code,
                Type = FileChangeType.Created,
                Reason = !string.IsNullOrEmpty(language) 
                    ? $"Generated {language} code" 
                    : "Generated code"
            });
            
            _logger.LogInformation("Parsed code block: {Language} → {Path} ({Len} chars)", 
                language, filePath, code.Length);
        }
        
        return files;
    }
    
    /// <summary>
    /// Get a sensible default file name based on language
    /// </summary>
    private static string GetDefaultFileName(string language, int index)
    {
        var suffix = index == 0 ? "" : $"_{index}";
        
        return language.ToLowerInvariant() switch
        {
            // C# - use common naming patterns
            "csharp" or "cs" or "c#" => $"Services/GeneratedService{suffix}",
            
            // TypeScript/JavaScript
            "typescript" or "ts" => $"src/generated{suffix}",
            "javascript" or "js" => $"src/generated{suffix}",
            "tsx" => $"src/components/Generated{suffix}",
            "jsx" => $"src/components/Generated{suffix}",
            
            // Python
            "python" or "py" => $"generated{suffix}",
            
            // Web
            "html" => $"index{suffix}",
            "css" or "scss" => $"styles{suffix}",
            
            // Config files
            "yaml" or "yml" => $"config{suffix}",
            "json" => $"config{suffix}",
            "xml" => $"config{suffix}",
            
            // SQL
            "sql" => $"scripts/query{suffix}",
            
            // Shell
            "shell" or "bash" or "sh" => $"scripts/script{suffix}",
            "powershell" or "ps1" => $"scripts/script{suffix}",
            
            // Dart (non-Flutter)
            "dart" => $"bin/main{suffix}",
            
            // Flutter - proper structure
            "flutter" => index switch
            {
                0 => "lib/main",
                1 => "pubspec",  // Will get .yaml extension
                _ => $"lib/widgets/widget{suffix}"
            },
            
            // Default
            _ => $"Generated{suffix}"
        };
    }

    /// <summary>
    /// Extract explanation text from before the first code block
    /// </summary>
    private string ExtractExplanation(string response)
    {
        var codeBlockStart = response.IndexOf("```", StringComparison.Ordinal);
        
        if (codeBlockStart > 0)
        {
            return response[..codeBlockStart].Trim();
        }
        
        // If no code blocks, take first 500 chars
        return response.Length > 500 ? response[..500] + "..." : response;
    }
    
    /// <summary>
    /// 🐳 Parse execution instructions from LLM response
    /// Looks for ```execution JSON block with language, mainFile, runCommand, etc.
    /// Falls back to inferring from file types if not provided
    /// </summary>
    private ExecutionInstructions? ParseExecutionInstructions(string response, List<FileChange> files)
    {
        // Try to find ```execution JSON block
        var executionPattern = new Regex(
            @"```execution\s*\n?\s*(\{[^`]*\})\s*\n?```",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        
        var match = executionPattern.Match(response);
        
        if (match.Success)
        {
            try
            {
                var json = match.Groups[1].Value.Trim();
                var instructions = JsonSerializer.Deserialize<ExecutionInstructions>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (instructions != null)
                {
                    _logger.LogInformation("🐳 Parsed execution instructions from LLM: {Language}, {MainFile}", 
                        instructions.Language, instructions.MainFile);
                    return instructions;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse execution JSON from LLM response");
            }
        }
        
        // Fallback: Infer from generated files
        return InferExecutionInstructions(files);
    }
    
    /// <summary>
    /// Infer execution instructions from file types when LLM doesn't provide them
    /// </summary>
    private ExecutionInstructions? InferExecutionInstructions(List<FileChange> files)
    {
        if (!files.Any())
            return null;
        
        // Find primary executable file
        var primaryFile = files.FirstOrDefault(f => 
            f.Path.EndsWith(".py") ||
            f.Path.EndsWith(".js") ||
            f.Path.EndsWith(".ts") ||
            f.Path.EndsWith(".cs") ||
            f.Path.EndsWith(".go") ||
            f.Path.EndsWith(".rs") ||
            f.Path.EndsWith(".rb") ||
            f.Path.EndsWith(".php") ||
            f.Path.EndsWith(".sh"));
        
        if (primaryFile == null)
            return null;
        
        var ext = Path.GetExtension(primaryFile.Path).ToLowerInvariant();
        var (language, buildCmd, runCmd) = ext switch
        {
            ".py" => ("python", $"python -c \"import ast; ast.parse(open('{primaryFile.Path}').read())\"", $"python {primaryFile.Path}"),
            ".js" => ("javascript", $"node --check {primaryFile.Path}", $"node {primaryFile.Path}"),
            ".ts" => ("typescript", $"npx tsc --noEmit {primaryFile.Path}", $"npx tsx {primaryFile.Path}"),
            ".cs" => ("csharp", "dotnet build", "dotnet run"),
            ".go" => ("go", $"go build -o /tmp/app {primaryFile.Path}", "/tmp/app"),
            ".rs" => ("rust", $"rustc {primaryFile.Path} -o /tmp/app", "/tmp/app"),
            ".rb" => ("ruby", $"ruby -c {primaryFile.Path}", $"ruby {primaryFile.Path}"),
            ".php" => ("php", $"php -l {primaryFile.Path}", $"php {primaryFile.Path}"),
            ".sh" => ("shell", $"bash -n {primaryFile.Path}", $"bash {primaryFile.Path}"),
            _ => (null, null, null)
        };
        
        if (language == null)
            return null;
        
        _logger.LogInformation("🐳 Inferred execution instructions: {Language}, {MainFile}", language, primaryFile.Path);
        
        return new ExecutionInstructions
        {
            Language = language,
            MainFile = primaryFile.Path,
            BuildCommand = buildCmd,
            RunCommand = runCmd!
        };
    }

    /// <summary>
    /// Get language-specific examples for the system prompt
    /// </summary>
    private static string GetLanguageExamples(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "flutter" => @"🚨 CRITICAL: GENERATE ALL FILES IN ONE RESPONSE! 🚨

You MUST generate ALL required files in a SINGLE response. Do NOT generate just 1 file.
Include EVERY file needed for a complete, working Flutter app.

FLUTTER FILE STRUCTURE (generate ALL of these):
```yaml:pubspec.yaml
```dart:lib/main.dart  
```dart:lib/models/<name>.dart
```dart:lib/providers/<name>_provider.dart
```dart:lib/screens/<name>_screen.dart

EXAMPLE - Minimum complete app needs:
1. pubspec.yaml - dependencies
2. lib/main.dart - entry point with runApp()
3. lib/models/*.dart - data classes
4. lib/providers/*.dart - state management
5. lib/screens/*.dart - UI screens

⚠️ DO NOT output just 1 file. Output ALL files with complete implementations.",

            "dart" => @"DART EXAMPLES:
- ```dart:bin/main.dart
- ```dart:lib/src/model.dart
- ```yaml:pubspec.yaml",

            "python" => @"PYTHON EXAMPLES:
- ```python:main.py
- ```python:app/services/user_service.py
- ```python:tests/test_service.py
- ```txt:requirements.txt",

            "csharp" or "cs" => @"C# EXAMPLES:
- ```csharp:Services/UserService.cs
- ```csharp:Models/User.cs
- ```csharp:Controllers/UserController.cs",

            "typescript" or "ts" => @"TYPESCRIPT EXAMPLES:
- ```typescript:src/index.ts
- ```typescript:src/components/Button.tsx
- ```typescript:src/services/api.ts",

            "javascript" or "js" => @"JAVASCRIPT EXAMPLES:
- ```javascript:src/index.js
- ```javascript:src/components/App.jsx
- ```javascript:server.js",

            _ => @"EXAMPLES:
- ```csharp:Services/UserService.cs
- ```typescript:src/components/Button.tsx
- ```python:app/services/user_service.py
- ```dart:lib/main.dart
- ```yaml:config.yml"
        };
    }

    /// <summary>
    /// 🧠 Estimate task complexity and recommended iterations
    /// Uses LLM to analyze the task and predict difficulty
    /// </summary>
    public async Task<EstimateComplexityResponse> EstimateComplexityAsync(
        EstimateComplexityRequest request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Estimating complexity for: {Task}", request.Task);
        
        var (model, port) = _modelOrchestrator.GetPrimaryModel();
        
        var systemPrompt = @"You are a task complexity estimator. Analyze coding tasks and estimate their difficulty.

OUTPUT FORMAT - Respond with ONLY this JSON (no other text):
{
  ""complexityLevel"": ""simple|moderate|complex|very_complex"",
  ""recommendedIterations"": <number - suggest based on complexity (50-500 for complex, 1000+ for huge projects)>,
  ""estimatedFiles"": <number of files>,
  ""reasoning"": ""brief explanation""
}

COMPLEXITY GUIDELINES:
- simple (12 iterations): Single file, basic CRUD, simple utilities, small scripts
- moderate (15-20 iterations): 2-3 files, basic patterns, standard services
- complex (25-35 iterations): Multi-file, API integration, state management, UI components
- very_complex (100-500 iterations): Full features, database + API + UI, complex algorithms, multi-service
- massive (500-1000+ iterations): Entire applications, multi-module systems, complete rewrites

ALWAYS respond with valid JSON only.";

        var userPrompt = $@"Estimate the complexity of this coding task:

TASK: {request.Task}
LANGUAGE: {request.Language ?? "not specified"}
CONTEXT: {request.Context ?? "none"}

Respond with JSON only.";

        try
        {
            var response = await _ollamaClient.GenerateAsync(
                model, 
                userPrompt, 
                systemPrompt, 
                port, 
                cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("LLM estimation failed, using heuristic");
                return EstimateFromHeuristics(request.Task);
            }

            // Parse JSON response
            try
            {
                // Clean up response - find JSON object
                var json = response.Response.Trim();
                var startIdx = json.IndexOf('{');
                var endIdx = json.LastIndexOf('}');
                
                if (startIdx >= 0 && endIdx > startIdx)
                {
                    json = json.Substring(startIdx, endIdx - startIdx + 1);
                }
                
                var result = JsonSerializer.Deserialize<EstimateComplexityResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (result != null)
                {
                    // Minimum 12 iterations, no max cap
                    result.RecommendedIterations = Math.Max(result.RecommendedIterations, 12);
                    result.Success = true;
                    
                    _logger.LogInformation("🧠 Complexity estimate: {Level}, {Iterations} iterations, {Files} files",
                        result.ComplexityLevel, result.RecommendedIterations, result.EstimatedFiles);
                    
                    return result;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse complexity JSON, using heuristic");
            }
            
            return EstimateFromHeuristics(request.Task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating complexity");
            return EstimateFromHeuristics(request.Task);
        }
    }
    
    /// <summary>
    /// Fallback heuristic-based complexity estimation
    /// </summary>
    private EstimateComplexityResponse EstimateFromHeuristics(string task)
    {
        var taskLower = task.ToLowerInvariant();
        var wordCount = task.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        
        // Complex keywords
        var complexKeywords = new[] { "api", "database", "authentication", "oauth", "jwt", 
            "integration", "multi", "several", "multiple", "full", "complete", "entire",
            "flutter", "react", "angular", "vue", "blazor", "async", "concurrent",
            "websocket", "real-time", "streaming", "upload", "download", "chart", "graph" };
        
        // Simple keywords
        var simpleKeywords = new[] { "simple", "basic", "hello", "test", "example", 
            "single", "small", "quick", "utility", "helper", "script" };
        
        var complexCount = complexKeywords.Count(k => taskLower.Contains(k));
        var simpleCount = simpleKeywords.Count(k => taskLower.Contains(k));
        
        // Determine complexity
        string level;
        int iterations;
        int files;
        
        if (simpleCount > complexCount && wordCount < 15)
        {
            level = "simple";
            iterations = 12;
            files = 1;
        }
        else if (complexCount >= 3 || wordCount > 50)
        {
            level = "very_complex";
            iterations = 45;
            files = 5;
        }
        else if (complexCount >= 2 || wordCount > 30)
        {
            level = "complex";
            iterations = 30;
            files = 3;
        }
        else
        {
            level = "moderate";
            iterations = 18;
            files = 2;
        }
        
        _logger.LogInformation("🧠 Heuristic estimate: {Level}, {Iterations} iterations (words: {Words}, complex: {Complex}, simple: {Simple})",
            level, iterations, wordCount, complexCount, simpleCount);
        
        return new EstimateComplexityResponse
        {
            Success = true,
            ComplexityLevel = level,
            RecommendedIterations = iterations,
            EstimatedFiles = files,
            Reasoning = $"Heuristic estimate based on task analysis (words: {wordCount}, complexity indicators: {complexCount})"
        };
    }
}
