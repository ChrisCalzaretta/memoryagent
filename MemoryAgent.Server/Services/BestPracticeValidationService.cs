using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for validating Azure best practices in code
/// </summary>
public class BestPracticeValidationService : IBestPracticeValidationService
{
    private readonly IPatternIndexingService _patternService;
    private readonly IGraphService _graphService;
    private readonly ILogger<BestPracticeValidationService> _logger;

    // Azure best practices catalog (based on AZURE_PATTERNS_COMPREHENSIVE.md)
    private static readonly Dictionary<string, (PatternType Type, PatternCategory Category, string Recommendation, string AzureUrl)> BestPracticesCatalog = new()
    {
        // Caching patterns
        ["cache-aside"] = (PatternType.Caching, PatternCategory.Performance, 
            "Implement Cache-Aside pattern to reduce database load and improve response times.", 
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside"),
        ["distributed-cache"] = (PatternType.Caching, PatternCategory.Performance,
            "Use distributed caching (Redis/Azure Cache) for scalability across multiple instances.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching"),
        ["response-cache"] = (PatternType.Caching, PatternCategory.Performance,
            "Implement response caching for HTTP endpoints to improve API performance.",
            "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response"),
        
        // Resilience patterns
        ["retry-logic"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Add retry policies (Polly) for transient fault handling in external service calls.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/retry"),
        ["circuit-breaker"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Implement circuit breaker pattern to prevent cascading failures.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker"),
        ["timeout-policy"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Configure timeout policies to prevent resource exhaustion from slow operations.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults"),
        
        // Validation patterns
        ["input-validation"] = (PatternType.Validation, PatternCategory.Security,
            "Add input validation (DataAnnotations/FluentValidation) to prevent injection attacks.",
            "https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation"),
        ["model-validation"] = (PatternType.Validation, PatternCategory.Security,
            "Implement model validation to ensure data integrity and business rules.",
            "https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation"),
        
        // Security patterns
        ["authentication"] = (PatternType.Security, PatternCategory.Security,
            "Implement JWT or Azure AD authentication for API security.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-security"),
        ["authorization"] = (PatternType.Security, PatternCategory.Security,
            "Add role-based or policy-based authorization to protect resources.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction"),
        ["data-encryption"] = (PatternType.Security, PatternCategory.Security,
            "Encrypt sensitive data at rest and in transit.",
            "https://learn.microsoft.com/en-us/azure/architecture/framework/security/design-storage"),
        
        // API Design patterns
        ["pagination"] = (PatternType.ApiDesign, PatternCategory.Performance,
            "Implement pagination for large result sets to improve performance.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#support-partial-responses-for-large-binary-resources"),
        ["versioning"] = (PatternType.ApiDesign, PatternCategory.Operational,
            "Use API versioning to maintain backward compatibility.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#versioning-a-restful-web-api"),
        ["rate-limiting"] = (PatternType.ApiDesign, PatternCategory.Reliability,
            "Add rate limiting to protect APIs from abuse and ensure fair usage.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/throttling"),
        
        // Monitoring patterns
        ["health-checks"] = (PatternType.Monitoring, PatternCategory.Reliability,
            "Implement health check endpoints for monitoring and orchestration.",
            "https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks"),
        ["structured-logging"] = (PatternType.Monitoring, PatternCategory.Operational,
            "Use structured logging (Serilog/Application Insights) for better diagnostics.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring"),
        ["metrics"] = (PatternType.Monitoring, PatternCategory.Operational,
            "Implement metrics collection for performance monitoring.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring"),
        
        // Background Jobs patterns
        ["background-tasks"] = (PatternType.BackgroundJobs, PatternCategory.Performance,
            "Use IHostedService or Hangfire for background task processing.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services"),
        ["message-queue"] = (PatternType.BackgroundJobs, PatternCategory.Performance,
            "Implement message queues (Azure Service Bus/RabbitMQ) for async processing.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/queue-based-load-leveling"),
        
        // Configuration patterns
        ["configuration-management"] = (PatternType.Configuration, PatternCategory.Operational,
            "Use Azure App Configuration or Key Vault for centralized configuration.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/configuration"),
        ["feature-flags"] = (PatternType.Configuration, PatternCategory.Operational,
            "Implement feature flags for controlled rollouts and A/B testing.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/feature-flags")
    };

    public BestPracticeValidationService(
        IPatternIndexingService patternService,
        IGraphService graphService,
        ILogger<BestPracticeValidationService> logger)
    {
        _patternService = patternService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<BestPracticeValidationResponse> ValidateBestPracticesAsync(
        BestPracticeValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating best practices for context: {Context}", request.Context);

        var response = new BestPracticeValidationResponse
        {
            Context = request.Context
        };

        // Determine which practices to check
        var practicesToCheck = request.BestPractices?.Any() == true
            ? request.BestPractices.Where(p => BestPracticesCatalog.ContainsKey(p)).ToList()
            : BestPracticesCatalog.Keys.ToList();

        response.TotalPracticesChecked = practicesToCheck.Count;

        // Check each practice
        foreach (var practice in practicesToCheck)
        {
            var practiceInfo = BestPracticesCatalog[practice];
            var result = await ValidatePracticeAsync(
                practice,
                practiceInfo.Type,
                practiceInfo.Category,
                practiceInfo.Recommendation,
                practiceInfo.AzureUrl,
                request,
                cancellationToken);

            response.Results.Add(result);

            if (result.Implemented)
            {
                response.PracticesImplemented++;
            }
            else
            {
                response.PracticesMissing++;
            }
        }

        // Calculate overall score
        response.OverallScore = response.TotalPracticesChecked > 0
            ? (float)response.PracticesImplemented / response.TotalPracticesChecked
            : 0f;

        _logger.LogInformation(
            "Validation complete for {Context}: {Score:P0} ({Implemented}/{Total} practices implemented)",
            request.Context, response.OverallScore, response.PracticesImplemented, response.TotalPracticesChecked);

        return response;
    }

    private async Task<BestPracticeResult> ValidatePracticeAsync(
        string practiceName,
        PatternType patternType,
        PatternCategory category,
        string recommendation,
        string azureUrl,
        BestPracticeValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = new BestPracticeResult
        {
            Practice = practiceName,
            PatternType = patternType,
            Category = category,
            Recommendation = recommendation,
            AzureUrl = azureUrl
        };

        try
        {
            // Query Neo4j for patterns of this type
            var patterns = await _graphService.GetPatternsByTypeAsync(patternType, request.Context, cancellationToken);

            // Filter by practice name (fuzzy matching)
            var matchingPatterns = patterns
                .Where(p => p.Confidence >= request.MinimumConfidence)
                .Where(p => MatchesPractice(p, practiceName))
                .OrderByDescending(p => p.Confidence)
                .ToList();

            result.Count = matchingPatterns.Count;
            result.Implemented = matchingPatterns.Any();
            result.AverageConfidence = matchingPatterns.Any()
                ? matchingPatterns.Average(p => p.Confidence)
                : 0f;

            // Add examples if requested
            if (request.IncludeExamples && matchingPatterns.Any())
            {
                var examplePatterns = matchingPatterns
                    .Take(request.MaxExamplesPerPractice)
                    .ToList();

                foreach (var pattern in examplePatterns)
                {
                    result.Examples.Add(new PatternExample
                    {
                        FilePath = pattern.FilePath,
                        LineNumber = pattern.LineNumber,
                        Name = pattern.Name,
                        Implementation = pattern.Implementation,
                        CodeSnippet = TruncateCode(pattern.Content, 200),
                        Confidence = pattern.Confidence
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating practice: {Practice}", practiceName);
        }

        return result;
    }

    private bool MatchesPractice(CodePattern pattern, string practiceName)
    {
        // Map practice names to pattern properties
        var practiceKeywords = practiceName.ToLowerInvariant().Split('-', '_');
        var patternText = $"{pattern.Name} {pattern.BestPractice} {pattern.Implementation}".ToLowerInvariant();

        return practiceKeywords.Any(keyword => patternText.Contains(keyword));
    }

    private string TruncateCode(string code, int maxLength)
    {
        if (code.Length <= maxLength)
        {
            return code;
        }

        return code.Substring(0, maxLength - 3) + "...";
    }

    public Task<List<string>> GetAvailableBestPracticesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BestPracticesCatalog.Keys.ToList());
    }
}

