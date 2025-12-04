using System.Text.Json;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.MCP;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for Blazor/Razor page transformation and component extraction
/// Tools: transform_page, learn_transformation, apply_transformation, list_transformation_patterns,
///        detect_reusable_components, extract_component, transform_css, analyze_css
/// </summary>
public class TransformationToolHandler : IMcpToolHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransformationToolHandler> _logger;

    public TransformationToolHandler(
        IServiceProvider serviceProvider,
        ILogger<TransformationToolHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "transform_page",
                Description = "Transform a Blazor/Razor page to modern architecture with clean code and CSS",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Path to the .razor/.cshtml file to transform" },
                        extractComponents = new { type = "boolean", description = "Extract reusable components", @default = true },
                        modernizeCSS = new { type = "boolean", description = "Modernize CSS (extract inline styles, use CSS variables)", @default = true },
                        addErrorHandling = new { type = "boolean", description = "Add error handling", @default = true },
                        addLoadingStates = new { type = "boolean", description = "Add loading states", @default = true },
                        addAccessibility = new { type = "boolean", description = "Add accessibility features", @default = true },
                        outputDirectory = new { type = "string", description = "Optional output directory for generated files" }
                    },
                    required = new[] { "sourcePath" }
                }
            },
            new McpTool
            {
                Name = "learn_transformation",
                Description = "Learn transformation pattern from example (old → new page)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        exampleOldPath = new { type = "string", description = "Path to old/legacy version of the page" },
                        exampleNewPath = new { type = "string", description = "Path to new/modernized version" },
                        patternName = new { type = "string", description = "Name for this transformation pattern" }
                    },
                    required = new[] { "exampleOldPath", "exampleNewPath", "patternName" }
                }
            },
            new McpTool
            {
                Name = "apply_transformation",
                Description = "Apply learned transformation pattern to a new page",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        patternId = new { type = "string", description = "ID of the learned pattern to apply" },
                        targetPath = new { type = "string", description = "Path to the page to transform" }
                    },
                    required = new[] { "patternId", "targetPath" }
                }
            },
            new McpTool
            {
                Name = "list_transformation_patterns",
                Description = "Get all learned transformation patterns",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Optional context to filter patterns" }
                    }
                }
            },
            new McpTool
            {
                Name = "detect_reusable_components",
                Description = "Scan project for reusable component patterns (repeated UI blocks)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to project directory to scan" },
                        minOccurrences = new { type = "number", description = "Minimum occurrences to be a candidate", @default = 2 },
                        minSimilarity = new { type = "number", description = "Minimum similarity score (0-1)", @default = 0.7 }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new McpTool
            {
                Name = "extract_component",
                Description = "Extract a detected component candidate into a reusable component",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        componentCandidateJson = new { type = "string", description = "JSON string of ComponentCandidate object" },
                        outputPath = new { type = "string", description = "Path where to save the extracted component" }
                    },
                    required = new[] { "componentCandidateJson", "outputPath" }
                }
            },
            new McpTool
            {
                Name = "transform_css",
                Description = "Transform CSS - extract inline styles, modernize, add variables",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Path to file with CSS/inline styles" },
                        generateVariables = new { type = "boolean", description = "Generate CSS variables", @default = true },
                        modernizeLayout = new { type = "boolean", description = "Modernize layout (Grid/Flexbox)", @default = true },
                        addResponsive = new { type = "boolean", description = "Add responsive design", @default = true },
                        addAccessibility = new { type = "boolean", description = "Add accessibility improvements", @default = true },
                        outputPath = new { type = "string", description = "Optional output CSS file path" }
                    },
                    required = new[] { "sourcePath" }
                }
            },
            new McpTool
            {
                Name = "analyze_css",
                Description = "Analyze CSS quality and get recommendations",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Path to file to analyze for CSS quality" }
                    },
                    required = new[] { "sourcePath" }
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
            "transform_page" => await TransformPageToolAsync(args, cancellationToken),
            "learn_transformation" => await LearnTransformationToolAsync(args, cancellationToken),
            "apply_transformation" => await ApplyTransformationToolAsync(args, cancellationToken),
            "list_transformation_patterns" => await ListTransformationPatternsToolAsync(args, cancellationToken),
            "detect_reusable_components" => await DetectReusableComponentsToolAsync(args, cancellationToken),
            "extract_component" => await ExtractComponentToolAsync(args, cancellationToken),
            "transform_css" => await TransformCSSToolAsync(args, cancellationToken),
            "analyze_css" => await AnalyzeCSSToolAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    private async Task<McpToolResult> TransformPageToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return ErrorResult("sourcePath is required");

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.TransformPage(
            sourcePath,
            SafeParseBool(args?.GetValueOrDefault("extractComponents"), true),
            SafeParseBool(args?.GetValueOrDefault("modernizeCSS"), true),
            SafeParseBool(args?.GetValueOrDefault("addErrorHandling"), true),
            SafeParseBool(args?.GetValueOrDefault("addLoadingStates"), true),
            SafeParseBool(args?.GetValueOrDefault("addAccessibility"), true),
            args?.GetValueOrDefault("outputDirectory")?.ToString()
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> LearnTransformationToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var exampleOldPath = args?.GetValueOrDefault("exampleOldPath")?.ToString();
        var exampleNewPath = args?.GetValueOrDefault("exampleNewPath")?.ToString();
        var patternName = args?.GetValueOrDefault("patternName")?.ToString();

        if (string.IsNullOrWhiteSpace(exampleOldPath) || string.IsNullOrWhiteSpace(exampleNewPath) || string.IsNullOrWhiteSpace(patternName))
            return ErrorResult("exampleOldPath, exampleNewPath, and patternName are required");

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.LearnTransformation(exampleOldPath, exampleNewPath, patternName);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ApplyTransformationToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var patternId = args?.GetValueOrDefault("patternId")?.ToString();
        var targetPath = args?.GetValueOrDefault("targetPath")?.ToString();

        if (string.IsNullOrWhiteSpace(patternId) || string.IsNullOrWhiteSpace(targetPath))
            return ErrorResult("patternId and targetPath are required");

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.ApplyTransformation(patternId, targetPath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ListTransformationPatternsToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString();

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.ListTransformationPatterns(context);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> DetectReusableComponentsToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var projectPath = args?.GetValueOrDefault("projectPath")?.ToString();
        if (string.IsNullOrWhiteSpace(projectPath))
            return ErrorResult("projectPath is required");

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.DetectReusableComponents(
            projectPath,
            SafeParseInt(args?.GetValueOrDefault("minOccurrences"), 2),
            (float)SafeParseDouble(args?.GetValueOrDefault("minSimilarity"), 0.7)
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ExtractComponentToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        // Handle componentCandidateJson - it can be passed as a JSON string OR as a JSON object
        string? componentCandidateJson = null;
        var candidateArg = args?.GetValueOrDefault("componentCandidateJson");
        if (candidateArg != null)
        {
            if (candidateArg is JsonElement jsonElement)
            {
                componentCandidateJson = jsonElement.GetRawText();
            }
            else if (candidateArg is string str)
            {
                componentCandidateJson = str;
            }
            else
            {
                componentCandidateJson = JsonSerializer.Serialize(candidateArg);
            }
        }
        
        var outputPath = args?.GetValueOrDefault("outputPath")?.ToString();

        if (string.IsNullOrWhiteSpace(componentCandidateJson) || string.IsNullOrWhiteSpace(outputPath))
            return ErrorResult("componentCandidateJson and outputPath are required");

        _logger.LogDebug("ExtractComponent - JSON received (first 200 chars): {Json}", 
            componentCandidateJson.Length > 200 ? componentCandidateJson[..200] + "..." : componentCandidateJson);

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.ExtractComponent(componentCandidateJson, outputPath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> TransformCSSToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return ErrorResult("sourcePath is required");

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.TransformCSS(
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
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> AnalyzeCSSToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
            return ErrorResult("sourcePath is required");

        var transformationTools = _serviceProvider.GetRequiredService<TransformationTools>();
        var result = await transformationTools.AnalyzeCSS(sourcePath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    // Helper methods
    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            _ => defaultValue
        };

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var i) => i,
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
            _ => defaultValue
        };

    private McpToolResult ErrorResult(string error) => new McpToolResult
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };
}

