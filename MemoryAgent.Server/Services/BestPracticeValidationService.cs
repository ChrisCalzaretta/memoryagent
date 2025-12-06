using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for validating Azure best practices in code
/// Uses BestPracticesCatalog for practice definitions
/// </summary>
public class BestPracticeValidationService : IBestPracticeValidationService
{
    private readonly IPatternIndexingService _patternService;
    private readonly IGraphService _graphService;
    private readonly ILogger<BestPracticeValidationService> _logger;

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
        var normalizedContext = request.Context?.ToLowerInvariant() ?? "default";
        _logger.LogInformation("Validating best practices for context: {Context}", normalizedContext);

        var response = new BestPracticeValidationResponse
        {
            Context = normalizedContext
        };

        // Determine which practices to check
        var practicesToCheck = request.BestPractices?.Any() == true
            ? request.BestPractices.Where(p => BestPracticesCatalog.Practices.ContainsKey(p)).ToList()
            : BestPracticesCatalog.Practices.Keys.ToList();

        response.TotalPracticesChecked = practicesToCheck.Count;

        // Check each practice
        foreach (var practice in practicesToCheck)
        {
            var practiceInfo = BestPracticesCatalog.Practices[practice];
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
            normalizedContext, response.OverallScore, response.PracticesImplemented, response.TotalPracticesChecked);

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
            return code;

        return code.Substring(0, maxLength - 3) + "...";
    }

    public Task<List<string>> GetAvailableBestPracticesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BestPracticesCatalog.Practices.Keys.ToList());
    }
}
