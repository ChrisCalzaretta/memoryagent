# Comprehensive Azure Best Practices Pattern Detection

Based on official Microsoft Azure Architecture Best Practices documentation.

## 📚 Source Documentation

All patterns derived from:
- [Azure Caching Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching)
- [Azure API Design Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design)
- [Azure API Implementation Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-implementation)
- [Azure Autoscaling Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/auto-scaling)
- [Azure Background Jobs Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/background-jobs)
- [Azure CDN Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/cdn)
- [Azure Data Partitioning Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/data-partitioning)
- [Azure Monitoring & Diagnostics](https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring)
- [Azure Transient Fault Handling](https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults)

---

## 🎯 Pattern Categories (Expanded)

### 1. CACHING PATTERNS (from Azure Caching Best Practices)

#### **Cache-Aside Pattern (Lazy Loading)**
```csharp
// Pattern to detect
if (!_cache.TryGetValue(key, out var value))
{
    value = await _database.GetAsync(key);
    _cache.Set(key, value, expirationTime);
}
return value;
```
**Detection Criteria:**
- Check cache before database
- Populate cache on miss
- Return cached value

#### **Read-Through Cache Pattern**
```csharp
// Cache handles database fetch
public class ReadThroughCache : ICacheProvider
{
    public async Task<T> GetAsync<T>(string key, Func<Task<T>> fetchFromSource)
    {
        // Cache fetches from source automatically
    }
}
```
**Detection Criteria:**
- Cache abstraction layer
- Automatic source fetching

#### **Write-Through Cache Pattern**
```csharp
// Both cache and database updated
await _cache.SetAsync(key, value);
await _database.UpdateAsync(key, value);
```
**Detection Criteria:**
- Simultaneous cache AND database writes
- Synchronous updates

#### **Write-Behind (Write-Back) Pattern**
```csharp
// Write to cache, async write to database
await _cache.SetAsync(key, value);
_backgroundQueue.Enqueue(() => _database.UpdateAsync(key, value));
```
**Detection Criteria:**
- Async database writes
- Background queue for persistence

#### **Refresh-Ahead Pattern**
```csharp
// Refresh cache before expiration
if (TimeUntilExpiration(key) < threshold)
{
    _ = Task.Run(() => RefreshCacheAsync(key));
}
```
**Detection Criteria:**
- Proactive cache refresh
- Expiration threshold checks

#### **Cache Stampede Prevention**
```csharp
// Lock pattern to prevent multiple loads
using var lockObj = await _distributedLock.AcquireAsync(key);
if (!_cache.TryGetValue(key, out var value))
{
    value = await _database.GetAsync(key);
    _cache.Set(key, value);
}
```
**Detection Criteria:**
- Distributed locking
- Single loader pattern

#### **Cache Expiration Policies**
```csharp
// Absolute expiration
_cache.Set(key, value, new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
});

// Sliding expiration
_cache.Set(key, value, new MemoryCacheEntryOptions
{
    SlidingExpiration = TimeSpan.FromMinutes(5)
});

// Combined expiration
_cache.Set(key, value, new MemoryCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
    SlidingExpiration = TimeSpan.FromMinutes(10)
});
```
**Detection Criteria:**
- AbsoluteExpiration usage
- SlidingExpiration usage
- Priority settings

---

### 2. API DESIGN PATTERNS (from Azure API Design Best Practices)

#### **HTTP Method Semantics**
```csharp
[HttpGet] // Safe, idempotent, cacheable
[HttpPost] // Not safe, not idempotent
[HttpPut] // Not safe, idempotent
[HttpPatch] // Not safe, not idempotent
[HttpDelete] // Not safe, idempotent
[HttpHead] // Safe, idempotent
[HttpOptions] // Safe, idempotent
```
**Detection Criteria:**
- Correct HTTP verb usage
- Idempotency implementation

#### **Pagination Support**
```csharp
public class PaginatedResult<T>
{
    public List<T> Items { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public bool HasPrevious { get; set; }
    public bool HasNext { get; set; }
    public string? PreviousPageLink { get; set; }
    public string? NextPageLink { get; set; }
}
```
**Detection Criteria:**
- PageNumber/PageSize parameters
- TotalCount in response
- Next/Previous links (HATEOAS)

#### **Filtering and Sorting**
```csharp
[HttpGet]
public ActionResult<IEnumerable<Product>> Get(
    [FromQuery] string? filter,
    [FromQuery] string? sort,
    [FromQuery] string? fields)
```
**Detection Criteria:**
- Query parameter filtering
- Sort parameter
- Field selection (sparse fieldsets)

#### **Versioning Strategies**
```csharp
// URI versioning
[Route("api/v1/products")]

// Header versioning
[ApiVersion("1.0")]

// Query parameter versioning
[Route("api/products?api-version=1.0")]
```
**Detection Criteria:**
- Version in URI
- Version in header
- Version in query string

#### **HATEOAS (Hypermedia)**
```csharp
public class ResourceResponse
{
    public object Data { get; set; }
    public Dictionary<string, Link> Links { get; set; }
}
```
**Detection Criteria:**
- Links in responses
- Rel attributes
- Discoverability

#### **Content Negotiation**
```csharp
[Produces("application/json", "application/xml")]
[Consumes("application/json")]
```
**Detection Criteria:**
- Accept header handling
- Multiple content types
- Format negotiation

---

### 3. API IMPLEMENTATION PATTERNS

#### **Idempotency Keys**
```csharp
public async Task<IActionResult> CreateOrder(
    [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
    [FromBody] Order order)
{
    if (await _idempotencyStore.ExistsAsync(idempotencyKey))
    {
        return await _idempotencyStore.GetResultAsync(idempotencyKey);
    }
    // Process and store result
}
```
**Detection Criteria:**
- Idempotency-Key header
- Deduplication logic
- Result caching

#### **Asynchronous Operations (Long-Running)**
```csharp
[HttpPost]
public async Task<IActionResult> StartJob()
{
    var jobId = await _jobQueue.EnqueueAsync(job);
    return Accepted(new { jobId, statusUrl = $"/api/jobs/{jobId}/status" });
}

[HttpGet("jobs/{id}/status")]
public async Task<IActionResult> GetJobStatus(string id)
{
    var status = await _jobQueue.GetStatusAsync(id);
    if (status.IsComplete)
        return Ok(status.Result);
    return Accepted(status);
}
```
**Detection Criteria:**
- 202 Accepted responses
- Status endpoint
- Polling mechanism

#### **Partial Response (Sparse Fieldsets)**
```csharp
[HttpGet]
public ActionResult Get([FromQuery] string? fields)
{
    var data = _repository.GetAll();
    if (!string.IsNullOrEmpty(fields))
    {
        return Ok(SelectFields(data, fields.Split(',')));
    }
    return Ok(data);
}
```
**Detection Criteria:**
- Field selection parameter
- Dynamic projection

#### **Batch Operations**
```csharp
[HttpPost("batch")]
public async Task<ActionResult<BatchResponse>> BatchOperation(
    [FromBody] List<Operation> operations)
{
    var results = new List<OperationResult>();
    foreach (var op in operations)
    {
        results.Add(await ExecuteAsync(op));
    }
    return Ok(results);
}
```
**Detection Criteria:**
- Batch endpoint
- Multiple operations
- Partial success handling

#### **ETags for Concurrency**
```csharp
[HttpGet("{id}")]
public ActionResult Get(int id)
{
    var entity = _repository.Get(id);
    var etag = GenerateETag(entity);
    Response.Headers["ETag"] = etag;
    return Ok(entity);
}

[HttpPut("{id}")]
public ActionResult Update(int id, 
    [FromBody] Entity entity,
    [FromHeader(Name = "If-Match")] string? ifMatch)
{
    var current = _repository.Get(id);
    if (ifMatch != null && GenerateETag(current) != ifMatch)
    {
        return StatusCode(412); // Precondition Failed
    }
    // Update
}
```
**Detection Criteria:**
- ETag header generation
- If-Match header validation
- Concurrency checks

---

### 4. AUTOSCALING PATTERNS

#### **Scale-Out Detection**
```csharp
// Azure App Service
[AutoScale(MinInstances = 2, MaxInstances = 10)]

// Azure Functions
[FunctionName("ProcessQueue")]
[ServiceBusTrigger(..., Connection = "...", IsSessionsEnabled = false)]
```
**Detection Criteria:**
- Auto-scale attributes
- Instance count configuration
- Metric-based scaling

#### **Queue-Based Load Leveling**
```csharp
// Producer
await _queue.SendMessageAsync(message);

// Consumer with back-pressure
[ServiceBusTrigger("queue", MaxConcurrentCalls = 16)]
public async Task ProcessMessage([ServiceBusTrigger] Message message)
```
**Detection Criteria:**
- Queue message sending
- Consumer throttling
- Concurrent call limits

---

### 5. BACKGROUND JOB PATTERNS

#### **Hosted Services**
```csharp
public class TimedHostedService : IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken ct)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        return Task.CompletedTask;
    }
}
```
**Detection Criteria:**
- IHostedService implementation
- Background execution
- Timer-based jobs

#### **Message Queue Consumers**
```csharp
[ServiceBusTrigger("queue")]
public async Task ProcessMessage(Message message)

[QueueTrigger("queue")]
public async Task ProcessQueueItem(QueueMessage msg)
```
**Detection Criteria:**
- Queue trigger attributes
- Message processing

#### **Hangfire Jobs**
```csharp
BackgroundJob.Enqueue(() => SendEmail(userId));
RecurringJob.AddOrUpdate("daily-report", () => GenerateReport(), Cron.Daily);
```
**Detection Criteria:**
- BackgroundJob.Enqueue
- RecurringJob usage
- Cron expressions

---

### 6. MONITORING & DIAGNOSTICS PATTERNS

#### **Structured Logging with Correlation IDs**
```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["UserId"] = userId
}))
{
    _logger.LogInformation("Processing request");
}
```
**Detection Criteria:**
- Correlation ID propagation
- Log scopes
- Structured properties

#### **Health Checks**
```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            await _db.ExecuteScalarAsync<int>("SELECT 1");
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
```
**Detection Criteria:**
- IHealthCheck implementation
- /health endpoint
- Dependency checks

#### **Application Insights Telemetry**
```csharp
_telemetryClient.TrackEvent("UserLogin", new Dictionary<string, string>
{
    ["UserId"] = userId,
    ["LoginMethod"] = method
});

_telemetryClient.TrackMetric("QueueLength", queueLength);
_telemetryClient.TrackDependency("Database", "SQL", "GetUser", startTime, duration, success);
```
**Detection Criteria:**
- TelemetryClient usage
- Custom events/metrics
- Dependency tracking

---

### 7. DATA PARTITIONING PATTERNS

#### **Horizontal Partitioning (Sharding)**
```csharp
public class ShardedRepository<T>
{
    private readonly Dictionary<int, IRepository<T>> _shards;
    
    public async Task<T> GetAsync(int id)
    {
        var shardKey = id % _shards.Count;
        return await _shards[shardKey].GetAsync(id);
    }
}
```
**Detection Criteria:**
- Shard key calculation
- Multiple data stores
- Partition routing

#### **Vertical Partitioning**
```csharp
// Hot data store
public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// Cold data store
public class UserHistory
{
    public int UserId { get; set; }
    public List<Activity> Activities { get; set; }
}
```
**Detection Criteria:**
- Separate stores for hot/cold data
- Data segregation by access pattern

---

### 8. CDN PATTERNS

#### **Static Content Offloading**
```csharp
public class StaticFileOptions
{
    public string CdnUrl { get; set; }
}

public string GetAssetUrl(string path)
{
    return _options.UseCdn 
        ? $"{_options.CdnUrl}/{path}" 
        : $"/assets/{path}";
}
```
**Detection Criteria:**
- CDN URL configuration
- Asset path rewriting
- Static file serving

#### **Cache-Control Headers**
```csharp
[ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
public IActionResult GetImage(string id)
```
**Detection Criteria:**
- Cache-Control header
- Long cache duration for static assets
- CDN-friendly headers

---

### 9. SECURITY PATTERNS

#### **Authentication & Authorization**
```csharp
[Authorize(Policy = "AdminOnly")]
[Authorize(Roles = "Admin,Manager")]
[Authorize(AuthenticationSchemes = "Bearer")]
```
**Detection Criteria:**
- Authorization attributes
- Policy-based auth
- Role-based access control

#### **CORS Configuration**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", builder =>
    {
        builder.WithOrigins("https://example.com")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});
```
**Detection Criteria:**
- CORS policy configuration
- Allowed origins
- Credential handling

#### **Rate Limiting**
```csharp
[EnableRateLimiting("fixed")]
[RateLimit(PermitLimit = 100, Window = 60)]
```
**Detection Criteria:**
- Rate limit policies
- Throttling configuration

---

### 10. CONFIGURATION PATTERNS

#### **Options Pattern**
```csharp
public class DatabaseOptions
{
    public string ConnectionString { get; set; }
    public int MaxRetries { get; set; }
}

builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection("Database"));

public class MyService
{
    public MyService(IOptions<DatabaseOptions> options)
    {
        var config = options.Value;
    }
}
```
**Detection Criteria:**
- IOptions<T> injection
- Configure<T> registration
- Strongly-typed configuration

#### **Named Options**
```csharp
builder.Services.Configure<DatabaseOptions>("Primary",
    builder.Configuration.GetSection("PrimaryDb"));
builder.Services.Configure<DatabaseOptions>("Secondary",
    builder.Configuration.GetSection("SecondaryDb"));

public MyService(IOptionsSnapshot<DatabaseOptions> options)
{
    var primaryDb = options.Get("Primary");
}
```
**Detection Criteria:**
- Named options
- IOptionsSnapshot usage
- Multiple configurations

---

## 🔍 Pattern Detection Checklist

For EACH pattern above, we need to detect:

### C# Detection:
- [ ] Exact syntax match
- [ ] Related interfaces/base classes
- [ ] Common naming conventions
- [ ] Attribute usage
- [ ] Configuration patterns

### Python Detection:
- [ ] Decorator patterns
- [ ] Framework-specific implementations
- [ ] Common library usage

### VB.NET Detection:
- [ ] VB.NET syntax equivalents
- [ ] Legacy patterns

---

## 📊 Pattern Priority (By Azure Docs)

### HIGH PRIORITY (Core Best Practices):
1. Caching (Cache-Aside, Read-Through, Write-Through)
2. Retry with exponential backoff
3. Circuit breaker
4. Idempotency
5. Pagination
6. Health checks
7. Structured logging

### MEDIUM PRIORITY (Performance & Scale):
8. Async operations (long-running tasks)
9. ETags for concurrency
10. Batch operations
11. Queue-based load leveling
12. Data partitioning

### LOWER PRIORITY (Advanced):
13. HATEOAS
14. CDN integration
15. Named options
16. Refresh-ahead caching

---

**This is the comprehensive list from Microsoft docs. Ready to implement ALL of these?**

