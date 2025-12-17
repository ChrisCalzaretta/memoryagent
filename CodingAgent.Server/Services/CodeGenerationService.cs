using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Models;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Code generation service using Ollama LLM with smart model orchestration
/// - Uses Anthropic Claude for code generation when ANTHROPIC_API_KEY is configured
/// - Falls back to local Ollama models for code generation if no cloud key
/// - Uses local models for validation, complexity estimation, and planning (cost savings)
/// - Supports ALL programming languages (not just C#!)
/// </summary>
public class CodeGenerationService : ICodeGenerationService
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly IOllamaClient _ollamaClient;
    private readonly IModelOrchestrator _modelOrchestrator;
    private readonly ILlmModelSelector? _llmModelSelector;
    private readonly IAnthropicClient? _anthropicClient;
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
        ILogger<CodeGenerationService> logger,
        ILlmModelSelector? llmModelSelector = null,       // Optional - graceful degradation
        IAnthropicClient? anthropicClient = null)         // Optional - cloud code gen
    {
        _promptBuilder = promptBuilder;
        _ollamaClient = ollamaClient;
        _modelOrchestrator = modelOrchestrator;
        _logger = logger;
        _llmModelSelector = llmModelSelector;
        _anthropicClient = anthropicClient;
        
        if (_anthropicClient?.IsConfigured == true)
        {
            _logger.LogInformation("CODE GENERATION: Using Claude ({Model}) for code generation", 
                _anthropicClient.ModelId);
        }
        else
        {
            _logger.LogInformation("CODE GENERATION: Using local Ollama models (no ANTHROPIC_API_KEY)");
        }
    }

    public async Task<GenerateCodeResponse> GenerateAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating code for task: {Task}, Language: {Language}", 
            request.Task, request.Language ?? "auto");

        // Build prompt from Lightning (includes context, patterns, similar solutions)
        var prompt = await _promptBuilder.BuildGeneratePromptAsync(request, cancellationToken);
        _logger.LogDebug("Built prompt with {Length} characters", prompt.Length);

        // ☁️ Use Anthropic Claude for code generation if configured
        if (_anthropicClient?.IsConfigured == true)
        {
            return await GenerateWithAnthropicAsync(prompt, request, cancellationToken);
        }

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
        
        // Determine complexity for smart model selection
        var isComplexTask = 
            language is "flutter" or "dart" or "swift" or "kotlin" ||  // Mobile/Desktop frameworks
            task.Contains("multi-file") || task.Contains("multiple file") ||
            task.Contains("full app") || task.Contains("complete app") ||
            task.Contains("project") || task.Contains("blackjack") ||
            (request.PreviousFeedback?.TriedModels?.Count ?? 0) > 0;  // Already tried before = complex
        
        var complexity = isComplexTask ? "complex" : "moderate";
        
        // Extract task keywords for better model matching
        var taskKeywords = ExtractTaskKeywords(task);
        
        _logger.LogInformation("🧠 Using smart model selection for {Language} ({Complexity}) task", language, complexity);
        
        // 🧠 Use SMART model selection with historical data, warm models, and LLM confirmation
        var selection = await _modelOrchestrator.SelectBestModelAsync(
            ModelPurpose.CodeGeneration,
            language,
            complexity,
            new HashSet<string>(),  // Don't exclude any models on first attempt
            taskKeywords,
            context: null,  // No specific context
            task,  // Pass full task description for LLM analysis
            _llmModelSelector,  // Pass LLM selector for smart selection
            cancellationToken);
        
        if (selection != null)
        {
            var (selectedModel, selectedPort) = selection.Value;
            _logger.LogInformation("✨ Smart selection chose {Model} for {Language} task", selectedModel, language);
            return (selectedModel, selectedPort);
        }
        
        // Fallback: use primary model (always loaded, fastest response)
        var (primaryModel, primaryPort) = _modelOrchestrator.GetPrimaryModel();
        _logger.LogInformation("Using primary model {Model} for {Language} task (smart selection returned null)", primaryModel, language);
        return (primaryModel, primaryPort);
    }

    public async Task<GenerateCodeResponse> FixAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fixing code for task: {Task}, Issues: {IssueCount}, HasBuildErrors: {HasBuildErrors}", 
            request.Task, request.PreviousFeedback?.Issues.Count ?? 0, request.PreviousFeedback?.HasBuildErrors ?? false);

        string prompt;
        
        // 🔧 USE FOCUSED PROMPT FOR BUILD ERRORS - much smaller, errors at top!
        if (request.PreviousFeedback?.HasBuildErrors == true && request.ExistingFiles?.Any() == true)
        {
            _logger.LogInformation("[BUILD-FIX] Using focused build error fix prompt");
            
            var brokenFiles = request.ExistingFiles.ToDictionary(f => f.Path, f => f.Content);
            prompt = await _promptBuilder.BuildBuildErrorFixPromptAsync(
                request.PreviousFeedback.BuildErrors!,
                brokenFiles,
                request.Language ?? "csharp",
                cancellationToken);
        }
        else
        {
            // Regular fix prompt for validation issues
            prompt = await _promptBuilder.BuildFixPromptAsync(request, cancellationToken);
        }

        // ☁️ Use Anthropic Claude for code fixes if configured
        if (_anthropicClient?.IsConfigured == true)
        {
            return await GenerateWithAnthropicAsync(prompt, request, cancellationToken);
        }

        // Get models that have already been tried (from previous feedback)
        var triedModels = request.PreviousFeedback?.TriedModels ?? new HashSet<string>();
        
        var language = request.Language?.ToLowerInvariant() ?? "auto";
        var taskKeywords = ExtractTaskKeywords(request.Task);
        
        // Local model selection for fixes (with tried models excluded)
        var selection = await _modelOrchestrator.SelectBestModelAsync(
            ModelPurpose.CodeGeneration, 
            language,
            "complex",  // Fixes are considered complex (need careful reasoning)
            triedModels,
            taskKeywords,
            context: null,
            request.Task,
            _llmModelSelector,
            cancellationToken);
        
        if (selection == null)
        {
            // Fall back to primary model if no alternatives available
            _logger.LogWarning("No alternative models available, using primary model again");
            var (model, port) = _modelOrchestrator.GetPrimaryModel();
            return await GenerateWithModelAsync(model, port, prompt, request, cancellationToken);
        }
        
        var (selectedModel, selectedPort) = selection.Value;
        _logger.LogInformation("Smart selection for fix: {Model} (previously tried: {Tried})",
            selectedModel, string.Join(", ", triedModels));
        
        return await GenerateWithModelAsync(selectedModel, selectedPort, prompt, request, cancellationToken);
    }
    
    /// <summary>
    /// ☁️ Generate code using Anthropic Claude API (highest quality)
    /// Automatically escalates to premium model (Opus) after 2 failed attempts with Sonnet
    /// </summary>
    private async Task<GenerateCodeResponse> GenerateWithAnthropicAsync(
        string prompt,
        GenerateCodeRequest request,
        CancellationToken cancellationToken)
    {
        if (_anthropicClient == null || !_anthropicClient.IsConfigured)
        {
            throw new InvalidOperationException("Anthropic client not configured");
        }
        
        // Check if we should escalate to premium model
        // Count how many times we've already tried with Claude (any model)
        var claudeAttempts = request.PreviousFeedback?.TriedModels?
            .Count(m => m.StartsWith("claude:", StringComparison.OrdinalIgnoreCase)) ?? 0;
        
        var usePremium = claudeAttempts >= 2 && _anthropicClient.HasPremiumModel;
        
        if (usePremium)
        {
            _logger.LogWarning("[CLAUDE-ESCALATION] After {Attempts} failed attempts, escalating to premium model", claudeAttempts);
        }
        
        var requestedLang = request.Language?.ToLowerInvariant() ?? "auto";
        var langExamples = GetLanguageExamples(requestedLang);
        
        var systemPrompt = $@"You are Claude, an expert software engineer. Generate production-quality code.

TARGET LANGUAGE: {requestedLang.ToUpperInvariant()}
You MUST generate code in {requestedLang}. Do NOT use any other language.

OUTPUT FORMAT - You MUST respond with:
1. A brief explanation of your approach (2-3 sentences max)
2. One or more code blocks in this EXACT format:

```{requestedLang}:path/to/file.ext
// code here
```

{langExamples}

CRITICAL RULES:
- ALWAYS include the file path after the language tag (e.g., ```csharp:Calculator.cs)
- ONLY generate {requestedLang} code - NO other languages
- Use proper conventions for {requestedLang}
- Include error handling appropriate for {requestedLang}
- Generate complete, working code (no placeholders or TODOs)
- For C#: Use class-based structure with namespace, class, and Main method
- Do NOT use top-level statements for C#";

        var modelBeingUsed = usePremium ? _anthropicClient.PremiumModelId : _anthropicClient.ModelId;
        _logger.LogInformation("[CLAUDE] Generating code with {Model}{Premium}", 
            modelBeingUsed, usePremium ? " [PREMIUM]" : "");
        
        try
        {
            var response = await _anthropicClient.GenerateAsync(systemPrompt, prompt, usePremium, cancellationToken);
            
            // Parse the Claude response to extract code files
            var fileChanges = ParseCodeBlocks(response.Content);
            
            // 🔧 AUTO-INJECT .csproj for C# projects if missing
            fileChanges = EnsureCSharpProjectFile(fileChanges, request.Language);
            
            var explanation = ExtractExplanation(response.Content);
            
            // Parse execution instructions from response
            var executionInstructions = ParseExecutionInstructions(response.Content, fileChanges);
            
            if (!fileChanges.Any())
            {
                _logger.LogWarning("[CLAUDE] No code blocks found. Response length: {Len}, Preview: {Preview}",
                    response.Content.Length,
                    response.Content.Length > 300 ? response.Content[..300] + "..." : response.Content);
                
                // Create fallback file from response if it looks like code
                if (!string.IsNullOrWhiteSpace(response.Content) && response.Content.Length > 50)
                {
                    var extension = LanguageExtensions.TryGetValue(requestedLang, out var ext) ? ext : ".txt";
                    fileChanges.Add(new FileChange
                    {
                        Path = $"GeneratedCode{extension}",
                        Content = response.Content,
                        Type = FileChangeType.Created,
                        Reason = "Fallback - no code blocks detected"
                    });
                }
            }
            
            _logger.LogInformation("[CLAUDE] Generated {Count} file(s), {InputTokens}+{OutputTokens} tokens, ${Cost:F4}",
                fileChanges.Count, response.InputTokens, response.OutputTokens, response.Cost);
            
            return new GenerateCodeResponse
            {
                Success = true,
                FileChanges = fileChanges,
                Explanation = explanation,
                TokensUsed = response.InputTokens + response.OutputTokens,
                ModelUsed = $"claude:{response.Model}",  // Use actual model from response
                Execution = executionInstructions,
                CloudUsage = new CloudGenerationUsage
                {
                    Provider = "anthropic",
                    Model = response.Model,
                    InputTokens = response.InputTokens,
                    OutputTokens = response.OutputTokens,
                    Cost = response.Cost,
                    TokensRemaining = response.TokensRemaining,
                    RequestsRemaining = response.RequestsRemaining
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CLAUDE] Error generating code");
            return new GenerateCodeResponse
            {
                Success = false,
                Error = $"Claude API error: {ex.Message}",
                ModelUsed = $"claude:{_anthropicClient.ModelId}"
            };
        }
    }
    
    /// <summary>
    /// Extract keywords from task for better model matching
    /// </summary>
    private static List<string> ExtractTaskKeywords(string task)
    {
        var keywords = new List<string>();
        var lower = task.ToLowerInvariant();
        
        // Framework keywords
        if (lower.Contains("flutter")) keywords.Add("flutter");
        if (lower.Contains("blazor")) keywords.Add("blazor");
        if (lower.Contains("react")) keywords.Add("react");
        if (lower.Contains("angular")) keywords.Add("angular");
        if (lower.Contains("vue")) keywords.Add("vue");
        if (lower.Contains("maui")) keywords.Add("maui");
        if (lower.Contains("wpf")) keywords.Add("wpf");
        if (lower.Contains("swiftui")) keywords.Add("swiftui");
        if (lower.Contains("jetpack")) keywords.Add("jetpack-compose");
        
        // Task type keywords
        if (lower.Contains("api")) keywords.Add("api");
        if (lower.Contains("database")) keywords.Add("database");
        if (lower.Contains("ui") || lower.Contains("interface")) keywords.Add("ui");
        if (lower.Contains("test")) keywords.Add("testing");
        if (lower.Contains("crud")) keywords.Add("crud");
        if (lower.Contains("auth")) keywords.Add("authentication");
        if (lower.Contains("game")) keywords.Add("game");
        
        return keywords;
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
            
            // 🔧 AUTO-INJECT .csproj for C# projects if missing
            fileChanges = EnsureCSharpProjectFile(fileChanges, request.Language);
            
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
                model: model, 
                taskType: "code_generation", 
                succeeded: true, 
                score: 10.0, // Will be updated by validator
                cancellationToken: cancellationToken);

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
    /// 🔧 AUTO-INJECT .csproj FILE for C# projects when LLM doesn't generate one
    /// This ensures multi-file C# projects can build properly
    /// </summary>
    private List<FileChange> EnsureCSharpProjectFile(List<FileChange> files, string? requestedLanguage)
    {
        // Only process if language is C# or we detected C# files
        var hasCSharpFiles = files.Any(f => f.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
        var isRequestedCSharp = requestedLanguage?.ToLowerInvariant() is "csharp" or "cs" or "c#";
        
        if (!hasCSharpFiles && !isRequestedCSharp)
            return files;
        
        // Extract packages from using statements across all C# files
        var allCode = string.Join("\n", files.Where(f => f.Path.EndsWith(".cs")).Select(f => f.Content));
        var packages = DetectRequiredPackages(allCode);
        
        // Check if .csproj already exists
        var existingCsproj = files.FirstOrDefault(f => 
            f.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        
        if (existingCsproj != null)
        {
            // Enhance existing .csproj with missing packages
            var enhancedCsproj = EnhanceCsprojWithPackages(existingCsproj.Content, packages);
            existingCsproj.Content = enhancedCsproj;
            _logger.LogInformation("🔧 ENHANCED existing {Csproj} with {PackageCount} detected packages", 
                existingCsproj.Path, packages.Count);
            return files;
        }
        
        // Detect if it's a web project
        var isWebProject = allCode.Contains("Microsoft.AspNetCore") || 
                          allCode.Contains("WebApplication") ||
                          allCode.Contains("app.MapGet") ||
                          allCode.Contains("builder.Services");
        
        // Extract project name from first .cs file's namespace or default
        var projectName = ExtractProjectName(files) ?? "GeneratedApp";
        
        // Generate .csproj content
        var csprojContent = GenerateCsprojContent(projectName, packages, isWebProject);
        
        _logger.LogInformation("🔧 AUTO-INJECTED {ProjectName}.csproj with {PackageCount} packages (isWeb: {IsWeb}): {Packages}",
            projectName, packages.Count, isWebProject, string.Join(", ", packages));
        
        // Add .csproj file to the list
        files.Add(new FileChange
        {
            Path = $"{projectName}.csproj",
            Content = csprojContent,
            Type = FileChangeType.Created,
            Reason = "Auto-generated project file for C# compilation"
        });
        
        return files;
    }
    
    /// <summary>
    /// Enhance an existing .csproj with missing packages
    /// </summary>
    private string EnhanceCsprojWithPackages(string csprojContent, HashSet<string> packages)
    {
        if (!packages.Any()) return csprojContent;
        
        // Check which packages are already present
        var missingPackages = packages
            .Where(pkg => !csprojContent.Contains($"Include=\"{pkg}\"", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if (!missingPackages.Any())
        {
            _logger.LogDebug("All detected packages already in .csproj");
            return csprojContent;
        }
        
        _logger.LogInformation("📦 Adding missing packages to .csproj: {Packages}", string.Join(", ", missingPackages));
        
        // Build PackageReference entries
        var packageRefs = new System.Text.StringBuilder();
        foreach (var pkg in missingPackages.OrderBy(p => p))
        {
            packageRefs.AppendLine($"    <PackageReference Include=\"{pkg}\" Version=\"*\" />");
            
            // Add companion packages for test frameworks
            if (pkg.Equals("xunit", StringComparison.OrdinalIgnoreCase))
            {
                packageRefs.AppendLine("    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.5.4\" />");
                packageRefs.AppendLine("    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.8.0\" />");
            }
        }
        
        // Find where to insert packages
        if (csprojContent.Contains("<ItemGroup>"))
        {
            // Insert after first ItemGroup opening
            var insertIndex = csprojContent.IndexOf("<ItemGroup>") + "<ItemGroup>".Length;
            return csprojContent.Insert(insertIndex, "\n" + packageRefs.ToString());
        }
        else
        {
            // No ItemGroup exists, add one before closing Project tag
            var itemGroup = $"\n  <ItemGroup>\n{packageRefs}  </ItemGroup>\n";
            var insertIndex = csprojContent.LastIndexOf("</Project>");
            if (insertIndex > 0)
            {
                return csprojContent.Insert(insertIndex, itemGroup);
            }
        }
        
        return csprojContent;
    }
    
    /// <summary>
    /// Detect NuGet packages required based on using statements
    /// </summary>
    private static HashSet<string> DetectRequiredPackages(string code)
    {
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Package mappings: namespace prefix -> NuGet package name
        var packageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Test Frameworks (CRITICAL - most common missing packages!)
            ["Xunit"] = "xunit",
            ["NUnit"] = "NUnit",
            ["Microsoft.VisualStudio.TestTools.UnitTesting"] = "MSTest.TestFramework",
            ["Moq"] = "Moq",
            ["FluentAssertions"] = "FluentAssertions",
            ["NSubstitute"] = "NSubstitute",
            ["Bogus"] = "Bogus",
            
            // JSON
            ["Newtonsoft.Json"] = "Newtonsoft.Json",
            
            // Microsoft.Extensions.*
            ["Microsoft.Extensions.DependencyInjection"] = "Microsoft.Extensions.DependencyInjection",
            ["Microsoft.Extensions.Logging"] = "Microsoft.Extensions.Logging",
            ["Microsoft.Extensions.Configuration"] = "Microsoft.Extensions.Configuration",
            ["Microsoft.Extensions.Http"] = "Microsoft.Extensions.Http",
            ["Microsoft.Extensions.Hosting"] = "Microsoft.Extensions.Hosting",
            ["Microsoft.Extensions.Options"] = "Microsoft.Extensions.Options",
            ["Microsoft.Extensions.Caching"] = "Microsoft.Extensions.Caching.Memory",
            
            // Data Access
            ["Microsoft.EntityFrameworkCore"] = "Microsoft.EntityFrameworkCore",
            ["Dapper"] = "Dapper",
            ["MongoDB.Driver"] = "MongoDB.Driver",
            ["Npgsql"] = "Npgsql",
            ["MySql.Data"] = "MySql.Data",
            ["Microsoft.Data.SqlClient"] = "Microsoft.Data.SqlClient",
            ["StackExchange.Redis"] = "StackExchange.Redis",
            
            // Common Libraries
            ["CsvHelper"] = "CsvHelper",
            ["Polly"] = "Polly",
            ["FluentValidation"] = "FluentValidation",
            ["MediatR"] = "MediatR",
            ["AutoMapper"] = "AutoMapper",
            ["Serilog"] = "Serilog",
            ["Humanizer"] = "Humanizer",
            
            // API/Web
            ["Swashbuckle"] = "Swashbuckle.AspNetCore",
            ["Microsoft.OpenApi"] = "Microsoft.OpenApi",
            ["NSwag"] = "NSwag.AspNetCore",
            ["RestSharp"] = "RestSharp",
            ["Refit"] = "Refit",
            
            // Built-in (no package needed)
            ["System.Text.Json"] = "",
            ["System.Linq"] = "",
            ["System.Collections"] = "",
        };
        
        // Extract using statements
        var usingPattern = new Regex(@"using\s+([A-Za-z0-9_.]+)\s*;", RegexOptions.Multiline);
        var matches = usingPattern.Matches(code);
        
        foreach (Match match in matches)
        {
            var ns = match.Groups[1].Value;
            foreach (var mapping in packageMap)
            {
                if (ns.StartsWith(mapping.Key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(mapping.Value))
                {
                    packages.Add(mapping.Value);
                }
            }
        }
        
        // Detect packages from method calls (for extension methods that don't need explicit using)
        var methodPatterns = new Dictionary<string, string>
        {
            // Swagger (very common in Web APIs)
            [@"\.AddSwaggerGen|\.UseSwagger|\.UseSwaggerUI"] = "Swashbuckle.AspNetCore",
            [@"Microsoft\.OpenApi"] = "Microsoft.OpenApi",
            // EF Core
            [@"\.UseSqlServer\(|\.UseNpgsql\(|\.UseSqlite\("] = "Microsoft.EntityFrameworkCore",
            // Identity
            [@"\.AddIdentity|\.AddDefaultIdentity"] = "Microsoft.AspNetCore.Identity.EntityFrameworkCore",
            // JWT
            [@"\.AddJwtBearer|JwtBearerDefaults"] = "Microsoft.AspNetCore.Authentication.JwtBearer",
            // Serilog
            [@"\.UseSerilog|Log\.Logger"] = "Serilog.AspNetCore",
        };
        
        foreach (var pattern in methodPatterns)
        {
            if (Regex.IsMatch(code, pattern.Key, RegexOptions.IgnoreCase))
            {
                packages.Add(pattern.Value);
            }
        }
        
        // Add companion packages for test frameworks (they need multiple packages to work)
        if (packages.Contains("xunit"))
        {
            packages.Add("xunit.runner.visualstudio");
            packages.Add("Microsoft.NET.Test.Sdk");
        }
        if (packages.Contains("NUnit"))
        {
            packages.Add("NUnit3TestAdapter");
            packages.Add("Microsoft.NET.Test.Sdk");
        }
        if (packages.Contains("MSTest.TestFramework"))
        {
            packages.Add("MSTest.TestAdapter");
            packages.Add("Microsoft.NET.Test.Sdk");
        }
        
        return packages;
    }
    
    /// <summary>
    /// Extract project name from namespace in C# files
    /// </summary>
    private static string? ExtractProjectName(List<FileChange> files)
    {
        foreach (var file in files.Where(f => f.Path.EndsWith(".cs")))
        {
            // Match: namespace ProjectName; or namespace ProjectName { or namespace ProjectName.SubNs
            var nsMatch = Regex.Match(file.Content, @"namespace\s+([A-Za-z_][A-Za-z0-9_]*)(?:\s*[;{.]|$)", RegexOptions.Multiline);
            if (nsMatch.Success)
            {
                return nsMatch.Groups[1].Value;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Generate .csproj content with detected packages
    /// </summary>
    private static string GenerateCsprojContent(string projectName, HashSet<string> packages, bool isWebProject)
    {
        var sdk = isWebProject ? "Microsoft.NET.Sdk.Web" : "Microsoft.NET.Sdk";
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($@"<Project Sdk=""{sdk}"">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <TargetFramework>net9.0</TargetFramework>");
        if (!isWebProject)
        {
            sb.AppendLine("    <OutputType>Exe</OutputType>");
        }
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("  </PropertyGroup>");
        
        if (packages.Any())
        {
            sb.AppendLine("  <ItemGroup>");
            foreach (var pkg in packages.OrderBy(p => p))
            {
                sb.AppendLine($@"    <PackageReference Include=""{pkg}"" Version=""*"" />");
            }
            sb.AppendLine("  </ItemGroup>");
        }
        
        sb.AppendLine("</Project>");
        
        return sb.ToString();
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
                // SMART naming - extract class/type name from the code itself!
                extension = ext;
                var extractedName = ExtractMainTypeName(code, language);
                
                if (!string.IsNullOrEmpty(extractedName))
                {
                    // Use extracted class name for file path
                    filePath = language.ToLowerInvariant() switch
                    {
                        "csharp" or "cs" or "c#" => extractedName == "Program" ? "Program.cs" : $"{extractedName}.cs",
                        "typescript" or "ts" => $"src/{extractedName}.ts",
                        "javascript" or "js" => $"src/{extractedName}.js",
                        "python" or "py" => $"{extractedName.ToLowerInvariant()}.py",
                        _ => extractedName + ext
                    };
                    _logger.LogInformation("Smart naming: extracted '{TypeName}' → {Path}", extractedName, filePath);
                }
                else
                {
                    // Fallback to default naming
                    extensionCounts.TryGetValue(ext, out var count);
                    extensionCounts[ext] = count + 1;
                    var baseName = GetDefaultFileName(language, count);
                    filePath = ext == "Dockerfile" || ext == "Makefile" 
                        ? ext  
                        : baseName + ext;
                    _logger.LogDebug("Fallback naming for {Language}: {Path}", language, filePath);
                }
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
            
            // Clean the code to remove markdown artifacts
            var cleanedCode = CleanGeneratedCode(code, language);
            
            if (string.IsNullOrWhiteSpace(cleanedCode))
            {
                _logger.LogWarning("Code block for {Path} was empty after cleanup, skipping", filePath);
                continue;
            }
            
            files.Add(new FileChange
            {
                Path = filePath,
                Content = cleanedCode,
                Type = FileChangeType.Created,
                Reason = !string.IsNullOrEmpty(language) 
                    ? $"Generated {language} code" 
                    : "Generated code"
            });
            
            _logger.LogInformation("Parsed code block: {Language} → {Path} ({Len} chars, cleaned from {OrigLen})", 
                language, filePath, cleanedCode.Length, code.Length);
        }
        
        return files;
    }
    
    /// <summary>
    /// Clean generated code by removing markdown artifacts and common LLM output problems
    /// </summary>
    private static string CleanGeneratedCode(string code, string language)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;
            
        var cleaned = code;
        
        // Remove any nested markdown code fences (LLM outputting ```csharp inside code)
        // Match opening fences like ```csharp, ```cs, ```python, etc.
        cleaned = Regex.Replace(cleaned, @"^```[a-zA-Z0-9#_+.-]*\s*$", "", RegexOptions.Multiline);
        
        // Remove closing fences
        cleaned = Regex.Replace(cleaned, @"^```\s*$", "", RegexOptions.Multiline);
        
        // Remove stray backticks at start/end of lines
        cleaned = Regex.Replace(cleaned, @"^`{1,4}\s*", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"\s*`{1,4}$", "", RegexOptions.Multiline);
        
        // Remove common LLM explanation text that sometimes leaks through
        // These patterns appear when LLM includes prose in the code block
        var explanationPatterns = new[]
        {
            @"^Here(?:'s| is).*?:\s*$",
            @"^This (?:code|implementation|class|method).*?:\s*$",
            @"^The following.*?:\s*$",
            @"^Below is.*?:\s*$",
            @"^I(?:'ve| have).*?:\s*$",
            @"^Let me.*?:\s*$"
        };
        
        foreach (var pattern in explanationPatterns)
        {
            cleaned = Regex.Replace(cleaned, pattern, "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }
        
        // For C#, ensure the code starts with valid C# syntax
        if (language.Equals("csharp", StringComparison.OrdinalIgnoreCase) || 
            language.Equals("cs", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("c#", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = CleanCSharpCode(cleaned);
        }
        
        // Trim and normalize line endings
        cleaned = cleaned.Trim();
        cleaned = Regex.Replace(cleaned, @"\r\n|\r", "\n");
        
        // Remove excessive blank lines (more than 2 consecutive)
        cleaned = Regex.Replace(cleaned, @"\n{4,}", "\n\n\n");
        
        return cleaned;
    }
    
    /// <summary>
    /// C#-specific code cleaning - ensures valid C# structure
    /// </summary>
    private static string CleanCSharpCode(string code)
    {
        var lines = code.Split('\n').ToList();
        var cleanedLines = new List<string>();
        var foundValidStart = false;
        
        // Valid C# starting tokens
        var validStartTokens = new[]
        {
            "using ", "namespace ", "public ", "internal ", "private ", "protected ",
            "class ", "interface ", "struct ", "enum ", "record ", "sealed ", "abstract ",
            "static ", "// ", "/* ", "/// ", "#region", "#pragma", "[", "global::"
        };
        
        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();
            
            // Skip lines until we find valid C# start
            if (!foundValidStart)
            {
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;
                    
                // Check if line starts with valid C# token
                if (validStartTokens.Any(token => trimmedLine.StartsWith(token, StringComparison.Ordinal)))
                {
                    foundValidStart = true;
                    cleanedLines.Add(line);
                }
                // Skip invalid starting lines (markdown, prose, etc.)
                continue;
            }
            
            cleanedLines.Add(line);
        }
        
        return string.Join("\n", cleanedLines);
    }
    
    /// <summary>
    /// Extract the main type name (class/interface/enum) from code for smart file naming
    /// </summary>
    private static string? ExtractMainTypeName(string code, string language)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        
        // C# patterns
        if (language.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("cs", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("c#", StringComparison.OrdinalIgnoreCase))
        {
            // Match: public/internal class/interface/struct/record/enum Name
            var match = Regex.Match(code, 
                @"(?:public|internal)\s+(?:partial\s+)?(?:class|interface|struct|record|enum)\s+(\w+)",
                RegexOptions.Multiline);
            if (match.Success)
                return match.Groups[1].Value;
        }
        
        // Python patterns
        if (language.Equals("python", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("py", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(code, @"^class\s+(\w+)", RegexOptions.Multiline);
            if (match.Success)
                return match.Groups[1].Value;
        }
        
        // TypeScript/JavaScript patterns  
        if (language.Equals("typescript", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("ts", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("javascript", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("js", StringComparison.OrdinalIgnoreCase))
        {
            // Match: export class/interface Name or class Name
            var match = Regex.Match(code, @"(?:export\s+)?(?:class|interface)\s+(\w+)", RegexOptions.Multiline);
            if (match.Success)
                return match.Groups[1].Value;
        }
        
        return null;
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
