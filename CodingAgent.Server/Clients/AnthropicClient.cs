using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;
using CodingAgent.Server.Services;

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
    private readonly IClaudeRateLimitTracker? _rateLimitTracker;
    private const string BaseUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    
    // 🔒 Global semaphore: ONE Claude API call at a time across ALL jobs/requests
    // Prevents token racing when multiple jobs run simultaneously
    private static readonly SemaphoreSlim _claudeSemaphore = new(1, 1);
    
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

    public AnthropicClient(HttpClient httpClient, ILogger<AnthropicClient> logger, IConfiguration config, IClaudeRateLimitTracker? rateLimitTracker = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _rateLimitTracker = rateLimitTracker;
        
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
            _logger.LogInformation("[CLAUDE] Configured: {Model} (rate limit tracking: {Enabled})", 
                _modelId, rateLimitTracker != null ? "enabled" : "disabled");
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

        // Haiku has 4096 token limit, Sonnet/Opus have 8192
        var maxTokens = modelToUse.Contains("haiku", StringComparison.OrdinalIgnoreCase) ? 4096 : 8192;
        
        // 📊 Estimate tokens (rough: 4 chars per token)
        var estimatedInputTokens = (systemPrompt.Length + userPrompt.Length) / 4;
        
        // 🚦 Check rate limits BEFORE making request
        if (_rateLimitTracker != null)
        {
            var (shouldWait, waitMs, reason) = _rateLimitTracker.ShouldThrottle(estimatedInputTokens + maxTokens);
            if (shouldWait)
            {
                _logger.LogWarning("🚦 Rate limit throttle: {Reason}. Waiting {WaitMs}ms...", reason, waitMs);
                await Task.Delay(waitMs, ct);
                _logger.LogInformation("✅ Rate limit wait complete, proceeding with request");
            }
        }
        
        var requestBody = new AnthropicRequest
        {
            Model = modelToUse,
            MaxTokens = maxTokens,
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

        // 🔒 WAIT FOR GLOBAL SEMAPHORE: Only ONE Claude call at a time
        // This prevents multiple jobs from racing for tokens
        _logger.LogInformation("[CLAUDE] Waiting for Claude semaphore...");
        await _claudeSemaphore.WaitAsync(ct);
        
        try
        {
            _logger.LogInformation("[CLAUDE] 🚀 Acquired semaphore! Calling {Model} ({PromptLen} chars prompt, ~{EstTokens} tokens){Premium}", 
                modelToUse, userPrompt.Length, estimatedInputTokens, usePremium ? " [PREMIUM]" : "");
            var startTime = DateTime.UtcNow;

            // 🔄 Polly retry policy for rate limits - will wait and retry up to 4 times (30s, 60s, 90s, 120s)
            var retryPolicy = Policy
                .HandleResult<(HttpResponseMessage Response, string Body)>(result => 
                    result.Response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .Or<HttpRequestException>(ex => 
                    ex.Message.Contains("TooManyRequests") || 
                    ex.Message.Contains("rate_limit_error"))
                .WaitAndRetryAsync(
                    retryCount: 4, // 4 retries: 30s, 60s, 90s, 120s (ensures we get past full rate limit window)
                    sleepDurationProvider: (retryAttempt, result, context) =>
                    {
                        // Try to get reset time from response
                        DateTime? resetAt = null;
                        var response = result?.Result.Response;
                        
                        if (response?.Headers.TryGetValues("retry-after", out var retryAfterValues) == true)
                        {
                            if (int.TryParse(retryAfterValues.FirstOrDefault(), out var retrySeconds))
                            {
                                resetAt = DateTime.UtcNow.AddSeconds(retrySeconds);
                            }
                        }
                        
                        // Record the rate limit hit
                        if (_rateLimitTracker != null)
                        {
                            _rateLimitTracker.RecordRateLimitHit(resetAt);
                        }
                        
                        // Wait: either until reset or exponential backoff (30s, 60s, 90s, 120s)
                        var waitTime = resetAt.HasValue 
                            ? resetAt.Value - DateTime.UtcNow 
                            : TimeSpan.FromSeconds(30 * retryAttempt);
                        
                        var cappedWait = TimeSpan.FromMilliseconds(Math.Max(1000, Math.Min(waitTime.TotalMilliseconds, 150000))); // Cap at 150s
                        
                        _logger.LogWarning("🚦 Rate limit hit (attempt {Attempt}/4), waiting {Wait}s before retry...", 
                            retryAttempt, (int)cappedWait.TotalSeconds);
                        
                        return cappedWait;
                    },
                    onRetryAsync: async (result, timespan, retryAttempt, context) =>
                    {
                        _logger.LogInformation("🔄 Retry {Attempt}: Waited {Wait}s, calling Claude again...", 
                            retryAttempt, (int)timespan.TotalSeconds);
                        await Task.CompletedTask;
                    });

            // Execute with retry policy
            var (response, responseBody) = await retryPolicy.ExecuteAsync(async () =>
            {
                var resp = await _httpClient.SendAsync(request, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                
                if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    // Return result for Polly to evaluate (will trigger retry)
                    return (resp, body);
                }
                
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Anthropic API error: {Status} - {Body}", resp.StatusCode, body);
                    throw new HttpRequestException($"Anthropic API error: {resp.StatusCode} - {body}");
                }
                
                return (resp, body);
            });
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic API error after retries: {Status} - {Body}", response.StatusCode, responseBody);
                throw new HttpRequestException($"Anthropic API error: {response.StatusCode} - {responseBody}");
            }

            var anthropicResult = JsonSerializer.Deserialize<AnthropicApiResponse>(responseBody, JsonOptions);
            if (anthropicResult == null)
            {
                throw new InvalidOperationException("Failed to parse Anthropic response");
            }

            var duration = DateTime.UtcNow - startTime;
            
            // Extract rate limit info from headers
            int? tokensRemaining = null;
            int? requestsRemaining = null;
            DateTime? resetAt = null;
            
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
            if (response.Headers.TryGetValues("anthropic-ratelimit-reset", out var resetValues))
            {
                if (DateTime.TryParse(resetValues.FirstOrDefault(), out var reset))
                    resetAt = reset;
            }

            // Calculate cost
            var inputTokens = anthropicResult.Usage?.InputTokens ?? 0;
            var outputTokens = anthropicResult.Usage?.OutputTokens ?? 0;
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
            
            // 📊 Record usage in rate limit tracker
            if (_rateLimitTracker != null)
            {
                _rateLimitTracker.RecordUsage(inputTokens, outputTokens, tokensRemaining, resetAt);
            }

            var responseText = anthropicResult.Content?.FirstOrDefault()?.Text ?? "";
            
            _logger.LogInformation(
                "☁️ Claude response: {OutputLen} chars, {InputTokens}+{OutputTokens} tokens, ${Cost:F4}, {Duration}ms",
                responseText.Length, inputTokens, outputTokens, cost, duration.TotalMilliseconds);

            return new AnthropicResponse
            {
                Content = responseText,
                Model = anthropicResult.Model ?? _modelId!,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Cost = cost,
                DurationMs = (int)duration.TotalMilliseconds,
                TokensRemaining = tokensRemaining,
                RequestsRemaining = requestsRemaining,
                StopReason = anthropicResult.StopReason
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Anthropic API call failed");
            throw;
        }
        finally
        {
            // 🔓 RELEASE SEMAPHORE: Let next Claude call proceed
            _claudeSemaphore.Release();
            _logger.LogInformation("[CLAUDE] ✅ Released semaphore");
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

