using Polly;
using Polly.Retry;
using System.Net;

namespace CodingAgent.Server.Services;

/// <summary>
/// Polly retry policy specifically for Claude rate limits
/// Implements intelligent wait-and-retry based on actual rate limit headers
/// </summary>
public static class ClaudeRateLimitPolicy
{
    /// <summary>
    /// Creates a Polly retry policy that waits for rate limits to reset
    /// Uses exponential backoff: 30s, 60s, 90s (max 3 retries)
    /// </summary>
    public static AsyncRetryPolicy<HttpResponseMessage> CreatePolicy(
        IClaudeRateLimitTracker rateLimitTracker,
        ILogger logger)
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>(ex => 
                ex.Message.Contains("TooManyRequests") || 
                ex.Message.Contains("rate_limit_error") ||
                ex.Message.Contains("429"))
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (retryAttempt, result, context) =>
                {
                    // Try to parse rate limit reset from headers or response
                    DateTime? resetAt = null;
                    
                    if (result?.Result?.Headers != null)
                    {
                        // Check for retry-after header (seconds)
                        if (result.Result.Headers.TryGetValues("retry-after", out var retryAfterValues))
                        {
                            if (int.TryParse(retryAfterValues.FirstOrDefault(), out var retryAfterSeconds))
                            {
                                resetAt = DateTime.UtcNow.AddSeconds(retryAfterSeconds);
                            }
                        }
                        
                        // Check for anthropic-ratelimit-reset header (ISO timestamp)
                        if (result.Result.Headers.TryGetValues("anthropic-ratelimit-reset", out var resetValues))
                        {
                            if (DateTime.TryParse(resetValues.FirstOrDefault(), out var reset))
                            {
                                resetAt = reset;
                            }
                        }
                    }
                    
                    // Record the rate limit hit
                    rateLimitTracker.RecordRateLimitHit(resetAt);
                    
                    // Calculate wait time
                    TimeSpan waitTime;
                    if (resetAt.HasValue)
                    {
                        // Wait until reset + small buffer
                        waitTime = (resetAt.Value - DateTime.UtcNow) + TimeSpan.FromSeconds(2);
                        logger.LogWarning("⏳ Rate limit hit, waiting until {ResetAt:HH:mm:ss} ({WaitSec}s)", 
                            resetAt.Value, (int)waitTime.TotalSeconds);
                    }
                    else
                    {
                        // Exponential backoff: 30s, 60s, 90s
                        waitTime = TimeSpan.FromSeconds(30 * retryAttempt);
                        logger.LogWarning("⏳ Rate limit hit (retry {Attempt}/3), waiting {WaitSec}s", 
                            retryAttempt, (int)waitTime.TotalSeconds);
                    }
                    
                    // Cap at 2 minutes max
                    return TimeSpan.FromMilliseconds(Math.Min(waitTime.TotalMilliseconds, 120000));
                },
                onRetryAsync: async (result, timespan, retryAttempt, context) =>
                {
                    logger.LogInformation("🔄 Retry {Attempt}: Waited {Wait}s, trying Claude again...", 
                        retryAttempt, (int)timespan.TotalSeconds);
                    await Task.CompletedTask;
                });
    }
}
