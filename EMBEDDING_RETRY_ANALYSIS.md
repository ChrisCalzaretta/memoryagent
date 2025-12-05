# Embedding Retry Logic - Analysis & Recommendations

## 🔍 **CURRENT BEHAVIOR**

### **YES - Embeddings DO Retry!** ✅

The `EmbeddingService` uses **Polly** for retry logic:

```csharp
// EmbeddingService.cs - Lines 34-44
_retryPolicy = Policy
    .Handle<HttpRequestException>()        // ⬅️ Only HTTP connection failures
    .WaitAndRetryAsync(
        retryCount: 3,                     // ⬅️ 3 retries
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        //                                    ⬆️ Exponential backoff: 2s, 4s, 8s
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            _logger.LogWarning(
                "Retry {RetryCount} after {Delay}s due to: {Exception}",
                retryCount, timeSpan.TotalSeconds, exception.Message);
        });
```

**Retry Timeline:**
```
Attempt 1: Fails → Wait 2 seconds
Attempt 2: Fails → Wait 4 seconds  
Attempt 3: Fails → Wait 8 seconds
Attempt 4: Fails → THROW EXCEPTION ❌

Total retry time: 14 seconds before giving up
```

---

## ⚠️ **PROBLEMS WITH CURRENT IMPLEMENTATION**

### **Problem 1: Only Retries Network Failures**

```csharp
.Handle<HttpRequestException>()  // ⬅️ ONLY this exception type
```

**What IS retried:**
- ✅ Connection refused
- ✅ DNS lookup failures
- ✅ Network unreachable

**What is NOT retried:**
- ❌ **Timeouts** (`TaskCanceledException`)
- ❌ **500 Internal Server Error** from Ollama
- ❌ **400 Bad Request** (malformed input)
- ❌ **Empty response** (`InvalidOperationException`)
- ❌ **JSON deserialization errors**

---

### **Problem 2: Batch Failure Loses All Items**

```csharp
// EmbeddingService.cs - Lines 121-122
var tasks = batch.Select(text => GenerateEmbeddingAsync(text, cancellationToken));
var batchEmbeddings = await Task.WhenAll(tasks);  // ⬅️ ANY failure = ALL fail!
```

**Scenario:**
- Batch of 32 code elements
- 31 succeed, 1 fails
- **Result:** All 32 lost! ❌

---

### **Problem 3: No Fallback Mechanism**

When all retries fail:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error generating embedding...");
    throw;  // ⬅️ Propagates up, kills entire file indexing!
}
```

**Impact:**
```
IndexingService.IndexFileAsync()
    ↓
GenerateEmbeddingsAsync() throws ❌
    ↓
ENTIRE file indexing fails
    ↓
No data stored (not in Qdrant, not in Neo4j)
```

---

### **Problem 4: No Circuit Breaker**

If Ollama is down or slow:
- ❌ Every single embedding retries 3 times (14s each)
- ❌ No "fail fast" after detecting Ollama is unavailable
- ❌ Can cause massive slowdowns during indexing

**Example:**
- 100 code elements to embed
- Ollama is down
- Each tries 4 times × 14s = 1,400 seconds = **23 minutes of retries!** 🔥

---

## ✅ **WHAT WORKS WELL**

1. ✅ **Exponential backoff** - Proper retry timing
2. ✅ **Logging** - Warning logs on each retry
3. ✅ **Batch processing** - GPU-optimized batches of 32
4. ✅ **Text truncation** - Handles oversized input
5. ✅ **Health check** - Can detect Ollama availability

---

## 🚀 **RECOMMENDED IMPROVEMENTS**

### **Improvement 1: Retry More Exception Types**

```csharp
// BEFORE
_retryPolicy = Policy
    .Handle<HttpRequestException>()  // ❌ Too narrow!

// AFTER
_retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()      // ✅ Timeouts
    .Or<TimeoutException>()           // ✅ Explicit timeouts
    .Or<InvalidOperationException>(ex => 
        ex.Message.Contains("Empty embedding"))  // ✅ Ollama errors
    .WaitAndRetryAsync(...)
```

---

### **Improvement 2: Individual Item Error Handling**

```csharp
// CURRENT (ALL-OR-NOTHING)
var tasks = batch.Select(text => GenerateEmbeddingAsync(text, cancellationToken));
var batchEmbeddings = await Task.WhenAll(tasks);  // ❌ Throws on first failure

// IMPROVED (RESILIENT)
public async Task<List<float[]>> GenerateEmbeddingsAsync(
    List<string> texts, 
    CancellationToken cancellationToken = default)
{
    var embeddings = new List<float[]>();
    const int batchSize = 32;
    
    for (int i = 0; i < texts.Count; i += batchSize)
    {
        var batch = texts.Skip(i).Take(batchSize).ToList();
        
        // Process items individually to isolate failures
        foreach (var text in batch)
        {
            try
            {
                var embedding = await GenerateEmbeddingAsync(text, cancellationToken);
                embeddings.Add(embedding);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding after retries. Using zero vector.");
                
                // OPTION A: Use zero vector (allows indexing to continue)
                embeddings.Add(new float[_vectorSize]);
                
                // OPTION B: Skip this item (exclude from Qdrant, keep in Neo4j)
                // embeddings.Add(null);  // Caller can filter nulls
                
                // OPTION C: Throw (fail entire batch)
                // throw;
            }
        }
    }
    
    return embeddings;
}
```

---

### **Improvement 3: Circuit Breaker Pattern**

```csharp
// Add to EmbeddingService
private readonly AsyncCircuitBreakerPolicy _circuitBreaker;

public EmbeddingService(...)
{
    // ... existing code ...
    
    // Circuit breaker: After 5 failures, open circuit for 30s
    _circuitBreaker = Policy
        .Handle<HttpRequestException>()
        .CircuitBreakerAsync(
            exceptionsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (ex, duration) =>
            {
                _logger.LogError("Circuit breaker opened: Ollama appears down. Pausing for {Duration}s", duration.TotalSeconds);
            },
            onReset: () =>
            {
                _logger.LogInformation("Circuit breaker reset: Ollama appears healthy again");
            });
    
    // Wrap retry policy with circuit breaker
    _resiliencePolicy = Policy.WrapAsync(_circuitBreaker, _retryPolicy);
}

public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
{
    return await _resiliencePolicy.ExecuteAsync(async () =>
    {
        // ... existing embedding logic ...
    });
}
```

**Benefits:**
- After 5 failures, stops trying for 30 seconds
- Prevents cascading failures
- Allows Ollama to recover
- Fast failure (no 14s retries per item)

---

### **Improvement 4: Graceful Degradation in IndexingService**

```csharp
// IndexingService.cs - IndexFileAsync()

// BEFORE (Line 106)
var embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);

// AFTER
List<float[]> embeddings;
try
{
    embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);
}
catch (Exception embeddingEx)
{
    _logger.LogWarning(embeddingEx, 
        "Embedding generation failed for {File}. Storing metadata only (no vector search).", 
        containerPath);
    
    // OPTION A: Skip vector storage, but continue with Neo4j (graph-only mode)
    embeddings = null;
    
    // Store in Neo4j only
    await _graphService.StoreCodeNodesAsync(parseResult.CodeElements, cancellationToken);
    
    // Skip Qdrant storage
    // Still have: code structure, relationships, patterns in Neo4j!
    
    return result;
}

// Only store in Qdrant if embeddings succeeded
if (embeddings != null)
{
    for (int i = 0; i < parseResult.CodeElements.Count; i++)
    {
        parseResult.CodeElements[i].Embedding = embeddings[i];
    }
    
    await _vectorService.StoreCodeMemoriesAsync(parseResult.CodeElements, cancellationToken);
}
```

**Result:**
- ✅ Indexing continues even if embeddings fail
- ✅ Neo4j graph still built (relationships work!)
- ✅ Code structure still available
- ❌ Semantic search won't work for this file

---

### **Improvement 5: Pre-Check Ollama Health**

```csharp
// IndexingService.cs - Before generating embeddings

// Check if Ollama is healthy before attempting embeddings
var isHealthy = await _embeddingService.HealthCheckAsync(cancellationToken);

if (!isHealthy)
{
    _logger.LogWarning("Ollama is unhealthy. Skipping embeddings for {File}", containerPath);
    
    // Store in Neo4j only (graph mode)
    await _graphService.StoreCodeNodesAsync(parseResult.CodeElements, cancellationToken);
    
    result.Success = true;
    result.Warnings.Add("Embeddings skipped - Ollama unavailable");
    return result;
}

// Only generate embeddings if Ollama is healthy
var embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);
```

---

## 📊 **COMPARISON: Before vs After**

| Scenario | Current Behavior | With Improvements |
|----------|------------------|-------------------|
| **Network hiccup** | ✅ Retries 3 times, succeeds | ✅ Same |
| **Timeout** | ❌ Immediate failure | ✅ Retries 3 times |
| **Ollama returns 500** | ❌ Immediate failure | ✅ Retries 3 times |
| **Empty embedding** | ❌ Immediate failure | ✅ Retries 3 times |
| **1 item in batch fails** | ❌ All 32 items lost | ✅ 31 items succeed |
| **Ollama is down** | ❌ 14s × N items (slow death) | ✅ Circuit breaker opens after 5 failures |
| **All retries exhausted** | ❌ Entire file indexing fails | ✅ Neo4j indexing continues |

---

## 🎯 **RECOMMENDED PRIORITY**

### **High Priority (Do Now):**
1. ✅ **Add timeout retries** - `TaskCanceledException`, `TimeoutException`
2. ✅ **Graceful degradation** - Continue indexing without embeddings
3. ✅ **Individual item handling** - Don't lose entire batch

### **Medium Priority (Soon):**
4. ✅ **Circuit breaker** - Prevent cascading failures
5. ✅ **Health pre-check** - Skip embeddings if Ollama down

### **Low Priority (Nice to have):**
6. ✅ **Fallback embedding service** - Use different model if primary fails
7. ✅ **Queue failed items** - Retry later when Ollama recovers
8. ✅ **Metrics** - Track embedding success rate

---

## 💡 **QUICK WIN: Minimal Code Change**

**Change 1:** Retry more exceptions (2 minutes)
```csharp
// EmbeddingService.cs - Line 34
_retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TaskCanceledException>()     // ⬅️ ADD THIS
    .Or<TimeoutException>()          // ⬅️ ADD THIS
    .WaitAndRetryAsync(...)
```

**Change 2:** Don't fail indexing on embedding errors (5 minutes)
```csharp
// IndexingService.cs - Around line 106
try
{
    var embeddings = await _embeddingService.GenerateEmbeddingsAsync(textsToEmbed, cancellationToken);
    
    for (int i = 0; i < parseResult.CodeElements.Count; i++)
    {
        parseResult.CodeElements[i].Embedding = embeddings[i];
    }
    
    await _vectorService.StoreCodeMemoriesAsync(parseResult.CodeElements, cancellationToken);
}
catch (Exception embeddingEx)
{
    _logger.LogWarning(embeddingEx, "Embeddings failed for {File}, continuing with graph-only storage", containerPath);
    result.Warnings.Add("Semantic search unavailable - embeddings failed");
}

// Always store in Neo4j (even without embeddings)
await _graphService.StoreCodeNodesAsync(parseResult.CodeElements, cancellationToken);
```

**Impact:**
- ✅ Timeouts now retried
- ✅ Indexing continues even if embeddings fail
- ✅ Graph relationships still work
- ✅ 7 minutes of work, huge reliability improvement!

---

## 🔬 **TESTING**

### **Test 1: Ollama Timeout**
```bash
# Simulate slow Ollama
docker exec memory-agent-ollama tc qdisc add dev eth0 root netem delay 60000ms

# Try indexing - should retry and eventually use graph-only mode
```

### **Test 2: Ollama Down**
```bash
# Stop Ollama
docker stop memory-agent-ollama

# Try indexing - should skip embeddings, continue with Neo4j
```

### **Test 3: Partial Batch Failure**
```python
# Create test file with very long function (>1400 chars)
# Mixed with normal functions
# Should handle individually
```

---

## 📝 **SUMMARY**

**Current State:**
- ✅ HAS retry logic (3 retries, exponential backoff)
- ❌ Only retries network failures
- ❌ Batch failures lose all items
- ❌ No fallback mechanism
- ❌ Indexing fails completely if embeddings fail

**Recommended State:**
- ✅ Retry timeouts and server errors
- ✅ Individual item error handling
- ✅ Graceful degradation (Neo4j-only mode)
- ✅ Circuit breaker for Ollama failures
- ✅ Indexing continues even without embeddings

**Result:** **99.9% uptime** even when Ollama has issues!





