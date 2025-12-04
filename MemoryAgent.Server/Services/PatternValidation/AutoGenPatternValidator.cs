using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for AutoGen patterns (LEGACY - marked for migration)
/// </summary>
public class AutoGenPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.AutoGen };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 2, // Start with low score since it's legacy
            SecurityScore = 5
        };

        // All AutoGen patterns are legacy
        result.Issues.Add(new ValidationIssue
        {
            Severity = IssueSeverity.High,
            Category = IssueCategory.Maintainability,
            Message = "AutoGen pattern is deprecated - migrate to Agent Framework",
            ScoreImpact = 0, // Already penalized with low base score
            FixGuidance = "See migration path for upgrading to Agent Framework"
        });

        // Code execution is especially dangerous
        if (pattern.Name.Contains("CodeExecution"))
        {
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Security,
                Message = "Code execution without sandboxing - CRITICAL security risk",
                ScoreImpact = 5,
                SecurityReference = "CWE-94: Improper Control of Generation of Code",
                FixGuidance = "Implement Docker/container sandboxing or migrate to Agent Framework with MCP"
            });
            result.SecurityScore = 1;
        }

        result.Summary = $"AutoGen Pattern (LEGACY): {result.Grade} - Migration Recommended";
        result.Recommendations.Add("Migrate to Agent Framework for better reliability and security");

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        // AutoGen has migration paths (handled by dedicated migration service)
        return null;
    }
}

