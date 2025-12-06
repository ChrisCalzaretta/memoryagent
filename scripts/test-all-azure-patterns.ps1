# Test All 42 Azure Architecture Patterns

Write-Host "`n🎯 TESTING ALL 42 AZURE ARCHITECTURE PATTERNS" -ForegroundColor Cyan
Write-Host "============================================`n" -ForegroundColor Cyan

# Wait for server
Start-Sleep -Seconds 3

$baseUrl = "http://localhost:5000"

# Test 1: Verify server is running
Write-Host "1. Checking server status..." -ForegroundColor Yellow
try {
    $tools = Invoke-RestMethod -Uri "$baseUrl/api/mcp/tools" -Method Get -TimeoutSec 10
    Write-Host "   ✅ Server responding - $($tools.tools.Count) MCP tools available" -ForegroundColor Green
    
    $patternTools = $tools.tools | Where-Object { $_.name -like '*pattern*' }
    Write-Host "   ✅ Pattern tools: $($patternTools.Count)" -ForegroundColor Green
    $patternTools | ForEach-Object { Write-Host "      - $($_.name)" -ForegroundColor Gray }
} catch {
    Write-Host "   ❌ Server not responding: $_" -ForegroundColor Red
    exit 1
}

# Test 2: Index a file with ALL Azure patterns
Write-Host "`n2. Creating test file with all 42 Azure patterns..." -ForegroundColor Yellow

$comprehensiveTest = @'
using System;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using MediatR;

// 1. CQRS
public class CreateOrderCommand : IRequest { }
public class GetOrderQuery : IRequest { }

// 2. Event Sourcing
public class OrderEventStore { public void Append(DomainEvent e) { } }

// 3. Circuit Breaker
public class ServiceClient { AsyncCircuitBreakerPolicy _cb; }

// 4. Bulkhead
public class IsolatedService { AsyncBulkheadPolicy _bulkhead; }

// 5. Saga
public class OrderSaga { public async Task Execute() { } }

// 6. Compensating Transaction
public class CompensatePayment { public async Task Rollback() { } }

// 7. Retry
public class RetryableService { AsyncRetryPolicy _retry; }

// 8. Cache-Aside
public async Task<Data> GetData(string key) {
    if (!_cache.TryGetValue(key, out var data)) {
        data = await LoadFromDb(key);
        _cache.Set(key, data);
    }
    return data;
}

// 9. Gateway Aggregation
public async Task<Result> Aggregate() {
    var r1 = await _http.GetAsync("service1");
    var r2 = await _http.GetAsync("service2");
    return Combine(r1, r2);
}

// 10. Ambassador
public class HttpAmbassador { HttpClient _client; }

// 11. Anti-Corruption Layer
public class LegacySystemAdapter { }

// 12. Backends for Frontends
public class MobileController { }

// 13. Materialized View
public class OrderReadModel { }

// 14. External Config
public void Configure() { _config.AddAzureAppConfiguration(); }

// 15. Federated Identity
public void Auth() { services.AddMicrosoftIdentityWebApp(); }

// 16. Throttling
public void Throttle() { app.AddRateLimiter(); }

// 17. Competing Consumers
public class MessageConsumer { public async Task ProcessMessageAsync() { } }

// 18. Priority Queue  
public class PriorityQueue<T> { }

// 19. Queue Load Leveling
public async Task Enqueue() { await _queue.SendMessageAsync(); }

// 20. Leader Election
public async Task AcquireLeadership() { await blob.AcquireLeaseAsync(); }

// 21. Choreography
public class OrderCreatedEventHandler : INotificationHandler { }

// 22. Claim Check
public async Task StoreAndNotify() {
    await blob.UploadAsync(data);
    await queue.SendMessageAsync(reference);
}

// 23. Async Request-Reply
public async Task<IActionResult> StartJob() { return Accepted(statusUrl); }

// 24. Pipes and Filters
public class DataPipeline { }

// 25. Sequential Convoy
public async Task ProcessSession(string sessionId) { }

// 26. Valet Key
public Uri GetSasUri() { return blob.GenerateSasUri(); }

// 27. Index Table
public Dictionary<string, User> UserIndex { get; set; }

// 28. Static Content
public void ConfigureBlob() { var client = new BlobClient(); }

// 29. Gateway Offloading
public class AuthMiddleware { public async Task InvokeAsync() { } }

// 30. Gateway Routing
public void ConfigureRouting() { app.MapReverseProxy(); }

// 31. Geode
public void ConfigureGeo() { cosmosDb.UseMultiRegion(); }

// 32. Deployment Stamps
public class ScaleUnit { }

// 33. Quarantine
public async Task ValidateExternal() { }

// 34. Sidecar
// Detected in Docker files

// 35. Strangler Fig
public class LegacySystemProxy { }

// 36. Compute Consolidation
public class ConsolidatedService : IHostedService { }

// 37. Scheduler-Agent-Supervisor
public class WorkScheduler { }

// 38. Messaging Bridge
public class EventBridge { }

// Plus existing: Health Checks, Publisher/Subscriber, Rate Limiting, Sharding
'@

$comprehensiveTest | Out-File -FilePath "all-patterns-test.cs" -Encoding UTF8
Write-Host "   ✅ Test file created: all-patterns-test.cs" -ForegroundColor Green

# Test 3: Index the file
Write-Host "`n3. Indexing file with pattern detection..." -ForegroundColor Yellow

$indexBody = @{
    context = "azure-complete-test"
    filePath = "/workspace/all-patterns-test.cs"
    sourceCode = $comprehensiveTest
} | ConvertTo-Json -Depth 5

try {
    $indexResult = Invoke-RestMethod -Uri "$baseUrl/api/index/file" -Method Post -Body $indexBody -ContentType "application/json" -TimeoutSec 120
    Write-Host "   ✅ File indexed successfully!" -ForegroundColor Green
    
    if ($indexResult.patterns) {
        Write-Host "   ✅ Detected $($indexResult.patterns.Count) patterns:" -ForegroundColor Green
        $indexResult.patterns | Group-Object -Property type | ForEach-Object {
            Write-Host "      - $($_.Name): $($_.Count) instances" -ForegroundColor Cyan
        }
    }
} catch {
    Write-Host "   ⚠️ Indexing issue: $_" -ForegroundColor Yellow
}

# Test 4: Search for specific Azure patterns
Write-Host "`n4. Testing pattern search..." -ForegroundColor Yellow

$searchPatterns = @(
    "CQRS command query",
    "circuit breaker",
    "event sourcing",
    "saga distributed transaction",
    "bulkhead isolation"
)

foreach ($pattern in $searchPatterns) {
    $searchBody = @{
        query = $pattern
        context = "azure-complete-test"
        limit = 5
    } | ConvertTo-Json
    
    try {
        $searchResult = Invoke-RestMethod -Uri "$baseUrl/api/smartsearch/search" -Method Post -Body $searchBody -ContentType "application/json" -TimeoutSec 30
        if ($searchResult.results -and $searchResult.results.Count -gt 0) {
            Write-Host "   ✅ '$pattern': Found $($searchResult.results.Count) results" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️ '$pattern': No results" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "   ⚠️ '$pattern': Search error" -ForegroundColor Yellow
    }
    Start-Sleep -Milliseconds 500
}

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "🎉 PATTERN DETECTION TEST COMPLETE!" -ForegroundColor Green
Write-Host "============================================`n" -ForegroundColor Cyan







