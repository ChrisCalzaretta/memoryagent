using System.Text;
using AgentContracts.Requests;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Builds prompts for the coding LLM - LEARNS FROM LIGHTNING
/// </summary>
public class PromptBuilder : IPromptBuilder
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<PromptBuilder> _logger;

    // Default fallback if Lightning is unavailable
    private const string DefaultSystemPrompt = @"You are an expert coding agent. Your task is to write production-quality code.

🔴 CRITICAL - SEARCH BEFORE WRITE:
1. ALWAYS check if the functionality already exists
2. NEVER recreate services, methods, or patterns that exist
3. EXTEND existing code instead of creating duplicates
4. REUSE existing interfaces and implementations
5. If similar code exists, INTEGRATE with it, don't duplicate

STRICT RULES:
1. ONLY create/modify files directly necessary for the requested task
2. Do NOT ""improve"" or refactor unrelated code
3. Do NOT add features that weren't requested
4. You MAY add package references if needed for your implementation
5. You MUST include proper error handling and null checks
6. You MUST include XML documentation on public methods
7. Follow naming conventions and best practices for the language

REQUIREMENTS:
- Always check for null before accessing properties
- Use async/await for I/O operations
- Prefer dependency injection for services
- Include CancellationToken support for async methods
- Log important operations";

    public PromptBuilder(IMemoryAgentClient memoryAgent, ILogger<PromptBuilder> logger)
    {
        _memoryAgent = memoryAgent;
        _logger = logger;
    }

    public async Task<string> BuildGeneratePromptAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        
        // ✅ LEARNING: Fetch system prompt from Lightning (not hardcoded!)
        var prompt = await _memoryAgent.GetPromptAsync("coding_agent_system", cancellationToken);
        var systemPrompt = prompt?.Content ?? DefaultSystemPrompt;
        
        _logger.LogDebug("Using prompt version {Version} for coding_agent_system", prompt?.Version ?? 0);
        
        sb.AppendLine(systemPrompt);
        sb.AppendLine();
        
        // 🔍 SEARCH BEFORE WRITE: Find existing code to avoid duplication
        var context = "memoryagent"; // Default context
        var existingCode = await _memoryAgent.SearchExistingCodeAsync(
            request.Task, context, request.WorkspacePath, cancellationToken);
        
        if (existingCode.HasReusableCode)
        {
            _logger.LogInformation("🔍 Found existing code to reuse: {Services} services, {Methods} methods",
                existingCode.ExistingServices.Count, existingCode.ExistingMethods.Count);
            
            sb.AppendLine(existingCode.GetPromptSummary());
            sb.AppendLine("=== ⚠️ IMPORTANT: REUSE EXISTING CODE ===");
            sb.AppendLine("DO NOT recreate any of the above services or methods.");
            sb.AppendLine("EXTEND or INTEGRATE with existing code instead.");
            sb.AppendLine("Only create NEW code for functionality that doesn't exist.");
            sb.AppendLine();
        }
        
        sb.AppendLine("=== TASK ===");
        sb.AppendLine(request.Task);
        sb.AppendLine();

        // ✅ LEARNING: Add similar past solutions from Lightning Q&A memory
        // Note: 'context' variable already defined above for SearchExistingCodeAsync
        var similarSolutions = await _memoryAgent.FindSimilarSolutionsAsync(
            request.Task, context, cancellationToken);
        
        if (similarSolutions.Any())
        {
            sb.AppendLine("=== SIMILAR PAST SOLUTIONS (learn from these) ===");
            foreach (var solution in similarSolutions.Take(3))
            {
                sb.AppendLine($"Q: {solution.Question}");
                sb.AppendLine($"A: {solution.Answer}");
                sb.AppendLine($"  Similarity: {solution.Similarity:P0}");
                sb.AppendLine();
            }
            _logger.LogDebug("Added {SolutionCount} similar solutions from Lightning", similarSolutions.Count);
        }

        // ✅ LEARNING: Add patterns from Lightning
        var lightningPatterns = await _memoryAgent.GetPatternsAsync(
            request.Task, context, cancellationToken);
        
        if (lightningPatterns.Any())
        {
            sb.AppendLine("=== PATTERNS TO APPLY (from Lightning) ===");
            foreach (var pattern in lightningPatterns.Take(3))
            {
                sb.AppendLine($"- {pattern.Name}: {pattern.Description}");
                if (!string.IsNullOrEmpty(pattern.BestPractice))
                {
                    sb.AppendLine($"  Best practice: {pattern.BestPractice}");
                }
                if (!string.IsNullOrEmpty(pattern.CodeExample))
                {
                    sb.AppendLine($"  Example: {pattern.CodeExample}");
                }
            }
            sb.AppendLine();
            _logger.LogDebug("Added {PatternCount} patterns from Lightning", lightningPatterns.Count);
        }

        // Add context from request if available
        if (request.Context != null)
        {
            if (request.Context.SimilarSolutions.Any())
            {
                sb.AppendLine("=== ADDITIONAL CONTEXT ===");
                foreach (var solution in request.Context.SimilarSolutions.Take(2))
                {
                    sb.AppendLine($"Q: {solution.Question}");
                    sb.AppendLine($"A: {solution.Answer}");
                    sb.AppendLine();
                }
            }

            if (request.Context.Patterns.Any())
            {
                foreach (var pattern in request.Context.Patterns.Take(2))
                {
                    sb.AppendLine($"- {pattern.Name}: {pattern.Description}");
                }
                sb.AppendLine();
            }

            if (request.Context.RelatedFiles.Any())
            {
                sb.AppendLine("=== RELATED FILES (may need to reference) ===");
                foreach (var file in request.Context.RelatedFiles.Take(5))
                {
                    sb.AppendLine($"- {file}");
                }
                sb.AppendLine();
            }
        }

        if (request.TargetFiles?.Any() == true)
        {
            sb.AppendLine("=== TARGET FILES (focus on these) ===");
            foreach (var file in request.TargetFiles)
            {
                sb.AppendLine($"- {file}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=== INSTRUCTIONS ===");
        sb.AppendLine("Generate the code to complete this task. Return ONLY the code files needed.");

        return sb.ToString();
    }

    public async Task<string> BuildFixPromptAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        
        // ✅ LEARNING: Fetch fix prompt from Lightning
        var prompt = await _memoryAgent.GetPromptAsync("coding_agent_fix", cancellationToken);
        var systemPrompt = prompt?.Content ?? DefaultSystemPrompt;
        
        _logger.LogDebug("Using prompt version {Version} for coding_agent_fix", prompt?.Version ?? 0);
        
        sb.AppendLine(systemPrompt);
        sb.AppendLine();
        sb.AppendLine("=== TASK ===");
        sb.AppendLine(request.Task);
        sb.AppendLine();

        if (request.PreviousFeedback != null)
        {
            sb.AppendLine("=== VALIDATION FEEDBACK (YOU MUST FIX THESE) ===");
            sb.AppendLine($"Score: {request.PreviousFeedback.Score}/10");
            sb.AppendLine();
            
            foreach (var issue in request.PreviousFeedback.Issues)
            {
                sb.AppendLine($"[{issue.Severity.ToUpperInvariant()}] {issue.Message}");
                if (!string.IsNullOrEmpty(issue.File))
                {
                    sb.AppendLine($"  File: {issue.File}, Line: {issue.Line}");
                }
                if (!string.IsNullOrEmpty(issue.Suggestion))
                {
                    sb.AppendLine($"  Suggestion: {issue.Suggestion}");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(request.PreviousFeedback.Summary))
            {
                sb.AppendLine($"Summary: {request.PreviousFeedback.Summary}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== INSTRUCTIONS ===");
        sb.AppendLine("Fix ALL the issues listed above. Return the corrected code files.");

        return sb.ToString();
    }
}
