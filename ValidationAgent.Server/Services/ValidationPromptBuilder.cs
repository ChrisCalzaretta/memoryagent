using System.Text;
using AgentContracts.Requests;
using ValidationAgent.Server.Clients;

namespace ValidationAgent.Server.Services;

/// <summary>
/// Builds prompts for the validation LLM - LEARNS FROM LIGHTNING
/// </summary>
public class ValidationPromptBuilder : IValidationPromptBuilder
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<ValidationPromptBuilder> _logger;

    private const string DefaultSystemPrompt = @"You are an expert code reviewer. Your task is to review code for quality, security, and best practices.

VALIDATION RULES:
1. Check for null reference vulnerabilities
2. Check for proper error handling
3. Check for security issues (SQL injection, hardcoded secrets, etc.)
4. Check for proper async patterns
5. Check for proper resource disposal
6. Check for code maintainability
7. Check for proper naming conventions

SCORING:
- 10: Perfect, no issues
- 8-9: Good, minor suggestions only
- 6-7: Acceptable, needs some fixes
- 4-5: Poor, significant issues
- 0-3: Critical, major problems

Be strict but fair. Focus on real issues, not style preferences.";

    public ValidationPromptBuilder(IMemoryAgentClient memoryAgent, ILogger<ValidationPromptBuilder> logger)
    {
        _memoryAgent = memoryAgent;
        _logger = logger;
    }

    public async Task<string> BuildValidationPromptAsync(ValidateCodeRequest request, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        
        // ✅ LEARNING: Fetch system prompt from Lightning (not hardcoded!)
        var prompt = await _memoryAgent.GetPromptAsync("validation_agent_system", cancellationToken);
        var systemPrompt = prompt?.Content ?? DefaultSystemPrompt;
        
        _logger.LogDebug("Using prompt version {Version} for validation_agent_system", prompt?.Version ?? 0);
        
        sb.AppendLine(systemPrompt);
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(request.OriginalTask))
        {
            sb.AppendLine("=== ORIGINAL TASK ===");
            sb.AppendLine(request.OriginalTask);
            sb.AppendLine();
        }

        // ✅ LEARNING: Get validation rules from Lightning patterns
        var lightningRules = await _memoryAgent.GetValidationRulesAsync(
            request.Context ?? "memoryagent", cancellationToken);
        
        if (lightningRules.Any())
        {
            sb.AppendLine("=== VALIDATION RULES FROM LIGHTNING ===");
            foreach (var rule in lightningRules)
            {
                sb.AppendLine($"- [{rule.Severity.ToUpperInvariant()}] {rule.Name}: {rule.Description}");
                if (!string.IsNullOrEmpty(rule.FixSuggestion))
                {
                    sb.AppendLine($"  Fix: {rule.FixSuggestion}");
                }
            }
            sb.AppendLine();
            _logger.LogDebug("Added {Count} validation rules from Lightning", lightningRules.Count);
        }

        // Add request-provided rules
        if (request.Rules.Any())
        {
            sb.AppendLine("=== ADDITIONAL RULES TO APPLY ===");
            foreach (var rule in request.Rules)
            {
                sb.AppendLine($"- {rule}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=== FILES TO REVIEW ===");
        foreach (var file in request.Files)
        {
            sb.AppendLine($"--- {file.Path} ({(file.IsNew ? "NEW" : "MODIFIED")}) ---");
            sb.AppendLine(file.Content);
            sb.AppendLine();
        }

        sb.AppendLine("=== INSTRUCTIONS ===");
        sb.AppendLine("Review the code above and provide:");
        sb.AppendLine("1. A score from 0-10");
        sb.AppendLine("2. List of issues found (with severity, file, line if possible)");
        sb.AppendLine("3. Suggestions for improvement");
        sb.AppendLine("4. Overall summary");

        return sb.ToString();
    }
}
