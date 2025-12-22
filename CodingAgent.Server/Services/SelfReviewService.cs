using System.Text;
using System.Text.Json;
using AgentContracts.Models;
using AgentContracts.Responses;
using AgentContracts.Services;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Self-review service - LLM critiques its own generated code
/// Mimics Claude's internal review process before submission
/// </summary>
public interface ISelfReviewService
{
    Task<SelfReviewResult> ReviewCodeAsync(
        string generatedCode,
        List<FileChange> parsedFiles,
        string originalTask,
        string language,
        CancellationToken cancellationToken);
}

public class SelfReviewService : ISelfReviewService
{
    private readonly IOllamaClient _ollama;
    private readonly IPromptSeedService _promptSeed;
    private readonly ILogger<SelfReviewService> _logger;
    
    public SelfReviewService(
        IOllamaClient ollama,
        IPromptSeedService promptSeed,
        ILogger<SelfReviewService> logger)
    {
        _ollama = ollama;
        _promptSeed = promptSeed;
        _logger = logger;
    }
    
    public async Task<SelfReviewResult> ReviewCodeAsync(
        string generatedCode,
        List<FileChange> parsedFiles,
        string originalTask,
        string language,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔍 Starting self-review of {FileCount} files", parsedFiles.Count);
            
            var prompt = await BuildReviewPromptAsync(generatedCode, parsedFiles, originalTask, language, cancellationToken);
            
            // Use a strong reasoning model for review (Qwen is good at analysis)
            var response = await _ollama.GenerateAsync(
                model: "qwen2.5-coder:14b",
                prompt: prompt,
                cancellationToken: cancellationToken);
            
            var result = ParseReviewResponse(response.Response);
            
            _logger.LogInformation("✅ Self-review complete: {Status}, {IssueCount} issues found",
                result.Approved ? "APPROVED" : "NEEDS FIXES", result.Issues.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Self-review failed");
            
            // Fail-safe: if review crashes, assume code is OK
            return new SelfReviewResult
            {
                Approved = true,
                Confidence = 0.5,
                Issues = new List<ReviewIssue>(),
                Summary = "Review failed - proceeding with caution"
            };
        }
    }
    
    private async Task<string> BuildReviewPromptAsync(string code, List<FileChange> files, string task, string language, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        
        // 🌱 GET PROMPT FROM LIGHTNING
        var promptMetadata = await _promptSeed.GetBestPromptAsync("self_review_v1", cancellationToken);
        
        if (promptMetadata == null || string.IsNullOrEmpty(promptMetadata.Content))
        {
            _logger.LogCritical("🚨 CRITICAL: Self-review prompt not found in Lightning!");
            throw new InvalidOperationException("Self-review prompt must be stored in Lightning.");
        }
        
        _logger.LogInformation("📋 Using Lightning self-review prompt: {Id}", promptMetadata.Id);
        
        sb.AppendLine(promptMetadata.Content);
        sb.AppendLine();
        sb.AppendLine("## ORIGINAL TASK:");
        sb.AppendLine(task);
        sb.AppendLine();
        sb.AppendLine("## GENERATED CODE:");
        sb.AppendLine();
        
        // Show each file
        foreach (var file in files.Take(10)) // Limit to 10 files for token efficiency
        {
            sb.AppendLine($"### File: {file.Path}");
            sb.AppendLine("```" + language);
            sb.AppendLine(file.Content.Length > 2000 
                ? file.Content.Substring(0, 2000) + "\n... (truncated)" 
                : file.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }
        
        // Note: Review checklist and output format are in the Lightning prompt
        // No hardcoded instructions needed here!
        
        return sb.ToString();
    }
    
    private SelfReviewResult ParseReviewResponse(string response)
    {
        var result = new SelfReviewResult
        {
            RawResponse = response
        };
        
        var lines = response.Split('\n');
        var inIssues = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Parse APPROVED
            if (trimmed.StartsWith("APPROVED:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring("APPROVED:".Length).Trim();
                result.Approved = value.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
                                 value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
            }
            // Parse CONFIDENCE
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(value, out var confidence))
                {
                    result.Confidence = confidence;
                }
            }
            // Parse SUMMARY
            else if (trimmed.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
            {
                result.Summary = trimmed.Substring("SUMMARY:".Length).Trim();
            }
            // Parse ISSUES section
            else if (trimmed.Equals("ISSUES:", StringComparison.OrdinalIgnoreCase))
            {
                inIssues = true;
            }
            // Parse individual issues
            else if (inIssues && trimmed.StartsWith("-") || trimmed.StartsWith("•"))
            {
                var issue = ParseIssue(trimmed.TrimStart('-', '•').Trim());
                if (issue != null)
                {
                    result.Issues.Add(issue);
                }
            }
        }
        
        // Default values if parsing failed
        if (string.IsNullOrEmpty(result.Summary))
        {
            result.Summary = "Review completed";
        }
        
        if (result.Confidence == 0 && result.Approved)
        {
            result.Confidence = 0.7; // Default confidence if not specified
        }
        
        return result;
    }
    
    private ReviewIssue? ParseIssue(string issueText)
    {
        // Format: [SEVERITY] Description
        var match = System.Text.RegularExpressions.Regex.Match(issueText, @"\[(\w+)\]\s*(.+)");
        
        if (match.Success)
        {
            return new ReviewIssue
            {
                Severity = match.Groups[1].Value.ToUpperInvariant(),
                Description = match.Groups[2].Value.Trim()
            };
        }
        
        // Fallback: treat entire line as issue
        if (!string.IsNullOrWhiteSpace(issueText))
        {
            return new ReviewIssue
            {
                Severity = "MEDIUM",
                Description = issueText
            };
        }
        
        return null;
    }
}

public class SelfReviewResult
{
    public bool Approved { get; set; }
    public double Confidence { get; set; }
    public List<ReviewIssue> Issues { get; set; } = new();
    public string Summary { get; set; } = "";
    public string RawResponse { get; set; } = "";
    
    public bool HasCriticalIssues => Issues.Any(i => i.Severity == "CRITICAL");
    public bool HasHighIssues => Issues.Any(i => i.Severity == "HIGH");
}

public class ReviewIssue
{
    public string Severity { get; set; } = "MEDIUM";
    public string Description { get; set; } = "";
}

