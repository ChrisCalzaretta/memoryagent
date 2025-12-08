using System.Text.Json;
using AgentContracts.Models;
using AgentContracts.Services;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Orchestrates model selection with smart GPU routing and rotation
/// - Dynamically discovers models from Ollama API
/// - Never unloads pinned models
/// - Rotates to fresh models when validation fails
/// - Respects VRAM constraints
/// - 🧠 NOW WITH LEARNING: Queries MemoryAgent for best model based on history!
/// </summary>
public partial class ModelOrchestrator : IModelOrchestrator
{
    private readonly ILogger<ModelOrchestrator> _logger;
    private readonly HttpClient _httpClient;
    private readonly IMemoryAgentClient? _memoryAgent;
    private readonly string _baseHost;
    
    public GpuConfig Config { get; }
    
    // Dynamically populated from Ollama
    private Dictionary<string, ModelInfo> _modelRegistry = new();
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public ModelOrchestrator(
        IConfiguration config, 
        ILogger<ModelOrchestrator> logger,
        HttpClient httpClient,
        IMemoryAgentClient? memoryAgent = null)  // Optional - graceful degradation if unavailable
    {
        _logger = logger;
        _httpClient = httpClient;
        _memoryAgent = memoryAgent;
        
        // Parse Ollama URL (support full URL or default to localhost)
        var configuredUrl = config.GetValue<string>("Ollama:Url");
        var defaultPort = config.GetValue<int>("Ollama:Port", 11434);
        
        if (!string.IsNullOrEmpty(configuredUrl) && Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri))
        {
            _baseHost = $"{uri.Scheme}://{uri.Host}";
            defaultPort = uri.Port > 0 ? uri.Port : defaultPort;
        }
        else
        {
            _baseHost = "http://localhost";
        }
        
        // Load GPU configuration
        Config = new GpuConfig
        {
            DualGpu = config.GetValue<bool>("Gpu:DualGpu", false),
            PinnedPort = config.GetValue<int>("Gpu:PinnedPort", defaultPort),
            SwapPort = config.GetValue<int>("Gpu:SwapPort", 11435),
            PrimaryModel = config.GetValue<string>("Gpu:PrimaryModel") ?? "deepseek-v2:16b",
            EmbeddingModel = config.GetValue<string>("Gpu:EmbeddingModel") ?? "mxbai-embed-large:latest",
            PinnedGpuVram = config.GetValue<int>("Gpu:PinnedGpuVram", 16),
            SwapGpuVram = config.GetValue<int>("Gpu:SwapGpuVram", 24),
            PinnedModelsVram = config.GetValue<int>("Gpu:PinnedModelsVram", 11),
            UseSmartModelSelection = config.GetValue<bool>("Gpu:UseSmartModelSelection", true)
        };
        
        _logger.LogInformation(
            "ModelOrchestrator initialized: BaseHost={BaseHost}, DualGpu={DualGpu}, Primary={Primary}, PinnedPort={PinnedPort}, SmartSelection={SmartSelection}",
            _baseHost, Config.DualGpu, Config.PrimaryModel, Config.PinnedPort, Config.UseSmartModelSelection);
        
        if (!Config.UseSmartModelSelection)
        {
            _logger.LogInformation("⚡ Smart model selection DISABLED - using primary model only (optimal for single GPU)");
        }
    }
    
    /// <summary>
    /// Discover all available models from Ollama API
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;
            
            _logger.LogInformation("Discovering models from Ollama...");
            
            // Query Ollama for installed models
            var models = await QueryOllamaModelsAsync(Config.PinnedPort, cancellationToken);
            
            // If dual GPU, also check swap port
            if (Config.DualGpu)
            {
                var swapModels = await QueryOllamaModelsAsync(Config.SwapPort, cancellationToken);
                foreach (var m in swapModels)
                {
                    if (!models.ContainsKey(m.Key))
                        models[m.Key] = m.Value;
                }
            }
            
            _modelRegistry = models;
            _isInitialized = true;
            
            _logger.LogInformation("Discovered {Count} models from Ollama:", models.Count);
            foreach (var m in models.Values.OrderBy(m => m.Purpose).ThenBy(m => m.Priority))
            {
                _logger.LogInformation("  {Model}: {Size}GB [{Purpose}]", 
                    m.Name, m.SizeGb.ToString("F1"), m.Purpose);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
    
    /// <summary>
    /// Query Ollama API for available models
    /// </summary>
    private async Task<Dictionary<string, ModelInfo>> QueryOllamaModelsAsync(
        int port, 
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ModelInfo>();
        
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseHost}:{port}/api/tags", 
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not query Ollama on port {Port}: {Status}", 
                    port, response.StatusCode);
                return result;
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tagsResponse = JsonSerializer.Deserialize<OllamaModelsResponse>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (tagsResponse?.Models == null) return result;
            
            foreach (var model in tagsResponse.Models)
            {
                var sizeGb = model.Size / 1_000_000_000.0; // bytes to GB
                var purpose = CategorizeModel(model.Name);
                var priority = CalculatePriority(model.Name, sizeGb);
                
                // Skip embedding models for inference
                if (model.Name.Contains("embed", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                result[model.Name] = new ModelInfo
                {
                    Name = model.Name,
                    SizeGb = sizeGb,
                    Purpose = purpose,
                    Priority = priority
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying Ollama on port {Port}", port);
        }
        
        return result;
    }
    
    /// <summary>
    /// Auto-categorize model by its name
    /// </summary>
    private static ModelPurpose CategorizeModel(string modelName)
    {
        var lower = modelName.ToLowerInvariant();
        
        // Code generation models
        if (lower.Contains("coder") || 
            lower.Contains("codellama") || 
            lower.Contains("codegemma") ||
            lower.Contains("starcoder") ||
            lower.Contains("codestral") ||
            lower.Contains("wizardcoder") ||
            lower.Contains("deepseek-coder"))
        {
            return ModelPurpose.CodeGeneration;
        }
        
        // Validation/reasoning models
        if (lower.Contains("phi") || 
            lower.Contains("qwen") && !lower.Contains("coder"))
        {
            return ModelPurpose.Validation;
        }
        
        // DeepSeek v2 is good for both
        if (lower.Contains("deepseek-v2") || lower.Contains("deepseek:"))
        {
            return ModelPurpose.CodeGeneration;
        }
        
        return ModelPurpose.General;
    }
    
    /// <summary>
    /// Calculate priority based on model characteristics
    /// Lower = higher priority (will be tried first)
    /// </summary>
    private static int CalculatePriority(string modelName, double sizeGb)
    {
        var lower = modelName.ToLowerInvariant();
        var basePriority = 50;
        
        // Prefer larger models (usually better quality)
        if (sizeGb > 15) basePriority -= 20;
        else if (sizeGb > 8) basePriority -= 10;
        else if (sizeGb > 4) basePriority -= 5;
        
        // Prefer known high-quality models
        if (lower.Contains("deepseek")) basePriority -= 15;
        if (lower.Contains("qwen2.5")) basePriority -= 10;
        if (lower.Contains("codellama")) basePriority -= 5;
        if (lower.Contains("phi4")) basePriority -= 8;
        
        // Prefer instruct/chat variants
        if (lower.Contains("instruct") || lower.Contains("chat")) basePriority -= 3;
        
        // Penalize old/uncensored models
        if (lower.Contains("uncensored")) basePriority += 20;
        if (lower.Contains("llama2")) basePriority += 10;
        
        return Math.Max(1, basePriority);
    }
}

#region Ollama API Response Types (for ModelOrchestrator)

internal class OllamaModelsResponse
{
    public List<OllamaModelEntry>? Models { get; set; }
}

internal class OllamaModelEntry
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
}

/// <summary>
/// Response from Ollama /api/ps (running models)
/// </summary>
internal class OllamaPsResponse
{
    public List<OllamaRunningModel>? Models { get; set; }
}

internal class OllamaRunningModel
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public long Size_vram { get; set; }
}

#endregion

public partial class ModelOrchestrator
{
    /// <summary>
    /// Ensure models are discovered before use
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get the always-loaded primary model (instant response, no load time)
    /// </summary>
    public (string Model, int Port) GetPrimaryModel()
    {
        return (Config.PrimaryModel, Config.PinnedPort);
    }

    /// <summary>
    /// Select the best available model for a purpose, respecting constraints
    /// </summary>
    public async Task<(string Model, int Port)?> SelectModelAsync(
        ModelPurpose purpose, 
        HashSet<string> excludeModels,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        var candidates = GetModelsForPurpose(purpose)
            .Where(m => !excludeModels.Contains(m.Name))
            .OrderBy(m => m.Priority)
            .ToList();

        _logger.LogDebug("Found {Count} candidate models for {Purpose} (excluding {Excluded})",
            candidates.Count, purpose, excludeModels.Count);

        if (!candidates.Any())
        {
            _logger.LogWarning("No available models for {Purpose} after excluding {Excluded}",
                purpose, string.Join(", ", excludeModels));
            return null;
        }

        // If we have dual GPU, prefer swap GPU for second opinions (big models)
        if (Config.DualGpu)
        {
            // Try to find a big model that fits on swap GPU (3090)
            var bigModel = candidates.FirstOrDefault(m => 
                m.SizeGb <= Config.SwapGpuVram - 2 && // 2GB safety buffer
                m.SizeGb > 10); // Only "big" models worth swapping
            
            if (bigModel != null)
            {
                _logger.LogInformation("Selected {Model} ({Size:F1}GB) for {Purpose} on SWAP GPU (port {Port})",
                    bigModel.Name, bigModel.SizeGb, purpose, Config.SwapPort);
                return (bigModel.Name, Config.SwapPort);
            }
        }

        // Single GPU or no big model available - use pinned GPU with small model
        var availableVram = Config.PinnedGpuVram - Config.PinnedModelsVram - 1; // 1GB safety
        
        var smallModel = candidates.FirstOrDefault(m => m.SizeGb <= availableVram);
        
        if (smallModel != null)
        {
            _logger.LogInformation("Selected {Model} ({Size:F1}GB) for {Purpose} on PINNED GPU (port {Port})",
                smallModel.Name, smallModel.SizeGb, purpose, Config.PinnedPort);
            return (smallModel.Name, Config.PinnedPort);
        }

        _logger.LogWarning("No model fits in available VRAM ({Available}GB) for {Purpose}. " +
            "Candidates were: {Candidates}",
            availableVram, purpose, 
            string.Join(", ", candidates.Select(c => $"{c.Name}({c.SizeGb:F1}GB)")));
        return null;
    }

    /// <summary>
    /// Check if a model can be loaded without evicting pinned models
    /// </summary>
    public bool CanLoadModel(string model, int port)
    {
        if (!_modelRegistry.TryGetValue(model, out var info))
        {
            _logger.LogWarning("Unknown model {Model}, assuming 5GB", model);
            info = new ModelInfo { Name = model, SizeGb = 5.0, Purpose = ModelPurpose.General };
        }

        if (port == Config.SwapPort && Config.DualGpu)
        {
            // Swap GPU has full VRAM available (models auto-unload)
            return info.SizeGb <= Config.SwapGpuVram - 2;
        }
        
        if (port == Config.PinnedPort)
        {
            // Pinned GPU has limited space (pinned models take most of it)
            var available = Config.PinnedGpuVram - Config.PinnedModelsVram - 1;
            var fits = info.SizeGb <= available;
            
            if (!fits)
            {
                _logger.LogWarning(
                    "Model {Model} ({Size:F1}GB) would NOT fit on pinned GPU (only {Available}GB available). " +
                    "Would require evicting pinned models!",
                    model, info.SizeGb, available);
            }
            
            return fits;
        }

        return false;
    }

    /// <summary>
    /// Get all available models for a purpose, sorted by priority
    /// </summary>
    public List<ModelInfo> GetModelsForPurpose(ModelPurpose purpose)
    {
        return _modelRegistry.Values
            .Where(m => m.Purpose == purpose || m.Purpose == ModelPurpose.General)
            .OrderBy(m => m.Priority)
            .ToList();
    }

    /// <summary>
    /// Refresh model registry from Ollama
    /// </summary>
    public async Task RefreshModelsAsync(CancellationToken cancellationToken = default)
    {
        _isInitialized = false;
        await InitializeAsync(cancellationToken);
    }

    /// <summary>
    /// Record model performance for future optimization - NOW STORES IN MEMORYAGENT!
    /// </summary>
    public async Task RecordModelPerformanceAsync(
        string model, 
        string taskType, 
        bool succeeded, 
        double score,
        string? language = null,
        string? complexity = null,
        int iterations = 1,
        long durationMs = 0,
        string? errorType = null,
        List<string>? taskKeywords = null,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "📊 Recording model performance: {Model} on {TaskType}/{Language} = {Success}, Score: {Score:F1}",
            model, taskType, language ?? "unknown", succeeded ? "SUCCESS" : "FAILED", score);
        
        // Store in MemoryAgent for learning
        if (_memoryAgent != null)
        {
            try
            {
                var record = new ModelPerformanceRecord
                {
                    Model = model,
                    TaskType = taskType,
                    Language = language ?? "unknown",
                    Complexity = complexity ?? "unknown",
                    Outcome = succeeded ? "success" : (score > 0 ? "partial" : "failure"),
                    Score = (int)score,
                    DurationMs = durationMs,
                    Iterations = iterations,
                    ErrorType = errorType,
                    TaskKeywords = taskKeywords ?? new List<string>(),
                    Context = context ?? "default"
                };
                
                await _memoryAgent.RecordModelPerformanceAsync(record, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store model performance in MemoryAgent (non-critical)");
            }
        }
    }
    
    /// <summary>
    /// 🧠 SMART MODEL SELECTION: 
    /// 1. Query historical stats from MemoryAgent
    /// 2. Use LLM to confirm/adjust selection based on task + historical rates
    /// 3. Falls back to priority-based selection if no data/LLM unavailable
    /// </summary>
    public async Task<(string Model, int Port)?> SelectBestModelAsync(
        ModelPurpose purpose,
        string? language,
        string? complexity,
        HashSet<string> excludeModels,
        List<string>? taskKeywords = null,
        string? context = null,
        string? taskDescription = null,  // Task description for LLM analysis
        object? llmSelector = null,  // ILlmModelSelector - use object to match interface
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        var taskType = purpose == ModelPurpose.CodeGeneration ? "code_generation" : 
                       purpose == ModelPurpose.Validation ? "validation" : "general";
        
        // Get available models (excluding already-tried ones)
        var availableModels = _modelRegistry.Keys
            .Where(m => !excludeModels.Contains(m))
            .ToList();
        
        // 🔧 FIX: If all models are excluded, fall back to primary model
        // This prevents the "no available models" error when retrying with the only model
        if (!availableModels.Any() && excludeModels.Any())
        {
            var (primaryModel, primaryPort) = GetPrimaryModel();
            _logger.LogWarning(
                "All models excluded ({Excluded}), falling back to primary model {Primary}",
                string.Join(", ", excludeModels), primaryModel);
            return (primaryModel, primaryPort);
        }
        
        // STEP 1: Get historical stats from MemoryAgent
        List<ModelStats> historicalStats = new();
        if (_memoryAgent != null)
        {
            try
            {
                historicalStats = await _memoryAgent.GetModelStatsAsync(language, taskType, cancellationToken);
                _logger.LogDebug("Got {Count} historical stats for {TaskType}/{Language}", 
                    historicalStats.Count, taskType, language);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get historical stats (continuing without)");
            }
        }
        
        // STEP 2: Use LLM to analyze task and confirm/adjust model selection
        // Skip if UseSmartModelSelection is disabled (saves model swap on single GPU)
        var typedLlmSelector = llmSelector as ILlmModelSelector;
        if (Config.UseSmartModelSelection && typedLlmSelector != null && !string.IsNullOrEmpty(taskDescription))
        {
            try
            {
                var llmRecommendation = await typedLlmSelector.SelectModelAsync(
                    taskDescription,
                    taskType,
                    language,
                    historicalStats,
                    availableModels,
                    cancellationToken);
                
                if (!string.IsNullOrEmpty(llmRecommendation.RecommendedModel) && 
                    _modelRegistry.TryGetValue(llmRecommendation.RecommendedModel, out var modelInfo))
                {
                    var port = DeterminePortForModel(modelInfo);
                    if (CanLoadModel(llmRecommendation.RecommendedModel, port))
                    {
                        _logger.LogInformation(
                            "🧠 LLM-confirmed model selection: {Model} (complexity={Complexity}, confidence={Confidence:P0})\n   Reasoning: {Reasoning}",
                            llmRecommendation.RecommendedModel,
                            llmRecommendation.TaskComplexity,
                            llmRecommendation.Confidence,
                            llmRecommendation.Reasoning);
                        return (llmRecommendation.RecommendedModel, port);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM model selection failed (falling back to historical/priority)");
            }
        }
        
        // STEP 3: Fall back to historical best performer
        if (historicalStats.Any())
        {
            var bestHistorical = historicalStats
                .Where(s => availableModels.Contains(s.Model))
                .OrderByDescending(s => s.SuccessRate)
                .ThenByDescending(s => s.AverageScore)
                .FirstOrDefault();
            
            if (bestHistorical != null && _modelRegistry.TryGetValue(bestHistorical.Model, out var modelInfo))
            {
                var port = DeterminePortForModel(modelInfo);
                if (CanLoadModel(bestHistorical.Model, port))
                {
                    _logger.LogInformation(
                        "🧠 Using historical best: {Model} ({SuccessRate:F0}% success, {Score:F1} avg score)",
                        bestHistorical.Model, bestHistorical.SuccessRate, bestHistorical.AverageScore);
                    return (bestHistorical.Model, port);
                }
            }
        }
        
        // STEP 4: Fall back to priority-based selection
        _logger.LogDebug("No historical data or LLM recommendation, using priority-based selection");
        return await SelectModelAsync(purpose, excludeModels, cancellationToken);
    }
    
    /// <summary>
    /// Determine which GPU port to use for a model based on size
    /// </summary>
    private int DeterminePortForModel(ModelInfo modelInfo)
    {
        if (!Config.DualGpu)
            return Config.PinnedPort;
        
        // Big models go to swap GPU
        if (modelInfo.SizeGb > 10)
            return Config.SwapPort;
        
        // Small models go to pinned GPU if space available
        var availableVram = Config.PinnedGpuVram - Config.PinnedModelsVram - 1;
        if (modelInfo.SizeGb <= availableVram)
            return Config.PinnedPort;
        
        // Otherwise swap GPU
        return Config.SwapPort;
    }
    
    /// <summary>
    /// Get summary of available models for logging/debugging
    /// </summary>
    public async Task<string> GetModelSummaryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        
        var codeModels = _modelRegistry.Values
            .Where(m => m.Purpose == ModelPurpose.CodeGeneration)
            .OrderBy(m => m.Priority)
            .Select(m => $"{m.Name} ({m.SizeGb:F1}GB)")
            .ToList();
            
        var validationModels = _modelRegistry.Values
            .Where(m => m.Purpose == ModelPurpose.Validation)
            .OrderBy(m => m.Priority)
            .Select(m => $"{m.Name} ({m.SizeGb:F1}GB)")
            .ToList();
            
        return $"Code Gen: [{string.Join(", ", codeModels)}] | Validation: [{string.Join(", ", validationModels)}]";
    }
    
    /// <summary>
    /// Get currently loaded models from Ollama /api/ps
    /// </summary>
    public async Task<List<LoadedModelInfo>> GetLoadedModelsAsync(
        int port,
        CancellationToken cancellationToken = default)
    {
        var result = new List<LoadedModelInfo>();
        
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_baseHost}:{port}/api/ps", 
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Could not query loaded models on port {Port}: {Status}", 
                    port, response.StatusCode);
                return result;
            }
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var psResponse = JsonSerializer.Deserialize<OllamaPsResponse>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (psResponse?.Models == null) return result;
            
            foreach (var model in psResponse.Models)
            {
                result.Add(new LoadedModelInfo
                {
                    Name = model.Name,
                    SizeGb = model.Size / 1_000_000_000.0,
                    VramGb = model.Size_vram / 1_000_000_000.0
                });
            }
            
            _logger.LogDebug("Port {Port} has {Count} models loaded: {Models}",
                port, result.Count, string.Join(", ", result.Select(m => m.Name)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying loaded models on port {Port}", port);
        }
        
        return result;
    }
    
    /// <summary>
    /// Get available VRAM on a port (accounting for loaded models)
    /// </summary>
    public async Task<double> GetAvailableVramAsync(int port, CancellationToken cancellationToken = default)
    {
        var totalVram = port == Config.SwapPort && Config.DualGpu 
            ? Config.SwapGpuVram 
            : Config.PinnedGpuVram;
            
        var loaded = await GetLoadedModelsAsync(port, cancellationToken);
        var usedVram = loaded.Sum(m => m.VramGb);
        
        var available = totalVram - usedVram - 1; // 1GB safety buffer
        
        _logger.LogDebug("Port {Port}: {Total}GB total, {Used:F1}GB used, {Available:F1}GB available",
            port, totalVram, usedVram, available);
            
        return Math.Max(0, available);
    }
    
    /// <summary>
    /// Check if a model is already loaded (instant inference)
    /// </summary>
    public async Task<bool> IsModelLoadedAsync(string model, int port, CancellationToken cancellationToken = default)
    {
        var loaded = await GetLoadedModelsAsync(port, cancellationToken);
        return loaded.Any(m => m.Name.Equals(model, StringComparison.OrdinalIgnoreCase));
    }
}

