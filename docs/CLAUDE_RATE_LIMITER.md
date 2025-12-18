# Claude Smart Rate Limiter

## Overview

Intelligent rate limiting for Anthropic Claude API that **waits** (doesn't fallback) when hitting token limits.

## Components

### 1. `ClaudeRateLimitTracker`
- **Purpose:** Track token usage per minute window (450k limit)
- **Location:** `CodingAgent.Server/Services/ClaudeRateLimitTracker.cs`

**Features:**
- Monitors tokens used in rolling 60-second window
- Reads `anthropic-ratelimit-tokens-remaining` header
- Tracks `anthropic-ratelimit-reset` timestamp
- Provides `ShouldThrottle()` check before API calls

**Thresholds:**
- Max tokens/min: `450,000`
- Safety buffer: `50,000` (throttle at 400k)
- Min tokens before throttle: `100,000` remaining

### 2. Polly Retry Policy
- **Purpose:** Handle 429 errors with exponential backoff
- **Location:** `AnthropicClient.cs` (inline policy)

**Retry Strategy:**
- Attempt 1: Wait 30 seconds
- Attempt 2: Wait 60 seconds
- Attempt 3: Wait 90 seconds
- Uses `retry-after` header when available

### 3. Pre-Request Throttling
- **Location:** `AnthropicClient.GenerateAsync()` line ~120

**Logic:**
```csharp
if (_rateLimitTracker != null)
{
    var (shouldWait, waitMs, reason) = _rateLimitTracker.ShouldThrottle(estimatedTokens);
    if (shouldWait)
    {
        await Task.Delay(waitMs, ct); // WAIT, don't fallback
    }
}
```

## How It Works

### Normal Operation:
1. Estimate tokens for request (~4 chars/token)
2. Check if window has capacity
3. If yes: Send request
4. Record usage from response headers

### Rate Limit Hit:
1. Receive 429 from Claude API
2. Parse `retry-after` or estimate 60s
3. Record cooldown period
4. **Wait** (Polly retries after delay)
5. Resume when tokens available

### Window Full:
1. Pre-request check calculates: `450k - used - 50k buffer`
2. If insufficient tokens: Calculate wait time
3. Wait for oldest usage to age out (60s)
4. Resume when capacity available

## Benefits

✅ **No Wasted API Calls** - Prevents 429 before sending
✅ **Smart Waiting** - Uses exact reset times when available
✅ **No Fallback** - Waits for Claude (as requested)
✅ **Distributed Safe** - Tracks per-instance (not global)
✅ **Logging** - Shows wait reasons and durations

## Configuration

Rate limiter is **automatically enabled** when `ANTHROPIC_API_KEY` is set.

No additional config needed!

## Monitoring

Watch for these log messages:

```
📊 Rate Limit: Used 380000/450000 tokens in window, 70000 remaining
🚦 Rate limit throttle: Window full. Waiting 15000ms...
✅ Rate limit wait complete, proceeding with request
🚨 Rate limit hit! Cooldown until 22:45:30
🔄 Retry 1: Waited 30s, calling Claude again...
```

## Testing

The rate limiter activates when:
- Using > 400k tokens in 60 seconds
- Receiving 429 from API
- Less than 100k tokens remaining (from headers)

With Blazor projects generating ~40k tokens per iteration, you can make ~10 iterations/minute before throttling.
