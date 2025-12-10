using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodingAgent.Server.Clients;

/// <summary>
/// HTTP client for Anthropic Claude API
/// Used for high-quality code generation when ANTHROPIC_API_KEY is configured
/// Supports automatic escalation to premium model after failures
/// </summary>
public interface IAnthropicClient
{
    bool IsConfigured { get; }
    string? ModelId { get; }
    string? PremiumModelId { get; }
    bool HasPremiumModel { get; }
    Task<AnthropicResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    Task<AnthropicResponse> GenerateAsync(string systemPrompt, string userPrompt, bool usePremium, CancellationToken ct = default);
    AnthropicUsageStats GetUsageStats();
}

public class AnthropicClient : IAnthropicClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AnthropicClient> _logger;
    private readonly string? _apiKey;
    private readonly string? _modelId;
    private readonly string? _premiumModelId;
    private const string BaseUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    
    // Usage tracking
    private readonly AnthropicUsageStats _usageStats = new();
    private readonly object _statsLock = new();
    
    // Model pricing per million tokens
    private static readonly Dictionary<string, (decimal Input, decimal Output)> ModelPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-opus-4-5-20251101"] = (5.00m, 25.00m),
        ["claude-sonnet-4-20250514"] = (3.00m, 15.00m),
        ["claude-sonnet-4-5-20241022"] = (3.00m, 15.00m),
        ["claude-3-5-sonnet-20241022"] = (3.00m, 15.00m),
        ["claude-3-5-haiku-20241022"] = (0.25m, 1.25m),
        ["claude-3-opus-20240229"] = (15.00m, 75.00m),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AnthropicClient(HttpClient httpClient, ILogger<AnthropicClient> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Read from environment variables (set in docker-compose or system)
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") 
                  ?? config.GetValue<string>("Anthropic:ApiKey");
        _modelId = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") 
                   ?? config.GetValue<string>("Anthropic:Model")
                   ?? "claude-sonnet-4-20250514";
        _premiumModelId = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL_PREMIUM") 
                          ?? config.GetValue<string>("Anthropic:PremiumModel");
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogInformation("[CLAUDE] Configured: {Model}", _modelId);
            if (!string.IsNullOrEmpty(_premiumModelId))
            {
                _logger.LogInformation("[CLAUDE] Premium fallback: {PremiumModel}", _premiumModelId);
            }
        }
        else
        {
            _logger.LogInformation("AnthropicClient not configured (no API key) - will use local models");
        }
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
    public string? ModelId => _modelId;
    public string? PremiumModelId => _premiumModelId;
    public bool HasPremiumModel => !string.IsNullOrEmpty(_premiumModelId);

    public Task<AnthropicResponse> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        return GenerateAsync(systemPrompt, userPrompt, usePremium: false, ct);
    }

    public async Task<AnthropicResponse> GenerateAsync(string systemPrompt, string userPrompt, bool usePremium, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Anthropic API key not configured");
        }

        // Use premium model if requested AND available, otherwise fall back to standard
        var modelToUse = usePremium && HasPremiumModel ? _premiumModelId! : _modelId!;
        
        if (usePremium && !HasPremiumModel)
        {
            _logger.LogWarning("[CLAUDE] Premium model requested but ANTHROPIC_MODEL_PREMIUM not configured, using {Model}", modelToUse);
        }
        else if (usePremium)
        {
            _logger.LogInformation("[CLAUDE-PREMIUM] Escalating to premium model: {Model}", modelToUse);
        }

        var requestBody = new AnthropicRequest
        {
            Model = modelToUse,
            MaxTokens = 8192,
            System = systemPrompt,
            Messages = new[]
            {
                new AnthropicMessage { Role = "user", Content = userPrompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
        request.Content = content;
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", ApiVersion);

        _logger.LogInformation("[CLAUDE] Calling {Model} ({PromptLen} chars prompt){Premium}", 
            modelToUse, userPrompt.Length, usePremium ? " [PREMIUM]" : "");
        var startTime = DateTime.UtcNow;

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API error: {Status} - {Body}", response.StatusCode, responseBody);
                throw new HttpRequestException($"Anthropic API error: {response.StatusCode} - {responseBody}");
            }

            var result = JsonSerializer.Deserialize<AnthropicApiResponse>(responseBody, JsonOptions);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to parse Anthropic response");
            }

            var duration = DateTime.UtcNow - startTime;
            
            // Extract rate limit info from headers
            int? tokensRemaining = null;
            int? requestsRemaining = null;
            
            if (response.Headers.TryGetValues("anthropic-ratelimit-tokens-remaining", out var tokenValues))
            {
                if (int.TryParse(tokenValues.FirstOrDefault(), out var tokens))
                    tokensRemaining = tokens;
            }
            if (response.Headers.TryGetValues("anthropic-ratelimit-requests-remaining", out var reqValues))
            {
                if (int.TryParse(reqValues.FirstOrDefault(), out var reqs))
                    requestsRemaining = reqs;
            }

            // Calculate cost
            var inputTokens = result.Usage?.InputTokens ?? 0;
            var outputTokens = result.Usage?.OutputTokens ?? 0;
            var cost = CalculateCost(_modelId!, inputTokens, outputTokens);

            // Update usage stats
            lock (_statsLock)
            {
                _usageStats.TotalRequests++;
                _usageStats.TotalInputTokens += inputTokens;
                _usageStats.TotalOutputTokens += outputTokens;
                _usageStats.TotalCost += cost;
                _usageStats.LastRequestAt = DateTime.UtcNow;
            }

            var responseText = result.Content?.FirstOrDefault()?.Text ?? "";
            
            _logger.LogInformation(
                "☁️ Claude response: {OutputLen} chars, {InputTokens}+{OutputTokens} tokens, ${Cost:F4}, {Duration}ms",
                responseText.Length, inputTokens, outputTokens, cost, duration.TotalMilliseconds);

            return new AnthropicResponse
            {
                Content = responseText,
                Model = result.Model ?? _modelId!,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Cost = cost,
                DurationMs = (int)duration.TotalMilliseconds,
                TokensRemaining = tokensRemaining,
                RequestsRemaining = requestsRemaining,
                StopReason = result.StopReason
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Anthropic API call failed");
            throw;
        }
    }

    public AnthropicUsageStats GetUsageStats()
    {
        lock (_statsLock)
        {
            return new AnthropicUsageStats
            {
                TotalRequests = _usageStats.TotalRequests,
                TotalInputTokens = _usageStats.TotalInputTokens,
                TotalOutputTokens = _usageStats.TotalOutputTokens,
                TotalCost = _usageStats.TotalCost,
                LastRequestAt = _usageStats.LastRequestAt
            };
        }
    }

    private static decimal CalculateCost(string model, int inputTokens, int outputTokens)
    {
        // Find matching pricing (handle model name variations)
        var pricing = ModelPricing
            .FirstOrDefault(p => model.Contains(p.Key, StringComparison.OrdinalIgnoreCase));
        
        if (pricing.Key == null)
        {
            // Default to Sonnet pricing if unknown
            pricing = new KeyValuePair<string, (decimal, decimal)>("default", (3.00m, 15.00m));
        }

        var (inputRate, outputRate) = pricing.Value;
        return (inputTokens * inputRate / 1_000_000m) + (outputTokens * outputRate / 1_000_000m);
    }
}

#region Request/Response Models

public class AnthropicRequest
{
    public string Model { get; set; } = "";
    public int MaxTokens { get; set; } = 4096;
    public string? System { get; set; }
    public AnthropicMessage[] Messages { get; set; } = Array.Empty<AnthropicMessage>();
}

public class AnthropicMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public class AnthropicApiResponse
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Role { get; set; }
    public string? Model { get; set; }
    public AnthropicContent[]? Content { get; set; }
    public string? StopReason { get; set; }
    public AnthropicUsage? Usage { get; set; }
}

public class AnthropicContent
{
    public string? Type { get; set; }
    public string? Text { get; set; }
}

public class AnthropicUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

public class AnthropicResponse
{
    public string Content { get; set; } = "";
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal Cost { get; set; }
    public int DurationMs { get; set; }
    public int? TokensRemaining { get; set; }
    public int? RequestsRemaining { get; set; }
    public string? StopReason { get; set; }
}

public class AnthropicUsageStats
{
    public int TotalRequests { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime? LastRequestAt { get; set; }
}

#endregion

