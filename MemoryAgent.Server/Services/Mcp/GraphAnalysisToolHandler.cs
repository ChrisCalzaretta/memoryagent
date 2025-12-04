using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for graph-based code analysis
/// Tools: impact_analysis, dependency_chain, find_circular_dependencies
/// </summary>
public class GraphAnalysisToolHandler : IMcpToolHandler
{
    private readonly IGraphService _graphService;
    private readonly ILogger<GraphAnalysisToolHandler> _logger;

    public GraphAnalysisToolHandler(
        IGraphService graphService,
        ILogger<GraphAnalysisToolHandler> logger)
    {
        _graphService = graphService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "impact_analysis",
                Description = "Analyze what code would be impacted if a class changes",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name" }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "dependency_chain",
                Description = "Get the dependency chain for a class",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name" },
                        maxDepth = new { type = "number", description = "Maximum depth", @default = 5 }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "find_circular_dependencies",
                Description = "Find circular dependencies in code",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Optional context to search within" }
                    }
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
            "impact_analysis" => await ImpactAnalysisToolAsync(args, cancellationToken),
            "dependency_chain" => await DependencyChainToolAsync(args, cancellationToken),
            "find_circular_dependencies" => await CircularDependenciesToolAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    private async Task<McpToolResult> ImpactAnalysisToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();

        if (string.IsNullOrWhiteSpace(className))
            return ErrorResult("className is required");

        var impacted = await _graphService.GetImpactAnalysisAsync(className, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Classes impacted by changes to {className}:\n" + 
                           string.Join("\n", impacted.Select(c => $"- {c}"))
                }
            }
        };
    }

    private async Task<McpToolResult> DependencyChainToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();
        var maxDepth = args?.GetValueOrDefault("maxDepth") as int? ?? 5;

        if (string.IsNullOrWhiteSpace(className))
            return ErrorResult("className is required");

        var dependencies = await _graphService.GetDependencyChainAsync(className, maxDepth, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Dependencies for {className}:\n" + 
                           string.Join("\n", dependencies.Select(d => $"- {d}"))
                }
            }
        };
    }

    private async Task<McpToolResult> CircularDependenciesToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString();

        var cycles = await _graphService.FindCircularDependenciesAsync(context, ct);
        
        if (!cycles.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = "No circular dependencies found!" }
                }
            };
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Found {cycles.Count} circular dependency cycles:\n" +
                           string.Join("\n\n", cycles.Select((cycle, i) => 
                               $"Cycle {i + 1}: {string.Join(" → ", cycle)}"))
                }
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

