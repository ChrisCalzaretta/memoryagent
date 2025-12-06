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
        _logger.LogInformation("Generating code for task: {Task}", request.Task);

        // Build prompt from Lightning (includes context, patterns, similar solutions)
        var prompt = await _promptBuilder.BuildGeneratePromptAsync(request, cancellationToken);
        _logger.LogDebug("Built prompt with {Length} characters", prompt.Length);

        // Use the primary pinned model (always loaded, instant)
        var (model, port) = _modelOrchestrator.GetPrimaryModel();
        
        return await GenerateWithModelAsync(model, port, prompt, request, cancellationToken);
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
        
        var systemPrompt = @"You are an expert code generator. Generate production-quality code in ANY programming language.

OUTPUT FORMAT - You MUST respond with:
1. A brief explanation of your approach
2. One or more code blocks in this format:

```language:path/to/file.ext
// code here
```

EXAMPLES:
- ```csharp:Services/UserService.cs
- ```typescript:src/components/Button.tsx
- ```python:app/services/user_service.py
- ```sql:migrations/001_create_users.sql
- ```yaml:docker-compose.yml

RULES:
- ALWAYS include the file path after the language tag
- Use proper conventions for the target language
- Include error handling appropriate for the language
- Follow best practices for the language/framework
- Use async patterns where applicable";

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
                ModelUsed = model
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
}
