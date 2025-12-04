using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for caching patterns
/// </summary>
public class CachingPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.Caching };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for expiration policy
        if (!Regex.IsMatch(pattern.Content, @"Expiration|TTL|TimeSpan|AbsoluteExpiration|SlidingExpiration", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Reliability,
                Message = "No cache expiration policy set - risk of stale data and memory leaks",
                ScoreImpact = 3,
                FixGuidance = "Add AbsoluteExpirationRelativeToNow or SlidingExpiration to cache options"
            });
            result.Score -= 3;
        }

        // Check for null handling
        if (!Regex.IsMatch(pattern.Content, @"!=\s*null|\?\.|if\s*\(\s*\w+\s*!=\s*null\)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.High,
                Category = IssueCategory.Correctness,
                Message = "Missing null check after data fetch - can cache null values",
                ScoreImpact = 2,
                FixGuidance = "Add null check: if (data != null) before caching"
            });
            result.Score -= 2;
        }

        // Check for concurrency protection
        if (!Regex.IsMatch(pattern.Content, @"lock|SemaphoreSlim|DistributedLock|Interlocked", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Reliability,
                Message = "No concurrency protection - race condition possible with multiple threads",
                ScoreImpact = 2,
                FixGuidance = "Use lock, SemaphoreSlim, or distributed lock for thread safety"
            });
            result.Score -= 2;
        }

        // Check for cache key prefix
        if (!Regex.IsMatch(pattern.Content, @"\$""[a-z]+:|[A-Z][a-z]+:", RegexOptions.None))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.BestPractice,
                Message = "Cache keys not prefixed - risk of key collisions",
                ScoreImpact = 1,
                FixGuidance = "Prefix cache keys with entity type: $\"user:{id}\""
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Caching Pattern Quality: {result.Grade} ({result.Score}/10) - {result.Issues.Count} issues found";

        // Generate recommendations
        foreach (var issue in result.Issues.Where(i => i.FixGuidance != null))
        {
            result.Recommendations.Add(issue.FixGuidance!);
        }

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        // Caching patterns don't have migration paths
        return null;
    }
}

