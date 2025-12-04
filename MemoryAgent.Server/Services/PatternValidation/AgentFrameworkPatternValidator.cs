using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Microsoft Agent Framework patterns
/// </summary>
public class AgentFrameworkPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.AgentFramework };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for timeout configuration
        if (!Regex.IsMatch(pattern.Content, @"Timeout|CancellationToken", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.High,
                Category = IssueCategory.Reliability,
                Message = "No timeout configured - agent calls can hang indefinitely",
                ScoreImpact = 2,
                FixGuidance = "Add timeout or pass CancellationToken"
            });
            result.Score -= 2;
        }

        // Check for retry policy
        if (!Regex.IsMatch(pattern.Content, @"Retry|Polly|Circuit", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Reliability,
                Message = "No retry policy - transient failures will immediately fail requests",
                ScoreImpact = 1,
                FixGuidance = "Add Polly retry policy for resilience"
            });
            result.Score -= 1;
        }

        // Check for input validation
        if (!Regex.IsMatch(pattern.Content, @"ArgumentNullException|Guard|Validate|if\s*\(.*==\s*null\)", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Security,
                Message = "No input validation - risk of injection attacks or crashes",
                ScoreImpact = 3,
                SecurityReference = "CWE-20: Improper Input Validation",
                FixGuidance = "Validate all user inputs before passing to agent"
            });
            result.Score -= 3;
        }

        // Check for telemetry
        if (!Regex.IsMatch(pattern.Content, @"Log|Telemetry|_logger", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.Maintainability,
                Message = "No telemetry - hard to monitor agent behavior in production",
                ScoreImpact = 1,
                FixGuidance = "Add logging and telemetry for agent calls"
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Agent Framework Pattern Quality: {result.Grade} ({result.Score}/10)";

        foreach (var issue in result.Issues.Where(i => i.FixGuidance != null))
        {
            result.Recommendations.Add(issue.FixGuidance!);
        }

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

