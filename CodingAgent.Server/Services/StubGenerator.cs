using System.Text;

namespace CodingAgent.Server.Services;

/// <summary>
/// Generates compilable stubs when code generation fails after maximum attempts
/// </summary>
public interface IStubGenerator
{
    /// <summary>
    /// Generate a stub for a failed file
    /// </summary>
    GeneratedStub GenerateStub(StubContext context);
}

/// <summary>
/// Context for stub generation
/// </summary>
public record StubContext
{
    /// <summary>
    /// Path of the file that failed
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// Programming language
    /// </summary>
    public required string Language { get; init; }
    
    /// <summary>
    /// Project namespace (for C#)
    /// </summary>
    public string Namespace { get; init; } = "GeneratedApp";
    
    /// <summary>
    /// Number of attempts made
    /// </summary>
    public int AttemptCount { get; init; }
    
    /// <summary>
    /// Highest score achieved
    /// </summary>
    public double HighestScore { get; init; }
    
    /// <summary>
    /// Root cause analysis (if available)
    /// </summary>
    public string? RootCause { get; init; }
    
    /// <summary>
    /// Recommended actions
    /// </summary>
    public string[] RecommendedActions { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Original task description
    /// </summary>
    public string? TaskDescription { get; init; }
    
    /// <summary>
    /// List of issues from validation
    /// </summary>
    public string[] ValidationIssues { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of stub generation
/// </summary>
public record GeneratedStub
{
    public required string FilePath { get; init; }
    public required string Content { get; init; }
    public required string Status { get; init; }
    public string? FailureReportPath { get; init; }
}

/// <summary>
/// Implementation of stub generator
/// </summary>
public class StubGenerator : IStubGenerator
{
    private readonly ILogger<StubGenerator> _logger;
    
    public StubGenerator(ILogger<StubGenerator> logger)
    {
        _logger = logger;
    }
    
    public GeneratedStub GenerateStub(StubContext context)
    {
        _logger.LogWarning(
            "Generating stub for failed file: {File} (Language: {Language}, Attempts: {Attempts})",
            context.FilePath, context.Language, context.AttemptCount);
        
        var content = context.Language.ToLowerInvariant() switch
        {
            "csharp" or "c#" or "cs" => GenerateCSharpStub(context),
            "flutter" or "dart" => GenerateDartStub(context),
            "typescript" or "ts" => GenerateTypeScriptStub(context),
            "javascript" or "js" => GenerateJavaScriptStub(context),
            "python" or "py" => GeneratePythonStub(context),
            _ => GenerateGenericStub(context)
        };
        
        var failureReportPath = Path.GetFileNameWithoutExtension(context.FilePath) + "_failure_report.md";
        
        return new GeneratedStub
        {
            FilePath = context.FilePath,
            Content = content,
            Status = "NEEDS_HUMAN_REVIEW",
            FailureReportPath = failureReportPath
        };
    }
    
    private string GenerateCSharpStub(StubContext context)
    {
        var className = Path.GetFileNameWithoutExtension(context.FilePath);
        var sb = new StringBuilder();
        
        sb.AppendLine($"namespace {context.Namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// TODO: NEEDS HUMAN REVIEW");
        sb.AppendLine($"/// This file failed code generation after {context.AttemptCount} attempts.");
        sb.AppendLine($"/// Highest validation score: {context.HighestScore:F1}/10");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        
        if (!string.IsNullOrEmpty(context.RootCause))
        {
            sb.AppendLine("/// Root Cause Analysis:");
            sb.AppendLine($"/// {context.RootCause}");
            sb.AppendLine("///");
        }
        
        if (context.RecommendedActions.Length > 0)
        {
            sb.AppendLine("/// Recommended Actions:");
            foreach (var action in context.RecommendedActions.Take(5))
            {
                sb.AppendLine($"/// - {action}");
            }
            sb.AppendLine("///");
        }
        
        if (context.ValidationIssues.Length > 0)
        {
            sb.AppendLine("/// Validation Issues:");
            foreach (var issue in context.ValidationIssues.Take(5))
            {
                sb.AppendLine($"/// - {issue}");
            }
        }
        
        sb.AppendLine("/// </remarks>");
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly ILogger<{className}>? _logger;");
        sb.AppendLine();
        sb.AppendLine($"    public {className}(ILogger<{className}>? logger = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    // TODO: Implement required methods");
        sb.AppendLine("    // See failure report for details on what went wrong");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Placeholder method - implement based on task requirements");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public void Execute()");
        sb.AppendLine("    {");
        sb.AppendLine("        throw new NotImplementedException(");
        sb.AppendLine($"            \"TODO: Implement {className}. \" +");
        sb.AppendLine($"            \"See failure report for {context.AttemptCount} attempts and suggested solutions.\");");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateDartStub(StubContext context)
    {
        var className = Path.GetFileNameWithoutExtension(context.FilePath);
        // Convert to PascalCase for Dart class name
        className = ToPascalCase(className);
        
        var sb = new StringBuilder();
        
        sb.AppendLine("// TODO: NEEDS HUMAN REVIEW");
        sb.AppendLine($"// This file failed code generation after {context.AttemptCount} attempts.");
        sb.AppendLine($"// Highest validation score: {context.HighestScore:F1}/10");
        sb.AppendLine("//");
        
        if (!string.IsNullOrEmpty(context.RootCause))
        {
            sb.AppendLine("// Root Cause Analysis:");
            sb.AppendLine($"// {context.RootCause}");
            sb.AppendLine("//");
        }
        
        if (context.RecommendedActions.Length > 0)
        {
            sb.AppendLine("// Recommended Actions:");
            foreach (var action in context.RecommendedActions.Take(5))
            {
                sb.AppendLine($"// - {action}");
            }
            sb.AppendLine("//");
        }
        
        sb.AppendLine();
        sb.AppendLine("import 'package:flutter/foundation.dart';");
        sb.AppendLine();
        sb.AppendLine($"/// {className} - Stub implementation");
        sb.AppendLine("///");
        sb.AppendLine("/// TODO: Implement this class based on the task requirements");
        sb.AppendLine($"class {className} {{");
        sb.AppendLine($"  {className}() {{");
        sb.AppendLine("    debugPrint('WARNING: Using stub implementation of " + className + "');");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  /// TODO: Implement required methods");
        sb.AppendLine("  void execute() {");
        sb.AppendLine($"    throw UnimplementedError(");
        sb.AppendLine($"      'TODO: Implement {className}. '");
        sb.AppendLine($"      'See failure report for {context.AttemptCount} attempts and suggested solutions.',");
        sb.AppendLine("    );");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateTypeScriptStub(StubContext context)
    {
        var className = ToPascalCase(Path.GetFileNameWithoutExtension(context.FilePath));
        var sb = new StringBuilder();
        
        sb.AppendLine("/**");
        sb.AppendLine(" * TODO: NEEDS HUMAN REVIEW");
        sb.AppendLine($" * This file failed code generation after {context.AttemptCount} attempts.");
        sb.AppendLine($" * Highest validation score: {context.HighestScore:F1}/10");
        
        if (!string.IsNullOrEmpty(context.RootCause))
        {
            sb.AppendLine(" *");
            sb.AppendLine($" * Root Cause: {context.RootCause}");
        }
        
        sb.AppendLine(" */");
        sb.AppendLine();
        sb.AppendLine($"export class {className} {{");
        sb.AppendLine("  constructor() {");
        sb.AppendLine($"    console.warn('Using stub implementation of {className}');");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  // TODO: Implement required methods");
        sb.AppendLine("  execute(): void {");
        sb.AppendLine($"    throw new Error('TODO: Implement {className}');");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateJavaScriptStub(StubContext context)
    {
        var className = ToPascalCase(Path.GetFileNameWithoutExtension(context.FilePath));
        var sb = new StringBuilder();
        
        sb.AppendLine("/**");
        sb.AppendLine(" * TODO: NEEDS HUMAN REVIEW");
        sb.AppendLine($" * This file failed code generation after {context.AttemptCount} attempts.");
        sb.AppendLine(" */");
        sb.AppendLine();
        sb.AppendLine($"class {className} {{");
        sb.AppendLine("  constructor() {");
        sb.AppendLine($"    console.warn('Using stub implementation of {className}');");
        sb.AppendLine("  }");
        sb.AppendLine();
        sb.AppendLine("  execute() {");
        sb.AppendLine($"    throw new Error('TODO: Implement {className}');");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"module.exports = {{ {className} }};");
        
        return sb.ToString();
    }
    
    private string GeneratePythonStub(StubContext context)
    {
        var className = ToPascalCase(Path.GetFileNameWithoutExtension(context.FilePath));
        var sb = new StringBuilder();
        
        sb.AppendLine($"\"\"\"");
        sb.AppendLine("TODO: NEEDS HUMAN REVIEW");
        sb.AppendLine($"This file failed code generation after {context.AttemptCount} attempts.");
        sb.AppendLine($"Highest validation score: {context.HighestScore:F1}/10");
        
        if (!string.IsNullOrEmpty(context.RootCause))
        {
            sb.AppendLine();
            sb.AppendLine($"Root Cause: {context.RootCause}");
        }
        
        sb.AppendLine("\"\"\"");
        sb.AppendLine();
        sb.AppendLine("import warnings");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"class {className}:");
        sb.AppendLine($"    \"\"\"Stub implementation of {className}\"\"\"");
        sb.AppendLine();
        sb.AppendLine("    def __init__(self):");
        sb.AppendLine($"        warnings.warn('Using stub implementation of {className}')");
        sb.AppendLine();
        sb.AppendLine("    def execute(self):");
        sb.AppendLine("        \"\"\"TODO: Implement this method\"\"\"");
        sb.AppendLine($"        raise NotImplementedError('TODO: Implement {className}')");
        
        return sb.ToString();
    }
    
    private string GenerateGenericStub(StubContext context)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("/*");
        sb.AppendLine(" * TODO: NEEDS HUMAN REVIEW");
        sb.AppendLine($" * This file failed code generation after {context.AttemptCount} attempts.");
        sb.AppendLine($" * Highest validation score: {context.HighestScore:F1}/10");
        sb.AppendLine(" *");
        
        if (!string.IsNullOrEmpty(context.RootCause))
        {
            sb.AppendLine($" * Root Cause: {context.RootCause}");
            sb.AppendLine(" *");
        }
        
        sb.AppendLine(" * Please implement this file manually based on the task requirements.");
        sb.AppendLine(" */");
        sb.AppendLine();
        sb.AppendLine("// TODO: Implement this file");
        
        return sb.ToString();
    }
    
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var words = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        
        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                result.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    result.Append(word[1..].ToLowerInvariant());
                }
            }
        }
        
        return result.ToString();
    }
}

