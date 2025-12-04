using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for code complexity analysis
/// Tools: analyze_code_complexity
/// </summary>
public class CodeAnalysisToolHandler : IMcpToolHandler
{
    private readonly ICodeComplexityService _complexityService;
    private readonly ILogger<CodeAnalysisToolHandler> _logger;

    public CodeAnalysisToolHandler(
        ICodeComplexityService complexityService,
        ILogger<CodeAnalysisToolHandler> logger)
    {
        _complexityService = complexityService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "analyze_code_complexity",
                Description = "Analyze code complexity metrics (cyclomatic, cognitive, LOC, nesting, code smells) for a file or specific method. Returns detailed complexity scores with grades and recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the file to analyze" },
                        methodName = new { type = "string", description = "Optional: specific method name to analyze (if omitted, analyzes all methods in file)" }
                    },
                    required = new[] { "filePath" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        if (toolName == "analyze_code_complexity")
            return await AnalyzeCodeComplexityToolAsync(args, cancellationToken);

        return ErrorResult($"Unknown tool: {toolName}");
    }

    private async Task<McpToolResult> AnalyzeCodeComplexityToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var filePath = args?.GetValueOrDefault("filePath")?.ToString() ?? "";
        var methodName = args?.GetValueOrDefault("methodName")?.ToString();

        var result = await _complexityService.AnalyzeFileAsync(filePath, methodName, cancellationToken);

        if (!result.Success)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error analyzing code complexity:\n{string.Join("\n", result.Errors)}"
                    }
                }
            };
        }

        var text = $"📊 Code Complexity Analysis\n" +
                   $"File: {result.FilePath}\n";
        if (!string.IsNullOrEmpty(result.MethodName))
            text += $"Method: {result.MethodName}\n";
        text += "\n";

        // Summary
        text += $"📈 Summary (Overall Grade: {result.Summary.OverallGrade})\n" +
                $"  Total Methods: {result.Summary.TotalMethods}\n" +
                $"  Avg Cyclomatic Complexity: {result.Summary.AverageCyclomaticComplexity}\n" +
                $"  Avg Cognitive Complexity: {result.Summary.AverageCognitiveComplexity}\n" +
                $"  Avg Lines of Code: {result.Summary.AverageLinesOfCode}\n" +
                $"  Max Cyclomatic Complexity: {result.Summary.MaxCyclomaticComplexity}\n" +
                $"  Max Cognitive Complexity: {result.Summary.MaxCognitiveComplexity}\n" +
                $"  Methods with High Complexity: {result.Summary.MethodsWithHighComplexity}\n" +
                $"  Methods with Code Smells: {result.Summary.MethodsWithCodeSmells}\n\n";

        if (result.Summary.FileRecommendations.Any())
        {
            text += "📋 File-Level Recommendations:\n";
            foreach (var rec in result.Summary.FileRecommendations)
                text += $"  {rec}\n";
            text += "\n";
        }

        // Method details
        if (result.Methods.Any())
        {
            text += "🔍 Method Details:\n\n";
            
            var sortedMethods = result.Methods
                .OrderBy(m => m.Grade switch { "F" => 1, "D" => 2, "C" => 3, "B" => 4, "A" => 5, _ => 6 })
                .ThenByDescending(m => m.CyclomaticComplexity)
                .ToList();

            foreach (var method in sortedMethods)
            {
                var gradeEmoji = method.Grade switch
                {
                    "A" => "✅",
                    "B" => "✅",
                    "C" => "⚠️",
                    "D" => "❌",
                    "F" => "🔴",
                    _ => "❓"
                };

                text += $"{gradeEmoji} {method.ClassName}.{method.MethodName} (Grade: {method.Grade})\n" +
                        $"  Lines: {method.StartLine}-{method.EndLine} ({method.LinesOfCode} LOC)\n" +
                        $"  Cyclomatic Complexity: {method.CyclomaticComplexity}\n" +
                        $"  Cognitive Complexity: {method.CognitiveComplexity}\n" +
                        $"  Max Nesting Depth: {method.MaxNestingDepth}\n" +
                        $"  Parameters: {method.ParameterCount}\n";
                
                if (method.DatabaseCalls > 0)
                    text += $"  Database Calls: {method.DatabaseCalls}\n";
                if (method.HasHttpCalls)
                    text += "  Has HTTP Calls: Yes\n";
                if (method.IsPublic)
                    text += "  Visibility: Public API\n";
                if (method.CodeSmells.Any())
                    text += $"  Code Smells: {string.Join(", ", method.CodeSmells)}\n";
                if (method.ExceptionTypes.Any())
                    text += $"  Exception Types: {string.Join(", ", method.ExceptionTypes)}\n";

                if (method.Recommendations.Any())
                {
                    text += "  Recommendations:\n";
                    foreach (var rec in method.Recommendations)
                        text += $"    {rec}\n";
                }
                
                text += "\n";
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private McpToolResult ErrorResult(string error) => new McpToolResult
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };
}

