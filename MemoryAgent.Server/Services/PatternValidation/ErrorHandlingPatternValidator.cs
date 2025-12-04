using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for error handling patterns
/// </summary>
public class ErrorHandlingPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.ErrorHandling };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for logging
        if (!Regex.IsMatch(pattern.Content, @"Log|_logger", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.Maintainability,
                Message = "Error handling without logging - errors will be silent",
                ScoreImpact = 2
            });
            result.Score -= 2;
        }

        // Check for generic catch
        if (Regex.IsMatch(pattern.Content, @"catch\s*\(\s*Exception\s+\w+\s*\)", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(pattern.Content, @"throw;|throw\s+new", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Low,
                Category = IssueCategory.BestPractice,
                Message = "Catching all exceptions without rethrowing - can hide serious bugs",
                ScoreImpact = 1,
                FixGuidance = "Catch specific exceptions or rethrow after logging"
            });
            result.Score -= 1;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Error Handling Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

