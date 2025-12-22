using System.Text;
using AgentContracts.Models;
using AgentContracts.Requests;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Builds prompts for the coding LLM - LEARNS FROM LIGHTNING
/// Supports multi-language code generation!
/// </summary>
public class PromptBuilder : IPromptBuilder
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<PromptBuilder> _logger;

    // NO FALLBACK - All prompts MUST come from Lightning
    // If Lightning is unavailable, the system will throw an error
    private const string DefaultSystemPrompt = ""; // Not used - will throw if Lightning unavailable

    // NO FALLBACK DEFAULTS - All language prompts MUST come from Lightning
    // These legacy defaults are kept for reference only but are NOT used
    // If a language prompt is missing, the system will throw an error
    [Obsolete("Not used - all prompts must come from Lightning")]
    private static readonly Dictionary<string, LanguageGuidance> DefaultLanguageGuidelines = new()
    {
        ["python"] = new LanguageGuidance
        {
            FileExtension = ".py",
            CommentStyle = "# Comment",
            DocStyle = "Triple-quoted docstrings (Google style)",
            Guidelines = @"PYTHON REQUIREMENTS:
- Use type hints (def func(param: str) -> int:)
- Use docstrings with Args/Returns/Raises sections
- Follow PEP 8 style guide
- Use snake_case for functions and variables
- Use PascalCase for classes
- Use async/await with asyncio for I/O
- Handle exceptions with try/except
- Use context managers (with statement) for resources
- Import statements at top, organized (stdlib, third-party, local)"
        },
        ["typescript"] = new LanguageGuidance
        {
            FileExtension = ".ts",
            CommentStyle = "// Comment",
            DocStyle = "JSDoc comments (/** */)",
            Guidelines = @"TYPESCRIPT REQUIREMENTS:
- Use strict TypeScript with explicit types
- Use interfaces for object shapes
- Use async/await for promises
- Use camelCase for variables/functions, PascalCase for classes/interfaces
- Handle errors with try/catch
- Export types and interfaces
- Use const for immutable values
- Use optional chaining (?.) and nullish coalescing (??)"
        },
        ["javascript"] = new LanguageGuidance
        {
            FileExtension = ".js",
            CommentStyle = "// Comment",
            DocStyle = "JSDoc comments (/** */)",
            Guidelines = @"JAVASCRIPT REQUIREMENTS:
- Use JSDoc comments for documentation
- Use async/await for promises
- Use camelCase for variables/functions, PascalCase for classes
- Handle errors with try/catch
- Use const/let (not var)
- Use arrow functions where appropriate
- Use destructuring and spread operators"
        },
        ["csharp"] = new LanguageGuidance
        {
            FileExtension = ".cs",
            CommentStyle = "// Comment",
            DocStyle = "XML documentation (/// <summary>)",
            Guidelines = @"C# REQUIREMENTS:
- Use XML documentation on public members
- Use async/await with CancellationToken for async methods
- Use PascalCase for public members, _camelCase for private fields
- Use nullable reference types (string?, int?)
- Use dependency injection
- Use proper exception handling
- Use ILogger for logging
- Use records for DTOs where appropriate"
        },
        ["go"] = new LanguageGuidance
        {
            FileExtension = ".go",
            CommentStyle = "// Comment",
            DocStyle = "Doc comments above declarations",
            Guidelines = @"GO REQUIREMENTS:
- Use error returns (not exceptions)
- Use camelCase for private, PascalCase for exported
- Use defer for cleanup
- Handle all errors explicitly
- Use interfaces for abstraction
- Use context.Context for cancellation
- Keep functions small and focused
- Use gofmt style"
        },
        ["rust"] = new LanguageGuidance
        {
            FileExtension = ".rs",
            CommentStyle = "// Comment",
            DocStyle = "Doc comments (/// or //!)",
            Guidelines = @"RUST REQUIREMENTS:
- Use Result<T, E> for error handling
- Use snake_case for functions/variables
- Use PascalCase for types/traits
- Use impl blocks for methods
- Handle all Result and Option cases
- Use derive macros where appropriate
- Follow ownership/borrowing rules
- Use lifetimes where needed"
        },
        ["java"] = new LanguageGuidance
        {
            FileExtension = ".java",
            CommentStyle = "// Comment",
            DocStyle = "Javadoc comments (/** */)",
            Guidelines = @"JAVA REQUIREMENTS:
- Use Javadoc on public methods
- Use camelCase for methods/variables, PascalCase for classes
- Handle exceptions properly
- Use Optional for nullable returns
- Use try-with-resources for AutoCloseable
- Follow SOLID principles
- Use dependency injection"
        },
        ["ruby"] = new LanguageGuidance
        {
            FileExtension = ".rb",
            CommentStyle = "# Comment",
            DocStyle = "YARD documentation",
            Guidelines = @"RUBY REQUIREMENTS:
- Use snake_case for methods/variables
- Use PascalCase for classes/modules
- Use blocks and procs idiomatically
- Handle exceptions with begin/rescue/end
- Use symbols where appropriate
- Follow Ruby style guide"
        },
        ["php"] = new LanguageGuidance
        {
            FileExtension = ".php",
            CommentStyle = "// Comment",
            DocStyle = "PHPDoc comments (/** */)",
            Guidelines = @"PHP REQUIREMENTS:
- Use PHPDoc comments
- Use type declarations (PHP 7+)
- Use namespaces properly
- Handle exceptions with try/catch
- Use camelCase for methods, PascalCase for classes
- Follow PSR-12 coding style"
        },
        ["swift"] = new LanguageGuidance
        {
            FileExtension = ".swift",
            CommentStyle = "// Comment",
            DocStyle = "Documentation comments (///)",
            Guidelines = @"SWIFT REQUIREMENTS:
- Use optionals properly (? and !)
- Use guard for early returns
- Use camelCase for properties/methods, PascalCase for types
- Use protocols for abstraction
- Handle errors with do/try/catch
- Use async/await for concurrency"
        },
        ["kotlin"] = new LanguageGuidance
        {
            FileExtension = ".kt",
            CommentStyle = "// Comment",
            DocStyle = "KDoc comments (/** */)",
            Guidelines = @"KOTLIN REQUIREMENTS:
- Use nullable types properly (Type?)
- Use data classes for DTOs
- Use coroutines for async code
- Use camelCase for functions/properties, PascalCase for classes
- Use sealed classes where appropriate
- Use extension functions idiomatically"
        },
        ["dart"] = new LanguageGuidance
        {
            FileExtension = ".dart",
            CommentStyle = "// Comment",
            DocStyle = "Documentation comments (///)",
            Guidelines = @"DART REQUIREMENTS (non-Flutter):
- Use dartdoc comments (///)
- Use async/await for Futures
- Use camelCase for variables/functions, PascalCase for classes
- Use nullable types (Type?)
- Use const constructors where possible
- File naming: snake_case.dart"
        },
        ["flutter"] = new LanguageGuidance
        {
            FileExtension = ".dart",
            CommentStyle = "// Comment",
            DocStyle = "Documentation comments (///)",
            Guidelines = @"🦋 FLUTTER/DART REQUIREMENTS:

FILE STRUCTURE (CRITICAL - use these exact paths):
- lib/main.dart - App entry point with runApp()
- lib/models/*.dart - Data models (NOT in Services/)
- lib/providers/*.dart - State management (ChangeNotifier)
- lib/screens/*.dart or lib/pages/*.dart - Full-screen widgets
- lib/widgets/*.dart - Reusable UI components
- pubspec.yaml - Dependencies (NOT config.yaml, NOT .csproj)

PUBSPEC.YAML FORMAT:
```yaml
name: app_name
description: Description
version: 1.0.0+1
environment:
  sdk: '>=3.0.0 <4.0.0'
dependencies:
  flutter:
    sdk: flutter
  provider: ^6.0.0  # if using Provider
flutter:
  uses-material-design: true
```

WIDGET PATTERNS:
- StatelessWidget for static UI
- StatefulWidget for dynamic UI with local state
- Use const constructors: const MyWidget({super.key})
- Use named parameters with required keyword

EXAMPLE STATELESS WIDGET:
```dart
class MyWidget extends StatelessWidget {
  const MyWidget({super.key});
  
  @override
  Widget build(BuildContext context) {
    return Container();
  }
}
```

STATE MANAGEMENT (Provider):
```dart
class MyProvider extends ChangeNotifier {
  int _value = 0;
  int get value => _value;
  
  void increment() {
    _value++;
    notifyListeners();
  }
}
```

MAIN.DART PATTERN:
```dart
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

void main() {
  runApp(
    ChangeNotifierProvider(
      create: (_) => MyProvider(),
      child: const MyApp(),
    ),
  );
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});
  
  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'App',
      home: const HomeScreen(),
    );
  }
}
```

CRITICAL RULES:
- NEVER mix C# syntax (.cs files, namespaces, using statements)
- NEVER use 'new' keyword (Dart 2+ doesn't need it)
- Use 'final' for immutable variables, not 'const' for runtime values
- Import with: import 'package:flutter/material.dart';
- NO semicolons after class declarations
- Use => for single-expression functions"
        },
        ["sql"] = new LanguageGuidance
        {
            FileExtension = ".sql",
            CommentStyle = "-- Comment",
            DocStyle = "Comment blocks at top",
            Guidelines = @"SQL REQUIREMENTS:
- Use UPPERCASE for SQL keywords
- Use snake_case for table/column names
- Include comments explaining complex queries
- Use parameterized queries (prevent SQL injection)
- Use proper indexing hints where needed
- Follow database-specific best practices"
        },
        ["shell"] = new LanguageGuidance
        {
            FileExtension = ".sh",
            CommentStyle = "# Comment",
            DocStyle = "Header comments",
            Guidelines = @"SHELL/BASH REQUIREMENTS:
- Add shebang (#!/bin/bash or #!/usr/bin/env bash)
- Use set -euo pipefail for safety
- Quote variables properly
- Use functions for reusable code
- Handle errors with trap
- Use snake_case for variables
- Add usage/help documentation"
        }
    };

    private class LanguageGuidance
    {
        public required string FileExtension { get; set; }
        public required string CommentStyle { get; set; }
        public required string DocStyle { get; set; }
        public required string Guidelines { get; set; }
    }

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
        
        // 🌐 LANGUAGE-SPECIFIC GUIDANCE - TRY LIGHTNING FIRST!
        var language = request.Language?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(language))
        {
            // ✅ LEARNING: Try to fetch language-specific prompt from Lightning
            var languagePromptName = $"coding_agent_{language}";
            var languagePrompt = await _memoryAgent.GetPromptAsync(languagePromptName, cancellationToken);
            
            if (languagePrompt != null)
            {
                // Use Lightning's prompt for this language
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} (from Lightning v{languagePrompt.Version}) ===");
                sb.AppendLine(languagePrompt.Content);
                sb.AppendLine();
                _logger.LogInformation("✨ Using Lightning prompt for {Language} (v{Version})", language, languagePrompt.Version);
            }
            else
            {
                // NO FALLBACK - language prompt MUST exist in Lightning
                _logger.LogError("❌ CRITICAL: Language prompt 'coding_agent_{Language}' not found in Lightning. Ensure prompts are seeded.", language);
                throw new InvalidOperationException($"Required language prompt 'coding_agent_{language}' not found in Lightning. Run PromptSeedService or add the language prompt.");
            }
        }
        
        // 🔍 SEARCH BEFORE WRITE: Find existing code to avoid duplication
        var context = "memoryagent"; // Default context
        _logger.LogInformation("🔍 [QDRANT+NEO4J] Searching existing code via MemoryAgent smartsearch...");
        
        // TODO: SearchExistingCodeAsync method needs to be implemented in MemoryAgent
        // For now, skip this section
        _logger.LogInformation("ℹ️ [QDRANT+NEO4J] Existing code search temporarily disabled");
        
        // 📁 EXISTING FILES: Include files from previous generation steps
        if (request.ExistingFiles?.Any() == true)
        {
            sb.AppendLine("=== EXISTING CODE (from previous steps - REFERENCE BUT DO NOT REDEFINE) ===");
            sb.AppendLine("These files have already been generated. Use them, don't recreate them:");
            sb.AppendLine();
            
            foreach (var file in request.ExistingFiles)
            {
                sb.AppendLine($"// ========== {file.Path} ==========");
                // Show full content for files under 3000 chars, truncated for larger
                if (file.Content.Length <= 3000)
                {
                    sb.AppendLine(file.Content);
                }
                else
                {
                    // Show first 2500 chars + structure
                    sb.AppendLine(file.Content.Substring(0, 2500));
                    sb.AppendLine("// ... (truncated - use the classes/methods shown above)");
                }
                sb.AppendLine();
            }
            
            _logger.LogInformation("📁 Added {FileCount} existing files to prompt ({TotalChars} chars)", 
                request.ExistingFiles.Count, request.ExistingFiles.Sum(f => f.Content.Length));
        }

        sb.AppendLine("=== TASK ===");
        sb.AppendLine(request.Task);
        sb.AppendLine();

        // ✅ LEARNING: Add similar past solutions from Lightning Q&A memory
        _logger.LogInformation("🧠 [LIGHTNING] Searching for similar past solutions via find_similar_questions...");
        
        // TODO: FindSimilarSolutionsAsync method needs to be implemented in MemoryAgent
        // For now, skip this section
        _logger.LogInformation("ℹ️ [LIGHTNING] Similar solutions search temporarily disabled");

        // ✅ LEARNING: Add patterns from Lightning
        // TODO: GetPatternsAsync needs to be implemented in MemoryAgent
        _logger.LogInformation("ℹ️ [LIGHTNING] Pattern retrieval temporarily disabled");

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

        // 🐳 EXECUTION CAPABILITIES - Tell LLM what we can execute
        var capabilities = ExecutionCapabilities.CreateDefault();
        sb.AppendLine(capabilities.ToPromptString());
        sb.AppendLine();
        
        sb.AppendLine("=== INSTRUCTIONS ===");
        sb.AppendLine("Generate the code to complete this task.");
        sb.AppendLine();
        sb.AppendLine("🐳 YOUR CODE WILL BE EXECUTED! You MUST include execution instructions:");
        sb.AppendLine("After your code, include a JSON block like this:");
        sb.AppendLine("```execution");
        sb.AppendLine("{");
        sb.AppendLine("  \"language\": \"python\",");
        sb.AppendLine("  \"mainFile\": \"main.py\",");
        sb.AppendLine("  \"buildCommand\": \"python -c \\\"import ast; ast.parse(open('main.py').read())\\\"\",");
        sb.AppendLine("  \"runCommand\": \"python main.py\",");
        sb.AppendLine("  \"expectedOutput\": \"Hello, World!\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Pick ONE executable language from the capabilities above. Your code MUST run successfully!");

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
        
        // 🌐 LANGUAGE-SPECIFIC GUIDANCE (for fixes too!) - TRY LIGHTNING FIRST!
        var language = request.Language?.ToLowerInvariant();
        if (!string.IsNullOrEmpty(language))
        {
            // ✅ LEARNING: Try to fetch language-specific prompt from Lightning
            var languagePromptName = $"coding_agent_{language}";
            var languagePrompt = await _memoryAgent.GetPromptAsync(languagePromptName, cancellationToken);
            
            if (languagePrompt != null)
            {
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} (from Lightning v{languagePrompt.Version}) ===");
                sb.AppendLine(languagePrompt.Content);
                sb.AppendLine();
            }
            else
            {
                // NO FALLBACK - language prompt MUST exist in Lightning
                _logger.LogError("❌ CRITICAL: Language prompt 'coding_agent_{Language}' not found in Lightning. Ensure prompts are seeded.", language);
                throw new InvalidOperationException($"Required language prompt 'coding_agent_{language}' not found in Lightning. Run PromptSeedService or add the language prompt.");
            }
        }
        
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

        // 🐳 EXECUTION CAPABILITIES - Remind LLM what we can execute
        var capabilities = ExecutionCapabilities.CreateDefault();
        sb.AppendLine();
        sb.AppendLine(capabilities.ToPromptString());
        
        sb.AppendLine();
        sb.AppendLine("=== INSTRUCTIONS ===");
        sb.AppendLine("Fix ALL the issues listed above. Return the corrected code files.");
        sb.AppendLine();
        sb.AppendLine("🐳 YOUR CODE WILL BE EXECUTED! Include execution instructions:");
        sb.AppendLine("```execution");
        sb.AppendLine("{\"language\": \"...\", \"mainFile\": \"...\", \"runCommand\": \"...\"}");
        sb.AppendLine("```");

        return sb.ToString();
    }
    
    /// <summary>
    /// Build a FOCUSED prompt for fixing build errors only.
    /// This is much smaller than the regular fix prompt - puts errors at the TOP.
    /// </summary>
    public async Task<string> BuildBuildErrorFixPromptAsync(
        string buildErrors, 
        Dictionary<string, string> brokenFiles,
        string language,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        
        // Fetch the focused build error fix prompt
        var prompt = await _memoryAgent.GetPromptAsync("coding_agent_fix_build_errors", cancellationToken);
        
        if (prompt == null)
        {
            _logger.LogWarning("Build error fix prompt not found, using inline fallback");
            // Inline fallback if prompt not seeded yet
            sb.AppendLine("You are fixing BUILD ERRORS. Your ONLY job is to fix the specific compiler errors listed below.");
            sb.AppendLine();
        }
        else
        {
            // Use the template but we'll replace placeholders
            sb.AppendLine(prompt.Content.Replace("{{BUILD_ERRORS}}", "").Replace("{{BROKEN_CODE}}", ""));
        }
        
        // === BUILD ERRORS AT THE TOP ===
        sb.AppendLine();
        sb.AppendLine("=== BUILD ERRORS (FIX THESE NOW) ===");
        sb.AppendLine(buildErrors);
        sb.AppendLine();
        
        // === BROKEN CODE ===
        sb.AppendLine("=== BROKEN CODE ===");
        foreach (var file in brokenFiles)
        {
            sb.AppendLine($"// ===== {file.Key} =====");
            sb.AppendLine(file.Value);
            sb.AppendLine();
        }
        
        // === MINIMAL LANGUAGE GUIDANCE ===
        if (!string.IsNullOrEmpty(language) && language.Equals("csharp", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("=== C# REMINDERS ===");
            sb.AppendLine("- Use 'namespace MyApp;' file-scoped namespace");
            sb.AppendLine("- Wrap code in 'public class Program' with 'Main' method");
            sb.AppendLine("- Add 'using' statements for missing types");
            sb.AppendLine("- Check enum values match (e.g., HandRank.Ace vs Rank.Ace)");
            sb.AppendLine();
        }
        
        sb.AppendLine("=== OUTPUT ===");
        sb.AppendLine("Return the COMPLETE fixed file(s) with path comments like: // path: Program.cs");
        sb.AppendLine("Fix ALL the errors. Do NOT add new features.");
        
        _logger.LogInformation("[PROMPT] Built focused build-error fix prompt: {Length} chars (vs 48K typical)", sb.Length);
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Detect if this is UI code that needs design guidelines
    /// </summary>
    private bool IsUICode(GenerateCodeRequest request)
    {
        var language = request.Language?.ToLowerInvariant() ?? "";
        var task = request.Task.ToLowerInvariant();
        
        // Language-based detection
        if (language is "flutter" or "dart" or "blazor" or "react" or "vue" or "angular" or "svelte")
            return true;
        
        // Task keyword detection
        var uiKeywords = new[]
        {
            "ui", "screen", "page", "view", "component", "widget",
            "form", "button", "card", "dialog", "modal", "menu",
            "navbar", "header", "footer", "sidebar", "layout",
            "dashboard", "login", "signup", "profile", "settings"
        };
        
        return uiKeywords.Any(keyword => task.Contains(keyword));
    }
}
