# Pattern Detection MCP Tools - Testing Guide

This guide walks through testing all 4 new MCP tools for pattern detection in Cursor.

---

## 🚀 Quick Start

### 1. Rebuild & Restart Containers

```powershell
# Stop containers
docker-compose down

# Rebuild with new code
docker-compose up -d --build

# Wait for services to be healthy
Start-Sleep -Seconds 30
```

### 2. Reindex Your Project (Patterns Auto-Detected)

```powershell
# Reindex to detect patterns
Invoke-RestMethod -Uri "http://localhost:5098/api/index/reindex" `
  -Method POST `
  -ContentType "application/json" `
  -Body (@{
    context = "CBC_AI"
    path = "E:\GitHub\CBC_AI"
    removeStale = $true
  } | ConvertTo-Json)
```

**Wait for reindexing to complete** (watch Docker logs):
```powershell
docker logs -f mcp-server --tail 100
```

### 3. Verify MCP Tools Are Registered

```powershell
.\test-mcp-tools-list.ps1
```

**Expected Output:**
```
✅ search_patterns
✅ validate_best_practices
✅ get_recommendations
✅ get_available_best_practices
```

### 4. Run Full Pattern Detection Tests

```powershell
.\test-pattern-mcp-tools.ps1
```

---

## 📋 MCP Tools Overview

### Tool 1: `search_patterns`

**Purpose:** Search for detected code patterns semantically

**Parameters:**
- `query` (string, required): Pattern search query
- `context` (string, optional): Project context
- `limit` (number, optional): Max results (default 20)

**Example:**
```json
{
  "name": "search_patterns",
  "arguments": {
    "query": "caching patterns",
    "context": "CBC_AI",
    "limit": 5
  }
}
```

**Output:**
```
🔍 Found 8 pattern(s) for 'caching patterns':

📊 CacheAside_TryGetValue
   Type: Caching (Performance)
   Implementation: IMemoryCache
   Language: csharp
   File: Services/UserService.cs:45
   Confidence: 95%
   Best Practice: Cache-Aside Pattern (Lazy Loading)
   📚 Azure Docs: https://learn.microsoft.com/...

   Code:
   if (!_cache.TryGetValue($"user_{id}", out User user))
   {
       user = await _dbContext.Users.FindAsync(id);
       ...
```

---

### Tool 2: `validate_best_practices`

**Purpose:** Validate project against Azure best practices

**Parameters:**
- `context` (string, required): Project context
- `bestPractices` (array, optional): Specific practices to check
- `includeExamples` (boolean, optional): Include code examples (default true)
- `maxExamplesPerPractice` (number, optional): Max examples per practice (default 5)

**Example:**
```json
{
  "name": "validate_best_practices",
  "arguments": {
    "context": "CBC_AI",
    "includeExamples": true,
    "maxExamplesPerPractice": 3
  }
}
```

**Output:**
```
📋 Best Practice Validation for 'CBC_AI'

Overall Score: 75% (15/20 practices)
✅ Implemented: 15
❌ Missing: 5

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ IMPLEMENTED PRACTICES:

• cache-aside (Caching)
  Count: 8 instances
  Avg Confidence: 92%
  Examples:
    - Services/UserService.cs:45 (IMemoryCache)
    - Services/ProductService.cs:67 (IMemoryCache)

❌ MISSING PRACTICES:

• circuit-breaker (Resilience)
  Recommendation: Implement circuit breaker pattern to prevent cascading failures
  📚 Learn more: https://learn.microsoft.com/...
```

---

### Tool 3: `get_recommendations`

**Purpose:** Get prioritized recommendations for missing/weak patterns

**Parameters:**
- `context` (string, required): Project context
- `categories` (array, optional): Focus categories (Performance, Security, etc.)
- `includeLowPriority` (boolean, optional): Include low priority (default false)
- `maxRecommendations` (number, optional): Max recommendations (default 10)

**Example:**
```json
{
  "name": "get_recommendations",
  "arguments": {
    "context": "CBC_AI",
    "includeLowPriority": false,
    "maxRecommendations": 10
  }
}
```

**Output:**
```
🎯 Architecture Recommendations for 'CBC_AI'

Overall Health: 65%
Patterns Detected: 42
Recommendations: 7

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

🚨 CRITICAL PRIORITY:

• No input validation detected
  Category: Security (Validation)
  Recommendation: Add DataAnnotations or FluentValidation to validate user inputs
  Impact: Missing validation can lead to security vulnerabilities
  📚 Learn more: https://learn.microsoft.com/...
  Example:
    public class CreateUserRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
    }

⚠️  HIGH PRIORITY:

• No retry logic detected in external service calls
  Category: Reliability (Resilience)
  Recommendation: Add Polly retry policies for transient fault handling
  Impact: Without retry logic, transient failures will cause user-facing errors
  Example:
    services.AddHttpClient<IMyService>()
        .AddTransientHttpErrorPolicy(policy => 
            policy.WaitAndRetryAsync(3, ...));
```

---

### Tool 4: `get_available_best_practices`

**Purpose:** List all available best practices

**Parameters:** None

**Example:**
```json
{
  "name": "get_available_best_practices",
  "arguments": {}
}
```

**Output:**
```
📚 Available Azure Best Practices (21 total):

Performance (Caching):
  • cache-aside
  • distributed-cache
  • response-cache

Reliability (Resilience):
  • retry-logic
  • circuit-breaker
  • timeout-policy

Security (Validation):
  • input-validation
  • model-validation

Security:
  • authentication
  • authorization
  • data-encryption

API Design:
  • pagination
  • versioning
  • rate-limiting

...
```

---

## 🧪 Test Scenarios

### Scenario 1: Pattern Discovery
**Goal:** Find all caching patterns in your codebase

```json
{
  "name": "search_patterns",
  "arguments": {
    "query": "caching patterns IMemoryCache Redis",
    "context": "CBC_AI",
    "limit": 20
  }
}
```

### Scenario 2: Security Audit
**Goal:** Check security-related best practices

```json
{
  "name": "validate_best_practices",
  "arguments": {
    "context": "CBC_AI",
    "bestPractices": ["authentication", "authorization", "input-validation", "data-encryption"],
    "includeExamples": true
  }
}
```

### Scenario 3: Performance Review
**Goal:** Get performance-related recommendations

```json
{
  "name": "get_recommendations",
  "arguments": {
    "context": "CBC_AI",
    "categories": ["Performance"],
    "maxRecommendations": 5
  }
}
```

### Scenario 4: Resilience Check
**Goal:** Find all retry and circuit breaker patterns

```json
{
  "name": "search_patterns",
  "arguments": {
    "query": "retry logic circuit breaker Polly resilience",
    "context": "CBC_AI",
    "limit": 15
  }
}
```

---

## 🔧 Troubleshooting

### Issue 1: No Patterns Found

**Cause:** Project hasn't been reindexed with pattern detection enabled

**Solution:**
```powershell
# Reindex with new pattern detection code
Invoke-RestMethod -Uri "http://localhost:5098/api/index/reindex" `
  -Method POST -ContentType "application/json" `
  -Body (@{context="CBC_AI"; path="E:\GitHub\CBC_AI"; removeStale=$true} | ConvertTo-Json)
```

### Issue 2: Tools Not Found

**Cause:** MCP service not properly registered

**Solution:**
```powershell
# Check Program.cs has all services registered
# Rebuild containers
docker-compose down
docker-compose up -d --build
```

### Issue 3: 0% Compliance Score

**Cause:** No patterns detected yet or wrong context name

**Solution:**
```powershell
# Verify context name matches
# Check that patterns were indexed
docker logs mcp-server | Select-String "pattern"
```

---

## ✅ Expected Results After Full Test

After running `.\test-pattern-mcp-tools.ps1`, you should see:

✅ **TEST 1:** 5-10 caching patterns found  
✅ **TEST 2:** 3-8 retry/resilience patterns found  
✅ **TEST 3:** 21 available best practices listed  
✅ **TEST 4:** Overall compliance score (e.g., 65-85%)  
✅ **TEST 5:** Specific practices validated (cache-aside, retry-logic, etc.)  
✅ **TEST 6:** 5-10 prioritized recommendations  
✅ **TEST 7:** Security/Performance-specific recommendations  
✅ **TEST 8:** 10-20 validation patterns found  

---

## 📊 Cursor Integration

### Using in Cursor Chat

Once the MCP server is running, Cursor will automatically detect the new tools:

**Example 1:**
```
You: "Search for caching patterns in my code"

Cursor: *Calls search_patterns*
        Found 8 caching patterns:
        - Cache-Aside in UserService.cs
        - Distributed Cache in ProductService.cs
        ...
```

**Example 2:**
```
You: "Does my project follow Azure best practices?"

Cursor: *Calls validate_best_practices*
        Overall compliance: 75%
        Missing: circuit-breaker, rate-limiting, ...
```

**Example 3:**
```
You: "What should I improve in my architecture?"

Cursor: *Calls get_recommendations*
        CRITICAL: Add input validation
        HIGH: Implement retry logic
        ...
```

---

## 🎯 Success Criteria

- [ ] All 4 new MCP tools appear in tools list
- [ ] `search_patterns` returns detected patterns with metadata
- [ ] `validate_best_practices` returns compliance score
- [ ] `get_recommendations` returns prioritized recommendations
- [ ] `get_available_best_practices` lists all 21 practices
- [ ] Cursor can call tools via natural language
- [ ] Pattern detection works for C#, Python, VB.NET files

---

## 🚀 Next Steps

1. **Reindex multiple projects** to build pattern knowledge base
2. **Use in Cursor** for architecture reviews
3. **Create custom patterns** (future feature)
4. **Track compliance over time** (future feature)
5. **Generate reports** for stakeholders

---

**Ready to test!** 🎉

