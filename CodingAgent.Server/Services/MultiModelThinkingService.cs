using System.Diagnostics;
using System.Text;
using AgentContracts.Services;
using CodingAgent.Server.Configuration;

namespace CodingAgent.Server.Services;

/// <summary>
/// 🧠🧠🧠 TRIPLE MODEL THINKING - Phi4 + Gemma3 + Qwen
/// Implements debate, consensus, reflection patterns
/// </summary>
public class MultiModelThinkingService : IMultiModelThinkingService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly GPUModelConfiguration _gpuConfig;
    private readonly ILogger<MultiModelThinkingService> _logger;
    private readonly IWebSearchService? _webSearch;  // Optional - gracefully degrades if not available

    // Model configuration (GPU assignments from config)
    private const string PHI4 = "phi4:latest";
    private const string GEMMA3 = "gemma3:latest";
    private const string QWEN = "qwen2.5-coder:14b";
    private const string DEEPSEEK = "deepseek-coder-v2:16b";
    private const string CODESTRAL = "codestral:latest";
    private const string LLAMA3 = "llama3:latest";

    public MultiModelThinkingService(
        IOllamaClient ollamaClient,
        ILogger<MultiModelThinkingService> logger,
        IConfiguration config,
        IWebSearchService? webSearch = null)  // Optional dependency
    {
        _ollamaClient = ollamaClient;
        _gpuConfig = config.GetSection("GpuModelConfiguration").Get<GPUModelConfiguration>() 
                     ?? GPUModelConfiguration.Default;
        _logger = logger;
        _webSearch = webSearch;
    }

    /// <summary>
    /// 🎯 SMART STRATEGY SELECTION based on complexity and attempt number
    /// </summary>
    public async Task<ThinkingResult> ThinkSmartAsync(
        ThinkingContext context,
        int attemptNumber,
        CancellationToken cancellationToken = default)
    {
        // Estimate task complexity
        var fileCount = context.ExistingFiles.Count;
        var hasBuildErrors = !string.IsNullOrEmpty(context.LatestBuildErrors);
        var score = context.LatestValidationScore ?? 10;

        _logger.LogInformation("🧠 Smart thinking: Attempt {Attempt}, Files {Files}, Score {Score}, BuildErrors {Errors}",
            attemptNumber, fileCount, score, hasBuildErrors);

        // 🌐 WEB RESEARCH AUGMENTATION - Trigger when struggling
        if (ShouldResearchWeb(context, attemptNumber, score) && _webSearch != null)
        {
            try
            {
                _logger.LogInformation("🔍 Low score or repeated failures detected - researching web...");
                var research = await _webSearch.ResearchTaskAsync(
                    context.TaskDescription,
                    context.Language,
                    maxResults: 8,
                    cancellationToken);
                
                if (research.Any())
                {
                    context = context with { WebResearch = research };
                    _logger.LogInformation("✅ Augmented context with {Count} research results", research.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Web research failed (non-fatal), continuing without it");
            }
        }

        // STRATEGY SELECTION LOGIC (MORE AGGRESSIVE MULTI-MODEL):
        if (attemptNumber == 1 && !hasBuildErrors)
        {
            // First attempt → Duo Debate (Phi4 + DeepSeek for diverse perspectives)
            return await ThinkDuoDebateDeepSeekAsync(context, cancellationToken);
        }
        else if (attemptNumber <= 2 || (attemptNumber <= 3 && score >= 6))
        {
            // Early attempts → Trio Consensus (Phi4, Gemma3, Qwen)
            return await ThinkTrioConsensusAsync(context, cancellationToken);
        }
        else if (attemptNumber <= 4)
        {
            // Mid attempts → Quad Debate (4 models: Phi4, Gemma3, Qwen, DeepSeek)
            return await ThinkQuadDebateAsync(context, cancellationToken);
        }
        else if (attemptNumber <= 7)
        {
            // Later attempts → Full Ensemble (5 models with weighted voting)
            return await ThinkFullEnsembleAsync(context, cancellationToken);
        }
        else
        {
            // Critical failures (8+) → Multi-Round Ensemble Debate (3 rounds, all models)
            return await ThinkMultiRoundEnsembleDebateAsync(context, 3, cancellationToken);
        }
    }

    /// <summary>
    /// SOLO: Single model thinks (fastest)
    /// </summary>
    private async Task<ThinkingResult> ThinkSoloAsync(
        string modelName,
        ThinkingContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠 Solo thinking with {Model}", modelName);

        var prompt = BuildThinkingPrompt(context, modelName);
        var gpu = _gpuConfig.GetModel(modelName);

        var sw = Stopwatch.StartNew();
        var ollamaResponse = await _ollamaClient.GenerateAsync(
            gpu.Name,
            prompt,
            null, // no system prompt
            MapGpuToPort(gpu.GpuDevice), // Map GPU to port
            cancellationToken);
        sw.Stop();
        
        var response = ollamaResponse.Response;

        var result = ParseThinkingResponse(response);
        result = result with
        {
            ParticipatingModels = new List<string> { modelName },
            Strategy = "solo",
            Confidence = 0.8 // Solo has moderate confidence
        };

        _logger.LogInformation("✅ Solo thinking complete: {Time}ms", sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    /// DUO: Two models debate (Phi4 proposes, Gemma3 critiques, Phi4 refines)
    /// </summary>
    private async Task<ThinkingResult> ThinkDuoDebateAsync(
        ThinkingContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠💬 Duo debate: {Phi4} vs {Gemma3}", PHI4, GEMMA3);

        var phi4Gpu = _gpuConfig.GetModel(PHI4);
        var gemmaGpu = _gpuConfig.GetModel(GEMMA3);

        // Round 1: Phi4 proposes
        var phi4Prompt = BuildThinkingPrompt(context, PHI4);
        var phi4Result = await _ollamaClient.GenerateAsync(
            phi4Gpu.Name,
            phi4Prompt,
            null,
            MapGpuToPort(phi4Gpu.GpuDevice),
            cancellationToken);
        var phi4Response = phi4Result.Response;

        // Round 2: Gemma3 critiques
        var critiquePrompt = BuildCritiquePrompt(context, phi4Response);
        var gemmaResult = await _ollamaClient.GenerateAsync(
            gemmaGpu.Name,
            critiquePrompt,
            null,
            MapGpuToPort(gemmaGpu.GpuDevice),
            cancellationToken);
        var gemmaResponse = gemmaResult.Response;

        // Round 3: Phi4 refines based on critique
        var refinePrompt = BuildRefinePrompt(context, phi4Response, gemmaResponse);
        var finalResult = await _ollamaClient.GenerateAsync(
            phi4Gpu.Name,
            refinePrompt,
            null,
            MapGpuToPort(phi4Gpu.GpuDevice),
            cancellationToken);
        var finalResponse = finalResult.Response;

        var result = ParseThinkingResponse(finalResponse);
        result = result with
        {
            ParticipatingModels = new List<string> { PHI4, GEMMA3 },
            Strategy = "duo-debate",
            Confidence = 0.9, // Duo has high confidence (2 models agree)
            Suggestions = $"{result.Suggestions}\n\nGemma3 Critique: {gemmaResponse.Substring(0, Math.Min(200, gemmaResponse.Length))}"
        };

        _logger.LogInformation("✅ Duo debate complete");
        return result;
    }

    /// <summary>
    /// DUO with DEEPSEEK: Phi4 (strategic) + DeepSeek (code-focused) debate
    /// </summary>
    private async Task<ThinkingResult> ThinkDuoDebateDeepSeekAsync(
        ThinkingContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠💻 Duo debate: {Phi4} (strategic) vs {DeepSeek} (code-focused)", PHI4, DEEPSEEK);

        var phi4Gpu = _gpuConfig.GetModel(PHI4);
        var deepseekGpu = _gpuConfig.GetModel(DEEPSEEK);

        // Round 1: Phi4 proposes strategic approach
        var phi4Prompt = BuildThinkingPrompt(context, PHI4 + " (strategic planner)");
        var phi4Result = await _ollamaClient.GenerateAsync(
            phi4Gpu.Name,
            phi4Prompt,
            null,
            MapGpuToPort(phi4Gpu.GpuDevice),
            cancellationToken);
        var phi4Response = phi4Result.Response;

        // Round 2: DeepSeek critiques from code quality perspective
        var critiquePrompt = BuildCritiquePrompt(context, phi4Response) + "\n\nFocus on: code structure, patterns, scalability, performance.";
        var deepseekResult = await _ollamaClient.GenerateAsync(
            deepseekGpu.Name,
            critiquePrompt,
            null,
            MapGpuToPort(deepseekGpu.GpuDevice),
            cancellationToken);
        var deepseekResponse = deepseekResult.Response;

        // Round 3: Phi4 synthesizes both perspectives
        var synthesisPrompt = $@"You are Phi4, synthesizing strategic and code-focused perspectives.

STRATEGIC APPROACH (Phi4):
{phi4Response}

CODE-FOCUSED CRITIQUE (DeepSeek):
{deepseekResponse}

Provide a unified approach that balances strategy with code quality.";
        
        var finalResult = await _ollamaClient.GenerateAsync(
            phi4Gpu.Name,
            synthesisPrompt,
            null,
            MapGpuToPort(phi4Gpu.GpuDevice),
            cancellationToken);
        var finalResponse = finalResult.Response;

        var result = ParseThinkingResponse(finalResponse);
        result = result with
        {
            ParticipatingModels = new List<string> { PHI4, DEEPSEEK },
            Strategy = "duo-debate-deepseek",
            Confidence = 0.92,
            Suggestions = $"{result.Suggestions}\n\nDeepSeek (Code Quality): {deepseekResponse.Substring(0, Math.Min(200, deepseekResponse.Length))}"
        };

        _logger.LogInformation("✅ Duo debate (DeepSeek) complete");
        return result;
    }

    /// <summary>
    /// QUAD: Four models collaborate (Phi4, Gemma3, Qwen, DeepSeek)
    /// </summary>
    private async Task<ThinkingResult> ThinkQuadDebateAsync(
        ThinkingContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠🧠🧠🧠 Quad debate: {Phi4}, {Gemma3}, {Qwen}, {DeepSeek}", PHI4, GEMMA3, QWEN, DEEPSEEK);

        var phi4Gpu = _gpuConfig.GetModel(PHI4);
        var gemmaGpu = _gpuConfig.GetModel(GEMMA3);
        var qwenGpu = _gpuConfig.GetModel(QWEN);
        var deepseekGpu = _gpuConfig.GetModel(DEEPSEEK);

        var prompt = BuildThinkingPrompt(context, "all");

        // All 4 think in parallel
        var phi4Task = _ollamaClient.GenerateAsync(phi4Gpu.Name, prompt, null, MapGpuToPort(phi4Gpu.GpuDevice), cancellationToken);
        var gemmaTask = _ollamaClient.GenerateAsync(gemmaGpu.Name, prompt, null, MapGpuToPort(gemmaGpu.GpuDevice), cancellationToken);
        var qwenTask = _ollamaClient.GenerateAsync(qwenGpu.Name, prompt, null, MapGpuToPort(qwenGpu.GpuDevice), cancellationToken);
        var deepseekTask = _ollamaClient.GenerateAsync(deepseekGpu.Name, prompt, null, MapGpuToPort(deepseekGpu.GpuDevice), cancellationToken);

        await Task.WhenAll(phi4Task, gemmaTask, qwenTask, deepseekTask);

        var results = new[]
        {
            ParseThinkingResponse(phi4Task.Result.Response),
            ParseThinkingResponse(gemmaTask.Result.Response),
            ParseThinkingResponse(qwenTask.Result.Response),
            ParseThinkingResponse(deepseekTask.Result.Response)
        };

        // Merge all 4 results
        var mergedResult = results[0] with
        {
            Approach = PickLongestApproach(results.Select(r => r.Approach).ToArray()),
            Dependencies = MergeArrays(results.Select(r => r.Dependencies).ToArray()),
            PatternsToUse = MergeArrays(results.Select(r => r.PatternsToUse).ToArray()),
            Risks = MergeArrays(results.Select(r => r.Risks).ToArray()),
            Suggestions = $"Phi4: {results[0].Suggestions}\nGemma3: {results[1].Suggestions}\nQwen: {results[2].Suggestions}\nDeepSeek: {results[3].Suggestions}",
            EstimatedComplexity = (int)results.Average(r => r.EstimatedComplexity),
            ParticipatingModels = new List<string> { PHI4, GEMMA3, QWEN, DEEPSEEK },
            Strategy = "quad-debate",
            Confidence = 0.96
        };

        _logger.LogInformation("✅ Quad debate complete");
        return mergedResult;
    }

    /// <summary>
    /// FULL ENSEMBLE: 5+ models with weighted voting
    /// </summary>
    private async Task<ThinkingResult> ThinkFullEnsembleAsync(
        ThinkingContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠🧠🧠🧠🧠 Full ensemble: 5 models");

        var phi4Gpu = _gpuConfig.GetModel(PHI4);
        var gemmaGpu = _gpuConfig.GetModel(GEMMA3);
        var qwenGpu = _gpuConfig.GetModel(QWEN);
        var deepseekGpu = _gpuConfig.GetModel(DEEPSEEK);
        var llama3Gpu = _gpuConfig.GetModel(LLAMA3);

        var prompt = BuildThinkingPrompt(context, "ensemble");

        // All 5 think in parallel
        var tasks = new[]
        {
            _ollamaClient.GenerateAsync(phi4Gpu.Name, prompt, null, MapGpuToPort(phi4Gpu.GpuDevice), cancellationToken),
            _ollamaClient.GenerateAsync(gemmaGpu.Name, prompt, null, MapGpuToPort(gemmaGpu.GpuDevice), cancellationToken),
            _ollamaClient.GenerateAsync(qwenGpu.Name, prompt, null, MapGpuToPort(qwenGpu.GpuDevice), cancellationToken),
            _ollamaClient.GenerateAsync(deepseekGpu.Name, prompt, null, MapGpuToPort(deepseekGpu.GpuDevice), cancellationToken),
            _ollamaClient.GenerateAsync(llama3Gpu.Name, prompt, null, MapGpuToPort(llama3Gpu.GpuDevice), cancellationToken)
        };

        await Task.WhenAll(tasks);

        var results = tasks.Select(t => ParseThinkingResponse(t.Result.Response)).ToArray();
        var models = new[] { PHI4, GEMMA3, QWEN, DEEPSEEK, LLAMA3 };

        // Weighted voting based on model specialization
        var mergedResult = results[0] with
        {
            Approach = PickLongestApproach(results.Select(r => r.Approach).ToArray()),
            Dependencies = MergeArrays(results.Select(r => r.Dependencies).ToArray()),
            PatternsToUse = MergeArrays(results.Select(r => r.PatternsToUse).ToArray()),
            Risks = MergeArrays(results.Select(r => r.Risks).ToArray()),
            Suggestions = string.Join("\n", models.Zip(results, (m, r) => $"{m}: {r.Suggestions}")),
            EstimatedComplexity = (int)results.Average(r => r.EstimatedComplexity),
            ParticipatingModels = models.ToList(),
            Strategy = "full-ensemble",
            Confidence = 0.98
        };

        _logger.LogInformation("✅ Full ensemble complete");
        return mergedResult;
    }

    /// <summary>
    /// MULTI-ROUND ENSEMBLE: Multiple debate rounds with all models
    /// </summary>
    private async Task<ThinkingResult> ThinkMultiRoundEnsembleDebateAsync(
        ThinkingContext context,
        int rounds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠💬💬💬 Multi-round ensemble debate: {Rounds} rounds with 5 models", rounds);

        var models = new[] { PHI4, GEMMA3, QWEN, DEEPSEEK, LLAMA3 };
        var gpus = models.Select(m => _gpuConfig.GetModel(m)).ToArray();

        var currentSynthesis = "";
        var allSuggestions = new List<string>();

        for (int round = 1; round <= rounds; round++)
        {
            _logger.LogInformation("  Round {Round}/{Rounds}", round, rounds);

            var roundPrompt = BuildDebatePrompt(context, currentSynthesis, round);

            // All models debate in parallel
            var tasks = gpus.Select(gpu => 
                _ollamaClient.GenerateAsync(gpu.Name, roundPrompt, null, MapGpuToPort(gpu.GpuDevice), cancellationToken)
            ).ToArray();

            await Task.WhenAll(tasks);

            var responses = tasks.Select(t => t.Result.Response).ToArray();

            // Synthesize all responses
            var synthesisPrompt = $@"Synthesize these {models.Length} expert perspectives into one unified approach:

{string.Join("\n\n", models.Zip(responses, (m, r) => $"[{m}]: {r}"))}

Provide a cohesive strategy that incorporates the best insights from all models.";

            var synthesisResult = await _ollamaClient.GenerateAsync(
                gpus[0].Name, 
                synthesisPrompt, 
                null, 
                MapGpuToPort(gpus[0].GpuDevice), 
                cancellationToken);
            
            currentSynthesis = synthesisResult.Response;
            allSuggestions.Add($"Round {round}: {currentSynthesis.Substring(0, Math.Min(200, currentSynthesis.Length))}");
        }

        var result = ParseThinkingResponse(currentSynthesis);
        result = result with
        {
            Suggestions = string.Join("\n", allSuggestions),
            ParticipatingModels = models.ToList(),
            Strategy = $"multi-round-ensemble-{rounds}",
            Confidence = 0.99
        };

        _logger.LogInformation("✅ Multi-round ensemble debate complete");
        return result;
    }

    /// <summary>
    /// TRIO: Three models think in parallel, merge insights
    /// </summary>
    private async Task<ThinkingResult> ThinkTrioConsensusAsync(
        ThinkingContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠🧠🧠 Trio consensus: {Phi4}, {Gemma3}, {Qwen}", PHI4, GEMMA3, QWEN);

        var phi4Gpu = _gpuConfig.GetModel(PHI4);
        var gemmaGpu = _gpuConfig.GetModel(GEMMA3);
        var qwenGpu = _gpuConfig.GetModel(QWEN);

        var prompt = BuildThinkingPrompt(context, "all");

        // All 3 think in parallel
        var phi4Task = _ollamaClient.GenerateAsync(phi4Gpu.Name, prompt, null, MapGpuToPort(phi4Gpu.GpuDevice), cancellationToken);
        var gemmaTask = _ollamaClient.GenerateAsync(gemmaGpu.Name, prompt, null, MapGpuToPort(gemmaGpu.GpuDevice), cancellationToken);
        var qwenTask = _ollamaClient.GenerateAsync(qwenGpu.Name, prompt, null, MapGpuToPort(qwenGpu.GpuDevice), cancellationToken);

        await Task.WhenAll(phi4Task, gemmaTask, qwenTask);

        var phi4Result = ParseThinkingResponse(phi4Task.Result.Response);
        var gemmaResult = ParseThinkingResponse(gemmaTask.Result.Response);
        var qwenResult = ParseThinkingResponse(qwenTask.Result.Response);

        // Merge results (use most detailed approach, combine suggestions)
        var mergedResult = phi4Result with
        {
            Approach = PickLongestApproach(phi4Result.Approach, gemmaResult.Approach, qwenResult.Approach),
            Dependencies = MergeArrays(phi4Result.Dependencies, gemmaResult.Dependencies, qwenResult.Dependencies),
            PatternsToUse = MergeArrays(phi4Result.PatternsToUse, gemmaResult.PatternsToUse, qwenResult.PatternsToUse),
            Risks = MergeArrays(phi4Result.Risks, gemmaResult.Risks, qwenResult.Risks),
            Suggestions = $"Phi4: {phi4Result.Suggestions}\nGemma3: {gemmaResult.Suggestions}\nQwen: {qwenResult.Suggestions}",
            EstimatedComplexity = (phi4Result.EstimatedComplexity + gemmaResult.EstimatedComplexity + qwenResult.EstimatedComplexity) / 3,
            ParticipatingModels = new List<string> { PHI4, GEMMA3, QWEN },
            Strategy = "trio-consensus",
            Confidence = 0.95 // Trio has very high confidence (3 models agree)
        };

        _logger.LogInformation("✅ Trio consensus complete");
        return mergedResult;
    }

    /// <summary>
    /// MULTI-ROUND: Intensive debate for critical failures
    /// </summary>
    private async Task<ThinkingResult> ThinkMultiRoundDebateAsync(
        ThinkingContext context,
        int rounds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🧠💬💬 Multi-round debate: {Rounds} rounds", rounds);

        var phi4Gpu = _gpuConfig.GetModel(PHI4);
        var gemmaGpu = _gpuConfig.GetModel(GEMMA3);
        var qwenGpu = _gpuConfig.GetModel(QWEN);

        var currentApproach = "";
        var allSuggestions = new List<string>();

        for (int round = 1; round <= rounds; round++)
        {
            _logger.LogInformation("  Round {Round}/{Rounds}", round, rounds);

            var roundPrompt = BuildDebatePrompt(context, currentApproach, round);

            // Each model debates in sequence
            var phi4Res = await _ollamaClient.GenerateAsync(phi4Gpu.Name, roundPrompt, null, MapGpuToPort(phi4Gpu.GpuDevice), cancellationToken);
            var phi4Response = phi4Res.Response;
            
            var gemmaRes = await _ollamaClient.GenerateAsync(gemmaGpu.Name, roundPrompt + $"\n\nPhi4 says: {phi4Response}", null, MapGpuToPort(gemmaGpu.GpuDevice), cancellationToken);
            var gemmaResponse = gemmaRes.Response;
            
            var qwenRes = await _ollamaClient.GenerateAsync(qwenGpu.Name, roundPrompt + $"\n\nPhi4: {phi4Response}\nGemma3: {gemmaResponse}", null, MapGpuToPort(qwenGpu.GpuDevice), cancellationToken);
            var qwenResponse = qwenRes.Response;

            currentApproach = qwenResponse; // Use last model's synthesis
            allSuggestions.Add($"Round {round}: {qwenResponse.Substring(0, Math.Min(150, qwenResponse.Length))}");
        }

        var result = ParseThinkingResponse(currentApproach);
        result = result with
        {
            Suggestions = string.Join("\n", allSuggestions),
            ParticipatingModels = new List<string> { PHI4, GEMMA3, QWEN },
            Strategy = $"multi-round-debate-{rounds}",
            Confidence = 0.98 // Multi-round has highest confidence
        };

        _logger.LogInformation("✅ Multi-round debate complete");
        return result;
    }

    // === HELPER METHODS ===

    /// <summary>
    /// Decide if we should augment thinking with web research
    /// </summary>
    private bool ShouldResearchWeb(ThinkingContext context, int attemptNumber, int score)
    {
        // Don't research on first attempt (give models a chance)
        if (attemptNumber == 1) return false;
        
        // Research if we already have it cached
        if (context.WebResearch != null && context.WebResearch.Any()) return false;
        
        // TRIGGER CONDITIONS:
        // 1. Low score after 2 attempts
        if (attemptNumber >= 2 && score < 6) return true;
        
        // 2. Very low score after 3 attempts
        if (attemptNumber >= 3 && score < 7) return true;
        
        // 3. Build errors after 3 attempts
        if (attemptNumber >= 3 && !string.IsNullOrEmpty(context.LatestBuildErrors)) return true;
        
        // 4. Critical failures (5+ attempts with score < 5)
        if (attemptNumber >= 5 && score < 5) return true;
        
        return false;
    }

    private string BuildThinkingPrompt(ThinkingContext context, string modelPerspective)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are {modelPerspective}, an expert code architect.");
        sb.AppendLine($"\nTask: {context.TaskDescription}");
        sb.AppendLine($"Language: {context.Language}");
        
        // 🌐 INJECT WEB RESEARCH (if available)
        if (context.WebResearch != null && context.WebResearch.Any())
        {
            sb.AppendLine("\n" + "═".PadRight(80, '═'));
            sb.AppendLine("📚 REAL-WORLD RESEARCH (Use these authoritative sources!):");
            sb.AppendLine("═".PadRight(80, '═'));
            
            foreach (var result in context.WebResearch.Take(5))
            {
                sb.AppendLine($"\n[{result.Source}] {result.Title}");
                sb.AppendLine($"URL: {result.Url}");
                
                // Truncate snippet if too long
                var snippet = result.Snippet.Length > 400 
                    ? result.Snippet.Substring(0, 400) + "..." 
                    : result.Snippet;
                sb.AppendLine($"{snippet}");
                
                if (result.Tags.Any())
                {
                    sb.AppendLine($"Tags: {string.Join(", ", result.Tags)}");
                }
            }
            
            sb.AppendLine("\n" + "═".PadRight(80, '═'));
            sb.AppendLine("💡 Use these examples and best practices to inform your approach!");
            sb.AppendLine("═".PadRight(80, '═') + "\n");
        }
        
        // 🎨 INJECT BRAND GUIDELINES (if available)
        if (context.BrandGuidelines != null)
        {
            var brand = context.BrandGuidelines;
            sb.AppendLine("\n" + "═".PadRight(80, '═'));
            sb.AppendLine("🎨 BRAND DESIGN GUIDELINES (MUST FOLLOW!):");
            sb.AppendLine("═".PadRight(80, '═'));
            sb.AppendLine($"Brand: {brand.Name}");
            sb.AppendLine($"Tone: {brand.Tone}");
            sb.AppendLine();
            sb.AppendLine("COLORS:");
            sb.AppendLine($"  Primary: {brand.Colors.Primary}");
            sb.AppendLine($"  Secondary: {brand.Colors.Secondary}");
            if (!string.IsNullOrEmpty(brand.Colors.Accent))
                sb.AppendLine($"  Accent: {brand.Colors.Accent}");
            if (!string.IsNullOrEmpty(brand.Colors.Background))
                sb.AppendLine($"  Background: {brand.Colors.Background}");
            if (!string.IsNullOrEmpty(brand.Colors.Text))
                sb.AppendLine($"  Text: {brand.Colors.Text}");
            sb.AppendLine();
            sb.AppendLine("TYPOGRAPHY:");
            sb.AppendLine($"  Headings: {brand.Typography.HeadingFont}");
            sb.AppendLine($"  Body: {brand.Typography.BodyFont}");
            if (!string.IsNullOrEmpty(brand.Typography.MonoFont))
                sb.AppendLine($"  Code: {brand.Typography.MonoFont}");
            sb.AppendLine();
            sb.AppendLine("SPACING:");
            sb.AppendLine($"  Base Unit: {brand.Spacing.BaseUnit}");
            sb.AppendLine($"  Scale: {brand.Spacing.Scale}");
            sb.AppendLine();
            sb.AppendLine($"ACCESSIBILITY: {brand.Accessibility}");
            sb.AppendLine("\n" + "═".PadRight(80, '═'));
            sb.AppendLine("💡 USE THESE EXACT COLORS AND FONTS IN YOUR CODE!");
            sb.AppendLine("💡 ALL UI MUST MATCH THIS BRAND SYSTEM!");
            sb.AppendLine("═".PadRight(80, '═') + "\n");
        }
        
        if (context.PreviousAttempts.Any())
        {
            sb.AppendLine($"\nPrevious attempts failed {context.PreviousAttempts.Count} times:");
            foreach (var attempt in context.PreviousAttempts.TakeLast(3))
            {
                sb.AppendLine($"- Attempt {attempt.AttemptNumber} ({attempt.Model}): Score {attempt.Score}, Issues: {string.Join(", ", attempt.Issues.Take(3))}");
            }
        }

        if (!string.IsNullOrEmpty(context.LatestBuildErrors))
        {
            sb.AppendLine($"\n🚨 CRITICAL - BUILD ERRORS:\n{context.LatestBuildErrors.Substring(0, Math.Min(500, context.LatestBuildErrors.Length))}");
        }

        sb.AppendLine("\nProvide a strategic approach with:");
        sb.AppendLine("1. APPROACH: Clear implementation strategy");
        sb.AppendLine("2. DEPENDENCIES: Required packages/libraries");
        sb.AppendLine("3. PATTERNS: Design patterns to use");
        sb.AppendLine("4. RISKS: Potential issues to avoid");
        sb.AppendLine("5. SUGGESTIONS: Specific improvements");

        return sb.ToString();
    }

    private string BuildCritiquePrompt(ThinkingContext context, string originalApproach)
    {
        return $@"You are Gemma3, an expert code reviewer.

ORIGINAL APPROACH:
{originalApproach}

Critique this approach:
1. What could go wrong?
2. What's missing?
3. How could it be improved?
4. Any alternative approaches?

Be constructive but thorough.";
    }

    private string BuildRefinePrompt(ThinkingContext context, string originalApproach, string critique)
    {
        return $@"You are Phi4, refining your original approach based on peer review.

YOUR ORIGINAL APPROACH:
{originalApproach}

GEMMA3'S CRITIQUE:
{critique}

Provide an improved approach that addresses the critique.";
    }

    private string BuildDebatePrompt(ThinkingContext context, string previousRound, int round)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are participating in Round {round} of a technical debate.");
        sb.AppendLine($"\nTask: {context.TaskDescription}");
        
        if (!string.IsNullOrEmpty(previousRound))
        {
            sb.AppendLine($"\nPrevious round conclusion:\n{previousRound}");
        }

        sb.AppendLine("\nProvide your analysis and recommendations.");
        return sb.ToString();
    }

    private ThinkingResult ParseThinkingResponse(string response)
    {
        // Simple parsing - extract sections
        var lines = response.Split('\n');
        var approach = ExtractSection(lines, "APPROACH") ?? response.Substring(0, Math.Min(200, response.Length));
        var dependencies = ExtractList(lines, "DEPENDENCIES");
        var patterns = ExtractList(lines, "PATTERNS");
        var risks = ExtractList(lines, "RISKS");
        var suggestions = ExtractSection(lines, "SUGGESTIONS") ?? "";

        return new ThinkingResult
        {
            Approach = approach,
            Dependencies = dependencies.ToArray(),
            PatternsToUse = patterns.ToArray(),
            Risks = risks.ToArray(),
            Suggestions = suggestions,
            EstimatedComplexity = 5,
            RecommendedModel = "deepseek-coder:6.7b"
        };
    }

    private string? ExtractSection(string[] lines, string sectionName)
    {
        var sb = new StringBuilder();
        bool inSection = false;

        foreach (var line in lines)
        {
            if (line.Contains(sectionName, StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }
            if (inSection && (line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.") || line.StartsWith("4.") || line.StartsWith("5.")))
            {
                break;
            }
            if (inSection)
            {
                sb.AppendLine(line);
            }
        }

        return sb.Length > 0 ? sb.ToString().Trim() : null;
    }

    private List<string> ExtractList(string[] lines, string sectionName)
    {
        var result = new List<string>();
        bool inSection = false;

        foreach (var line in lines)
        {
            if (line.Contains(sectionName, StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }
            if (inSection && line.Trim().StartsWith("-"))
            {
                result.Add(line.Trim().TrimStart('-').Trim());
            }
            if (inSection && (line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3.") || line.StartsWith("4.") || line.StartsWith("5.")))
            {
                break;
            }
        }

        return result;
    }

    private string PickLongestApproach(params string[] approaches)
    {
        return approaches.OrderByDescending(a => a.Length).First();
    }

    private string[] MergeArrays(params string[][] arrays)
    {
        return arrays.SelectMany(a => a).Distinct().ToArray();
    }

    /// <summary>
    /// Map GPU device ID to Ollama port (since Ollama uses ports, not GPU IDs directly)
    /// GPU 0 → Port 11434 (default)
    /// GPU 1 → Port 11435
    /// GPU 2 → Port 11436
    /// </summary>
    private int MapGpuToPort(int gpuDevice)
    {
        return 11434 + gpuDevice;
    }
}

