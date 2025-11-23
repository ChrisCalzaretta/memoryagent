# Code Pattern Detection & Best Practice Validation

## 🎯 Goal

Enable the Memory Agent to:
1. **Detect** common coding patterns (caching, retry logic, validation, etc.) in C#, Python, VB.NET
2. **Chunk and index** these patterns for semantic search
3. **Validate** that projects implement best practices
4. **Search** for patterns: "Show me all caching implementations" or "Does this project use retry logic?"

---

## 🔍 Patterns to Detect

### **1. Caching Patterns**

#### **C# Caching:**
```csharp
// Pattern: IMemoryCache
if (_cache.TryGetValue(key, out var value))
    return value;
    
_cache.Set(key, result, TimeSpan.FromMinutes(10));

// Pattern: IDistributedCache
var cached = await _distributedCache.GetAsync(key);

// Pattern: Redis
var db = _redis.GetDatabase();
var cached = await db.StringGetAsync(key);

// Pattern: ResponseCache attribute
[ResponseCache(Duration = 60)]
public IActionResult Get()

// Pattern: Output caching
[OutputCache(Duration = 300)]

// Pattern: Manual dictionary caching
private static readonly ConcurrentDictionary<string, object> _cache = new();
if (_cache.TryGetValue(key, out var value))
```

#### **Python Caching:**
```python
# Pattern: functools.lru_cache
@lru_cache(maxsize=128)
def expensive_function(arg):
    return result

# Pattern: cachetools
from cachetools import cached, TTLCache
cache = TTLCache(maxsize=100, ttl=300)

@cached(cache)
def get_data(key):
    return result

# Pattern: Redis
r = redis.Redis()
cached = r.get(key)
if cached:
    return cached
r.setex(key, 3600, value)

# Pattern: Django cache
from django.core.cache import cache
result = cache.get('key')
if result is None:
    result = expensive_operation()
    cache.set('key', result, 300)

# Pattern: Flask-Caching
@cache.memoize(timeout=300)
def get_user(id):
```

#### **VB.NET Caching:**
```vbnet
' Pattern: MemoryCache
If _cache.Contains(key) Then
    Return _cache.Get(key)
End If
_cache.Set(key, value, DateTimeOffset.Now.AddMinutes(10))

' Pattern: HttpRuntime.Cache (legacy)
If HttpRuntime.Cache(key) IsNot Nothing Then
    Return HttpRuntime.Cache(key)
End If

' Pattern: OutputCache
<OutputCache(Duration:=60)>
Public Function GetData() As ActionResult
```

---

### **2. Retry/Resilience Patterns**

#### **C# Retry Patterns:**
```csharp
// Pattern: Polly retry
Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))

// Pattern: Polly circuit breaker
Policy
    .Handle<Exception>()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30))

// Pattern: Manual retry
for (int i = 0; i < maxRetries; i++)
{
    try { return await operation(); }
    catch (Exception ex) when (i < maxRetries - 1)
    {
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
    }
}

// Pattern: Azure SDK retry
var options = new BlobClientOptions
{
    Retry = {
        MaxRetries = 3,
        Mode = RetryMode.Exponential
    }
};
```

#### **Python Retry Patterns:**
```python
# Pattern: tenacity
from tenacity import retry, stop_after_attempt, wait_exponential

@retry(stop=stop_after_attempt(3), wait=wait_exponential(multiplier=1, min=4, max=10))
def call_api():
    return requests.get(url)

# Pattern: backoff
@backoff.on_exception(backoff.expo, requests.exceptions.RequestException, max_tries=3)
def get_data():

# Pattern: Manual retry
for attempt in range(max_retries):
    try:
        return make_request()
    except Exception as e:
        if attempt < max_retries - 1:
            time.sleep(2 ** attempt)
```

---

### **3. Validation Patterns**

#### **C# Validation:**
```csharp
// Pattern: Data Annotations
public class UserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; }
    
    [EmailAddress]
    public string Email { get; set; }
    
    [Range(18, 120)]
    public int Age { get; set; }
}

// Pattern: FluentValidation
public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress();
    }
}

// Pattern: Guard clauses
Guard.Against.Null(user, nameof(user));
Guard.Against.NullOrEmpty(name, nameof(name));

// Pattern: Manual validation
if (string.IsNullOrWhiteSpace(name))
    throw new ArgumentException("Name is required", nameof(name));
```

#### **Python Validation:**
```python
# Pattern: Pydantic
from pydantic import BaseModel, EmailStr, validator

class User(BaseModel):
    name: str
    email: EmailStr
    age: int
    
    @validator('age')
    def age_must_be_valid(cls, v):
        if v < 18 or v > 120:
            raise ValueError('Invalid age')
        return v

# Pattern: Marshmallow
from marshmallow import Schema, fields, validate

class UserSchema(Schema):
    name = fields.Str(required=True, validate=validate.Length(min=3, max=100))
    email = fields.Email(required=True)
    age = fields.Int(validate=validate.Range(min=18, max=120))

# Pattern: Manual validation
if not name or len(name.strip()) == 0:
    raise ValueError("Name is required")
```

---

### **4. Dependency Injection Patterns**

#### **C# DI:**
```csharp
// Pattern: Constructor injection
public class UserService
{
    private readonly IUserRepository _repository;
    private readonly ILogger<UserService> _logger;
    
    public UserService(IUserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}

// Pattern: Service registration
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddSingleton<ICache, MemoryCache>();
builder.Services.AddTransient<IEmailService, EmailService>();

// Pattern: Options pattern
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

public class MyService
{
    private readonly AppSettings _settings;
    public MyService(IOptions<AppSettings> options)
    {
        _settings = options.Value;
    }
}
```

#### **Python DI:**
```python
# Pattern: dependency_injector
from dependency_injector import containers, providers

class Container(containers.DeclarativeContainer):
    config = providers.Configuration()
    database = providers.Singleton(Database, config.db.url)
    user_service = providers.Factory(UserService, db=database)

# Pattern: FastAPI dependency injection
from fastapi import Depends

def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

@app.get("/users/")
def read_users(db: Session = Depends(get_db)):
```

---

### **5. Logging Patterns**

#### **C# Logging:**
```csharp
// Pattern: ILogger structured logging
_logger.LogInformation("User {UserId} created order {OrderId}", userId, orderId);
_logger.LogWarning("Retry attempt {Attempt} for operation {Operation}", attempt, operation);
_logger.LogError(ex, "Failed to process request for {RequestId}", requestId);

// Pattern: Serilog
Log.Information("User {UserId} logged in at {LoginTime}", userId, DateTime.UtcNow);

// Pattern: Log scopes
using (_logger.BeginScope("Processing order {OrderId}", orderId))
{
    _logger.LogInformation("Validating order");
    _logger.LogInformation("Saving order");
}
```

#### **Python Logging:**
```python
# Pattern: Structured logging
logger.info("User login", extra={"user_id": user_id, "ip": ip_address})
logger.error("Database error", exc_info=True, extra={"query": query})

# Pattern: structlog
log = structlog.get_logger()
log.info("user.login", user_id=user_id, username=username)

# Pattern: Context logging
with log_context(request_id=request_id):
    logger.info("Processing request")
```

---

### **6. Error Handling Patterns**

#### **C# Error Handling:**
```csharp
// Pattern: Global exception handler
app.UseExceptionHandler("/error");
app.UseMiddleware<ErrorHandlingMiddleware>();

// Pattern: ProblemDetails
return Problem(
    detail: "Invalid input",
    statusCode: StatusCodes.Status400BadRequest,
    title: "Validation Error"
);

// Pattern: Result pattern
public Result<User> GetUser(int id)
{
    if (user == null)
        return Result<User>.Failure("User not found");
    return Result<User>.Success(user);
}

// Pattern: Custom exceptions
throw new UserNotFoundException($"User {id} not found");
```

#### **Python Error Handling:**
```python
# Pattern: Custom exceptions
class UserNotFoundException(Exception):
    pass

# Pattern: Context managers
@contextmanager
def transaction():
    try:
        yield
        commit()
    except Exception:
        rollback()
        raise

# Pattern: FastAPI exception handlers
@app.exception_handler(UserNotFoundException)
async def user_not_found_handler(request, exc):
    return JSONResponse(status_code=404, content={"error": str(exc)})
```

---

## 🔧 Implementation Strategy

### **Step 1: Extend Parsers to Detect Patterns**

#### **Enhance RoslynParser.cs:**
```csharp
public class PatternDetector
{
    public static List<CodePattern> DetectPatterns(SyntaxNode root, string filePath)
    {
        var patterns = new List<CodePattern>();
        
        // Detect caching patterns
        patterns.AddRange(DetectCachingPatterns(root, filePath));
        
        // Detect retry patterns
        patterns.AddRange(DetectRetryPatterns(root, filePath));
        
        // Detect validation patterns
        patterns.AddRange(DetectValidationPatterns(root, filePath));
        
        // Detect DI patterns
        patterns.AddRange(DetectDependencyInjectionPatterns(root, filePath));
        
        // Detect logging patterns
        patterns.AddRange(DetectLoggingPatterns(root, filePath));
        
        // Detect error handling patterns
        patterns.AddRange(DetectErrorHandlingPatterns(root, filePath));
        
        return patterns;
    }
    
    private static List<CodePattern> DetectCachingPatterns(SyntaxNode root, string filePath)
    {
        var patterns = new List<CodePattern>();
        
        // Find IMemoryCache.TryGetValue
        var tryGetValueCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("TryGetValue"));
            
        foreach (var call in tryGetValueCalls)
        {
            var lineNumber = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            
            patterns.Add(new CodePattern
            {
                Name = "MemoryCache_TryGetValue",
                Type = "Caching",
                Category = "Performance",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = call.ToString(),
                BestPractice = "IMemoryCache caching",
                Metadata = new Dictionary<string, object>
                {
                    ["pattern_type"] = "caching",
                    ["cache_type"] = "memory",
                    ["azure_best_practice"] = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching"
                }
            });
        }
        
        // Find [ResponseCache] attributes
        var responseCacheAttributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString().Contains("ResponseCache"));
            
        foreach (var attr in responseCacheAttributes)
        {
            patterns.Add(new CodePattern
            {
                Name = "ResponseCache_Attribute",
                Type = "Caching",
                Category = "Performance",
                FilePath = filePath,
                LineNumber = attr.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Content = attr.ToString(),
                BestPractice = "HTTP response caching",
                Metadata = new Dictionary<string, object>
                {
                    ["pattern_type"] = "caching",
                    ["cache_type"] = "http_response"
                }
            });
        }
        
        // Find IDistributedCache usage
        var distributedCacheCalls = root.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(ma => ma.Expression.ToString().Contains("distributedCache") || 
                         ma.Expression.ToString().Contains("_cache"));
                         
        // ... more detection logic
        
        return patterns;
    }
    
    private static List<CodePattern> DetectRetryPatterns(SyntaxNode root, string filePath)
    {
        var patterns = new List<CodePattern>();
        
        // Find Polly Policy usage
        var pollyPolicies = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("Policy.Handle") || 
                         inv.ToString().Contains("WaitAndRetry"));
                         
        foreach (var policy in pollyPolicies)
        {
            patterns.Add(new CodePattern
            {
                Name = "Polly_RetryPolicy",
                Type = "Resilience",
                Category = "Reliability",
                FilePath = filePath,
                LineNumber = policy.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Content = policy.Parent?.ToString() ?? policy.ToString(),
                BestPractice = "Polly retry policy for transient faults",
                Metadata = new Dictionary<string, object>
                {
                    ["pattern_type"] = "retry",
                    ["library"] = "Polly",
                    ["azure_best_practice"] = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults"
                }
            });
        }
        
        return patterns;
    }
    
    private static List<CodePattern> DetectValidationPatterns(SyntaxNode root, string filePath)
    {
        var patterns = new List<CodePattern>();
        
        // Find [Required], [Range], etc. attributes
        var validationAttributes = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString() is "Required" or "Range" or "EmailAddress" or "StringLength");
            
        foreach (var attr in validationAttributes)
        {
            patterns.Add(new CodePattern
            {
                Name = $"DataAnnotation_{attr.Name}",
                Type = "Validation",
                Category = "Security",
                FilePath = filePath,
                LineNumber = attr.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Content = attr.Parent?.ToString() ?? attr.ToString(),
                BestPractice = "Data Annotations validation",
                Metadata = new Dictionary<string, object>
                {
                    ["pattern_type"] = "validation",
                    ["validation_type"] = attr.Name.ToString()
                }
            });
        }
        
        // Find FluentValidation
        var fluentValidators = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.BaseList?.Types.Any(t => t.ToString().Contains("AbstractValidator")) == true);
            
        // ... more validation detection
        
        return patterns;
    }
}
```

---

### **Step 2: Python Pattern Detection**

```python
# MemoryAgent.Server/CodeAnalysis/PythonPatternDetector.py

import ast
import re

class PythonPatternDetector:
    @staticmethod
    def detect_patterns(source_code, file_path):
        patterns = []
        tree = ast.parse(source_code)
        
        # Detect caching decorators
        for node in ast.walk(tree):
            if isinstance(node, ast.FunctionDef):
                for decorator in node.decorator_list:
                    # @lru_cache
                    if isinstance(decorator, ast.Name) and decorator.id == 'lru_cache':
                        patterns.append({
                            'name': f'{node.name}_lru_cache',
                            'type': 'Caching',
                            'category': 'Performance',
                            'file_path': file_path,
                            'line_number': node.lineno,
                            'content': f'@lru_cache on {node.name}',
                            'best_practice': 'functools.lru_cache for function memoization',
                            'metadata': {
                                'pattern_type': 'caching',
                                'cache_type': 'function_decorator'
                            }
                        })
                    
                    # @cached, @memoize, etc.
                    if isinstance(decorator, ast.Call):
                        if hasattr(decorator.func, 'id'):
                            if decorator.func.id in ['cached', 'memoize', 'cache']:
                                patterns.append({
                                    'name': f'{node.name}_{decorator.func.id}',
                                    'type': 'Caching',
                                    'category': 'Performance',
                                    'file_path': file_path,
                                    'line_number': node.lineno,
                                    'content': f'@{decorator.func.id} on {node.name}',
                                    'best_practice': f'{decorator.func.id} caching decorator'
                                })
        
        # Detect retry decorators
        for node in ast.walk(tree):
            if isinstance(node, ast.FunctionDef):
                for decorator in node.decorator_list:
                    # @retry
                    if isinstance(decorator, ast.Call):
                        if hasattr(decorator.func, 'id') and decorator.func.id == 'retry':
                            patterns.append({
                                'name': f'{node.name}_retry',
                                'type': 'Resilience',
                                'category': 'Reliability',
                                'file_path': file_path,
                                'line_number': node.lineno,
                                'content': ast.unparse(decorator),
                                'best_practice': 'tenacity retry decorator'
                            })
        
        # Detect Pydantic models
        for node in ast.walk(tree):
            if isinstance(node, ast.ClassDef):
                for base in node.bases:
                    if isinstance(base, ast.Name) and base.id == 'BaseModel':
                        patterns.append({
                            'name': node.name,
                            'type': 'Validation',
                            'category': 'Security',
                            'file_path': file_path,
                            'line_number': node.lineno,
                            'content': f'class {node.name}(BaseModel)',
                            'best_practice': 'Pydantic data validation'
                        })
        
        return patterns
```

---

### **Step 3: Create CodePattern Model**

```csharp
// MemoryAgent.Server/Models/CodePattern.cs

public class CodePattern
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Caching, Resilience, Validation, etc.
    public string Category { get; set; } = string.Empty; // Performance, Reliability, Security
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string BestPractice { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string AzureBestPracticeUrl { get; set; } = string.Empty;
    public bool IsImplemented { get; set; } = true;
}
```

---

### **Step 4: Index Patterns in Qdrant**

```csharp
// Create new collection: "patterns"
await _vectorService.CreateCollectionAsync("patterns", 1024);

// Index detected patterns
foreach (var pattern in detectedPatterns)
{
    var embedding = await _embeddingService.GenerateEmbeddingAsync(
        $"{pattern.Type}: {pattern.BestPractice}\n{pattern.Content}"
    );
    
    await _vectorService.StoreCodeMemoryAsync(new CodeMemory
    {
        Type = CodeMemoryType.Pattern,
        Name = pattern.Name,
        Content = pattern.Content,
        FilePath = pattern.FilePath,
        LineNumber = pattern.LineNumber,
        Metadata = new Dictionary<string, object>
        {
            ["pattern_type"] = pattern.Type,
            ["pattern_category"] = pattern.Category,
            ["best_practice"] = pattern.BestPractice,
            ["azure_url"] = pattern.AzureBestPracticeUrl
        }
    }, embedding);
}
```

---

## 🔍 Search Queries Enabled

Once patterns are indexed, you can search:

```
"Show me all caching implementations"
→ Returns: IMemoryCache, Redis, @lru_cache, ResponseCache

"Find retry logic in the codebase"
→ Returns: Polly policies, @retry decorators, manual retry loops

"Where do we validate input?"
→ Returns: [Required] attributes, Pydantic models, FluentValidation

"What dependency injection patterns are used?"
→ Returns: Constructor injection, service registrations, Depends()

"Show error handling"
→ Returns: try/catch blocks, exception filters, @exception_handler

"Find logging patterns"
→ Returns: ILogger calls, structlog, Log.Information
```

---

## ✅ Validation Use Cases

### **Best Practice Validation:**

```csharp
// API endpoint: /api/validation/check-best-practices
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
    "results": [
        {
            "practice": "caching",
            "implemented": true,
            "count": 15,
            "examples": [
                "UserService.cs:45 - IMemoryCache",
                "ProductService.cs:67 - IDistributedCache"
            ],
            "azureUrl": "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching"
        },
        {
            "practice": "retry-logic",
            "implemented": true,
            "count": 8,
            "examples": [
                "ApiClient.cs:23 - Polly retry policy",
                "DatabaseService.cs:90 - exponential backoff"
            ],
            "azureUrl": "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults"
        },
        {
            "practice": "input-validation",
            "implemented": false,
            "count": 0,
            "recommendation": "Implement FluentValidation or Data Annotations"
        }
    ],
    "overallScore": 0.75
}
```

---

## 💬 Discussion

### **Questions:**

1. **Which patterns are most important to detect first?**
   - Caching (performance)
   - Retry logic (reliability)
   - Validation (security)
   - All of the above?

2. **Chunking strategy for patterns:**
   - Full method containing pattern?
   - Just the pattern snippet?
   - Pattern + context (5 lines before/after)?

3. **Validation reporting:**
   - Report missing patterns?
   - Suggest where to add them?
   - Generate TODOs automatically?

4. **Pattern libraries to support:**
   - C#: Polly, FluentValidation, Serilog, AutoMapper
   - Python: tenacity, pydantic, structlog, FastAPI
   - VB.NET: Legacy ASP.NET patterns

---

**Is this what you're looking for? Pattern DETECTION and SEARCH, not implementation?**

