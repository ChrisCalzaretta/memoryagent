using System.Text.Json;
using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Services;

/// <summary>
/// MCP handler that exposes MemoryRouter to Cursor IDE
/// Single entry point: execute_task (FunctionGemma figures out the rest)
/// </summary>
public class McpHandler : IMcpHandler
{
    private readonly IRouterService _routerService;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<McpHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public McpHandler(
        IRouterService routerService,
        IToolRegistry toolRegistry,
        ILogger<McpHandler> logger)
    {
        _routerService = routerService;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public IEnumerable<object> GetToolDefinitions()
    {
        var tools = new List<object>
        {
            // Primary entry point - FunctionGemma-powered smart routing
            new Dictionary<string, object>
            {
                ["name"] = "execute_task",
                ["description"] = @"🧠 **Smart AI Router** - Single entry point for ANY development task. 

Uses FunctionGemma to automatically figure out which tools to call and in what order.

**What it does:**
- Analyzes your natural language request
- Searches for existing code/patterns when needed
- Generates code in any language
- Validates and checks quality
- Creates designs and brands
- Plans and breaks down complex tasks

**Examples:**
- ""Create a REST API for users with authentication""
- ""Find all code that handles database transactions""
- ""Generate a React dashboard with charts""
- ""Design a brand system for my fintech app""
- ""Explain how the authentication system works""

Just describe what you want - the router figures out the rest!",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["request"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Your task in natural language (e.g., 'Create a user service', 'Find authentication code', 'Design a dark mode theme')"
                        },
                        ["context"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Optional project context name for memory/continuity"
                        },
                        ["workspacePath"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Optional workspace path for file operations"
                        }
                    },
                    ["required"] = new[] { "request" }
                }
            },

            // Discovery tool - see what's available
            new Dictionary<string, object>
            {
                ["name"] = "list_available_tools",
                ["description"] = "List all tools that MemoryRouter can use (from MemoryAgent and CodingOrchestrator). Shows capabilities of the system.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["category"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Optional: filter by category (search, code, design, plan, validate)",
                            ["enum"] = new[] { "all", "search", "code", "design", "plan", "validate" }
                        }
                    }
                }
            }
        };

        return tools;
    }

    public async Task<string> HandleToolCallAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🎯 MCP tool call: {Tool}", toolName);

        return toolName switch
        {
            "execute_task" => await HandleExecuteTaskAsync(arguments, cancellationToken),
            "list_available_tools" => HandleListAvailableTools(arguments),
            _ => $"❌ Unknown tool: {toolName}"
        };
    }

    private async Task<string> HandleExecuteTaskAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        var request = GetStringArg(arguments, "request");
        if (string.IsNullOrEmpty(request))
        {
            return "❌ Error: 'request' parameter is required";
        }

        var context = new Dictionary<string, object>();
        
        if (arguments.TryGetValue("context", out var ctxValue) && ctxValue != null)
        {
            context["context"] = ctxValue.ToString() ?? string.Empty;
        }
        
        if (arguments.TryGetValue("workspacePath", out var wsValue) && wsValue != null)
        {
            context["workspacePath"] = wsValue.ToString() ?? string.Empty;
        }

        _logger.LogInformation("🚀 Executing task: {Request}", request);

        try
        {
            var result = await _routerService.ExecuteRequestAsync(request, context, cancellationToken);

            // Format result for display
            var output = new System.Text.StringBuilder();
            
            if (result.Success)
            {
                output.AppendLine("# ✅ Task Completed Successfully");
                output.AppendLine();
                output.AppendLine($"**Request:** {result.OriginalRequest}");
                output.AppendLine($"**Duration:** {result.TotalDurationMs}ms");
                output.AppendLine();
                
                output.AppendLine("## 🤖 FunctionGemma's Plan");
                output.AppendLine($"> {result.Plan.Reasoning}");
                output.AppendLine();
                
                output.AppendLine("## 📋 Execution Steps");
                output.AppendLine();
                foreach (var step in result.Steps)
                {
                    var icon = step.Success ? "✅" : "❌";
                    output.AppendLine($"{icon} **{step.ToolName}** ({step.DurationMs}ms)");
                    
                    if (!step.Success && step.Error != null)
                    {
                        output.AppendLine($"   ❌ Error: {step.Error}");
                    }
                }
                output.AppendLine();
                
                output.AppendLine("## 🎯 Final Result");
                output.AppendLine();
                output.AppendLine(result.FinalResult ?? "Task completed");
            }
            else
            {
                output.AppendLine("# ❌ Task Failed");
                output.AppendLine();
                output.AppendLine($"**Request:** {result.OriginalRequest}");
                output.AppendLine($"**Error:** {result.Error}");
                output.AppendLine();
                
                if (result.Steps.Any())
                {
                    output.AppendLine("## Completed Steps:");
                    foreach (var step in result.Steps.Where(s => s.Success))
                    {
                        output.AppendLine($"- ✅ {step.ToolName}");
                    }
                }
            }

            return output.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute task");
            return $"❌ Error executing task: {ex.Message}";
        }
    }

    private string HandleListAvailableTools(Dictionary<string, object> arguments)
    {
        var category = GetStringArg(arguments, "category", "all").ToLowerInvariant();
        var tools = _toolRegistry.GetAllTools();

        // Filter by category if specified
        if (category != "all")
        {
            tools = category switch
            {
                "search" => tools.Where(t => t.Keywords.Any(k => k.Contains("search") || k.Contains("find"))),
                "code" => tools.Where(t => t.Keywords.Any(k => k.Contains("generate") || k.Contains("code") || k.Contains("build"))),
                "design" => tools.Where(t => t.Keywords.Any(k => k.Contains("design") || k.Contains("brand") || k.Contains("UI"))),
                "plan" => tools.Where(t => t.Keywords.Any(k => k.Contains("plan") || k.Contains("todo"))),
                "validate" => tools.Where(t => t.Keywords.Any(k => k.Contains("validate") || k.Contains("check"))),
                _ => tools
            };
        }

        var output = new System.Text.StringBuilder();
        output.AppendLine($"# 🛠️ Available Tools ({tools.Count()})");
        output.AppendLine();

        var groupedTools = tools.GroupBy(t => t.Service);
        
        foreach (var group in groupedTools)
        {
            var serviceIcon = group.Key == "memory-agent" ? "🧠" : "🎯";
            output.AppendLine($"## {serviceIcon} {group.Key} ({group.Count()} tools)");
            output.AppendLine();

            foreach (var tool in group.OrderBy(t => t.Name))
            {
                output.AppendLine($"### `{tool.Name}`");
                output.AppendLine(tool.Description);
                
                if (tool.UseCases.Any())
                {
                    output.AppendLine($"**Use Cases:** {string.Join(", ", tool.UseCases)}");
                }
                
                output.AppendLine();
            }
        }

        return output.ToString();
    }

    private static string GetStringArg(Dictionary<string, object> args, string key, string defaultValue = "")
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.GetString() ?? defaultValue;
            }
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }
}

