using System.Text;

namespace CodingAgent.Server.Services;

/// <summary>
/// Generates comprehensive failure reports for failed code generation attempts
/// </summary>
public interface IFailureReportGenerator
{
    /// <summary>
    /// Generate a markdown failure report
    /// </summary>
    string GenerateReport(FailureReportContext context);
}

/// <summary>
/// Context for failure report generation
/// </summary>
public record FailureReportContext
{
    /// <summary>
    /// Name of the file that failed
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// Programming language
    /// </summary>
    public required string Language { get; init; }
    
    /// <summary>
    /// Original task description
    /// </summary>
    public string? TaskDescription { get; init; }
    
    /// <summary>
    /// List of generation attempts
    /// </summary>
    public List<AttemptRecord> Attempts { get; init; } = new();
    
    /// <summary>
    /// Root cause analysis
    /// </summary>
    public string? RootCause { get; init; }
    
    /// <summary>
    /// Recommended next steps
    /// </summary>
    public string[] RecommendedActions { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// What models struggled with
    /// </summary>
    public Dictionary<string, string[]> ModelStruggles { get; init; } = new();
}

/// <summary>
/// Record of a single generation attempt
/// </summary>
public record AttemptRecord
{
    public int AttemptNumber { get; init; }
    public required string Model { get; init; }
    public double Score { get; init; }
    public string[] Issues { get; init; } = Array.Empty<string>();
    public TimeSpan Duration { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Implementation of failure report generator
/// </summary>
public class FailureReportGenerator : IFailureReportGenerator
{
    private readonly ILogger<FailureReportGenerator> _logger;
    
    public FailureReportGenerator(ILogger<FailureReportGenerator> logger)
    {
        _logger = logger;
    }
    
    public string GenerateReport(FailureReportContext context)
    {
        _logger.LogInformation(
            "Generating failure report for: {File} ({Attempts} attempts)",
            context.FileName, context.Attempts.Count);
        
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine($"# Failure Report: {context.FileName}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Language:** {context.Language}  ");
        sb.AppendLine($"**Total Attempts:** {context.Attempts.Count}  ");
        
        if (context.Attempts.Count > 0)
        {
            var highestScore = context.Attempts.Max(a => a.Score);
            sb.AppendLine($"**Highest Score:** {highestScore:F1}/10  ");
        }
        
        sb.AppendLine($"**Status:** ❌ NEEDS HUMAN REVIEW");
        sb.AppendLine();
        
        // Original Task
        if (!string.IsNullOrEmpty(context.TaskDescription))
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Original Task");
            sb.AppendLine();
            sb.AppendLine($"> {context.TaskDescription}");
            sb.AppendLine();
        }
        
        // Attempt History
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Attempt History");
        sb.AppendLine();
        
        foreach (var attempt in context.Attempts.OrderBy(a => a.AttemptNumber))
        {
            var scoreEmoji = attempt.Score >= 8 ? "✅" : attempt.Score >= 6 ? "⚠️" : "❌";
            
            sb.AppendLine($"### Attempt {attempt.AttemptNumber}: {attempt.Model}");
            sb.AppendLine();
            sb.AppendLine($"- **Score:** {scoreEmoji} {attempt.Score:F1}/10");
            sb.AppendLine($"- **Duration:** {attempt.Duration.TotalSeconds:F1}s");
            sb.AppendLine($"- **Time:** {attempt.Timestamp:HH:mm:ss}");
            
            if (attempt.Issues.Length > 0)
            {
                sb.AppendLine("- **Issues:**");
                foreach (var issue in attempt.Issues.Take(5))
                {
                    sb.AppendLine($"  - {issue}");
                }
            }
            
            sb.AppendLine();
        }
        
        // Score Progression
        if (context.Attempts.Count > 1)
        {
            sb.AppendLine("### Score Progression");
            sb.AppendLine();
            sb.AppendLine("```");
            
            var maxScore = 10.0;
            var barWidth = 30;
            
            foreach (var attempt in context.Attempts.OrderBy(a => a.AttemptNumber))
            {
                var filledWidth = (int)(attempt.Score / maxScore * barWidth);
                var bar = new string('█', filledWidth) + new string('░', barWidth - filledWidth);
                sb.AppendLine($"Attempt {attempt.AttemptNumber,2}: [{bar}] {attempt.Score:F1}/10 ({attempt.Model})");
            }
            
            sb.AppendLine("```");
            sb.AppendLine();
        }
        
        // Root Cause Analysis
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Root Cause Analysis");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(context.RootCause))
        {
            sb.AppendLine("**Primary Issue:**");
            sb.AppendLine();
            sb.AppendLine(context.RootCause);
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("*No automated root cause analysis available.*");
            sb.AppendLine();
        }
        
        // What Models Struggled With
        if (context.ModelStruggles.Count > 0)
        {
            sb.AppendLine("### What Models Struggled With");
            sb.AppendLine();
            
            foreach (var (model, struggles) in context.ModelStruggles)
            {
                sb.AppendLine($"**{model}:**");
                foreach (var struggle in struggles)
                {
                    sb.AppendLine($"- {struggle}");
                }
                sb.AppendLine();
            }
        }
        
        // Common Issues
        var allIssues = context.Attempts
            .SelectMany(a => a.Issues)
            .GroupBy(i => i)
            .OrderByDescending(g => g.Count())
            .Take(5);
        
        if (allIssues.Any())
        {
            sb.AppendLine("### Most Common Issues");
            sb.AppendLine();
            
            foreach (var issue in allIssues)
            {
                sb.AppendLine($"- {issue.Key} (occurred {issue.Count()} times)");
            }
            
            sb.AppendLine();
        }
        
        // Recommendations
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Recommended Next Steps");
        sb.AppendLine();
        
        if (context.RecommendedActions.Length > 0)
        {
            for (int i = 0; i < context.RecommendedActions.Length; i++)
            {
                sb.AppendLine($"{i + 1}. {context.RecommendedActions[i]}");
            }
        }
        else
        {
            sb.AppendLine("1. Review the validation issues above");
            sb.AppendLine("2. Consider breaking the task into smaller pieces");
            sb.AppendLine("3. Provide more specific requirements or examples");
            sb.AppendLine("4. Implement manually based on the partial attempts");
        }
        
        sb.AppendLine();
        
        // Footer
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*This report was generated automatically by the Code Generation Agent.*");
        sb.AppendLine("*For questions or improvements, please refer to the documentation.*");
        
        return sb.ToString();
    }
}

