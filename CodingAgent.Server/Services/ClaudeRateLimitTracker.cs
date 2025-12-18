using System.Collections.Concurrent;

namespace CodingAgent.Server.Services;

/// <summary>
/// Tracks Claude API rate limits and provides intelligent throttling
/// Monitors: anthropic-ratelimit-* headers to stay within 450k tokens/min
/// </summary>
public interface IClaudeRateLimitTracker
{
    /// <summary>
    /// Record tokens used in a request
    /// </summary>
    void RecordUsage(int inputTokens, int outputTokens, int? tokensRemaining, DateTime? resetAt);
    
    /// <summary>
    /// Check if we should wait before making next request
    /// Returns: (shouldWait, waitTimeMs, reason)
    /// </summary>
    (bool ShouldWait, int WaitTimeMs, string Reason) ShouldThrottle(int estimatedTokens);
    
    /// <summary>
    /// Get current rate limit status
    /// </summary>
    RateLimitStatus GetStatus();
    
    /// <summary>
    /// Mark that a rate limit error occurred
    /// </summary>
    void RecordRateLimitHit(DateTime? resetAt = null);
}

public class ClaudeRateLimitTracker : IClaudeRateLimitTracker
{
    private readonly ILogger<ClaudeRateLimitTracker> _logger;
    
    // Track usage in current minute window
    private readonly ConcurrentQueue<TokenUsage> _usageHistory = new();
    private int? _lastKnownTokensRemaining;
    private DateTime? _rateLimitResetAt;
    private readonly object _lock = new();
    
    // Rate limit: 450,000 tokens per minute (Anthropic)
    private const int MaxTokensPerMinute = 450_000;
    private const int SafetyBuffer = 50_000; // Keep 50k buffer
    private const int MinTokensBeforeThrottle = 100_000; // Start throttling at 100k remaining
    
    public ClaudeRateLimitTracker(ILogger<ClaudeRateLimitTracker> logger)
    {
        _logger = logger;
    }
    
    public void RecordUsage(int inputTokens, int outputTokens, int? tokensRemaining, DateTime? resetAt)
    {
        lock (_lock)
        {
            var totalTokens = inputTokens + outputTokens;
            _usageHistory.Enqueue(new TokenUsage
            {
                Tokens = totalTokens,
                Timestamp = DateTime.UtcNow
            });
            
            _lastKnownTokensRemaining = tokensRemaining;
            if (resetAt.HasValue)
            {
                _rateLimitResetAt = resetAt;
            }
            
            // Clean old entries (older than 1 minute)
            CleanOldUsage();
            
            var usedInWindow = GetTokensUsedInCurrentWindow();
            _logger.LogInformation("📊 Rate Limit: Used {Used}/{Max} tokens in window, {Remaining} remaining", 
                usedInWindow, MaxTokensPerMinute, tokensRemaining?.ToString() ?? "unknown");
        }
    }
    
    public (bool ShouldWait, int WaitTimeMs, string Reason) ShouldThrottle(int estimatedTokens)
    {
        lock (_lock)
        {
            CleanOldUsage();
            
            // Check if we're in a rate limit cooldown
            if (_rateLimitResetAt.HasValue && DateTime.UtcNow < _rateLimitResetAt.Value)
            {
                var waitMs = (int)(_rateLimitResetAt.Value - DateTime.UtcNow).TotalMilliseconds;
                if (waitMs > 0)
                {
                    return (true, waitMs, $"Rate limit cooldown until {_rateLimitResetAt:HH:mm:ss}");
                }
            }
            
            var usedInWindow = GetTokensUsedInCurrentWindow();
            var availableInWindow = MaxTokensPerMinute - usedInWindow - SafetyBuffer;
            
            // Check if we have enough tokens in current window
            if (estimatedTokens > availableInWindow)
            {
                // Calculate wait time: when will oldest usage expire?
                var oldestUsage = _usageHistory.TryPeek(out var oldest) ? oldest : null;
                if (oldestUsage != null)
                {
                    var ageMs = (int)(DateTime.UtcNow - oldestUsage.Timestamp).TotalMilliseconds;
                    var waitMs = Math.Max(0, 60000 - ageMs); // Wait for oldest to age out
                    
                    if (waitMs > 0)
                    {
                        return (true, waitMs, 
                            $"Window full: {usedInWindow}/{MaxTokensPerMinute} used, need {estimatedTokens} tokens");
                    }
                }
            }
            
            // Check last known remaining from API
            if (_lastKnownTokensRemaining.HasValue && _lastKnownTokensRemaining < MinTokensBeforeThrottle)
            {
                // Throttle aggressively when low
                var waitMs = Math.Max(5000, (MinTokensBeforeThrottle - _lastKnownTokensRemaining.Value) * 10);
                return (true, Math.Min(waitMs, 30000), 
                    $"Low token reserve: {_lastKnownTokensRemaining} remaining");
            }
            
            return (false, 0, "");
        }
    }
    
    public RateLimitStatus GetStatus()
    {
        lock (_lock)
        {
            CleanOldUsage();
            return new RateLimitStatus
            {
                TokensUsedInWindow = GetTokensUsedInCurrentWindow(),
                TokensRemainingEstimate = _lastKnownTokensRemaining,
                WindowResetAt = _rateLimitResetAt,
                RequestsInWindow = _usageHistory.Count
            };
        }
    }
    
    public void RecordRateLimitHit(DateTime? resetAt = null)
    {
        lock (_lock)
        {
            _rateLimitResetAt = resetAt ?? DateTime.UtcNow.AddSeconds(60);
            _logger.LogWarning("🚨 Rate limit hit! Cooldown until {ResetAt:HH:mm:ss}", _rateLimitResetAt);
        }
    }
    
    private int GetTokensUsedInCurrentWindow()
    {
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        return _usageHistory
            .Where(u => u.Timestamp > oneMinuteAgo)
            .Sum(u => u.Tokens);
    }
    
    private void CleanOldUsage()
    {
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        
        while (_usageHistory.TryPeek(out var oldest) && oldest.Timestamp < oneMinuteAgo)
        {
            _usageHistory.TryDequeue(out _);
        }
    }
    
    private class TokenUsage
    {
        public int Tokens { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

public class RateLimitStatus
{
    public int TokensUsedInWindow { get; set; }
    public int? TokensRemainingEstimate { get; set; }
    public DateTime? WindowResetAt { get; set; }
    public int RequestsInWindow { get; set; }
}
