using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for resilience patterns (Polly retry, circuit breaker, etc.)
/// </summary>
public class ResiliencePatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.Resilience };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for exponential backoff
        if (Regex.IsMatch(pattern.Content, @"RetryAsync|WaitAndRetry", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(pattern.Content, @"Math\.Pow|exponential|backoff", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.BestPractice,
                Message = "Retry policy without exponential backoff - can overwhelm failing service",
                ScoreImpact = 2,
                FixGuidance = "Use exponential backoff: TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))"
            });
            result.Score -= 2;
        }

        // Check for circuit breaker
        if (!Regex.IsMatch(pattern.Content, @"CircuitBreaker|CircuitBreakerAsync", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.BestPractice,
                Message = "No circuit breaker - system won't fail fast during outages",
                ScoreImpact = 1,
                FixGuidance = "Consider adding circuit breaker for faster failure detection"
            });
            result.Score -= 1;
        }

        // Check for logging
        if (!Regex.IsMatch(pattern.Content, @"Log|_logger|ILogger", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.Maintainability,
                Message = "No logging for retry attempts - hard to diagnose issues",
                ScoreImpact = 1,
                FixGuidance = "Add logging in onRetry callback"
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Resilience Pattern Quality: {result.Grade} ({result.Score}/10)";

        foreach (var issue in result.Issues.Where(i => i.FixGuidance != null))
        {
            result.Recommendations.Add(issue.FixGuidance!);
        }

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        // Resilience patterns don't have migration paths
        return null;
    }
}

