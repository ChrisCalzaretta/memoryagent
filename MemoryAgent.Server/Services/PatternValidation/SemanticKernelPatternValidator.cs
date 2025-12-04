using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Semantic Kernel patterns
/// </summary>
public class SemanticKernelPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.SemanticKernel };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for [Description] attribute on functions
        if (pattern.Name.Contains("Plugin") && 
            !Regex.IsMatch(pattern.Content, @"\[Description\(", RegexOptions.IgnoreCase))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Medium,
                Category = IssueCategory.BestPractice,
                Message = "Missing [Description] attribute - LLM won't understand function purpose",
                ScoreImpact = 2,
                FixGuidance = "Add [Description] to help LLM understand when to call this function"
            });
            result.Score -= 2;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Semantic Kernel Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        // Semantic Kernel Planners have migration paths (handled by migration service)
        return null;
    }
}

