using System.Text;
using AgentContracts.Requests;

namespace CodingAgent.Server.Services;

/// <summary>
/// Formats attempt history for LLM consumption
/// Shows complete previous attempts with code, errors, and scores
/// </summary>
public interface IHistoryFormatterService
{
    string FormatHistoryForLLM(List<AttemptHistory>? history, int maxAttempts = 3);
}

public class HistoryFormatterService : IHistoryFormatterService
{
    private readonly ILogger<HistoryFormatterService> _logger;
    
    public HistoryFormatterService(ILogger<HistoryFormatterService> logger)
    {
        _logger = logger;
    }
    
    public string FormatHistoryForLLM(List<AttemptHistory>? history, int maxAttempts = 3)
    {
        if (history == null || !history.Any())
        {
            return "No previous attempts.";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("# PREVIOUS ATTEMPTS HISTORY");
        sb.AppendLine();
        sb.AppendLine("Learn from these previous attempts. See what worked and what didn't:");
        sb.AppendLine();
        
        // Show last N attempts (most recent first)
        var attemptsToShow = history.OrderByDescending(h => h.AttemptNumber).Take(maxAttempts).ToList();
        
        foreach (var attempt in attemptsToShow)
        {
            sb.AppendLine($"## ATTEMPT #{attempt.AttemptNumber} - {attempt.Model}");
            sb.AppendLine($"Score: {attempt.Score}/10");
            sb.AppendLine($"Timestamp: {attempt.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // Show generated files (abbreviated)
            if (attempt.GeneratedFiles != null && attempt.GeneratedFiles.Any())
            {
                sb.AppendLine($"### Generated {attempt.GeneratedFiles.Count} files:");
                sb.AppendLine();
                
                foreach (var file in attempt.GeneratedFiles.Take(5)) // Limit to 5 files for token efficiency
                {
                    sb.AppendLine($"#### {file.Path}");
                    sb.AppendLine("```");
                    
                    // Show first 50 lines or 2000 chars
                    var lines = file.Content.Split('\n');
                    var preview = string.Join("\n", lines.Take(50));
                    if (preview.Length > 2000)
                    {
                        preview = preview.Substring(0, 2000);
                    }
                    
                    sb.AppendLine(preview);
                    
                    if (lines.Length > 50 || file.Content.Length > 2000)
                    {
                        sb.AppendLine($"\n... (truncated, {lines.Length} total lines)");
                    }
                    
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
                
                if (attempt.GeneratedFiles.Count > 5)
                {
                    sb.AppendLine($"... and {attempt.GeneratedFiles.Count - 5} more files");
                    sb.AppendLine();
                }
            }
            
            // Show compilation errors
            if (!string.IsNullOrEmpty(attempt.CompilationOutput))
            {
                sb.AppendLine("### Compilation Errors:");
                sb.AppendLine("```");
                sb.AppendLine(attempt.CompilationOutput.Length > 1000 
                    ? attempt.CompilationOutput.Substring(0, 1000) + "\n... (truncated)"
                    : attempt.CompilationOutput);
                sb.AppendLine("```");
                sb.AppendLine();
            }
            
            // Show validation issues
            if (attempt.Issues != null && attempt.Issues.Any())
            {
                sb.AppendLine("### Validation Issues:");
                foreach (var issue in attempt.Issues.Take(10)) // Limit to 10 issues
                {
                    sb.AppendLine($"- [{issue.Severity}] {issue.Message}");
                    if (!string.IsNullOrEmpty(issue.Suggestion))
                    {
                        sb.AppendLine($"  Suggestion: {issue.Suggestion}");
                    }
                }
                
                if (attempt.Issues.Count > 10)
                {
                    sb.AppendLine($"... and {attempt.Issues.Count - 10} more issues");
                }
                sb.AppendLine();
            }
            
            // Show summary
            if (!string.IsNullOrEmpty(attempt.Summary))
            {
                sb.AppendLine($"### Summary: {attempt.Summary}");
                sb.AppendLine();
            }
            
            sb.AppendLine("---");
            sb.AppendLine();
        }
        
        // Add analysis prompt
        sb.AppendLine("## YOUR TASK:");
        sb.AppendLine("Analyze the above attempts and:");
        sb.AppendLine("1. Identify what went wrong (compilation errors, logic issues, missing components)");
        sb.AppendLine("2. Identify what worked well (keep those parts!)");
        sb.AppendLine("3. Generate improved code that fixes the issues");
        sb.AppendLine();
        
        return sb.ToString();
    }
}


