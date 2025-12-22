using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;
using CodingAgent.Server.Clients;
using CodingAgent.Server.Configuration;

namespace CodingAgent.Server.Services;

/// <summary>
/// 💻💻💻 MULTI-MODEL CODING - Coordinates code generation across multiple models
/// </summary>
public class MultiModelCodingService : IMultiModelCodingService
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly IOllamaClient _ollamaClient;
    private readonly IAnthropicClient? _anthropicClient;
    private readonly IClaudeRateLimitTracker? _rateLimitTracker;
    private readonly ILogger<MultiModelCodingService> _logger;
    private readonly GPUModelConfiguration _gpuConfig;

    public MultiModelCodingService(
        IPromptBuilder promptBuilder,
        IOllamaClient ollamaClient,
        ILogger<MultiModelCodingService> logger,
        IConfiguration config,
        IAnthropicClient? anthropicClient = null,
        IClaudeRateLimitTracker? rateLimitTracker = null)
    {
        _promptBuilder = promptBuilder;
        _ollamaClient = ollamaClient;
        _logger = logger;
        _anthropicClient = anthropicClient;
        _rateLimitTracker = rateLimitTracker;
        _gpuConfig = config.GetSection("GpuModelConfiguration").Get<GPUModelConfiguration>()
                     ?? GPUModelConfiguration.Default;
    }

    public async Task<MultiModelCodeResult> GenerateSmartAsync(
        GenerateCodeRequest request,
        int attemptNumber,
        string thinkingGuidance,
        CancellationToken cancellationToken)
    {
        // 🎯 ADAPTIVE STRATEGY: Free models first, Claude escalation only when needed!
        _logger.LogInformation("💻 Smart coding strategy for attempt {Attempt}", attemptNumber);

        if (attemptNumber <= 2)
        {
            // 🆓 Attempts 1-2: Solo Qwen (FAST + FREE)
            _logger.LogInformation("🆓 Using FREE local model: Qwen Solo");
            var model = _gpuConfig.GetModel("qwen2.5-coder:14b");
            return await GenerateSoloAsync(request, model.Name, thinkingGuidance, cancellationToken);
        }
        else if (attemptNumber <= 4)
        {
            // 🆓 Attempts 3-4: Duo (Qwen + Codestral) - FREE with review
            _logger.LogInformation("🆓 Using FREE local models: Qwen + Codestral Duo");
            return await GenerateDuoAsync(request, "qwen2.5-coder:14b", "codestral:latest", thinkingGuidance, cancellationToken);
        }
        else if (attemptNumber <= 6)
        {
            // 🆓 Attempts 5-6: Trio (3 local models) - FREE parallel
            _logger.LogInformation("🆓 Using FREE local models: Qwen + Codestral + Llama3 Trio");
            return await GenerateTrioAsync(request, new[] { "qwen2.5-coder:14b", "codestral:latest", "llama3:latest" }, thinkingGuidance, cancellationToken);
        }
        else if (attemptNumber <= 8 && _anthropicClient?.IsConfigured == true)
        {
            // 💰 Attempts 7-8: Collaborative with Claude Sonnet (PAID escalation)
            _logger.LogWarning("💰 Escalating to PAID Claude Sonnet (attempt {Attempt})", attemptNumber);
            return await GenerateCollaborativeAsync(request, thinkingGuidance, true, cancellationToken);
        }
        else if (attemptNumber <= 10 && _anthropicClient?.IsConfigured == true)
        {
            // 💰💰 Attempts 9-10: Claude Opus (PREMIUM escalation)
            _logger.LogError("💰💰 PREMIUM escalation to Claude Opus (attempt {Attempt})", attemptNumber);
            return await GenerateCollaborativeAsync(request, thinkingGuidance, true, cancellationToken);
        }
        else
        {
            // 🔄 Fall back to Trio if Claude unavailable
            _logger.LogWarning("⚠️ Claude unavailable, using Trio strategy");
            return await GenerateTrioAsync(request, new[] { "qwen2.5-coder:14b", "codestral:latest", "llama3:latest" }, thinkingGuidance, cancellationToken);
        }
    }

    public async Task<MultiModelCodeResult> GenerateSoloAsync(
        GenerateCodeRequest request,
        string modelName,
        string thinkingGuidance,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("💻 Solo coding with {Model}", modelName);

        var sw = Stopwatch.StartNew();
        var gpu = _gpuConfig.GetModel(modelName);

        var prompt = await _promptBuilder.BuildGeneratePromptAsync(request, cancellationToken);
        if (!string.IsNullOrEmpty(thinkingGuidance))
        {
            prompt = $"{thinkingGuidance}\n\n{prompt}";
        }
        
        // Add output format instructions for local models
        prompt = AppendOutputFormatToPrompt(prompt);

        var ollamaResponse = await _ollamaClient.GenerateAsync(gpu.Name, prompt, null, MapGpuToPort(gpu.GpuDevice), cancellationToken);
        sw.Stop();
        
        var response = ollamaResponse.Response;

        _logger.LogInformation("🔍 Raw response from {Model} ({Length} chars):\n{Response}", 
            modelName, response.Length, response.Length > 500 ? response.Substring(0, 500) + "..." : response);

        var fileChanges = ParseCodeResponse(response);

        return new MultiModelCodeResult
        {
            FileChanges = fileChanges,
            Strategy = "solo",
            ParticipatingModels = new List<string> { modelName },
            TotalDurationMs = sw.ElapsedMilliseconds,
            Contributions = new List<ModelContribution>
            {
                new ModelContribution
                {
                    ModelName = modelName,
                    Role = "generator",
                    Output = response,
                    ConfidenceScore = 0.8,
                    DurationMs = sw.ElapsedMilliseconds,
                    GPU = gpu.GpuDevice
                }
            }
        };
    }

    public async Task<MultiModelCodeResult> GenerateDuoAsync(
        GenerateCodeRequest request,
        string generatorModel,
        string reviewerModel,
        string thinkingGuidance,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("💻💻 Duo coding: {Generator} generates, {Reviewer} reviews", generatorModel, reviewerModel);

        var sw = Stopwatch.StartNew();
        var generatorGpu = _gpuConfig.GetModel(generatorModel);
        var reviewerGpu = _gpuConfig.GetModel(reviewerModel);

        // Step 1: Generator generates
        var prompt = await _promptBuilder.BuildGeneratePromptAsync(request, cancellationToken);
        if (!string.IsNullOrEmpty(thinkingGuidance))
        {
            prompt = $"{thinkingGuidance}\n\n{prompt}";
        }

        var generatorResult = await _ollamaClient.GenerateAsync(generatorGpu.Name, prompt, null, MapGpuToPort(generatorGpu.GpuDevice), cancellationToken);
        var generatorResponse = generatorResult.Response;
        var initialFiles = ParseCodeResponse(generatorResponse);

        // Step 2: Reviewer critiques
        var reviewPrompt = BuildReviewPrompt(request, initialFiles);
        var reviewerResult = await _ollamaClient.GenerateAsync(reviewerGpu.Name, reviewPrompt, null, MapGpuToPort(reviewerGpu.GpuDevice), cancellationToken);
        var reviewerResponse = reviewerResult.Response;

        // Step 3: Generator refines
        var refinePrompt = BuildRefinePrompt(request, initialFiles, reviewerResponse);
        var refinedResult = await _ollamaClient.GenerateAsync(generatorGpu.Name, refinePrompt, null, MapGpuToPort(generatorGpu.GpuDevice), cancellationToken);
        var refinedResponse = refinedResult.Response;
        var finalFiles = ParseCodeResponse(refinedResponse);

        sw.Stop();

        return new MultiModelCodeResult
        {
            FileChanges = finalFiles,
            Strategy = "duo-review",
            ParticipatingModels = new List<string> { generatorModel, reviewerModel },
            TotalDurationMs = sw.ElapsedMilliseconds,
            CollaborationLog = $"Generator: {generatorModel}\nReviewer: {reviewerModel}\nReview: {reviewerResponse.Substring(0, Math.Min(200, reviewerResponse.Length))}"
        };
    }

    public async Task<MultiModelCodeResult> GenerateTrioAsync(
        GenerateCodeRequest request,
        string[] models,
        string thinkingGuidance,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("💻💻💻 Trio parallel: {Models}", string.Join(", ", models));

        var sw = Stopwatch.StartNew();
        var prompt = await _promptBuilder.BuildGeneratePromptAsync(request, cancellationToken);
        if (!string.IsNullOrEmpty(thinkingGuidance))
        {
            prompt = $"{thinkingGuidance}\n\n{prompt}";
        }

        // All 3 generate in parallel
        var tasks = models.Select(async model =>
        {
            var gpu = _gpuConfig.GetModel(model);
            var result = await _ollamaClient.GenerateAsync(gpu.Name, prompt, null, MapGpuToPort(gpu.GpuDevice), cancellationToken);
            return new { Model = model, Response = result.Response, Files = ParseCodeResponse(result.Response) };
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        sw.Stop();

        // Select best result (most files or longest code)
        var bestResult = results.OrderByDescending(r => r.Files.Sum(f => f.Content.Length)).First();

        return new MultiModelCodeResult
        {
            FileChanges = bestResult.Files,
            Strategy = "trio-parallel",
            ParticipatingModels = models.ToList(),
            TotalDurationMs = sw.ElapsedMilliseconds,
            CollaborationLog = $"3 models generated in parallel. Selected best from {bestResult.Model}."
        };
    }

    public async Task<MultiModelCodeResult> GenerateCollaborativeAsync(
        GenerateCodeRequest request,
        string thinkingGuidance,
        bool includeCloud,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("💻☁️ Collaborative coding (cloud: {Cloud})", includeCloud);

        var sw = Stopwatch.StartNew();

        // Step 1: Local model generates draft
        var qwenGpu = _gpuConfig.GetModel("qwen2.5-coder:14b");
        var prompt = await _promptBuilder.BuildGeneratePromptAsync(request, cancellationToken);
        if (!string.IsNullOrEmpty(thinkingGuidance))
        {
            prompt = $"{thinkingGuidance}\n\n{prompt}";
        }

        var draftResult = await _ollamaClient.GenerateAsync(qwenGpu.Name, prompt, null, MapGpuToPort(qwenGpu.GpuDevice), cancellationToken);
        var draftResponse = draftResult.Response;
        var draftFiles = ParseCodeResponse(draftResponse);

        // Step 2: If cloud available, use Claude to refine
        if (includeCloud && _anthropicClient != null && _anthropicClient.IsConfigured)
        {
            _logger.LogInformation("☁️ Using Claude to refine draft");

            var claudePrompt = $"{prompt}\n\nIMPROVE THIS CODE:\n{FormatFilesForReview(draftFiles)}";
            var claudeResponse = await _anthropicClient.GenerateAsync(
                "claude-sonnet-4-20250514",
                claudePrompt,
                cancellationToken);

            var refinedFiles = ParseCodeResponse(claudeResponse.Content);
            sw.Stop();

            return new MultiModelCodeResult
            {
                FileChanges = refinedFiles,
                Strategy = "collaborative-cloud",
                ParticipatingModels = new List<string> { "qwen2.5-coder:14b", "claude-sonnet-4" },
                TotalDurationMs = sw.ElapsedMilliseconds,
                UsedCloudAPI = true,
                EstimatedCost = 0.10m, // Estimate
                CollaborationLog = "Local draft → Claude refinement"
            };
        }
        else
        {
            // Fallback: Local review only
            var codestralGpu = _gpuConfig.GetModel("codestral:latest");
            var reviewPrompt = BuildReviewPrompt(request, draftFiles);
            var reviewResult = await _ollamaClient.GenerateAsync(codestralGpu.Name, reviewPrompt, null, MapGpuToPort(codestralGpu.GpuDevice), cancellationToken);
            var reviewResponse = reviewResult.Response;

            var refinePrompt = BuildRefinePrompt(request, draftFiles, reviewResponse);
            var refinedResult = await _ollamaClient.GenerateAsync(qwenGpu.Name, refinePrompt, null, MapGpuToPort(qwenGpu.GpuDevice), cancellationToken);
            var refinedResponse = refinedResult.Response;
            var refinedFiles = ParseCodeResponse(refinedResponse);

            sw.Stop();

            return new MultiModelCodeResult
            {
                FileChanges = refinedFiles,
                Strategy = "collaborative-local",
                ParticipatingModels = new List<string> { "qwen2.5-coder:14b", "codestral:latest" },
                TotalDurationMs = sw.ElapsedMilliseconds,
                CollaborationLog = "Deepseek draft → Qwen review → Deepseek refinement"
            };
        }
    }

    // === HELPER METHODS ===

    private List<FileChange> ParseCodeResponse(string response)
    {
        var files = new List<FileChange>();
        var lines = response.Split('\n');

        _logger.LogInformation("🔍 Parsing response: {Lines} lines", lines.Length);

        string? currentFile = null;
        var currentContent = new StringBuilder();
        bool inCodeBlock = false;
        string? detectedLanguage = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Opening code fence: ```csharp, ```cs, ```csharp Calculator.cs, etc.
            if (line.StartsWith("```") && !inCodeBlock)
            {
                inCodeBlock = true;
                
                // Extract language and optional filename
                var fence = line.Substring(3).Trim();
                _logger.LogInformation("🔍 Opening fence detected: '{Fence}'", fence);
                
                // Check for filename in fence (e.g., "```csharp Calculator.cs")
                if (fence.Contains(' '))
                {
                    var parts = fence.Split(' ', 2);
                    detectedLanguage = parts[0];
                    currentFile = parts[1].Trim();
                    _logger.LogInformation("✅ Extracted filename from fence: '{File}' (language: {Lang})", currentFile, detectedLanguage);
                }
                else if (fence.Contains('/'))
                {
                    // Full path in fence (e.g., "```Services/Calculator.cs")
                    currentFile = fence.Replace("csharp", "").Replace("cs", "").Trim();
                    _logger.LogInformation("✅ Extracted path from fence: '{File}'", currentFile);
                }
                else
                {
                    // Just language tag
                    detectedLanguage = fence;
                    currentFile = null;
                    _logger.LogInformation("⚠️ No filename in fence, only language: '{Lang}'", detectedLanguage);
                }
                
                currentContent.Clear();
                continue;
            }

            // Closing code fence
            if (line.StartsWith("```") && inCodeBlock)
            {
                inCodeBlock = false;
                
                // Try to infer filename if not already set
                if (currentFile == null && currentContent.Length > 0)
                {
                    currentFile = InferFilename(currentContent.ToString(), detectedLanguage);
                }
                
                if (currentFile != null && currentContent.Length > 0)
                {
                    files.Add(new FileChange
                    {
                        Path = currentFile,
                        Content = currentContent.ToString().Trim()
                    });
                    _logger.LogInformation("📄 Parsed code block: {File} ({Length} chars)", currentFile, currentContent.Length);
                }
                
                currentFile = null;
                currentContent.Clear();
                detectedLanguage = null;
                continue;
            }

            // Look for file path comments INSIDE code blocks
            if (inCodeBlock)
            {
                // Check for path hints: "// File: Calculator.cs" or "// path: Services/Calculator.cs"
                var pathMatch = Regex.Match(line, @"^\s*//\s*(file|path):\s*(.+)", RegexOptions.IgnoreCase);
                if (pathMatch.Success && currentFile == null)
                {
                    currentFile = pathMatch.Groups[2].Value.Trim();
                    continue; // Don't include this comment in the code
                }
                
                currentContent.AppendLine(line);
            }
        }

        // Catch unclosed code block
        if (currentFile != null && currentContent.Length > 0)
        {
            files.Add(new FileChange
            {
                Path = currentFile,
                Content = currentContent.ToString().Trim()
            });
            _logger.LogInformation("📄 Parsed unclosed code block: {File} ({Length} chars)", currentFile, currentContent.Length);
        }

        _logger.LogInformation("✅ Parsed {FileCount} files from response", files.Count);
        return files;
    }

    private string InferFilename(string code, string? language)
    {
        // 🔥 FIX: Handle Razor components FIRST (most important for Blazor)
        if (language?.ToLowerInvariant() == "razor")
        {
            // Try to extract component name from @page directive or @code block
            var pageMatch = Regex.Match(code, @"@page\s+""[^""]*""", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var codeBlockMatch = Regex.Match(code, @"@code\s*\{", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            
            // Try to find a meaningful name in comments or page path
            var commentMatch = Regex.Match(code, @"<!--\s*(\w+)\.razor\s*-->", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (commentMatch.Success)
            {
                return $"{commentMatch.Groups[1].Value}.razor";
            }
            
            // Try to extract from @page path (e.g., @page "/game" -> Game.razor)
            if (pageMatch.Success)
            {
                var pagePath = Regex.Match(pageMatch.Value, @"""([^""]+)""").Groups[1].Value.Trim('/');
                if (!string.IsNullOrEmpty(pagePath))
                {
                    var componentName = char.ToUpper(pagePath[0]) + pagePath.Substring(1);
                    return $"{componentName}.razor";
                }
            }
            
            // Check if it has @code block (typical for pages)
            if (codeBlockMatch.Success)
            {
                return $"Component{Guid.NewGuid().ToString().Substring(0, 4)}.razor";
            }
            
            // Generic razor component
            return $"Component{Guid.NewGuid().ToString().Substring(0, 4)}.razor";
        }
        
        // Try to extract class name from C# code
        var classMatch = Regex.Match(code, @"\b(?:public|internal)\s+(?:class|interface|enum|record)\s+(\w+)", RegexOptions.Multiline);
        if (classMatch.Success)
        {
            var className = classMatch.Groups[1].Value;
            return $"{className}.cs";
        }

        // Fallback to generic name based on language
        var ext = language?.ToLowerInvariant() switch
        {
            "csharp" or "cs" => ".cs",
            "python" or "py" => ".py",
            "javascript" or "js" => ".js",
            "typescript" or "ts" => ".ts",
            "razor" => ".razor", // 🔥 ADDED: Explicit razor support
            _ => ".txt"
        };

        return $"Generated{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
    }

    private string BuildReviewPrompt(GenerateCodeRequest request, List<FileChange> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a code reviewer. Review this generated code:");
        sb.AppendLine();

        foreach (var file in files.Take(3)) // Review first 3 files
        {
            sb.AppendLine($"File: {file.Path}");
            sb.AppendLine("```");
            sb.AppendLine(file.Content.Length > 1000 ? file.Content.Substring(0, 1000) + "\n// ... truncated ..." : file.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("Provide:");
        sb.AppendLine("1. Issues found");
        sb.AppendLine("2. Missing elements");
        sb.AppendLine("3. Improvements needed");

        return sb.ToString();
    }

    private string BuildRefinePrompt(GenerateCodeRequest request, List<FileChange> files, string review)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Your code has been reviewed. Fix the issues:");
        sb.AppendLine();
        sb.AppendLine("REVIEW:");
        sb.AppendLine(review);
        sb.AppendLine();
        sb.AppendLine("ORIGINAL CODE:");
        sb.AppendLine(FormatFilesForReview(files));
        sb.AppendLine();
        sb.AppendLine("Provide IMPROVED code that addresses all review points.");
        sb.AppendLine();
        AppendOutputFormatInstructions(sb);
        return sb.ToString();
    }

    /// <summary>
    /// Append CLEAR output format instructions for local models
    /// </summary>
    private void AppendOutputFormatInstructions(StringBuilder sb)
    {
        sb.AppendLine("=== OUTPUT FORMAT (CRITICAL!) ===");
        sb.AppendLine("You MUST format your response like this:");
        sb.AppendLine();
        sb.AppendLine("C# FILES:");
        sb.AppendLine("```csharp Calculator.cs");
        sb.AppendLine("// Your C# code here");
        sb.AppendLine("public class Calculator");
        sb.AppendLine("{");
        sb.AppendLine("    public int Add(int a, int b) => a + b;");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("BLAZOR RAZOR COMPONENTS:");
        sb.AppendLine("```razor GameBoard.razor");
        sb.AppendLine("<div class=\"game-board\">");
        sb.AppendLine("    @* Your Razor component here *@");
        sb.AppendLine("</div>");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("CRITICAL RULES:");
        sb.AppendLine("1. ✅ CORRECT: ```csharp Calculator.cs  (language SPACE filename)");
        sb.AppendLine("2. ✅ CORRECT: ```razor GameBoard.razor  (language SPACE filename)");
        sb.AppendLine("3. ❌ WRONG: ```csharp  (missing filename!)");
        sb.AppendLine("4. ❌ WRONG: ```razor  (missing filename!)");
        sb.AppendLine("5. Put the filename AFTER the language tag with a SPACE");
        sb.AppendLine("6. For Blazor, ALWAYS include .razor extension");
        sb.AppendLine("7. Include complete, compilable code");
        sb.AppendLine("8. Close with ``` on its own line");
        sb.AppendLine("9. Generate ALL necessary files (C# classes AND Razor components)");
    }

    /// <summary>
    /// Append output format instructions to a prompt string
    /// </summary>
    private string AppendOutputFormatToPrompt(string prompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine(prompt);
        sb.AppendLine();
        AppendOutputFormatInstructions(sb);
        return sb.ToString();
    }

    private string FormatFilesForReview(List<FileChange> files)
    {
        var sb = new StringBuilder();
        foreach (var file in files.Take(3))
        {
            sb.AppendLine($"\n{file.Path}:");
            sb.AppendLine("```");
            sb.AppendLine(file.Content.Length > 500 ? file.Content.Substring(0, 500) + "\n// ... truncated ..." : file.Content);
            sb.AppendLine("```");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Map GPU device ID to Ollama port
    /// GPU 0 → Port 11434 (default)
    /// GPU 1 → Port 11435
    /// GPU 2 → Port 11436
    /// </summary>
    private int MapGpuToPort(int gpuDevice)
    {
        return 11434 + gpuDevice;
    }
}

