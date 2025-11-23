# Pattern Detection Integration Plan

## ✅ Completed (Phase 1)

### Models Created:
1. **`CodePattern.cs`** - Complete model with:
   - PatternType enum (Caching, Resilience, Validation, DI, Logging, ErrorHandling, etc.)
   - PatternCategory enum (Performance, Reliability, Security, Operational, Cost)
   - BestPracticeValidation model
   - PatternValidationResult model

### Pattern Detectors Created:
2. **`IPatternDetector.cs`** - Interface for all detectors
3. **`CSharpPatternDetector.cs`** - Comprehensive C# pattern detection:
   - ✅ Caching: IMemoryCache, IDistributedCache, ResponseCache, OutputCache
   - ✅ Resilience: Polly policies, retry, circuit breaker, manual retry
   - ✅ Validation: DataAnnotations, FluentValidation, Guard clauses
   - ✅ DI: Constructor injection, service registration, Options pattern
   - ✅ Logging: ILogger, Serilog, structured logging, BeginScope
   - ✅ Error Handling: try/catch, custom exceptions, global handlers
   - ✅ Security: [Authorize], [ValidateAntiForgeryToken]
   - ✅ Configuration: IConfiguration, Options pattern
   - ✅ API Design: async/await, ActionResult<T>

4. **`PythonPatternDetector.cs`** - Comprehensive Python pattern detection:
   - ✅ Caching: @lru_cache, @cached, Redis, Django cache
   - ✅ Resilience: @retry, @backoff, manual retry
   - ✅ Validation: Pydantic BaseModel, @validator, Marshmallow
   - ✅ DI: FastAPI Depends, dependency_injector
   - ✅ Logging: structured logging, structlog
   - ✅ Error Handling: try/except, custom exceptions, @exception_handler
   - ✅ API Design: async def, FastAPI routes

5. **`VBNetPatternDetector.cs`** - VB.NET pattern detection:
   - ✅ Caching: MemoryCache, HttpRuntime.Cache, OutputCache
   - ✅ Resilience: Manual retry loops
   - ✅ Validation: DataAnnotations, manual validation
   - ✅ DI: Constructor injection
   - ✅ Logging: ILogger
   - ✅ Error Handling: Try/Catch blocks

---

## 🔄 Next Steps (Phase 2 - Integration)

### Step 1: Create Pattern Indexing Service

```csharp
// MemoryAgent.Server/Services/PatternIndexingService.cs
public class PatternIndexingService : IPatternIndexingService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorService _vectorService;
    private readonly IGraphService _graphService;
    private readonly ILogger<PatternIndexingService> _logger;
    
    public async Task IndexPatternsAsync(
        List<CodePattern> patterns, 
        CancellationToken ct = default)
    {
        foreach (var pattern in patterns)
        {
            // Generate embedding for pattern
            var embeddingText = $"{pattern.Type}: {pattern.BestPractice}\n{pattern.Content}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText, ct);
            
            // Store in Qdrant "patterns" collection
            var codeMemory = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = pattern.Name,
                Content = pattern.Content,
                FilePath = pattern.FilePath,
                LineNumber = pattern.LineNumber,
                Context = pattern.Context,
                Metadata = new Dictionary<string, object>
                {
                    ["pattern_type"] = pattern.Type.ToString(),
                    ["pattern_category"] = pattern.Category.ToString(),
                    ["implementation"] = pattern.Implementation,
                    ["best_practice"] = pattern.BestPractice,
                    ["azure_url"] = pattern.AzureBestPracticeUrl,
                    ["confidence"] = pattern.Confidence,
                    ["language"] = pattern.Language
                }
            };
            
            await _vectorService.StoreCodeMemoryAsync(codeMemory, embedding, ct);
            
            // Also store in Neo4j for graph queries
            await _graphService.StorePatternAsync(pattern, ct);
        }
    }
}
```

### Step 2: Update RoslynParser Integration

```csharp
// MemoryAgent.Server/CodeAnalysis/RoslynParser.cs
public class RoslynParser
{
    private readonly CSharpPatternDetector _patternDetector;
    private readonly IPatternIndexingService _patternIndexingService;
    
    public async Task<ParseResult> ParseCSharpFileAsync(
        string filePath, 
        string? context, 
        CancellationToken ct)
    {
        var result = new ParseResult();
        
        // ... existing parsing logic for classes/methods ...
        
        // NEW: Detect patterns
        var sourceCode = await File.ReadAllTextAsync(filePath, ct);
        var patterns = _patternDetector.DetectPatterns(sourceCode, filePath, context);
        
        // Index patterns
        await _patternIndexingService.IndexPatternsAsync(patterns, ct);
        
        // Add to result for reporting
        result.Metadata["patterns_detected"] = patterns.Count;
        result.Metadata["pattern_types"] = patterns.GroupBy(p => p.Type).Select(g => $"{g.Key}: {g.Count()}");
        
        return result;
    }
}
```

### Step 3: Update PythonParser Integration

```csharp
// MemoryAgent.Server/CodeAnalysis/PythonParser.cs
public static class PythonParser
{
    private static readonly PythonPatternDetector _patternDetector = new();
    
    public static ParseResult ParsePythonFile(
        string filePath, 
        string? context)
    {
        var result = new ParseResult();
        
        // ... existing parsing logic ...
        
        // NEW: Detect patterns
        var sourceCode = File.ReadAllText(filePath);
        var patterns = _patternDetector.DetectPatterns(sourceCode, filePath, context);
        
        // Store patterns (needs service injection)
        // For now, add to result metadata
        result.Metadata["patterns_detected"] = patterns.Count;
        
        return result;
    }
}
```

### Step 4: Create Best Practice Validation API

```csharp
// MemoryAgent.Server/Controllers/ValidationController.cs
[ApiController]
[Route("api/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly IVectorService _vectorService;
    private readonly IGraphService _graphService;
    
    [HttpPost("check-best-practices")]
    public async Task<ActionResult<BestPracticeValidation>> CheckBestPractices(
        [FromBody] BestPracticeRequest request,
        CancellationToken ct)
    {
        var validation = new BestPracticeValidation
        {
            Project = request.Context ?? "default",
            ValidatedAt = DateTime.UtcNow
        };
        
        foreach (var practice in request.BestPractices)
        {
            var result = await ValidatePattern(practice, request.Context, ct);
            validation.Results.Add(result);
        }
        
        validation.OverallScore = validation.Results.Count(r => r.Implemented) / (float)validation.Results.Count;
        
        return Ok(validation);
    }
    
    private async Task<PatternValidationResult> ValidatePattern(
        string practice, 
        string? context, 
        CancellationToken ct)
    {
        // Query Qdrant for patterns of this type
        var patternType = MapPracticeToPatternType(practice);
        
        // Get all patterns of this type
        var patterns = await _vectorService.GetPatternsByTypeAsync(patternType, context, ct);
        
        var result = new PatternValidationResult
        {
            Practice = practice,
            Type = patternType,
            Implemented = patterns.Any(),
            Count = patterns.Count,
            Examples = patterns.Take(3).Select(p => $"{Path.GetFileName(p.FilePath)}:{p.LineNumber} - {p.Implementation}").ToList(),
            AzureUrl = GetAzureUrlForPattern(patternType)
        };
        
        if (!result.Implemented)
        {
            result.Recommendation = GetRecommendationForPattern(patternType);
        }
        
        return result;
    }
}
```

### Step 5: Add Pattern Search to SmartSearch

```csharp
// MemoryAgent.Server/Services/SmartSearchService.cs
public async Task<SmartSearchResponse> SearchAsync(...)
{
    // ... existing logic ...
    
    // NEW: Detect if query is asking for patterns
    if (IsPatternQuery(request.Query))
    {
        return await SearchPatternsAsync(request, ct);
    }
    
    // ... existing search logic ...
}

private async Task<SmartSearchResponse> SearchPatternsAsync(...)
{
    // Extract pattern type from query
    // e.g., "caching implementations" → PatternType.Caching
    var patternType = ExtractPatternType(request.Query);
    
    // Query for patterns
    var patterns = await _vectorService.GetPatternsByTypeAsync(patternType, request.Context, ct);
    
    // Convert to search results
    var results = patterns.Select(p => new SmartSearchResult
    {
        Name = p.Name,
        Type = "Pattern",
        Content = p.Content,
        FilePath = p.FilePath,
        LineNumber = p.LineNumber,
        Metadata = new()
        {
            ["pattern_type"] = p.Type.ToString(),
            ["implementation"] = p.Implementation,
            ["best_practice"] = p.BestPractice,
            ["azure_url"] = p.AzureBestPracticeUrl
        }
    }).ToList();
    
    return new SmartSearchResponse
    {
        Query = request.Query,
        Strategy = "pattern-search",
        Results = results,
        TotalFound = results.Count
    };
}
```

---

## 📊 Usage Examples

### Example 1: Find All Caching Patterns

```http
POST /api/smartsearch
{
  "query": "caching implementations",
  "context": "CBC_AI",
  "limit": 20
}

Response:
{
  "strategy": "pattern-search",
  "results": [
    {
      "name": "UserService_MemoryCache",
      "type": "Pattern",
      "content": "if (_cache.TryGetValue(key, out var user)) return user;",
      "filePath": "Services/UserService.cs",
      "lineNumber": 45,
      "metadata": {
        "pattern_type": "Caching",
        "implementation": "IMemoryCache",
        "best_practice": "In-memory caching with IMemoryCache",
        "azure_url": "https://learn.microsoft.com/.../caching"
      }
    },
    // ... 19 more results
  ],
  "totalFound": 15
}
```

### Example 2: Validate Best Practices

```http
POST /api/validation/check-best-practices
{
  "context": "CBC_AI",
  "bestPractices": [
    "caching",
    "retry-logic",
    "input-validation",
    "structured-logging"
  ]
}

Response:
{
  "project": "CBC_AI",
  "validatedAt": "2025-11-23T10:00:00Z",
  "overallScore": 0.75,
  "results": [
    {
      "practice": "caching",
      "type": "Caching",
      "implemented": true,
      "count": 15,
      "examples": [
        "UserService.cs:45 - IMemoryCache",
        "ProductCache.cs:67 - Redis",
        "ApiController.cs:23 - ResponseCache"
      ],
      "azureUrl": "https://learn.microsoft.com/.../caching"
    },
    {
      "practice": "retry-logic",
      "type": "Resilience",
      "implemented": true,
      "count": 8,
      "examples": [
        "ApiClient.cs:23 - Polly.Retry",
        "DatabaseService.cs:90 - CircuitBreaker"
      ],
      "azureUrl": "https://learn.microsoft.com/.../transient-faults"
    },
    {
      "practice": "input-validation",
      "type": "Validation",
      "implemented": false,
      "count": 0,
      "recommendation": "Implement FluentValidation or Data Annotations for input validation"
    },
    {
      "practice": "structured-logging",
      "type": "Logging",
      "implemented": true,
      "count": 12,
      "examples": [
        "UserController.cs:56 - ILogger structured logging"
      ],
      "azureUrl": "https://learn.microsoft.com/.../monitoring"
    }
  ],
  "recommendations": [
    "Add input validation using FluentValidation or Data Annotations"
  ]
}
```

### Example 3: Search for Specific Pattern

```http
POST /api/smartsearch
{
  "query": "Show me all Polly retry policies",
  "context": "CBC_AI"
}

Response:
{
  "strategy": "pattern-search",
  "results": [
    {
      "name": "Polly_RetryPolicy",
      "content": "Policy.Handle<HttpRequestException>().WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))",
      "filePath": "Services/ApiClient.cs",
      "lineNumber": 23,
      "metadata": {
        "pattern_type": "Resilience",
        "implementation": "Polly.Retry",
        "best_practice": "Exponential backoff retry policy",
        "retry_type": "exponential_backoff"
      }
    }
  ]
}
```

---

## 🎯 Implementation Timeline

### Week 1: Core Integration
- [x] Day 1-2: Create pattern models and detectors (DONE)
- [ ] Day 3: Create PatternIndexingService
- [ ] Day 4: Integrate into RoslynParser
- [ ] Day 5: Integrate into PythonParser

### Week 2: API & Search
- [ ] Day 1: Create ValidationController
- [ ] Day 2: Add pattern search to SmartSearch
- [ ] Day 3: Create pattern recommendation engine
- [ ] Day 4-5: Integration testing

### Week 3: Testing & Documentation
- [ ] Write integration tests
- [ ] Update documentation
- [ ] Performance testing
- [ ] User acceptance testing

---

## 📝 Next Immediate Steps

1. **Create `IPatternIndexingService` interface**
2. **Create `PatternIndexingService` implementation**
3. **Update `RoslynParser.cs` to detect and index patterns**
4. **Update `PythonParser.cs` to detect and index patterns**
5. **Create `ValidationController.cs` for best practice checking**
6. **Test with CBC_AI project**

---

## 🚀 Expected Benefits

Once complete, you'll be able to:

1. **Search for patterns**: `"Show me all caching"`
2. **Validate projects**: `"Does CBC_AI use retry logic?"`
3. **Get recommendations**: `"What best practices are missing?"`
4. **Track adoption**: See which patterns are used across projects
5. **Learn from examples**: Find real implementations of patterns
6. **Enforce standards**: Validate all projects follow best practices

---

**Ready to continue with Phase 2 integration?**

