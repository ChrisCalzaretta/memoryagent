using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Models;
using AgentContracts.Services;
using DesignAgent.Server.Clients;

namespace DesignAgent.Server.Services;

/// <summary>
/// 🧠 Smart model selection for Design Agent
/// - Uses historical performance from MemoryAgent
/// - Prefers warm/loaded models
/// - Uses LLM to confirm selection
/// - Fetches prompts from Lightning
/// </summary>
public interface IDesignModelSelector
{
    /// <summary>
    /// Select the best model for a design task
    /// </summary>
    Task<DesignModelSelection> SelectModelAsync(
        string taskDescription,
        string taskType, // "brand_generation", "validation", "suggestion"
        CancellationToken cancellationToken = default);
}

public class DesignModelSelection
{
    public string Model { get; set; } = "";
    public int Port { get; set; } = 11434;
    public string Reasoning { get; set; } = "";
    public bool IsWarmModel { get; set; }
    public double Confidence { get; set; } = 0.5;
}

public class DesignModelSelector : IDesignModelSelector
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly IOllamaClient _ollamaClient;
    private readonly IConfiguration _config;
    private readonly ILogger<DesignModelSelector> _logger;
    
    private readonly string _defaultModel;
    private readonly int _defaultPort;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public DesignModelSelector(
        IMemoryAgentClient memoryAgent,
        IOllamaClient ollamaClient,
        IConfiguration config,
        ILogger<DesignModelSelector> logger)
    {
        _memoryAgent = memoryAgent;
        _ollamaClient = ollamaClient;
        _config = config;
        _logger = logger;
        
        _defaultModel = config.GetValue<string>("Ollama:DefaultModel") ?? "phi4:latest";
        _defaultPort = config.GetValue<int>("Ollama:Port", 11434);
    }

    public async Task<DesignModelSelection> SelectModelAsync(
        string taskDescription,
        string taskType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Get available models
            var availableModels = await _ollamaClient.GetModelsAsync(_defaultPort, cancellationToken);
            if (!availableModels.Any())
            {
                _logger.LogWarning("No models available, using default");
                return new DesignModelSelection 
                { 
                    Model = _defaultModel, 
                    Port = _defaultPort,
                    Reasoning = "No models available - using default" 
                };
            }
            
            // 2. Get warm/loaded models
            var loadedModels = await GetLoadedModelsAsync(cancellationToken);
            
            // 3. Get historical stats
            var historicalStats = await _memoryAgent.GetModelStatsAsync("design", taskType, cancellationToken);
            
            // 4. Identify new/untried models
            var triedModels = historicalStats.Select(s => s.Model).ToHashSet();
            var newModels = availableModels.Where(m => !triedModels.Contains(m)).ToList();
            
            // 5. Get prompt from Lightning
            var systemPrompt = await GetSelectionPromptAsync(cancellationToken);
            
            // 6. Build selection prompt
            var prompt = BuildSelectionPrompt(taskDescription, taskType, 
                historicalStats, availableModels, newModels, loadedModels);
            
            // 7. Ask LLM to select
            var response = await _ollamaClient.GenerateAsync(
                _defaultModel,
                prompt,
                systemPrompt,
                _defaultPort,
                cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("LLM selection failed: {Error}", response.Error);
                return GetFallbackSelection(historicalStats, availableModels, loadedModels);
            }

            // 8. Parse response
            var selection = ParseLlmResponse(response.Response, availableModels, loadedModels);
            
            _logger.LogInformation(
                "🎨 Design model selected: {Model} (warm={Warm}, confidence={Confidence:P0})\n   Reasoning: {Reasoning}",
                selection.Model, selection.IsWarmModel, selection.Confidence, selection.Reasoning);

            return selection;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model selection failed, using default");
            return new DesignModelSelection 
            { 
                Model = _defaultModel, 
                Port = _defaultPort,
                Reasoning = $"Selection failed: {ex.Message}" 
            };
        }
    }

    private async Task<List<string>> GetLoadedModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var ollamaUrl = _config.GetValue<string>("Ollama:Url") ?? "http://localhost:11434";
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            
            var response = await httpClient.GetAsync($"{ollamaUrl}/api/ps", cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<string>();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var psResponse = JsonSerializer.Deserialize<OllamaPsResponse>(content, JsonOptions);
            
            return psResponse?.Models?.Select(m => m.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList() 
                ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query Ollama for loaded models");
            return new List<string>();
        }
    }

    private async Task<string> GetSelectionPromptAsync(CancellationToken cancellationToken)
    {
        // This will throw if prompt not found - NO FALLBACK
        var prompt = await _memoryAgent.GetPromptAsync("design_model_selector", cancellationToken);
        if (prompt == null || string.IsNullOrEmpty(prompt.Content))
        {
            throw new InvalidOperationException("Required prompt 'design_model_selector' not found or empty in Lightning. Run PromptSeedService.");
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
        List<ModelStats> historicalStats,
        List<string> availableModels,
        List<string> newModels,
        List<string> loadedModels)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine("# Design Model Selection Request");
        sb.AppendLine();
        sb.AppendLine($"**Task Type:** {taskType}");
        sb.AppendLine();
        sb.AppendLine("**Task Description:**");
        sb.AppendLine(taskDescription.Length > 500 ? taskDescription[..500] + "..." : taskDescription);
        sb.AppendLine();
        
        // Warm models
        if (loadedModels.Any())
        {
            sb.AppendLine("## 🔥 WARM MODELS (Already loaded - INSTANT response!)");
            sb.AppendLine();
            foreach (var model in loadedModels)
            {
                var stat = historicalStats.FirstOrDefault(s => s.Model == model);
                if (stat != null)
                {
                    sb.AppendLine($"- 🔥 **{model}** - Success: {stat.SuccessRate:F0}%, Score: {stat.AverageScore:F1}");
                }
                else
                {
                    sb.AppendLine($"- 🔥 **{model}** - No historical data, but WARM!");
                }
            }
            sb.AppendLine();
        }
        
        // Historical performance
        sb.AppendLine("## Historical Performance (Design Tasks)");
        sb.AppendLine();
        
        if (historicalStats.Any())
        {
            sb.AppendLine("| Model | Success Rate | Avg Score | Samples | Warm? |");
            sb.AppendLine("|-------|--------------|-----------|---------|-------|");
            
            foreach (var stat in historicalStats.OrderByDescending(s => s.SuccessRate))
            {
                var isWarm = loadedModels.Contains(stat.Model) ? "🔥 YES" : "❄️ cold";
                sb.AppendLine($"| {stat.Model} | {stat.SuccessRate:F0}% | {stat.AverageScore:F1} | {stat.TotalAttempts} | {isWarm} |");
            }
        }
        else
        {
            sb.AppendLine("*No historical data available yet.*");
        }
        
        sb.AppendLine();
        
        // New models
        if (newModels.Any())
        {
            sb.AppendLine("## 🆕 NEW MODELS (Never tried for design tasks)");
            sb.AppendLine();
            foreach (var model in newModels.Take(5))
            {
                var isWarm = loadedModels.Contains(model) ? " 🔥 WARM" : "";
                sb.AppendLine($"- **{model}**{isWarm} ← Consider trying on simple tasks!");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine("## Selection Instructions");
        sb.AppendLine();
        sb.AppendLine("Select the best model considering:");
        sb.AppendLine("1. **🔥 WARM MODELS** - Strongly prefer already-loaded models");
        sb.AppendLine("2. **Historical success rates** - Choose proven performers");
        sb.AppendLine("3. **Design capability** - Creative/larger models often better for design");
        
        return sb.ToString();
    }

    private DesignModelSelection ParseLlmResponse(string response, List<string> availableModels, List<string> loadedModels)
    {
        try
        {
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}", RegexOptions.Singleline);
            if (!jsonMatch.Success)
            {
                return GetFallbackSelection(new List<ModelStats>(), availableModels, loadedModels);
            }
            
            var parsed = JsonSerializer.Deserialize<LlmSelectionResponse>(jsonMatch.Value, JsonOptions);
            
            if (parsed != null && !string.IsNullOrEmpty(parsed.SelectedModel))
            {
                var model = availableModels.FirstOrDefault(m => 
                    m.Equals(parsed.SelectedModel, StringComparison.OrdinalIgnoreCase));
                
                if (model != null)
                {
                    return new DesignModelSelection
                    {
                        Model = model,
                        Port = _defaultPort,
                        Reasoning = parsed.Reasoning ?? "LLM selection",
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
        
        return GetFallbackSelection(new List<ModelStats>(), availableModels, loadedModels);
    }

    private DesignModelSelection GetFallbackSelection(
        List<ModelStats> historicalStats,
        List<string> availableModels,
        List<string> loadedModels)
    {
        // Priority 1: Warm model with good history
        var warmWithHistory = loadedModels
            .Select(m => historicalStats.FirstOrDefault(s => s.Model == m))
            .Where(s => s != null && s.SuccessRate >= 50)
            .OrderByDescending(s => s!.SuccessRate)
            .FirstOrDefault();
        
        if (warmWithHistory != null)
        {
            return new DesignModelSelection
            {
                Model = warmWithHistory.Model,
                Port = _defaultPort,
                Reasoning = $"Using warm model with good history ({warmWithHistory.SuccessRate:F0}% success rate)",
                Confidence = 0.7,
                IsWarmModel = true
            };
        }
        
        // Priority 2: Any warm model
        if (loadedModels.Any() && availableModels.Any(m => loadedModels.Contains(m)))
        {
            var warmModel = availableModels.First(m => loadedModels.Contains(m));
            return new DesignModelSelection
            {
                Model = warmModel,
                Port = _defaultPort,
                Reasoning = "Using warm model for instant response",
                Confidence = 0.5,
                IsWarmModel = true
            };
        }
        
        // Priority 3: Default model
        return new DesignModelSelection
        {
            Model = _defaultModel,
            Port = _defaultPort,
            Reasoning = "Fallback to default model",
            Confidence = 0.5,
            IsWarmModel = false
        };
    }
}

#region Helper Classes

internal class LlmSelectionResponse
{
    public string? SelectedModel { get; set; }
    public string? Reasoning { get; set; }
    public double Confidence { get; set; }
}

internal class OllamaPsResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("models")]
    public List<OllamaPsModel>? Models { get; set; }
}

internal class OllamaPsModel
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }
}

#endregion

