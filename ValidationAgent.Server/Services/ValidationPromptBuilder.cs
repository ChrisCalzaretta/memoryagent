using System.Text;
using AgentContracts.Requests;
using ValidationAgent.Server.Clients;

namespace ValidationAgent.Server.Services;

/// <summary>
/// Builds prompts for the validation LLM - LEARNS FROM LIGHTNING
/// Supports multi-language validation!
/// </summary>
public class ValidationPromptBuilder : IValidationPromptBuilder
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<ValidationPromptBuilder> _logger;

    // NO FALLBACK - All prompts MUST come from Lightning
    // If Lightning is unavailable, the system will throw an error
    private const string DefaultSystemPrompt = ""; // Not used - will throw if Lightning unavailable

    // NO FALLBACK - All language validation prompts MUST come from Lightning
    // These legacy defaults are kept for reference only but are NOT used
    [Obsolete("Not used - all validation prompts must come from Lightning")]
    private static readonly Dictionary<string, string> LanguageValidationRules = new()
    {
        ["python"] = @"PYTHON VALIDATION RULES:
- Check for type hints on function parameters and returns
- Check for docstrings on functions/classes
- Check for proper exception handling (try/except)
- Check for PEP 8 compliance (naming, spacing)
- Check for proper use of context managers
- Check for potential security issues (eval, exec, pickle)
- Check for proper async/await patterns if using asyncio",

        ["typescript"] = @"TYPESCRIPT VALIDATION RULES:
- Check for proper type annotations (no 'any' abuse)
- Check for strict null checks
- Check for proper async/await patterns
- Check for proper error handling (try/catch)
- Check for interface usage for object shapes
- Check for potential XSS vulnerabilities
- Check for proper module exports",

        ["javascript"] = @"JAVASCRIPT VALIDATION RULES:
- Check for JSDoc documentation
- Check for const/let usage (no var)
- Check for proper async/await patterns
- Check for proper error handling (try/catch)
- Check for potential security issues (eval, innerHTML)
- Check for proper null/undefined handling",

        ["csharp"] = @"C# VALIDATION RULES:
- Check for XML documentation on public members
- Check for nullable reference type handling
- Check for proper async/await with CancellationToken
- Check for proper exception handling
- Check for IDisposable pattern compliance
- Check for proper dependency injection usage
- Check for potential security issues (SQL injection, XSS)",

        ["go"] = @"GO VALIDATION RULES:
- Check for proper error handling (all errors checked)
- Check for proper defer usage for cleanup
- Check for proper context.Context usage
- Check for exported names capitalization
- Check for proper interface usage
- Check for potential goroutine leaks",

        ["rust"] = @"RUST VALIDATION RULES:
- Check for proper Result/Option handling
- Check for proper error propagation
- Check for ownership/borrowing correctness
- Check for proper lifetime annotations
- Check for unsafe code justification
- Check for proper derive macro usage",

        ["java"] = @"JAVA VALIDATION RULES:
- Check for Javadoc on public methods
- Check for proper exception handling
- Check for try-with-resources usage
- Check for Optional usage for nullables
- Check for proper access modifiers
- Check for potential security issues",

        ["ruby"] = @"RUBY VALIDATION RULES:
- Check for YARD documentation
- Check for proper exception handling (begin/rescue)
- Check for proper method visibility
- Check for Ruby idioms usage
- Check for potential security issues",

        ["php"] = @"PHP VALIDATION RULES:
- Check for PHPDoc comments
- Check for type declarations
- Check for proper exception handling
- Check for SQL injection vulnerabilities
- Check for XSS vulnerabilities
- Check for PSR-12 compliance",

        ["swift"] = @"SWIFT VALIDATION RULES:
- Check for documentation comments
- Check for proper optional handling
- Check for proper error handling (do/try/catch)
- Check for proper access control
- Check for memory management (weak/unowned)",

        ["kotlin"] = @"KOTLIN VALIDATION RULES:
- Check for KDoc documentation
- Check for proper null safety usage
- Check for proper coroutine patterns
- Check for proper sealed class usage
- Check for proper extension function usage",

        ["dart"] = @"DART/FLUTTER VALIDATION RULES:
- Check for dartdoc comments
- Check for proper null safety
- Check for proper async/await patterns
- Check for proper widget composition (if Flutter)
- Check for const usage where possible",

        ["sql"] = @"SQL VALIDATION RULES:
- Check for SQL injection vulnerabilities
- Check for proper parameterization
- Check for proper indexing considerations
- Check for proper transaction handling
- Check for proper error handling",

        ["shell"] = @"SHELL/BASH VALIDATION RULES:
- Check for shebang line
- Check for set -euo pipefail
- Check for proper quoting
- Check for proper error handling (trap)
- Check for shellcheck compliance
- Check for potential command injection"
    };

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
        
        // 🌐 LANGUAGE-SPECIFIC VALIDATION FROM LIGHTNING
        var language = DetectLanguage(request);
        if (!string.IsNullOrEmpty(language))
        {
            var languagePromptName = $"validation_{language}";
            var languagePrompt = await _memoryAgent.GetPromptAsync(languagePromptName, cancellationToken);
            
            if (languagePrompt != null)
            {
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} (from Lightning v{languagePrompt.Version}) ===");
                sb.AppendLine(languagePrompt.Content);
                sb.AppendLine();
                _logger.LogInformation("✨ Using Lightning validation prompt for {Language} (v{Version})", language, languagePrompt.Version);
            }
            else
            {
                // NO FALLBACK - language validation prompt MUST exist in Lightning
                _logger.LogError("❌ CRITICAL: Language validation prompt 'validation_{Language}' not found in Lightning. Ensure prompts are seeded.", language);
                throw new InvalidOperationException($"Required language validation prompt 'validation_{language}' not found in Lightning. Run PromptSeedService or add the validation prompt.");
            }
        }
        
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
        sb.AppendLine();
        sb.AppendLine("⭐ FIRST: TASK COMPLIANCE CHECK");
        sb.AppendLine("- Go through EACH requirement in the ORIGINAL TASK");
        sb.AppendLine("- Verify it is implemented correctly");
        sb.AppendLine("- List any MISSING or BROKEN requirements as CRITICAL issues");
        sb.AppendLine();
        sb.AppendLine("⭐ SECOND: FUNCTIONALITY CHECK");
        sb.AppendLine("- Will this code actually run without errors?");
        sb.AppendLine("- Is the logic correct? (e.g., game rules, calculations)");
        sb.AppendLine("- Are there runtime bugs?");
        sb.AppendLine();
        sb.AppendLine("THEN: Code quality, security, best practices");
        sb.AppendLine();
        sb.AppendLine("OUTPUT:");
        sb.AppendLine("1. A score from 0-10 (MAX 5 if requirements missing, MAX 3 if core broken)");
        sb.AppendLine("2. List of issues found (with severity: critical/high/medium/low)");
        sb.AppendLine("3. For each missing requirement: severity=critical");
        sb.AppendLine("4. Suggestions for improvement");
        sb.AppendLine("5. Overall summary");

        return sb.ToString();
    }

    /// <summary>
    /// Detect language from request or file extensions
    /// </summary>
    private static string DetectLanguage(ValidateCodeRequest request)
    {
        // If explicitly set, use it
        if (!string.IsNullOrEmpty(request.Language))
        {
            return request.Language.ToLowerInvariant();
        }

        // Auto-detect from file extensions
        var extensionCounts = new Dictionary<string, int>();
        foreach (var file in request.Files)
        {
            var ext = System.IO.Path.GetExtension(file.Path)?.ToLowerInvariant();
            var lang = ext switch
            {
                ".py" => "python",
                ".ts" => "typescript",
                ".tsx" => "typescript",
                ".js" => "javascript",
                ".jsx" => "javascript",
                ".cs" => "csharp",
                ".go" => "go",
                ".rs" => "rust",
                ".java" => "java",
                ".rb" => "ruby",
                ".php" => "php",
                ".swift" => "swift",
                ".kt" => "kotlin",
                ".kts" => "kotlin",
                ".dart" => "dart",
                ".sql" => "sql",
                ".sh" => "shell",
                ".bash" => "shell",
                ".ps1" => "shell",
                _ => null
            };

            if (lang != null)
            {
                extensionCounts[lang] = extensionCounts.GetValueOrDefault(lang, 0) + 1;
            }
        }

        // Return the most common language, or default to csharp
        return extensionCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "csharp";
    }
}
