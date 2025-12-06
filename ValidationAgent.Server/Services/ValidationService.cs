using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;
using ValidationAgent.Server.Clients;

namespace ValidationAgent.Server.Services;

/// <summary>
/// Validates code quality using rules + LLM analysis - with smart model rotation
/// </summary>
public class ValidationService : IValidationService
{
    private readonly IValidationPromptBuilder _promptBuilder;
    private readonly IOllamaClient _ollamaClient;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<ValidationService> _logger;
    private readonly IConfiguration _config;
    
    // Model configuration
    private readonly string _validationModel;
    private readonly int _ollamaPort;

    public ValidationService(
        IValidationPromptBuilder promptBuilder,
        IOllamaClient ollamaClient,
        IMemoryAgentClient memoryAgent,
        ILogger<ValidationService> logger,
        IConfiguration config)
    {
        _promptBuilder = promptBuilder;
        _ollamaClient = ollamaClient;
        _memoryAgent = memoryAgent;
        _logger = logger;
        _config = config;
        
        // Load model configuration
        _validationModel = config.GetValue<string>("Gpu:ValidationModel") ?? "phi4:latest";
        _ollamaPort = config.GetValue<int>("Ollama:Port", 11434);
    }

    public async Task<ValidateCodeResponse> ValidateAsync(ValidateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating {FileCount} files with rules: {Rules}", 
            request.Files.Count, string.Join(", ", request.Rules));
        
        var response = new ValidateCodeResponse
        {
            Passed = true,
            Score = 10,
            Issues = new List<ValidationIssue>(),
            Suggestions = new List<string>()
        };

        // Phase 1: Rule-based validation (fast, deterministic)
        foreach (var file in request.Files)
        {
            var issues = await ValidateFileWithRulesAsync(file, request.Rules, cancellationToken);
            response.Issues.AddRange(issues);
        }

        // Phase 2: LLM validation (deep analysis, quality assessment)
        var llmIssues = await ValidateWithLlmAsync(request, cancellationToken);
        response.Issues.AddRange(llmIssues);

        // Calculate score based on all issues
        response.Score = CalculateScore(response.Issues);
        response.Passed = response.Score >= 8; // Require 8/10 to pass

        // Generate summary
        response.Summary = GenerateSummary(response);

        // Add suggestions
        response.Suggestions = GenerateSuggestions(response.Issues);

        _logger.LogInformation("Validation complete: Score={Score}/10, Passed={Passed}, Issues={IssueCount}",
            response.Score, response.Passed, response.Issues.Count);

        return response;
    }

    /// <summary>
    /// Validate code using LLM for deep analysis
    /// </summary>
    private async Task<List<ValidationIssue>> ValidateWithLlmAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        
        // Build the validation prompt
        var prompt = await _promptBuilder.BuildValidationPromptAsync(request, cancellationToken);
        
        var systemPrompt = @"You are an expert code reviewer. Analyze the provided code and identify issues.

OUTPUT FORMAT - Respond with JSON only:
{
    ""issues"": [
        {
            ""severity"": ""critical|high|warning|info"",
            ""file"": ""path/to/file.cs"",
            ""line"": 42,
            ""message"": ""Description of the issue"",
            ""suggestion"": ""How to fix it"",
            ""rule"": ""category_name""
        }
    ],
    ""summary"": ""Brief overall assessment""
}

RULES:
- critical: Security vulnerabilities, data loss risks, crashes
- high: Bugs, logic errors, missing error handling
- warning: Code smells, performance issues, maintainability
- info: Style issues, minor improvements

Be thorough but fair. Only report real issues.";

        try
        {
            _logger.LogInformation("Calling LLM validation with model {Model}", _validationModel);
            
            var response = await _ollamaClient.GenerateAsync(
                _validationModel,
                prompt,
                systemPrompt,
                _ollamaPort,
                cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("LLM validation failed: {Error}. Continuing with rule-based only.", response.Error);
                return issues;
            }

            // Parse LLM response
            var parsedIssues = ParseLlmResponse(response.Response);
            issues.AddRange(parsedIssues);
            
            _logger.LogInformation("LLM found {Count} additional issues in {Duration}ms",
                parsedIssues.Count, response.TotalDurationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM validation error. Continuing with rule-based only.");
        }

        return issues;
    }

    /// <summary>
    /// Parse LLM JSON response to extract issues
    /// </summary>
    private List<ValidationIssue> ParseLlmResponse(string response)
    {
        var issues = new List<ValidationIssue>();
        
        try
        {
            // Extract JSON from response (may have markdown formatting)
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}", RegexOptions.Singleline);
            if (!jsonMatch.Success)
            {
                _logger.LogWarning("No JSON found in LLM response");
                return issues;
            }
            
            var json = jsonMatch.Value;
            var parsed = JsonSerializer.Deserialize<LlmValidationResponse>(json, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (parsed?.Issues != null)
            {
                foreach (var issue in parsed.Issues)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = issue.Severity ?? "info",
                        File = issue.File,
                        Line = issue.Line,
                        Message = issue.Message ?? "Unknown issue",
                        Suggestion = issue.Suggestion,
                        Rule = $"llm_{issue.Rule ?? "analysis"}"
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM JSON response");
        }
        
        return issues;
    }

    private async Task<List<ValidationIssue>> ValidateFileWithRulesAsync(
        CodeFile file, 
        List<string> rules,
        CancellationToken cancellationToken)
    {
        var issues = new List<ValidationIssue>();
        var content = file.Content;
        var lines = content.Split('\n');

        await Task.Delay(10, cancellationToken); // Simulate processing

        // Best practices checks
        if (rules.Contains("best_practices"))
        {
            // Check for null checks
            if (content.Contains("public") && !content.Contains("null"))
            {
                if (content.Contains("string ") || content.Contains("object ") || content.Contains("?"))
                {
                    if (!content.Contains("ArgumentNullException") && !content.Contains("?? throw"))
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = "warning",
                            File = file.Path,
                            Message = "Consider adding null checks for nullable parameters",
                            Suggestion = "Use 'ArgumentNullException.ThrowIfNull()' or null-coalescing operators",
                            Rule = "best_practices"
                        });
                    }
                }
            }

            // Check for XML documentation
            if (content.Contains("public class") || content.Contains("public interface"))
            {
                if (!content.Contains("/// <summary>"))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "info",
                        File = file.Path,
                        Message = "Public types should have XML documentation",
                        Suggestion = "Add /// <summary> comments to public classes and methods",
                        Rule = "best_practices"
                    });
                }
            }

            // Check for async without CancellationToken
            if (content.Contains("async Task") && !content.Contains("CancellationToken"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    File = file.Path,
                    Message = "Async methods should accept CancellationToken",
                    Suggestion = "Add 'CancellationToken cancellationToken = default' parameter",
                    Rule = "best_practices"
                });
            }

            // Check for proper using statements
            if (content.Contains("IDisposable") && !content.Contains("using ") && !content.Contains("await using"))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    File = file.Path,
                    Message = "IDisposable resources should be properly disposed",
                    Suggestion = "Use 'using' or 'await using' statements",
                    Rule = "best_practices"
                });
            }
        }

        // Security checks
        if (rules.Contains("security"))
        {
            // Check for SQL injection vulnerabilities
            if (content.Contains("SELECT") && content.Contains("+ "))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "critical",
                    File = file.Path,
                    Message = "Potential SQL injection vulnerability",
                    Suggestion = "Use parameterized queries instead of string concatenation",
                    Rule = "security"
                });
            }

            // Check for hardcoded secrets
            if (content.Contains("password") && content.Contains("\"") && content.Contains("="))
            {
                var hasLiteralPassword = lines.Any(l => 
                    l.Contains("password", StringComparison.OrdinalIgnoreCase) && 
                    l.Contains("\"") && 
                    !l.TrimStart().StartsWith("//") &&
                    !l.Contains("Configuration") &&
                    !l.Contains("Options"));
                    
                if (hasLiteralPassword)
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "critical",
                        File = file.Path,
                        Message = "Potential hardcoded password detected",
                        Suggestion = "Use configuration or secrets management",
                        Rule = "security"
                    });
                }
            }
        }

        // Pattern checks
        if (rules.Contains("patterns"))
        {
            // Check for proper DI
            if (content.Contains("new ") && content.Contains("Service(") && !content.Contains("Test"))
            {
                if (!file.Path.Contains("Test") && !file.Path.Contains("Program.cs"))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "info",
                        File = file.Path,
                        Message = "Consider using dependency injection instead of direct instantiation",
                        Suggestion = "Inject dependencies through constructor",
                        Rule = "patterns"
                    });
                }
            }
        }

        return issues;
    }

    private int CalculateScore(List<ValidationIssue> issues)
    {
        var score = 10;

        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                "critical" => 3,
                "high" => 2,
                "warning" => 1,
                "info" => 0,
                _ => 0
            };
        }

        return Math.Max(0, Math.Min(10, score));
    }

    private string GenerateSummary(ValidateCodeResponse response)
    {
        if (response.Score == 10)
        {
            return "Code passes all validation checks. Excellent quality!";
        }
        else if (response.Score >= 8)
        {
            return $"Code quality is good (Score: {response.Score}/10). Minor improvements suggested.";
        }
        else if (response.Score >= 5)
        {
            return $"Code needs improvement (Score: {response.Score}/10). Please address the issues found.";
        }
        else
        {
            return $"Code has significant issues (Score: {response.Score}/10). Critical fixes required.";
        }
    }

    private List<string> GenerateSuggestions(List<ValidationIssue> issues)
    {
        var suggestions = new List<string>();
        var groupedIssues = issues.GroupBy(i => i.Rule);

        foreach (var group in groupedIssues)
        {
            if (group.Key == "best_practices" && group.Count() > 2)
            {
                suggestions.Add("Review C# best practices guidelines for common patterns");
            }
            if (group.Key == "security" && group.Any(i => i.Severity == "critical"))
            {
                suggestions.Add("CRITICAL: Address security vulnerabilities before proceeding");
            }
            if (group.Key?.StartsWith("llm_") == true)
            {
                suggestions.Add("LLM analysis identified additional improvements - review suggestions");
            }
        }

        if (!issues.Any())
        {
            suggestions.Add("Code looks great! Consider adding unit tests if not already present.");
        }

        return suggestions;
    }
}

/// <summary>
/// LLM response structure for validation
/// </summary>
internal class LlmValidationResponse
{
    public List<LlmValidationIssue>? Issues { get; set; }
    public string? Summary { get; set; }
}

internal class LlmValidationIssue
{
    public string? Severity { get; set; }
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Message { get; set; }
    public string? Suggestion { get; set; }
    public string? Rule { get; set; }
}
