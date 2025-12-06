using System.Text.Json;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.MCP;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for code transformation operations.
/// Tools: transform, get_migration_path
/// Consolidates: transform_page, transform_css, learn_transformation, apply_transformation, 
///              detect_reusable_components, extract_component, analyze_css
/// </summary>
public class TransformToolHandler : IMcpToolHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPatternValidationService _patternValidationService;
    private readonly ILogger<TransformToolHandler> _logger;

    public TransformToolHandler(
        IServiceProvider serviceProvider,
        IPatternValidationService patternValidationService,
        ILogger<TransformToolHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _patternValidationService = patternValidationService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "transform",
                Description = "Transform code. Use type: 'page' (Blazor/Razor), 'css' (modernize CSS), 'analyze_css' (analyze quality), 'learn_pattern' (learn from example), 'apply_pattern' (apply learned), 'detect_components' (find reusable), 'extract_component', 'list_patterns' (show learned patterns).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        type = new { type = "string", description = "Transformation type", @enum = new[] { "page", "css", "analyze_css", "learn_pattern", "apply_pattern", "detect_components", "extract_component", "list_patterns" } },
                        sourcePath = new { type = "string", description = "Path to source file/directory" },
                        outputPath = new { type = "string", description = "Output path (optional)" },
                        
                        // Page transformation options
                        extractComponents = new { type = "boolean", description = "For page: extract reusable components", @default = true },
                        modernizeCSS = new { type = "boolean", description = "For page: modernize inline CSS", @default = true },
                        addErrorHandling = new { type = "boolean", description = "For page: add error handling", @default = true },
                        addLoadingStates = new { type = "boolean", description = "For page: add loading states", @default = true },
                        addAccessibility = new { type = "boolean", description = "For page/css: add a11y features", @default = true },
                        
                        // CSS transformation options
                        generateVariables = new { type = "boolean", description = "For css: generate CSS variables", @default = true },
                        modernizeLayout = new { type = "boolean", description = "For css: use Grid/Flexbox", @default = true },
                        addResponsive = new { type = "boolean", description = "For css: add responsive design", @default = true },
                        
                        // Pattern learning options
                        exampleOldPath = new { type = "string", description = "For learn_pattern: old version path" },
                        exampleNewPath = new { type = "string", description = "For learn_pattern: new version path" },
                        patternName = new { type = "string", description = "For learn_pattern: pattern name" },
                        patternId = new { type = "string", description = "For apply_pattern: pattern ID to apply" },
                        
                        // Component detection options
                        minOccurrences = new { type = "number", description = "For detect_components: min occurrences", @default = 2 },
                        minSimilarity = new { type = "number", description = "For detect_components: min similarity (0-1)", @default = 0.7 },
                        
                        // Component extraction options
                        componentCandidateJson = new { type = "string", description = "For extract_component: JSON of component candidate" },
                        
                        // Context filter for list operations
                        context = new { type = "string", description = "Project context for filtering" }
                    },
                    required = new[] { "type" }
                }
            },
            new McpTool
            {
                Name = "get_migration_path",
                Description = "Get step-by-step migration path for legacy/deprecated patterns. Returns instructions, code examples, and effort estimate.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        patternId = new { type = "string", description = "Pattern ID to get migration path for" },
                        includeCodeExample = new { type = "boolean", description = "Include before/after code example", @default = true }
                    },
                    required = new[] { "patternId" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "transform" => await TransformAsync(args, cancellationToken),
            "get_migration_path" => await GetMigrationPathAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Transform

    private async Task<McpToolResult> TransformAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var type = args?.GetValueOrDefault("type")?.ToString()?.ToLowerInvariant();

        return type switch
        {
            "page" => await TransformPageAsync(args, ct),
            "css" => await TransformCSSAsync(args, ct),
            "analyze_css" => await AnalyzeCSSAsync(args, ct),
            "learn_pattern" => await LearnPatternAsync(args, ct),
            "apply_pattern" => await ApplyPatternAsync(args, ct),
            "detect_components" => await DetectComponentsAsync(args, ct),
            "extract_component" => await ExtractComponentAsync(args, ct),
            "list_patterns" => await ListTransformationPatternsAsync(args, ct),
            _ => ErrorResult($"Unknown transform type: {type}. Valid: page, css, analyze_css, learn_pattern, apply_pattern, detect_components, extract_component, list_patterns")
        };
    }

    private async Task<McpToolResult> TransformPageAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return ErrorResult("sourcePath is required for page transformation");

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await tools.TransformPage(
            sourcePath,
            SafeParseBool(args?.GetValueOrDefault("extractComponents"), true),
            SafeParseBool(args?.GetValueOrDefault("modernizeCSS"), true),
            SafeParseBool(args?.GetValueOrDefault("addErrorHandling"), true),
            SafeParseBool(args?.GetValueOrDefault("addLoadingStates"), true),
            SafeParseBool(args?.GetValueOrDefault("addAccessibility"), true),
            args?.GetValueOrDefault("outputPath")?.ToString()
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> TransformCSSAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return ErrorResult("sourcePath is required for CSS transformation");

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await tools.TransformCSS(
            sourcePath,
            SafeParseBool(args?.GetValueOrDefault("generateVariables"), true),
            SafeParseBool(args?.GetValueOrDefault("modernizeLayout"), true),
            SafeParseBool(args?.GetValueOrDefault("addResponsive"), true),
            SafeParseBool(args?.GetValueOrDefault("addAccessibility"), true),
            args?.GetValueOrDefault("outputPath")?.ToString()
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> AnalyzeCSSAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return ErrorResult("sourcePath is required for CSS analysis");

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await tools.AnalyzeCSS(sourcePath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> LearnPatternAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var exampleOldPath = args?.GetValueOrDefault("exampleOldPath")?.ToString();
        var exampleNewPath = args?.GetValueOrDefault("exampleNewPath")?.ToString();
        var patternName = args?.GetValueOrDefault("patternName")?.ToString();

        if (string.IsNullOrWhiteSpace(exampleOldPath) || string.IsNullOrWhiteSpace(exampleNewPath) || string.IsNullOrWhiteSpace(patternName))
            return ErrorResult("exampleOldPath, exampleNewPath, and patternName are required for learn_pattern");

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await tools.LearnTransformation(exampleOldPath, exampleNewPath, patternName);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> ApplyPatternAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var patternId = args?.GetValueOrDefault("patternId")?.ToString();
        var targetPath = args?.GetValueOrDefault("sourcePath")?.ToString();

        if (string.IsNullOrWhiteSpace(patternId) || string.IsNullOrWhiteSpace(targetPath))
            return ErrorResult("patternId and sourcePath are required for apply_pattern");

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await tools.ApplyTransformation(patternId, targetPath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> DetectComponentsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var projectPath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(projectPath))
            return ErrorResult("sourcePath (project directory) is required for detect_components");

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await tools.DetectReusableComponents(
            projectPath,
            SafeParseInt(args?.GetValueOrDefault("minOccurrences"), 2),
            (float)SafeParseDouble(args?.GetValueOrDefault("minSimilarity"), 0.7)
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> ExtractComponentAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var candidateArg = args?.GetValueOrDefault("componentCandidateJson");
        var outputPath = args?.GetValueOrDefault("outputPath")?.ToString();

        if (candidateArg == null || string.IsNullOrWhiteSpace(outputPath))
            return ErrorResult("componentCandidateJson and outputPath are required for extract_component");

        string componentCandidateJson;
        if (candidateArg is JsonElement jsonElement)
            componentCandidateJson = jsonElement.GetRawText();
        else if (candidateArg is string str)
            componentCandidateJson = str;
        else
            componentCandidateJson = JsonSerializer.Serialize(candidateArg);

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await tools.ExtractComponent(componentCandidateJson, outputPath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) }
            }
        };
    }

    private async Task<McpToolResult> ListTransformationPatternsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();

        var tools = _serviceProvider.GetRequiredService<TransformationTools>();
        var patterns = await tools.ListTransformationPatterns(context);

        if (patterns == null || patterns.Count == 0)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = "📋 No transformation patterns found.\n\nUse transform with type='learn_pattern' to learn from before/after examples." }
                }
            };
        }

        var output = $"📋 Transformation Patterns ({patterns.Count})\n\n";
        foreach (var pattern in patterns)
        {
            output += $"• {pattern.Name}\n";
            output += $"  ID: {pattern.Id}\n";
            if (!string.IsNullOrEmpty(pattern.Description))
                output += $"  Description: {pattern.Description}\n";
            output += $"  Context: {pattern.Context}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Migration Path

    private async Task<McpToolResult> GetMigrationPathAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var patternId = args?.GetValueOrDefault("patternId")?.ToString() ?? "";
        var includeCodeExample = SafeParseBool(args?.GetValueOrDefault("includeCodeExample"), true);

        var result = await _patternValidationService.GetMigrationPathAsync(patternId, includeCodeExample, ct);

        if (result == null)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"No migration path available for pattern: {patternId}" }
                }
            };
        }

        var output = $"🔄 Migration Path\n\n";
        output += $"Current: {result.CurrentPattern}\n";
        output += $"Target: {result.TargetPattern}\n";
        output += $"Status: {result.Status}\n";
        output += $"Effort: {result.EffortEstimate}\n";
        output += $"Complexity: {result.Complexity}\n\n";
        output += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
        output += "📋 Steps:\n\n";

        foreach (var step in result.Steps)
        {
            output += $"{step.StepNumber}. {step.Title}\n";
            output += $"   {step.Instructions}\n";
            if (step.FilesToModify.Any())
                output += $"   Files: {string.Join(", ", step.FilesToModify)}\n";
            output += "\n";
        }

        if (result.CodeExample != null && includeCodeExample)
        {
            output += "💡 Code Example:\n\n";
            output += $"{result.CodeExample.Description}\n\n";
            output += "Before:\n```\n";
            output += result.CodeExample.Before;
            output += "\n```\n\nAfter:\n```\n";
            output += result.CodeExample.After;
            output += "\n```\n\n";
        }

        if (result.Benefits.Any())
        {
            output += "✅ Benefits:\n";
            foreach (var b in result.Benefits)
                output += $"  • {b}\n";
            output += "\n";
        }

        if (result.Risks.Any())
        {
            output += "⚠️ Risks of NOT migrating:\n";
            foreach (var r in result.Risks)
                output += $"  • {r}\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Helpers

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var i) => i,
            JsonElement je when je.TryGetInt32(out var i) => i,
            _ => defaultValue
        };

    private static double SafeParseDouble(object? value, double defaultValue) =>
        value switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s when double.TryParse(s, out var d) => d,
            JsonElement je when je.TryGetDouble(out var d) => d,
            _ => defaultValue
        };

    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            _ => defaultValue
        };

    private static McpToolResult ErrorResult(string error) => new()
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };

    #endregion
}

