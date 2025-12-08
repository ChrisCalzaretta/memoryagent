using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Services;
using ValidationAgent.Server.Clients;

namespace ValidationAgent.Server.Services;

/// <summary>
/// 🧠 Smart model selection for ValidationAgent
/// - Queries historical performance from MemoryAgent
/// - Uses LLM to select best model based on task + history
/// - Includes exploration mechanism for new/untried models
/// - Fetches prompts from Lightning (Agent Lightning)
/// - 🔥 Warm model preference (avoid cold start)
/// - 📊 VRAM-aware model sizing
/// - 🔗 Cross-language learning transfer
/// - 🕐 Time-decay weighting (recent = more important)
/// </summary>
public interface IValidationModelSelector
{
    /// <summary>
    /// Select the best model for a validation task
    /// </summary>
    Task<ValidationModelSelection> SelectModelAsync(
        string taskDescription,
        string language,
        int fileCount,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of model selection
/// </summary>
public class ValidationModelSelection
{
    public string Model { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public bool IsExploration { get; set; } = false;
    public double Confidence { get; set; } = 0.5;
    public bool IsWarmModel { get; set; } = false;
}

public class ValidationModelSelector : IValidationModelSelector
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly IOllamaClient _ollamaClient;
    private readonly ILogger<ValidationModelSelector> _logger;
    private readonly IConfiguration _config;
    
    private readonly string _selectorModel;  // Model used to make selection decisions
    private readonly int _ollamaPort;
    private readonly double _explorationRate;
    private readonly bool _useSmartModelSelection;  // Whether to use LLM for model selection
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ValidationModelSelector(
        IMemoryAgentClient memoryAgent,
        IOllamaClient ollamaClient,
        ILogger<ValidationModelSelector> logger,
        IConfiguration config)
    {
        _memoryAgent = memoryAgent;
        _ollamaClient = ollamaClient;
        _logger = logger;
        _config = config;
        
        // Use a small fast model for selection decisions
        _selectorModel = config.GetValue<string>("Gpu:SelectorModel") ?? 
                         config.GetValue<string>("Gpu:ValidationModel") ?? "phi4:latest";
        _ollamaPort = config.GetValue<int>("Ollama:Port", 11434);
        _explorationRate = config.GetValue<double>("ModelSelection:ExplorationRate", 0.1); // 10% exploration
        _useSmartModelSelection = config.GetValue<bool>("Gpu:UseSmartModelSelection", true);
        
        if (!_useSmartModelSelection)
        {
            _logger.LogInformation("⚡ Smart validation model selection DISABLED - using {Model} only (optimal for single GPU)", _selectorModel);
        }
    }

    public async Task<ValidationModelSelection> SelectModelAsync(
        string taskDescription,
        string language,
        int fileCount,
        CancellationToken cancellationToken = default)
    {
        // ⚡ Fast path: Skip LLM selection when disabled (avoids model swap on single GPU)
        if (!_useSmartModelSelection)
        {
            _logger.LogDebug("⚡ Smart selection disabled - using default model {Model}", _selectorModel);
            return new ValidationModelSelection
            {
                Model = _selectorModel,
                Reasoning = "Smart selection disabled - using configured validation model",
                Confidence = 1.0,
                IsWarmModel = true  // Assume it's warm since we're not swapping
            };
        }
        
        try
        {
            // 1. Get available models from Ollama
            var availableModels = await GetAvailableModelsAsync(cancellationToken);
            if (!availableModels.Any())
            {
                _logger.LogWarning("No models available, using default");
                return new ValidationModelSelection 
                { 
                    Model = _selectorModel, 
                    Reasoning = "No models available - using default" 
                };
            }
            
            // 2. Get historical stats from MemoryAgent (with time-decay + cross-language)
            var historicalStats = await GetHistoricalStatsAsync(language, cancellationToken);
            
            // 3. Identify new/untried models
            var triedModels = historicalStats.Select(s => s.Model).ToHashSet();
            var newModels = availableModels.Where(m => !triedModels.Contains(m)).ToList();
            
            // 4. 🔥 Get currently loaded (warm) models
            var loadedModels = await GetLoadedModelsAsync(cancellationToken);
            
            // 5. 📊 Get model sizes for VRAM-aware selection
            var modelSizes = await GetModelSizesAsync(cancellationToken);
            
            // 6. Get prompt from Lightning
            var systemPrompt = await GetSelectionPromptAsync(cancellationToken);
            
            // 7. Build the selection prompt with ALL enriched data
            var prompt = BuildSelectionPrompt(taskDescription, language, fileCount, 
                historicalStats, availableModels, newModels, loadedModels, modelSizes);
            
            // 8. Ask LLM to select
            var response = await _ollamaClient.GenerateAsync(
                _selectorModel,
                prompt,
                systemPrompt,
                _ollamaPort,
                cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("LLM selection failed: {Error}", response.Error);
                return GetFallbackSelection(historicalStats, availableModels, loadedModels);
            }

            // 9. Parse response
            var selection = ParseLlmResponse(response.Response, availableModels, loadedModels);
            
            _logger.LogInformation(
                "🧠 Validation model selected: {Model} (exploration={IsExploration}, warm={Warm}, confidence={Confidence:P0})\n   Reasoning: {Reasoning}",
                selection.Model, selection.IsExploration, selection.IsWarmModel, selection.Confidence, selection.Reasoning);

            return selection;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model selection failed, using default");
            return new ValidationModelSelection 
            { 
                Model = _selectorModel, 
                Reasoning = $"Selection failed: {ex.Message}" 
            };
        }
    }
    
    /// <summary>
    /// 🔥 Get models currently loaded in Ollama (warm/instant response)
    /// </summary>
    private async Task<List<string>> GetLoadedModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ollamaUrl = _config.GetValue<string>("Ollama:Url") ?? "http://localhost:11434";
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            
            var response = await httpClient.GetAsync($"{ollamaUrl}/api/ps", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new List<string>();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var psResponse = JsonSerializer.Deserialize<OllamaPsResponse>(content, JsonOptions);
            
            var loadedModels = psResponse?.Models?.Select(m => m.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() 
                ?? new List<string>();
            
            _logger.LogDebug("🔥 Found {Count} warm models: {Models}", loadedModels.Count, string.Join(", ", loadedModels));
            return loadedModels;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query Ollama for loaded models");
            return new List<string>();
        }
    }
    
    /// <summary>
    /// 📊 Get model sizes for VRAM-aware selection
    /// </summary>
    private async Task<Dictionary<string, ModelSizeInfo>> GetModelSizesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ollamaUrl = _config.GetValue<string>("Ollama:Url") ?? "http://localhost:11434";
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            
            var response = await httpClient.GetAsync($"{ollamaUrl}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, ModelSizeInfo>();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var tagsResponse = JsonSerializer.Deserialize<ModelSelectorTagsResponse>(content, JsonOptions);
            
            return tagsResponse?.Models?.ToDictionary(
                m => m.Name ?? "",
                m => new ModelSizeInfo
                {
                    Name = m.Name ?? "",
                    SizeBytes = m.Size,
                    SizeGb = m.Size / (1024.0 * 1024 * 1024),
                    ParameterSize = ExtractParameterSize(m.Name ?? "")
                }) ?? new Dictionary<string, ModelSizeInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query Ollama for model sizes");
            return new Dictionary<string, ModelSizeInfo>();
        }
    }
    
    /// <summary>
    /// Extract parameter size from model name (e.g., "phi4:latest" -> 4, "deepseek:33b" -> 33)
    /// </summary>
    private static int ExtractParameterSize(string modelName)
    {
        var match = Regex.Match(modelName, @":?(\d+)b", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var size))
            return size;
        
        // Extract from phi4, llama7, etc.
        match = Regex.Match(modelName, @"(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out size) && size < 200)
            return size;
        
        return 0;
    }

    private async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ollamaUrl = _config.GetValue<string>("Ollama:Url") ?? "http://localhost:11434";
            using var httpClient = new HttpClient { BaseAddress = new Uri(ollamaUrl) };
            
            var response = await httpClient.GetAsync("/api/tags", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<ModelSelectorTagsResponse>(content, JsonOptions);
                return result?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Ollama models");
        }
        
        // Return default model as fallback
        return new List<string> { _selectorModel };
    }

    private async Task<List<ModelPerformanceStats>> GetHistoricalStatsAsync(
        string language, CancellationToken cancellationToken)
    {
        try
        {
            // Call MemoryAgent's get_model_stats tool
            var stats = await _memoryAgent.GetModelStatsAsync("validation", language, cancellationToken);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get historical stats");
            return new List<ModelPerformanceStats>();
        }
    }

    private async Task<string> GetSelectionPromptAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try to get prompt from Lightning
            var prompt = await _memoryAgent.GetPromptAsync("validation_model_selector", cancellationToken);
            if (prompt != null && !string.IsNullOrEmpty(prompt.Content))
            {
                _logger.LogDebug("Using prompt from Lightning: {PromptName} v{Version}", 
                    prompt.Name, prompt.Version);
                return prompt.Content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get prompt from Lightning, using default");
        }
        
        // Default prompt
        return GetDefaultSelectionPrompt();
    }

    private string GetDefaultSelectionPrompt()
    {
        return @"You are a model selection expert for code validation tasks.

Your job is to select the best LLM model for validating code quality.

SELECTION CRITERIA:
1. Historical performance - prefer models with higher success rates
2. Task complexity - simple validation can use smaller/faster models
3. Language expertise - some models work better with certain languages
4. Exploration - occasionally try NEW models to gather data (especially for simple tasks)

EXPLORATION GUIDELINES:
- If task is SIMPLE and there are untried models, consider exploring (20% chance)
- If task is COMPLEX, stick with proven models
- New models should be tried on low-risk tasks first

🚨 CRITICAL: OUTPUT ONLY RAW JSON - NO EXPLANATION, NO MARKDOWN, NO CODE BLOCKS!

{
    ""selectedModel"": ""model_name_here"",
    ""reasoning"": ""brief explanation"",
    ""isExploration"": false,
    ""confidence"": 0.85
}

DO NOT wrap in ```json``` blocks. DO NOT add any text before or after the JSON.";
    }

    private string BuildSelectionPrompt(
        string taskDescription,
        string language,
        int fileCount,
        List<ModelPerformanceStats> historicalStats,
        List<string> availableModels,
        List<string> newModels,
        List<string> loadedModels,
        Dictionary<string, ModelSizeInfo> modelSizes)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("# Validation Model Selection Request");
        sb.AppendLine();
        sb.AppendLine($"**Task:** Code validation");
        sb.AppendLine($"**Language:** {language}");
        sb.AppendLine($"**Files to validate:** {fileCount}");
        sb.AppendLine();
        sb.AppendLine("**Task Description:**");
        sb.AppendLine(taskDescription.Length > 500 ? taskDescription[..500] + "..." : taskDescription);
        sb.AppendLine();
        
        // 🔥 WARM MODELS - Instant response!
        if (loadedModels.Any())
        {
            sb.AppendLine("## 🔥 WARM MODELS (Already loaded - INSTANT response!)");
            sb.AppendLine();
            sb.AppendLine("These models are currently loaded in VRAM. **Strongly prefer these** to avoid 10-30s cold start:");
            foreach (var model in loadedModels)
            {
                var stat = historicalStats.FirstOrDefault(s => s.Model == model);
                var size = modelSizes.TryGetValue(model, out var sizeInfo) ? $"{sizeInfo.SizeGb:F1}GB" : "?GB";
                if (stat != null)
                {
                    sb.AppendLine($"- 🔥 **{model}** ({size}) - Success: {stat.SuccessRate:F0}%, Score: {stat.AverageScore:F1}");
                }
                else
                {
                    sb.AppendLine($"- 🔥 **{model}** ({size}) - No historical data, but WARM!");
                }
            }
            sb.AppendLine();
        }
        
        // Historical performance data (with time-decay note)
        sb.AppendLine("## Historical Performance (Time-Decay Weighted)");
        sb.AppendLine("*Recent performance (24h) counts 3x, last week 2x, older 1x*");
        sb.AppendLine();
        
        if (historicalStats.Any())
        {
            sb.AppendLine("| Model | Success Rate | Avg Score | Samples | Size | Warm? |");
            sb.AppendLine("|-------|--------------|-----------|---------|------|-------|");
            
            foreach (var stat in historicalStats.OrderByDescending(s => s.SuccessRate))
            {
                var size = modelSizes.TryGetValue(stat.Model, out var sizeInfo) ? $"{sizeInfo.SizeGb:F1}GB" : "?";
                var isWarm = loadedModels.Contains(stat.Model) ? "🔥 YES" : "❄️ cold";
                sb.AppendLine($"| {stat.Model} | {stat.SuccessRate:F0}% | {stat.AverageScore:F1} | {stat.TotalAttempts} | {size} | {isWarm} |");
            }
        }
        else
        {
            sb.AppendLine("*No historical data available yet.*");
        }
        
        sb.AppendLine();
        
        // Highlight new/untried models
        if (newModels.Any())
        {
            sb.AppendLine("## 🆕 NEW MODELS (Never tried for validation)");
            sb.AppendLine();
            sb.AppendLine("These models have been downloaded but never used for validation:");
            foreach (var model in newModels)
            {
                var size = modelSizes.TryGetValue(model, out var sizeInfo) ? $"({sizeInfo.SizeGb:F1}GB, ~{sizeInfo.ParameterSize}B params)" : "";
                var isWarm = loadedModels.Contains(model) ? " 🔥 WARM" : "";
                sb.AppendLine($"- **{model}** {size}{isWarm} ← Consider trying on simple tasks!");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("## Selection Instructions");
        sb.AppendLine();
        sb.AppendLine("Select the best model considering (IN PRIORITY ORDER):");
        sb.AppendLine("1. **🔥 WARM MODELS** - Strongly prefer already-loaded models (10-30s faster!)");
        sb.AppendLine("2. **Historical success rates** - Choose proven performers");
        sb.AppendLine("3. **Model size vs file count** - Few files → smaller models OK, Many files → larger models");
        sb.AppendLine("4. **Exploration** - For small validations (1-2 files), consider trying NEW models (20% chance)");
        sb.AppendLine("5. **Cross-language transfer** - If no direct data, similar languages may transfer");
        
        return sb.ToString();
    }

    private ValidationModelSelection ParseLlmResponse(string response, List<string> availableModels, List<string> loadedModels)
    {
        try
        {
            // Log raw response for debugging
            _logger.LogDebug("Raw LLM response for validation model selection: {Response}", 
                response.Length > 500 ? response[..500] + "..." : response);
            
            // Try multiple extraction methods
            var json = ExtractJsonFromResponse(response);
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("No JSON found in LLM response. Response preview: {Preview}", 
                    response.Length > 200 ? response[..200] : response);
                return new ValidationModelSelection { Model = _selectorModel, Reasoning = "Could not parse response" };
            }
            
            var parsed = JsonSerializer.Deserialize<LlmSelectionResponse>(json, JsonOptions);
            
            if (parsed != null && !string.IsNullOrEmpty(parsed.SelectedModel))
            {
                // Validate model is available
                var model = availableModels.FirstOrDefault(m => 
                    m.Equals(parsed.SelectedModel, StringComparison.OrdinalIgnoreCase));
                
                if (model != null)
                {
                    return new ValidationModelSelection
                    {
                        Model = model,
                        Reasoning = parsed.Reasoning ?? "LLM selection",
                        IsExploration = parsed.IsExploration,
                        Confidence = Math.Clamp(parsed.Confidence, 0, 1),
                        IsWarmModel = loadedModels.Contains(model)
                    };
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response");
        }
        
        return new ValidationModelSelection { Model = _selectorModel, Reasoning = "Parse failed - using default" };
    }

    /// <summary>
    /// Extract JSON from LLM response, handling various formats:
    /// - Raw JSON
    /// - Markdown code blocks (```json ... ```)
    /// - JSON with surrounding text
    /// </summary>
    private string? ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;
        
        // Method 1: Try to find JSON in markdown code block
        var markdownMatch = Regex.Match(response, @"```(?:json)?\s*\n?\s*(\{[\s\S]*?\})\s*\n?```", RegexOptions.Singleline);
        if (markdownMatch.Success)
        {
            _logger.LogDebug("Extracted JSON from markdown code block");
            return markdownMatch.Groups[1].Value.Trim();
        }
        
        // Method 2: Find raw JSON object (greedy - takes first complete object)
        var jsonMatch = Regex.Match(response, @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            _logger.LogDebug("Extracted raw JSON object");
            return jsonMatch.Value.Trim();
        }
        
        // Method 3: Try the whole response if it looks like JSON
        var trimmed = response.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            _logger.LogDebug("Response is raw JSON");
            return trimmed;
        }
        
        return null;
    }

    private ValidationModelSelection GetFallbackSelection(
        List<ModelPerformanceStats> historicalStats,
        List<string> availableModels,
        List<string> loadedModels)
    {
        // 🔥 Priority 1: Prefer warm models with good history
        var warmWithHistory = loadedModels
            .Select(m => historicalStats.FirstOrDefault(s => s.Model == m))
            .Where(s => s != null && s.SuccessRate >= 50)
            .OrderByDescending(s => s!.SuccessRate)
            .FirstOrDefault();
        
        if (warmWithHistory != null)
        {
            return new ValidationModelSelection
            {
                Model = warmWithHistory.Model,
                Reasoning = $"Using warm model with good history ({warmWithHistory.SuccessRate:F0}% success rate)",
                Confidence = 0.7,
                IsWarmModel = true
            };
        }
        
        // 🔥 Priority 2: Any warm model
        if (loadedModels.Any() && availableModels.Any(m => loadedModels.Contains(m)))
        {
            var warmModel = availableModels.First(m => loadedModels.Contains(m));
            return new ValidationModelSelection
            {
                Model = warmModel,
                Reasoning = "Using warm model for instant response (no historical data)",
                Confidence = 0.5,
                IsWarmModel = true
            };
        }
        
        // Priority 3: Best historical performer or default
        var best = historicalStats
            .Where(s => availableModels.Contains(s.Model))
            .OrderByDescending(s => s.SuccessRate)
            .ThenByDescending(s => s.AverageScore)
            .FirstOrDefault();
        
        return new ValidationModelSelection
        {
            Model = best?.Model ?? _selectorModel,
            Reasoning = best != null 
                ? $"Fallback to best historical: {best.SuccessRate:F0}% success rate"
                : "Fallback to default model",
            Confidence = 0.5,
            IsWarmModel = false
        };
    }
}

#region Helper Classes

// Local copies for JSON deserialization (internal in OllamaClient)
internal class ModelSelectorTagsResponse
{
    public List<ModelSelectorModelInfo>? Models { get; set; }
}

internal class ModelSelectorModelInfo
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
}

internal class LlmSelectionResponse
{
    public string? SelectedModel { get; set; }
    public string? Reasoning { get; set; }
    public bool IsExploration { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Ollama /api/ps response for loaded models
/// </summary>
internal class OllamaPsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("models")]
    public List<OllamaPsModel>? Models { get; set; }
}

internal class OllamaPsModel
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// Model size information for VRAM-aware selection
/// </summary>
internal class ModelSizeInfo
{
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public double SizeGb { get; set; }
    public int ParameterSize { get; set; }
}

#endregion

