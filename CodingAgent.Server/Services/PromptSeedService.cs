using System.Text.Json;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Manages prompts loaded from promptseed.json (in-memory)
/// Sends feedback to MemoryAgent when prompts are used
/// </summary>
public interface IPromptSeedService
{
    Task SeedPromptsAsync(CancellationToken cancellationToken = default);
    Task<PromptMetadata?> GetBestPromptAsync(string id, CancellationToken cancellationToken = default);
    Task RecordPromptUsageAsync(string promptId, PromptUsageResult result, CancellationToken cancellationToken = default);
}

public class PromptSeedService : IPromptSeedService
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<PromptSeedService> _logger;
    private readonly string _seedFilePath;
    private PromptSeedData? _seedData;
    
    // In-memory cache of all prompts
    private readonly Dictionary<string, PromptMetadata> _promptCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    public PromptSeedService(
        IMemoryAgentClient memoryAgent,
        IConfiguration configuration,
        ILogger<PromptSeedService> logger)
    {
        _memoryAgent = memoryAgent;
        _logger = logger;
        _seedFilePath = Path.Combine(
            configuration["PromptSeed:Path"] ?? "/app",
            "promptseed.json"
        );
    }
    
    public async Task SeedPromptsAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("🌱 Loading prompts from seed file: {Path}", _seedFilePath);
            
            // Load seed data
            if (!File.Exists(_seedFilePath))
            {
                _logger.LogWarning("⚠️ Prompt seed file not found: {Path}", _seedFilePath);
                return;
            }
            
            var json = await File.ReadAllTextAsync(_seedFilePath, cancellationToken);
            _seedData = JsonSerializer.Deserialize<PromptSeedData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (_seedData?.Prompts == null || !_seedData.Prompts.Any())
            {
                _logger.LogWarning("⚠️ No prompts found in seed file");
                return;
            }
            
            _logger.LogInformation("📦 Found {Count} prompts in seed file (version: {Version})",
                _seedData.Prompts.Count, _seedData.Version);
            
            // Load all prompts into memory cache
            _promptCache.Clear();
            foreach (var prompt in _seedData.Prompts)
            {
                _promptCache[prompt.Id] = prompt;
                _logger.LogDebug("✅ Loaded prompt: {Id} - {Name}", prompt.Id, prompt.Name);
            }
            
            _logger.LogInformation("🎉 Prompt loading complete: {Total} prompts cached in memory",
                _promptCache.Count);
            _logger.LogInformation("✅ Prompt seeding completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Prompt loading failed");
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    
    public async Task<PromptMetadata?> GetBestPromptAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cacheLock.WaitAsync(cancellationToken);
            try
            {
                // Get from in-memory cache
                if (_promptCache.TryGetValue(id, out var prompt))
                {
                    _logger.LogDebug("📋 Retrieved prompt from cache: {Id}", id);
                    return prompt;
                }
                
                _logger.LogWarning("⚠️ Prompt not found in cache: {Id}", id);
                return null;
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to get prompt: {Id}", id);
            return null;
        }
    }
    
    public async Task RecordPromptUsageAsync(string promptId, PromptUsageResult result, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("📊 Recording prompt usage: {Id}, Score: {Score}, Iterations: {Iterations}",
                promptId, result.Score, result.Iterations);
            
            // Update in-memory cache
            if (_promptCache.TryGetValue(promptId, out var prompt))
            {
                // Update statistics
                prompt.UsageCount++;
                var totalScore = prompt.AvgScore * (prompt.UsageCount - 1) + result.Score;
                prompt.AvgScore = totalScore / prompt.UsageCount;
                
                var totalIterations = prompt.AvgIterations * (prompt.UsageCount - 1) + result.Iterations;
                prompt.AvgIterations = totalIterations / prompt.UsageCount;
                
                prompt.SuccessRate = result.Success 
                    ? (prompt.SuccessRate * (prompt.UsageCount - 1) + 1.0) / prompt.UsageCount
                    : (prompt.SuccessRate * (prompt.UsageCount - 1)) / prompt.UsageCount;
                
                _logger.LogDebug("✅ Updated prompt stats in cache: {Id}, Usage: {Usage}, SuccessRate: {Rate:P0}",
                    promptId, prompt.UsageCount, prompt.SuccessRate);
            }
            
            // Also send feedback to MemoryAgent for Lightning learning (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _memoryAgent.RecordPromptFeedbackAsync(promptId, result, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to send prompt feedback to MemoryAgent (non-critical): {Id}", promptId);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to record prompt usage: {Id}", promptId);
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}

// Data models

public class PromptSeedData
{
    public string Version { get; set; } = "1.0.0";
    public string LastUpdated { get; set; } = "";
    public List<PromptMetadata> Prompts { get; set; } = new();
    public PromptSeedMetadata Metadata { get; set; } = new();
}

public class PromptSeedMetadata
{
    public string Description { get; set; } = "";
    public DateTime? SeededAt { get; set; }
    public int TotalPrompts { get; set; }
}

public class PromptMetadata
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Context { get; set; } = "";
    public int Version { get; set; }
    public string Content { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public double SuccessRate { get; set; }
    public int UsageCount { get; set; }
    public double AvgScore { get; set; }
    public double AvgIterations { get; set; }
}

public class PromptUsageResult
{
    public bool Success { get; set; }
    public int Score { get; set; }
    public int Iterations { get; set; }
    public List<string> Issues { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}


