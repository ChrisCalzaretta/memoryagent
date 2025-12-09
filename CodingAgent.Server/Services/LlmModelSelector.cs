using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Models;
using AgentContracts.Services;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// 🧠 LLM-based model selection with historical rates in prompt
/// Uses the primary (pinned) model to analyze the task and recommend the best model
/// Includes: warm model preference, VRAM tracking, cross-language learning, time-decay
/// </summary>
public interface ILlmModelSelector
{
    /// <summary>
    /// Use LLM to confirm/adjust model selection based on task and historical data
    /// </summary>
    Task<LlmModelRecommendation> SelectModelAsync(
        string taskDescription,
        string taskType,
        string? language,
        List<ModelStats> historicalStats,
        List<string> availableModels,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM's recommendation for which model to use
/// </summary>
public class LlmModelRecommendation
{
    public string RecommendedModel { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public string TaskComplexity { get; set; } = "moderate";
    public double Confidence { get; set; } = 0.5;
    public bool UsedLlm { get; set; } = false;
    public bool IsExploration { get; set; } = false;
    public bool IsWarmModel { get; set; } = false;
}

public class LlmModelSelector : ILlmModelSelector
{
    private readonly IModelOrchestrator _modelOrchestrator;
    private readonly IOllamaClient _ollamaClient;
    private readonly IMemoryAgentClient? _memoryAgent;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LlmModelSelector> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public LlmModelSelector(
        IModelOrchestrator modelOrchestrator,
        IOllamaClient ollamaClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LlmModelSelector> logger,
        IMemoryAgentClient? memoryAgent = null)  // Optional - graceful degradation
    {
        _modelOrchestrator = modelOrchestrator;
        _ollamaClient = ollamaClient;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _memoryAgent = memoryAgent;
        _logger = logger;
    }

    public async Task<LlmModelRecommendation> SelectModelAsync(
        string taskDescription,
        string taskType,
        string? language,
        List<ModelStats> historicalStats,
        List<string> availableModels,
        CancellationToken cancellationToken = default)
    {
        var (primaryModel, primaryPort) = _modelOrchestrator.GetPrimaryModel();
        
        // Identify new/untried models for exploration
        var triedModels = historicalStats.Select(s => s.Model).ToHashSet();
        var newModels = availableModels.Where(m => !triedModels.Contains(m)).ToList();
        
        // 🔥 Get currently loaded (warm) models for instant response preference
        var loadedModels = await GetLoadedModelsAsync(cancellationToken);
        
        // Get model sizes/VRAM info
        var modelSizes = await GetModelSizesAsync(cancellationToken);
        
        // Build the prompt with ALL enriched data
        var prompt = BuildSelectionPrompt(
            taskDescription, taskType, language, 
            historicalStats, availableModels, newModels,
            loadedModels, modelSizes);
        
        // Get prompt from Lightning (or use default)
        var systemPrompt = await GetSelectionPromptAsync(cancellationToken);

        try
        {
            _logger.LogInformation("🧠 Asking LLM ({Model}) to select best model for task", primaryModel);
            
            var response = await _ollamaClient.GenerateAsync(
                primaryModel,
                prompt,
                systemPrompt,
                primaryPort,
                cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("LLM model selection failed: {Error}. Using default selection.", response.Error);
                return GetDefaultRecommendation(historicalStats, availableModels, loadedModels);
            }

            // Parse LLM response
            var recommendation = ParseLlmResponse(response.Response, historicalStats, availableModels, loadedModels);
            recommendation.UsedLlm = true;
            
            // Check if recommended model is warm
            recommendation.IsWarmModel = loadedModels.Any(m => 
                m.Equals(recommendation.RecommendedModel, StringComparison.OrdinalIgnoreCase));
            
            _logger.LogInformation(
                "🧠 LLM recommends {Model} ({Complexity} task, {Confidence:P0} confidence, warm={Warm}): {Reasoning}",
                recommendation.RecommendedModel,
                recommendation.TaskComplexity,
                recommendation.Confidence,
                recommendation.IsWarmModel,
                recommendation.Reasoning);

            return recommendation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error in LLM model selection. Using default selection.");
            return GetDefaultRecommendation(historicalStats, availableModels, loadedModels);
        }
    }
    
    /// <summary>
    /// 🔥 Get models currently loaded in Ollama (warm/instant response)
    /// </summary>
    private async Task<List<string>> GetLoadedModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ollamaUrl = _configuration["Ollama:PrimaryUrl"] ?? "http://localhost:11434";
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            
            var response = await client.GetAsync($"{ollamaUrl}/api/ps", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new List<string>();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var psResponse = JsonSerializer.Deserialize<CodingSelectorPsResponse>(content, JsonOptions);
            
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
    private async Task<Dictionary<string, CodingModelSizeInfo>> GetModelSizesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ollamaUrl = _configuration["Ollama:PrimaryUrl"] ?? "http://localhost:11434";
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            
            var response = await client.GetAsync($"{ollamaUrl}/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, CodingModelSizeInfo>();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var tagsResponse = JsonSerializer.Deserialize<CodingSelectorTagsResponse>(content, JsonOptions);
            
            return tagsResponse?.Models?.ToDictionary(
                m => m.Name ?? "",
                m => new CodingModelSizeInfo
                {
                    Name = m.Name ?? "",
                    SizeBytes = m.Size,
                    SizeGb = m.Size / (1024.0 * 1024 * 1024),
                    ParameterSize = ExtractParameterSize(m.Name ?? "")
                }) ?? new Dictionary<string, CodingModelSizeInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query Ollama for model sizes");
            return new Dictionary<string, CodingModelSizeInfo>();
        }
    }
    
    /// <summary>
    /// Extract parameter size from model name (e.g., "deepseek-coder:33b" -> 33)
    /// </summary>
    private static int ExtractParameterSize(string modelName)
    {
        var match = Regex.Match(modelName, @":(\d+)b", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var size))
            return size;
        
        // Common model sizes
        if (modelName.Contains("7b", StringComparison.OrdinalIgnoreCase)) return 7;
        if (modelName.Contains("13b", StringComparison.OrdinalIgnoreCase)) return 13;
        if (modelName.Contains("16b", StringComparison.OrdinalIgnoreCase)) return 16;
        if (modelName.Contains("34b", StringComparison.OrdinalIgnoreCase)) return 34;
        if (modelName.Contains("70b", StringComparison.OrdinalIgnoreCase)) return 70;
        
        return 0; // Unknown
    }

    /// <summary>
    /// Get selection prompt from Lightning - NO FALLBACK
    /// </summary>
    private async Task<string> GetSelectionPromptAsync(CancellationToken cancellationToken)
    {
        if (_memoryAgent == null)
        {
            throw new InvalidOperationException("MemoryAgent client not available. Cannot get model selection prompt.");
        }
        
        // This will throw if prompt not found - NO FALLBACK
        var prompt = await _memoryAgent.GetPromptAsync("coding_model_selector", cancellationToken);
        if (prompt == null || string.IsNullOrEmpty(prompt.Content))
        {
            throw new InvalidOperationException("Required prompt 'coding_model_selector' not found or empty in Lightning. Run PromptSeedService.");
        }
        
        _logger.LogDebug("Using prompt from Lightning: {PromptName} v{Version}", prompt.Name, prompt.Version);
        return prompt.Content;
    }
    
    // NO FALLBACK PROMPTS - All prompts MUST come from Lightning
    // If a prompt is missing, the system will throw an error
    // Run PromptSeedService to seed required prompts into Neo4j

    private string BuildSelectionPrompt(
        string taskDescription,
        string taskType,
        string? language,
        List<ModelStats> historicalStats,
        List<string> availableModels,
        List<string> newModels,
        List<string> loadedModels,
        Dictionary<string, CodingModelSizeInfo> modelSizes)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("# Model Selection Request");
        sb.AppendLine();
        sb.AppendLine($"**Task Type:** {taskType}");
        sb.AppendLine($"**Language:** {language ?? "unknown"}");
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
        
        // Add historical performance data (with time-decay note)
        sb.AppendLine("## Historical Performance Data (Time-Decay Weighted)");
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
            sb.AppendLine("*No historical data available. This is the first time running this type of task.*");
        }
        
        sb.AppendLine();
        
        // Highlight new/untried models for exploration
        if (newModels.Any())
        {
            sb.AppendLine("## 🆕 NEW MODELS (Never tried for this task type)");
            sb.AppendLine();
            sb.AppendLine("These models have been downloaded but never used for this task type:");
            foreach (var model in newModels)
            {
                var size = modelSizes.TryGetValue(model, out var sizeInfo) ? $"({sizeInfo.SizeGb:F1}GB, ~{sizeInfo.ParameterSize}B params)" : "";
                var isWarm = loadedModels.Contains(model) ? " 🔥 WARM" : "";
                sb.AppendLine($"- **{model}** {size}{isWarm} ← Consider trying on simple tasks!");
            }
            sb.AppendLine();
        }
        
        // Model size info for VRAM-aware selection
        if (modelSizes.Any())
        {
            sb.AppendLine("## 📊 Model Sizes (VRAM Consideration)");
            sb.AppendLine();
            sb.AppendLine("For SIMPLE tasks, prefer smaller models. For COMPLEX tasks, larger models may be needed:");
            sb.AppendLine();
            foreach (var kvp in modelSizes.OrderBy(m => m.Value.SizeBytes).Take(10))
            {
                var paramSize = kvp.Value.ParameterSize > 0 ? $"~{kvp.Value.ParameterSize}B" : "?B";
                sb.AppendLine($"- {kvp.Key}: {kvp.Value.SizeGb:F1}GB ({paramSize})");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("## Selection Instructions");
        sb.AppendLine();
        sb.AppendLine("Select the best model considering (IN PRIORITY ORDER):");
        sb.AppendLine("1. **🔥 WARM MODELS** - Strongly prefer already-loaded models (10-30s faster!)");
        sb.AppendLine("2. **Historical success rates** - Choose proven performers");
        sb.AppendLine("3. **Model size vs complexity** - Simple tasks → smaller models, Complex → larger");
        sb.AppendLine("4. **Exploration** - For SIMPLE tasks, consider trying NEW models (20% chance)");
        sb.AppendLine("5. **Cross-language transfer** - If no direct data, similar languages may transfer");
        
        return sb.ToString();
    }

    private LlmModelRecommendation ParseLlmResponse(
        string response,
        List<ModelStats> historicalStats,
        List<string> availableModels,
        List<string> loadedModels)
    {
        try
        {
            // Log raw response for debugging
            _logger.LogDebug("Raw LLM response for model selection: {Response}", 
                response.Length > 500 ? response[..500] + "..." : response);
            
            // Try multiple extraction methods
            var json = ExtractJsonFromResponse(response);
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("No JSON found in LLM response. Response preview: {Preview}", 
                    response.Length > 200 ? response[..200] : response);
                return GetDefaultRecommendation(historicalStats, availableModels, loadedModels);
            }
            var parsed = JsonSerializer.Deserialize<LlmCodingSelectionResponse>(json, JsonOptions);
            
            if (parsed != null && !string.IsNullOrEmpty(parsed.SelectedModel))
            {
                // Validate that recommended model is in available list
                var recommendedModel = availableModels.FirstOrDefault(m => 
                    m.Equals(parsed.SelectedModel, StringComparison.OrdinalIgnoreCase));
                
                if (recommendedModel != null)
                {
                    return new LlmModelRecommendation
                    {
                        RecommendedModel = recommendedModel,
                        Reasoning = parsed.Reasoning ?? "LLM recommendation",
                        TaskComplexity = parsed.TaskComplexity ?? "moderate",
                        Confidence = Math.Clamp(parsed.Confidence, 0, 1),
                        IsExploration = parsed.IsExploration,
                        IsWarmModel = loadedModels.Contains(recommendedModel)
                    };
                }
                else
                {
                    _logger.LogWarning("LLM recommended unavailable model: {Model}", parsed.SelectedModel);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM selection response");
        }
        
        return GetDefaultRecommendation(historicalStats, availableModels, loadedModels);
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

    private LlmModelRecommendation GetDefaultRecommendation(
        List<ModelStats> historicalStats,
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
            return new LlmModelRecommendation
            {
                RecommendedModel = warmWithHistory.Model,
                Reasoning = $"Using warm model with good history ({warmWithHistory.SuccessRate:F0}% success rate)",
                TaskComplexity = "moderate",
                Confidence = 0.7,
                UsedLlm = false,
                IsWarmModel = true
            };
        }
        
        // 🔥 Priority 2: Any warm model
        if (loadedModels.Any() && availableModels.Any(m => loadedModels.Contains(m)))
        {
            var warmModel = availableModels.First(m => loadedModels.Contains(m));
            return new LlmModelRecommendation
            {
                RecommendedModel = warmModel,
                Reasoning = "Using warm model for instant response (no historical data)",
                TaskComplexity = "moderate",
                Confidence = 0.5,
                UsedLlm = false,
                IsWarmModel = true
            };
        }
        
        // Priority 3: Best historical performer
        var bestModel = historicalStats
            .Where(s => availableModels.Contains(s.Model))
            .OrderByDescending(s => s.SuccessRate)
            .ThenByDescending(s => s.AverageScore)
            .FirstOrDefault()?.Model ?? availableModels.FirstOrDefault() ?? "";
        
        return new LlmModelRecommendation
        {
            RecommendedModel = bestModel,
            Reasoning = historicalStats.Any() 
                ? "Selected based on historical success rate (LLM selection failed)"
                : "No historical data - using default model",
            TaskComplexity = "moderate",
            Confidence = 0.5,
            UsedLlm = false,
            IsWarmModel = false
        };
    }
}

/// <summary>
/// LLM's JSON response structure for model selection
/// </summary>
internal class LlmCodingSelectionResponse
{
    public string? SelectedModel { get; set; }
    public string? Reasoning { get; set; }
    public string? TaskComplexity { get; set; }
    public double Confidence { get; set; }
    public bool IsExploration { get; set; }
}

/// <summary>
/// Ollama /api/ps response for loaded models (local copy)
/// </summary>
internal class CodingSelectorPsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("models")]
    public List<CodingSelectorPsModel>? Models { get; set; }
}

internal class CodingSelectorPsModel
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// Ollama /api/tags response for model info (local copy)
/// </summary>
internal class CodingSelectorTagsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("models")]
    public List<CodingSelectorModelInfo>? Models { get; set; }
}

internal class CodingSelectorModelInfo
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// Model size information for VRAM-aware selection
/// </summary>
internal class CodingModelSizeInfo
{
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public double SizeGb { get; set; }
    public int ParameterSize { get; set; } // e.g., 7 for 7B model
}

