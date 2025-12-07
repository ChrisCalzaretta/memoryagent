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

    // Default fallback if Lightning is unavailable (generic, multi-language)
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
5. You MUST include proper error handling
6. You MUST include documentation comments
7. Follow naming conventions and best practices for the target language

REQUIREMENTS:
- Include proper error handling for the target language
- Use async patterns where appropriate
- Follow idiomatic patterns for the target language
- Include type hints/annotations where the language supports them";

    // Language-specific guidance - FALLBACK DEFAULTS (Lightning prompts take priority!)
    // These are used only if Lightning doesn't have a prompt for the language
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
                // Use Lightning's learned prompt for this language
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} (from Lightning v{languagePrompt.Version}) ===");
                sb.AppendLine(languagePrompt.Content);
                sb.AppendLine();
                _logger.LogInformation("✨ Using LEARNED prompt for {Language} (v{Version})", language, languagePrompt.Version);
            }
            else if (DefaultLanguageGuidelines.TryGetValue(language, out var guidance))
            {
                // Fall back to hardcoded defaults
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} (default) ===");
                sb.AppendLine($"File Extension: {guidance.FileExtension}");
                sb.AppendLine($"Documentation Style: {guidance.DocStyle}");
                sb.AppendLine();
                sb.AppendLine(guidance.Guidelines);
                sb.AppendLine();
                _logger.LogInformation("Using DEFAULT guidance for: {Language} (not yet in Lightning)", language);
            }
            else
            {
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} ===");
                sb.AppendLine("Follow best practices and idiomatic patterns for this language.");
                sb.AppendLine();
                _logger.LogWarning("No guidance found for language: {Language}", language);
            }
        }
        
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
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} (from Lightning) ===");
                sb.AppendLine(languagePrompt.Content);
                sb.AppendLine();
            }
            else if (DefaultLanguageGuidelines.TryGetValue(language, out var guidance))
            {
                sb.AppendLine($"=== 🎯 TARGET LANGUAGE: {language.ToUpperInvariant()} ===");
                sb.AppendLine($"File Extension: {guidance.FileExtension}");
                sb.AppendLine(guidance.Guidelines);
                sb.AppendLine();
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
}
