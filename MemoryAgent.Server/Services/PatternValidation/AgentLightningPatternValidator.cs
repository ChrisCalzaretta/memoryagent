using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Agent Lightning patterns (RL training, reward signals)
/// </summary>
public class AgentLightningPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.AgentLightning };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10
        };

        // Check for reward signals if RL training
        if (pattern.Name.Contains("RLTraining"))
        {
            result.MissingPatterns.Add("RewardSignals");
            result.Issues.Add(new ValidationIssue
            {
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.Correctness,
                Message = "RL training requires reward signals - check if RewardSignals pattern exists",
                ScoreImpact = 5,
                FixGuidance = "Implement reward signal calculation before training"
            });
            result.Score -= 5;
        }

        result.Score = Math.Max(0, result.Score);
        result.Summary = $"Agent Lightning Pattern Quality: {result.Grade} ({result.Score}/10)";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

