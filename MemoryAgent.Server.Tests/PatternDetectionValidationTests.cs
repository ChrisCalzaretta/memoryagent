using Xunit;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Validation tests for ALL Azure best practice pattern detection
/// Ensures each pattern detector correctly identifies its target patterns
/// </summary>
public class PatternDetectionValidationTests
{
    private readonly CSharpPatternDetectorEnhanced _detector = new();

    #region CACHING PATTERN VALIDATION

    [Fact]
    public void Should_Detect_CacheAside_Pattern()
    {
        var code = @"
public class UserService
{
    private readonly IMemoryCache _cache;
    
    public async Task<User> GetUserAsync(int id)
    {
        if (!_cache.TryGetValue(id, out User user))
        {
            user = await _database.GetUserAsync(id);
            _cache.Set(id, user, TimeSpan.FromMinutes(10));
        }
        return user;
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "UserService.cs", "test");
        
        var cacheAsidePattern = patterns.FirstOrDefault(p => p.Implementation == "Cache-Aside");
        Assert.NotNull(cacheAsidePattern);
        Assert.Equal(PatternType.Caching, cacheAsidePattern.Type);
        Assert.True(cacheAsidePattern.Confidence >= 0.9f);
        Assert.True((bool?)cacheAsidePattern.Metadata.GetValueOrDefault("lazy_loading") ?? false);
    }

    [Fact]
    public void Should_Detect_WriteThrough_Pattern()
    {
        var code = @"
public class ProductService
{
    public async Task UpdateProductAsync(Product product)
    {
        await _cache.SetAsync(product.Id, product);
        await _database.UpdateAsync(product);
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductService.cs", "test");
        
        var writeThroughPattern = patterns.FirstOrDefault(p => p.Implementation == "Write-Through");
        Assert.NotNull(writeThroughPattern);
        Assert.Equal("strong", writeThroughPattern.Metadata.GetValueOrDefault("consistency"));
    }

    [Fact]
    public void Should_Detect_WriteBehind_Pattern()
    {
        var code = @"
public class OrderService
{
    public async Task CreateOrderAsync(Order order)
    {
        await _cache.SetAsync(order.Id, order);
        _queue.Enqueue(() => _database.SaveAsync(order));
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "OrderService.cs", "test");
        
        var writeBehindPattern = patterns.FirstOrDefault(p => p.Implementation == "Write-Behind");
        Assert.NotNull(writeBehindPattern);
        Assert.True((bool?)writeBehindPattern.Metadata.GetValueOrDefault("async_persistence") ?? false);
    }

    [Fact]
    public void Should_Detect_CacheExpiration_Policies()
    {
        var code = @"
public class CacheService
{
    public void CacheItem(string key, object value)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(10),
            Priority = CacheItemPriority.High
        };
        _cache.Set(key, value, options);
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "CacheService.cs", "test");
        
        var expirationPattern = patterns.FirstOrDefault(p => p.Name == "CacheExpiration_Policy");
        Assert.NotNull(expirationPattern);
        Assert.True((bool?)expirationPattern.Metadata.GetValueOrDefault("absolute_expiration") ?? false);
        Assert.True((bool?)expirationPattern.Metadata.GetValueOrDefault("sliding_expiration") ?? false);
    }

    [Fact]
    public void Should_Detect_CacheStampede_Prevention()
    {
        var code = @"
public class DataService
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public async Task<Data> GetDataAsync(string key)
    {
        using (await _lock.WaitAsync())
        {
            if (!_cache.TryGetValue(key, out var data))
            {
                data = await LoadDataAsync(key);
                _cache.Set(key, data);
            }
            return data;
        }
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "DataService.cs", "test");
        
        var stampedePattern = patterns.FirstOrDefault(p => p.Name.Contains("CacheStampede"));
        Assert.NotNull(stampedePattern);
        Assert.Equal("semaphore", stampedePattern.Metadata.GetValueOrDefault("concurrency_control"));
    }

    #endregion

    #region API DESIGN PATTERN VALIDATION

    [Fact]
    public void Should_Detect_HttpVerb_Patterns()
    {
        var code = @"
[ApiController]
public class ProductsController
{
    [HttpGet]
    public ActionResult Get() => Ok();
    
    [HttpPost]
    public ActionResult Create() => Ok();
    
    [HttpPut]
    public ActionResult Update() => Ok();
    
    [HttpDelete]
    public ActionResult Delete() => Ok();
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductsController.cs", "test");
        
        var httpGetPattern = patterns.FirstOrDefault(p => p.Implementation == "HttpGet");
        Assert.NotNull(httpGetPattern);
        Assert.True((bool?)httpGetPattern.Metadata.GetValueOrDefault("safe") ?? false);
        Assert.True((bool?)httpGetPattern.Metadata.GetValueOrDefault("idempotent") ?? false);
        Assert.True((bool?)httpGetPattern.Metadata.GetValueOrDefault("cacheable") ?? false);
        
        var httpPostPattern = patterns.FirstOrDefault(p => p.Implementation == "HttpPost");
        Assert.NotNull(httpPostPattern);
        Assert.False((bool?)httpPostPattern.Metadata.GetValueOrDefault("safe") ?? true);
        Assert.False((bool?)httpPostPattern.Metadata.GetValueOrDefault("idempotent") ?? true);
    }

    [Fact]
    public void Should_Detect_Pagination_Pattern()
    {
        var code = @"
public class PaginatedResult<T>
{
    public List<T> Items { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNext { get; set; }
    public bool HasPrevious { get; set; }
    public string NextPageLink { get; set; }
    public string PreviousPageLink { get; set; }
}";
        
        var patterns = _detector.DetectPatterns(code, "PaginatedResult.cs", "test");
        
        var paginationPattern = patterns.FirstOrDefault(p => p.Implementation == "Pagination");
        Assert.NotNull(paginationPattern);
        Assert.True((bool?)paginationPattern.Metadata.GetValueOrDefault("has_navigation_links") ?? false);
        Assert.True((bool?)paginationPattern.Metadata.GetValueOrDefault("hateoas_compliant") ?? false);
    }

    [Fact]
    public void Should_Detect_Pagination_Parameters()
    {
        var code = @"
[HttpGet]
public ActionResult GetProducts(int pageNumber, int pageSize)
{
    return Ok();
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductsController.cs", "test");
        
        var paginationPattern = patterns.FirstOrDefault(p => p.Name.Contains("PaginationParams"));
        Assert.NotNull(paginationPattern);
    }

    [Fact]
    public void Should_Detect_Filtering_Sorting_Pattern()
    {
        var code = @"
[HttpGet]
public ActionResult GetProducts(
    [FromQuery] string filter,
    [FromQuery] string sort,
    [FromQuery] string fields)
{
    return Ok();
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductsController.cs", "test");
        
        var queryPattern = patterns.FirstOrDefault(p => p.Name.Contains("QueryCapabilities"));
        Assert.NotNull(queryPattern);
        Assert.True((bool?)queryPattern.Metadata.GetValueOrDefault("supports_filtering") ?? false);
        Assert.True((bool?)queryPattern.Metadata.GetValueOrDefault("supports_sorting") ?? false);
        Assert.True((bool?)queryPattern.Metadata.GetValueOrDefault("supports_field_selection") ?? false);
    }

    [Fact]
    public void Should_Detect_Versioning_URI_Pattern()
    {
        var code = @"
[Route(""api/v1/products"")]
[ApiController]
public class ProductsController : ControllerBase
{
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductsController.cs", "test");
        
        var versioningPattern = patterns.FirstOrDefault(p => p.Implementation == "UriVersioning");
        Assert.NotNull(versioningPattern);
        Assert.Equal("uri", versioningPattern.Metadata.GetValueOrDefault("versioning_strategy"));
    }

    [Fact]
    public void Should_Detect_Versioning_Attribute_Pattern()
    {
        var code = @"
[ApiVersion(""1.0"")]
[ApiController]
public class ProductsController : ControllerBase
{
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductsController.cs", "test");
        
        var versioningPattern = patterns.FirstOrDefault(p => p.Implementation == "ApiVersionAttribute");
        Assert.NotNull(versioningPattern);
        Assert.Equal("attribute", versioningPattern.Metadata.GetValueOrDefault("versioning_strategy"));
    }

    [Fact]
    public void Should_Detect_HATEOAS_Pattern()
    {
        var code = @"
public class ResourceResponse
{
    public object Data { get; set; }
    public Dictionary<string, Link> Links { get; set; }
}";
        
        var patterns = _detector.DetectPatterns(code, "ResourceResponse.cs", "test");
        
        var hateoasPattern = patterns.FirstOrDefault(p => p.Implementation == "HATEOAS");
        Assert.NotNull(hateoasPattern);
        Assert.True((bool?)hateoasPattern.Metadata.GetValueOrDefault("hypermedia_support") ?? false);
    }

    [Fact]
    public void Should_Detect_ContentNegotiation_Pattern()
    {
        var code = @"
[Produces(""application/json"", ""application/xml"")]
[Consumes(""application/json"")]
public class ProductsController
{
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductsController.cs", "test");
        
        var producesPattern = patterns.FirstOrDefault(p => p.Implementation == "Produces");
        Assert.NotNull(producesPattern);
        Assert.True((bool?)producesPattern.Metadata.GetValueOrDefault("supports_multiple_formats") ?? false);
    }

    #endregion

    #region API IMPLEMENTATION PATTERN VALIDATION

    [Fact]
    public void Should_Detect_Idempotency_Pattern()
    {
        var code = @"
[HttpPost]
public async Task<ActionResult> CreateOrder(
    [FromHeader(Name = ""Idempotency-Key"")] string idempotencyKey,
    [FromBody] Order order)
{
    return Ok();
}";
        
        var patterns = _detector.DetectPatterns(code, "OrdersController.cs", "test");
        
        var idempotencyPattern = patterns.FirstOrDefault(p => p.Implementation == "IdempotencyKey");
        Assert.NotNull(idempotencyPattern);
        Assert.True((bool?)idempotencyPattern.Metadata.GetValueOrDefault("prevents_duplicate_operations") ?? false);
    }

    [Fact]
    public void Should_Detect_AsyncOperation_Pattern()
    {
        var code = @"
[HttpPost]
public async Task<IActionResult> StartProcessing()
{
    var jobId = await _queue.EnqueueAsync(job);
    return Accepted(new { jobId, statusUrl = $""/api/jobs/{jobId}"" });
}";
        
        var patterns = _detector.DetectPatterns(code, "JobsController.cs", "test");
        
        var asyncPattern = patterns.FirstOrDefault(p => p.Implementation == "AsyncOperation");
        Assert.NotNull(asyncPattern);
        Assert.Equal(202, asyncPattern.Metadata.GetValueOrDefault("http_status"));
    }

    [Fact]
    public void Should_Detect_BatchOperation_Pattern()
    {
        var code = @"
[HttpPost(""batch"")]
public async Task<ActionResult> BatchCreate([FromBody] List<Product> products)
{
    return Ok();
}";
        
        var patterns = _detector.DetectPatterns(code, "ProductsController.cs", "test");
        
        var batchPattern = patterns.FirstOrDefault(p => p.Implementation == "BatchAPI");
        Assert.NotNull(batchPattern);
        Assert.True((bool?)batchPattern.Metadata.GetValueOrDefault("reduces_round_trips") ?? false);
    }

    [Fact]
    public void Should_Detect_ETag_Pattern()
    {
        var code = @"
[HttpGet(""{id}"")]
public ActionResult Get(int id)
{
    var entity = _repository.Get(id);
    Response.Headers[""ETag""] = GenerateETag(entity);
    return Ok(entity);
}";
        
        var patterns = _detector.DetectPatterns(code, "Controller.cs", "test");
        
        var etagPattern = patterns.FirstOrDefault(p => p.Implementation == "ETag");
        Assert.NotNull(etagPattern);
        Assert.True((bool?)etagPattern.Metadata.GetValueOrDefault("supports_caching") ?? false);
    }

    #endregion

    #region MONITORING PATTERN VALIDATION

    [Fact]
    public void Should_Detect_CorrelationId_Pattern()
    {
        var code = @"
public class MyService
{
    public async Task ProcessAsync(string correlationId)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [""CorrelationId""] = correlationId
        }))
        {
            _logger.LogInformation(""Processing"");
        }
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "MyService.cs", "test");
        
        var correlationPattern = patterns.FirstOrDefault(p => p.Implementation == "CorrelationId");
        Assert.NotNull(correlationPattern);
        Assert.True((bool?)correlationPattern.Metadata.GetValueOrDefault("distributed_tracing") ?? false);
    }

    [Fact]
    public void Should_Detect_HealthCheck_Pattern()
    {
        var code = @"
public class DatabaseHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        return HealthCheckResult.Healthy();
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "DatabaseHealthCheck.cs", "test");
        
        var healthCheckPattern = patterns.FirstOrDefault(p => p.Implementation == "HealthCheck");
        Assert.NotNull(healthCheckPattern);
        Assert.Equal(PatternType.Monitoring, healthCheckPattern.Type);
    }

    [Fact]
    public void Should_Detect_Telemetry_Pattern()
    {
        var code = @"
public class MyService
{
    public void ProcessEvent()
    {
        _telemetryClient.TrackEvent(""UserLogin"", new Dictionary<string, string>
        {
            [""UserId""] = userId
        });
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "MyService.cs", "test");
        
        var telemetryPattern = patterns.FirstOrDefault(p => p.Implementation == "ApplicationInsights");
        Assert.NotNull(telemetryPattern);
        Assert.Equal("Event", telemetryPattern.Metadata.GetValueOrDefault("telemetry_type"));
    }

    #endregion

    #region BACKGROUND JOB PATTERN VALIDATION

    [Fact]
    public void Should_Detect_HostedService_Pattern()
    {
        var code = @"
public class TimedHostedService : IHostedService, IDisposable
{
    public Task StartAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
    
    public void Dispose() { }
}";
        
        var patterns = _detector.DetectPatterns(code, "TimedHostedService.cs", "test");
        
        var hostedServicePattern = patterns.FirstOrDefault(p => p.Implementation == "IHostedService");
        Assert.NotNull(hostedServicePattern);
        Assert.Equal(PatternType.BackgroundJobs, hostedServicePattern.Type);
    }

    [Fact]
    public void Should_Detect_MessageQueue_Pattern()
    {
        var code = @"
public class QueueProcessor
{
    [ServiceBusTrigger(""myqueue"")]
    public async Task ProcessMessage(Message message)
    {
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "QueueProcessor.cs", "test");
        
        var queuePattern = patterns.FirstOrDefault(p => p.Name == "MessageQueue_Consumer");
        Assert.NotNull(queuePattern);
    }

    [Fact]
    public void Should_Detect_Hangfire_Pattern()
    {
        var code = @"
public class JobScheduler
{
    public void ScheduleJob()
    {
        BackgroundJob.Enqueue(() => SendEmail());
        RecurringJob.AddOrUpdate(""daily"", () => GenerateReport(), Cron.Daily);
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "JobScheduler.cs", "test");
        
        var hangfirePattern = patterns.FirstOrDefault(p => p.Implementation == "Hangfire");
        Assert.NotNull(hangfirePattern);
        Assert.Equal("Hangfire", hangfirePattern.Metadata.GetValueOrDefault("library"));
    }

    #endregion

    #region DATA PARTITIONING PATTERN VALIDATION

    [Fact]
    public void Should_Detect_Sharding_Pattern()
    {
        var code = @"
public class ShardedRepository
{
    private readonly Dictionary<int, IRepository> _shards;
    
    public async Task<User> GetAsync(int id)
    {
        var shardKey = id % _shards.Count;
        return await _shards[shardKey].GetAsync(id);
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "ShardedRepository.cs", "test");
        
        var shardingPattern = patterns.FirstOrDefault(p => p.Implementation == "HorizontalPartitioning");
        Assert.NotNull(shardingPattern);
        Assert.True((bool?)shardingPattern.Metadata.GetValueOrDefault("scalability_pattern") ?? false);
    }

    [Fact]
    public void Should_Detect_VerticalPartition_Pattern()
    {
        var code = @"
public class UserHistory
{
    public int UserId { get; set; }
    public List<Activity> Activities { get; set; }
}";
        
        // Need corresponding hot class
        var hotCode = @"
public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; }
}";
        
        var patterns = _detector.DetectPatterns(code + hotCode, "UserData.cs", "test");
        
        var verticalPattern = patterns.FirstOrDefault(p => p.Implementation == "VerticalPartitioning");
        Assert.NotNull(verticalPattern);
        Assert.Equal("cold", verticalPattern.Metadata.GetValueOrDefault("data_temperature"));
    }

    #endregion

    #region SECURITY PATTERN VALIDATION

    [Fact]
    public void Should_Detect_Authorization_Patterns()
    {
        var code = @"
[Authorize(Policy = ""AdminOnly"")]
public class AdminController
{
    [Authorize(Roles = ""Admin,Manager"")]
    public ActionResult Get() => Ok();
}";
        
        var patterns = _detector.DetectPatterns(code, "AdminController.cs", "test");
        
        var policyPattern = patterns.FirstOrDefault(p => p.Metadata.ContainsKey("has_policy") && (bool)p.Metadata["has_policy"]);
        Assert.NotNull(policyPattern);
        
        var rolePattern = patterns.FirstOrDefault(p => p.Metadata.ContainsKey("has_roles") && (bool)p.Metadata["has_roles"]);
        Assert.NotNull(rolePattern);
    }

    [Fact]
    public void Should_Detect_CORS_Pattern()
    {
        var code = @"
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(""AllowAll"", builder =>
            {
                builder.WithOrigins(""https://example.com"")
                       .AllowCredentials();
            });
        });
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "Startup.cs", "test");
        
        var corsPattern = patterns.FirstOrDefault(p => p.Implementation == "CORS");
        Assert.NotNull(corsPattern);
        Assert.True((bool?)corsPattern.Metadata.GetValueOrDefault("with_origins") ?? false);
    }

    [Fact]
    public void Should_Detect_RateLimiting_Pattern()
    {
        var code = @"
[EnableRateLimiting(""fixed"")]
public class ApiController
{
}";
        
        var patterns = _detector.DetectPatterns(code, "ApiController.cs", "test");
        
        var rateLimitPattern = patterns.FirstOrDefault(p => p.Implementation == "RateLimiting");
        Assert.NotNull(rateLimitPattern);
        Assert.True((bool?)rateLimitPattern.Metadata.GetValueOrDefault("protects_from_abuse") ?? false);
    }

    #endregion

    #region CONFIGURATION PATTERN VALIDATION

    [Fact]
    public void Should_Detect_Options_Pattern()
    {
        var code = @"
public class MyService
{
    private readonly DatabaseOptions _options;
    
    public MyService(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "MyService.cs", "test");
        
        var optionsPattern = patterns.FirstOrDefault(p => p.Metadata.GetValueOrDefault("strongly_typed_config") is true);
        Assert.NotNull(optionsPattern);
        Assert.Equal("standard", optionsPattern.Metadata.GetValueOrDefault("options_type"));
    }

    [Fact]
    public void Should_Detect_NamedOptions_Pattern()
    {
        var code = @"
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<DatabaseOptions>(""Primary"",
            Configuration.GetSection(""PrimaryDb""));
        services.Configure<DatabaseOptions>(""Secondary"",
            Configuration.GetSection(""SecondaryDb""));
    }
}";
        
        var patterns = _detector.DetectPatterns(code, "Startup.cs", "test");
        
        var namedOptionsPattern = patterns.FirstOrDefault(p => p.Implementation == "NamedOptions");
        Assert.NotNull(namedOptionsPattern);
        Assert.True((bool?)namedOptionsPattern.Metadata.GetValueOrDefault("supports_multiple_configs") ?? false);
    }

    #endregion
}

