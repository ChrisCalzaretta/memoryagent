using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for generating pattern recommendations based on missing/weak patterns
/// </summary>
public class RecommendationService : IRecommendationService
{
    private readonly IPatternIndexingService _patternService;
    private readonly IGraphService _graphService;
    private readonly IVectorService _vectorService;
    private readonly ILogger<RecommendationService> _logger;

    // Critical patterns that should be present in most applications
    private static readonly Dictionary<PatternType, (string Issue, string Recommendation, string Impact, string Priority, string AzureUrl)> CriticalPatternChecks = new()
    {
        [PatternType.Resilience] = (
            "No retry logic detected in external service calls",
            "Add Polly retry policies for transient fault handling in database and API calls",
            "Without retry logic, transient failures will cause user-facing errors instead of being handled gracefully",
            "HIGH",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/retry"
        ),
        [PatternType.Validation] = (
            "No input validation detected",
            "Add DataAnnotations or FluentValidation to validate user inputs and prevent injection attacks",
            "Missing validation can lead to security vulnerabilities and data integrity issues",
            "CRITICAL",
            "https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation"
        ),
        [PatternType.Security] = (
            "No authentication/authorization detected",
            "Implement JWT or Azure AD authentication and role-based authorization",
            "Without authentication, your application is vulnerable to unauthorized access",
            "CRITICAL",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-security"
        ),
        [PatternType.Monitoring] = (
            "No health checks detected",
            "Implement IHealthCheck endpoints for monitoring and orchestration",
            "Without health checks, it's difficult to monitor application health in production",
            "HIGH",
            "https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks"
        ),
        [PatternType.Caching] = (
            "No caching detected in API endpoints",
            "Add IMemoryCache or distributed caching to reduce database load and improve response times",
            "Missing caching can lead to poor performance and high database costs",
            "MEDIUM",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching"
        )
    };

    // Code examples for common patterns
    private static readonly Dictionary<PatternType, string> CodeExamples = new()
    {
        [PatternType.Resilience] = @"
// Add Polly retry policy
services.AddHttpClient<IMyService, MyService>()
    .AddTransientHttpErrorPolicy(policy => 
        policy.WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));",

        [PatternType.Validation] = @"
// Add input validation
public class CreateUserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; }
}",

        [PatternType.Security] = @"
// Add JWT authentication
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* ... */ });

[Authorize(Roles = ""Admin"")]
public IActionResult SecureEndpoint() { /* ... */ }",

        [PatternType.Monitoring] = @"
// Add health checks
services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddUrlGroup(new Uri(""https://api.example.com/health""));

app.MapHealthChecks(""/health"");",

        [PatternType.Caching] = @"
// Add caching
public async Task<User> GetUserById(int id)
{
    if (!_cache.TryGetValue($""user_{id}"", out User user))
    {
        user = await _dbContext.Users.FindAsync(id);
        _cache.Set($""user_{id}"", user, TimeSpan.FromMinutes(5));
    }
    return user;
}"
    };

    public RecommendationService(
        IPatternIndexingService patternService,
        IGraphService graphService,
        IVectorService vectorService,
        ILogger<RecommendationService> logger)
    {
        _patternService = patternService;
        _graphService = graphService;
        _vectorService = vectorService;
        _logger = logger;
    }

    public async Task<RecommendationResponse> AnalyzeAndRecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing project for recommendations: {Context}", request.Context);

        var response = new RecommendationResponse
        {
            Context = request.Context
        };

        try
        {
            // Get all detected patterns for this context
            var allPatterns = new List<CodePattern>();
            foreach (var patternType in Enum.GetValues<PatternType>())
            {
                if (patternType == PatternType.Unknown) continue;

                var patterns = await _graphService.GetPatternsByTypeAsync(patternType, request.Context, cancellationToken);
                allPatterns.AddRange(patterns);
            }

            response.TotalPatternsDetected = allPatterns.Count;

            // Calculate overall health (based on coverage of critical patterns)
            var criticalPatternCoverage = CriticalPatternChecks.Keys
                .Count(pt => allPatterns.Any(p => p.Type == pt)) / (float)CriticalPatternChecks.Count;
            response.OverallHealth = criticalPatternCoverage;

            // Check for missing critical patterns
            foreach (var (patternType, info) in CriticalPatternChecks)
            {
                // Filter by requested categories if specified
                if (request.Categories?.Any() == true)
                {
                    var category = GetCategoryForPatternType(patternType);
                    if (!request.Categories.Contains(category))
                    {
                        continue;
                    }
                }

                var existingPatterns = allPatterns.Where(p => p.Type == patternType).ToList();

                if (!existingPatterns.Any())
                {
                    // Pattern is completely missing - HIGH/CRITICAL priority
                    var recommendation = new PatternRecommendation
                    {
                        Priority = info.Priority,
                        Category = GetCategoryForPatternType(patternType),
                        PatternType = patternType,
                        Issue = info.Issue,
                        Recommendation = info.Recommendation,
                        Impact = info.Impact,
                        AzureUrl = info.AzureUrl,
                        CodeExample = CodeExamples.GetValueOrDefault(patternType)
                    };

                    response.Recommendations.Add(recommendation);
                }
                else if (existingPatterns.Count < 3 && info.Priority != "LOW")
                {
                    // Pattern exists but is underutilized - MEDIUM priority
                    var recommendation = new PatternRecommendation
                    {
                        Priority = "MEDIUM",
                        Category = GetCategoryForPatternType(patternType),
                        PatternType = patternType,
                        Issue = $"Limited {patternType} implementation detected ({existingPatterns.Count} instances)",
                        Recommendation = $"Expand {info.Recommendation.ToLowerInvariant()} to more areas",
                        Impact = $"Inconsistent {patternType.ToString().ToLowerInvariant()} coverage can lead to reliability issues",
                        AzureUrl = info.AzureUrl,
                        AffectedFiles = existingPatterns.Select(p => p.FilePath).Distinct().ToList()
                    };

                    if (request.IncludeLowPriority || recommendation.Priority != "LOW")
                    {
                        response.Recommendations.Add(recommendation);
                    }
                }
            }

            // Detect anti-patterns or weak implementations
            await DetectAntiPatternsAsync(allPatterns, response, request, cancellationToken);

            // Sort by priority and limit results
            response.Recommendations = response.Recommendations
                .OrderBy(r => GetPriorityScore(r.Priority))
                .ThenBy(r => r.Category)
                .Take(request.MaxRecommendations)
                .ToList();

            _logger.LogInformation(
                "Analysis complete for {Context}: {Health:P0} health, {Recommendations} recommendations",
                request.Context, response.OverallHealth, response.Recommendations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing project for recommendations: {Context}", request.Context);
            throw;
        }

        return response;
    }

    private async Task DetectAntiPatternsAsync(
        List<CodePattern> allPatterns,
        RecommendationResponse response,
        RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        // Check for database calls without caching
        var hasDbCalls = await CheckForDatabaseCallsAsync(request.Context, cancellationToken);
        var hasCaching = allPatterns.Any(p => p.Type == PatternType.Caching);

        if (hasDbCalls && !hasCaching)
        {
            response.Recommendations.Add(new PatternRecommendation
            {
                Priority = "MEDIUM",
                Category = PatternCategory.Performance,
                PatternType = PatternType.Caching,
                Issue = "Database calls detected without caching implementation",
                Recommendation = "Add caching layer to reduce database load and improve performance",
                Impact = "Every request hits the database, causing slow response times and high costs",
                AzureUrl = "https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside",
                CodeExample = CodeExamples[PatternType.Caching]
            });
        }

        // Check for API endpoints without rate limiting
        var hasApiEndpoints = await CheckForApiEndpointsAsync(request.Context, cancellationToken);
        var hasRateLimiting = allPatterns.Any(p => p.Name.Contains("RateLimit", StringComparison.OrdinalIgnoreCase));

        if (hasApiEndpoints && !hasRateLimiting)
        {
            response.Recommendations.Add(new PatternRecommendation
            {
                Priority = "MEDIUM",
                Category = PatternCategory.Reliability,
                PatternType = PatternType.ApiDesign,
                Issue = "API endpoints detected without rate limiting",
                Recommendation = "Add rate limiting to protect APIs from abuse and ensure fair usage",
                Impact = "Without rate limiting, APIs are vulnerable to DoS attacks and resource exhaustion",
                AzureUrl = "https://learn.microsoft.com/en-us/azure/architecture/patterns/throttling"
            });
        }
    }

    private async Task<bool> CheckForDatabaseCallsAsync(string? context, CancellationToken cancellationToken)
    {
        try
        {
            // Search for common database patterns (DbContext, Repository, etc.)
            var dbQuery = "DbContext database repository sql";
            var embedding = await Task.FromResult(new float[0]); // Simplified for now
            // In real implementation, would search vector DB for database-related code
            return false; // Placeholder
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckForApiEndpointsAsync(string? context, CancellationToken cancellationToken)
    {
        try
        {
            // Search for API controllers
            // In real implementation, would search for [ApiController] or controller classes
            return await Task.FromResult(false); // Placeholder
        }
        catch
        {
            return false;
        }
    }

    private PatternCategory GetCategoryForPatternType(PatternType type)
    {
        return type switch
        {
            PatternType.Caching => PatternCategory.Performance,
            PatternType.Resilience => PatternCategory.Reliability,
            PatternType.Validation => PatternCategory.Security,
            PatternType.Security => PatternCategory.Security,
            PatternType.Monitoring => PatternCategory.Operational,
            PatternType.ApiDesign => PatternCategory.Operational,
            PatternType.BackgroundJobs => PatternCategory.Performance,
            PatternType.Configuration => PatternCategory.Operational,
            PatternType.AgentFramework => PatternCategory.AIAgents,
            PatternType.SemanticKernel => PatternCategory.AIAgents,
            PatternType.AutoGen => PatternCategory.AIAgents,
            _ => PatternCategory.General
        };
    }

    private int GetPriorityScore(string priority)
    {
        return priority switch
        {
            "CRITICAL" => 1,
            "HIGH" => 2,
            "MEDIUM" => 3,
            "LOW" => 4,
            _ => 5
        };
    }
}

