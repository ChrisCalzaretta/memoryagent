using System.Diagnostics;
using System.Text.Json;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services.Mcp;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Orchestrator for all MCP tools - delegates to specialized handlers
/// Refactored from 2979 lines to ~150 lines using Strategy Pattern
/// Now includes Agent Lightning tool usage tracking
/// </summary>
public class McpService : IMcpService
{
    private readonly IEnumerable<IMcpToolHandler> _handlers;
    private readonly ILearningService _learningService;
    private readonly ILogger<McpService> _logger;
    private readonly Dictionary<string, IMcpToolHandler> _toolHandlerMap;
    
    // Tools that should NOT be tracked to avoid infinite loops
    private static readonly HashSet<string> _excludedFromTracking = new()
    {
        "get_tool_usage",
        "get_popular_tools", 
        "get_recent_tool_invocations",
        "get_tool_patterns"
    };

    public McpService(
        IEnumerable<IMcpToolHandler> handlers,
        ILearningService learningService,
        ILogger<McpService> logger)
    {
        _handlers = handlers;
        _learningService = learningService;
        _logger = logger;

        // Build tool→handler mapping for O(1) lookup
        _toolHandlerMap = new Dictionary<string, IMcpToolHandler>();
        foreach (var handler in handlers)
        {
            foreach (var tool in handler.GetTools())
            {
                _toolHandlerMap[tool.Name] = handler;
            }
        }

        _logger.LogInformation("🎯 MCP Service initialized with {HandlerCount} handlers and {ToolCount} tools (with Agent Lightning tracking)",
            handlers.Count(), _toolHandlerMap.Count);
    }

    public async Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔧 MCP Tool Call: {ToolName}", toolCall.Name);
        var stopwatch = Stopwatch.StartNew();

        if (!_toolHandlerMap.TryGetValue(toolCall.Name, out var handler))
        {
            _logger.LogWarning("❌ Unknown MCP tool: {ToolName}", toolCall.Name);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error: Unknown tool '{toolCall.Name}'\n\n" +
                               $"Available tools:\n{string.Join("\n", _toolHandlerMap.Keys.OrderBy(k => k).Select(k => $"  • {k}"))}"
                    }
                }
            };
        }

        try
        {
            var result = await handler.HandleToolAsync(toolCall.Name, toolCall.Arguments, cancellationToken);
            stopwatch.Stop();
            
            _logger.LogInformation("✅ MCP Tool '{ToolName}' completed {Status} in {Duration}ms",
                toolCall.Name, result.IsError ? "with errors" : "successfully", stopwatch.ElapsedMilliseconds);
            
            // Track tool usage with Agent Lightning (except for tracking tools themselves)
            if (!_excludedFromTracking.Contains(toolCall.Name))
            {
                await TrackToolInvocationAsync(toolCall, result, stopwatch.ElapsedMilliseconds, cancellationToken);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "❌ MCP Tool '{ToolName}' failed with exception in {Duration}ms", 
                toolCall.Name, stopwatch.ElapsedMilliseconds);
            
            // Track failed invocation
            if (!_excludedFromTracking.Contains(toolCall.Name))
            {
                await TrackToolInvocationAsync(toolCall, null, stopwatch.ElapsedMilliseconds, cancellationToken, ex.Message);
            }
            
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error executing tool '{toolCall.Name}': {ex.Message}\n\n{ex.StackTrace}"
                    }
                }
            };
        }
    }
    
    private async Task TrackToolInvocationAsync(
        McpToolCall toolCall, 
        McpToolResult? result, 
        long durationMs, 
        CancellationToken cancellationToken,
        string? errorMessage = null)
    {
        try
        {
            // Extract context and query from arguments
            var context = toolCall.Arguments?.GetValueOrDefault("context")?.ToString();
            var query = ExtractQueryFromArguments(toolCall.Name, toolCall.Arguments);
            var sessionId = toolCall.Arguments?.GetValueOrDefault("sessionId")?.ToString();
            
            // Extract result summary and count
            string? resultSummary = null;
            int? resultCount = null;
            
            if (result != null && !result.IsError && result.Content?.Any() == true)
            {
                var textContent = result.Content.FirstOrDefault(c => c.Type == "text")?.Text;
                if (textContent != null)
                {
                    resultSummary = textContent.Length > 200 ? textContent[..200] + "..." : textContent;
                    
                    // Try to extract result count from common patterns
                    if (textContent.Contains(" results") || textContent.Contains(" found"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(textContent, @"(\d+)\s+(results?|found|items?|files?|patterns?)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                        {
                            resultCount = count;
                        }
                    }
                }
            }
            
            await _learningService.RecordToolInvocationAsync(
                toolCall.Name,
                context,
                sessionId,
                query,
                toolCall.Arguments,
                errorMessage == null && (result?.IsError != true),
                errorMessage ?? (result?.IsError == true ? "Tool returned error" : null),
                durationMs,
                resultSummary,
                resultCount,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't let tracking failures break the tool call
            _logger.LogWarning(ex, "Failed to track tool invocation for {ToolName}", toolCall.Name);
        }
    }
    
    private static string? ExtractQueryFromArguments(string toolName, Dictionary<string, object>? args)
    {
        if (args == null) return null;
        
        // Different tools use different parameter names for the query/question
        var queryKeys = new[] { "query", "question", "search", "filePath", "path", "className", "pattern_id" };
        
        foreach (var key in queryKeys)
        {
            if (args.TryGetValue(key, out var value) && value != null)
            {
                var strValue = value.ToString();
                if (!string.IsNullOrWhiteSpace(strValue))
                    return strValue;
            }
        }
        
        return null;
    }

    public async Task<List<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var allTools = _handlers
            .SelectMany(handler => handler.GetTools())
            .OrderBy(tool => tool.Name)
            .ToList();

        _logger.LogInformation("📋 Listing {Count} MCP tools", allTools.Count);
        return allTools;
    }

    public async Task<McpResponse?> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📥 MCP Request: {Method}", request.Method);

        return request.Method switch
        {
            "initialize" => HandleInitializeAsync(request),
            "notifications/initialized" => null, // Client notification, no response needed
            "tools/list" => await HandleListToolsAsync(request, cancellationToken),
            "tools/call" => await HandleCallToolAsync(request, cancellationToken),
            _ => new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}"
                }
            }
        };
    }

    private McpResponse HandleInitializeAsync(McpRequest request)
    {
        _logger.LogInformation("🤝 MCP Initialize handshake");
        
        return new McpResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { listChanged = true }
                },
                serverInfo = new
                {
                    name = "memory-code-agent",
                    version = "1.0.0"
                }
            }
        };
    }

    private async Task<McpResponse> HandleListToolsAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var tools = await GetToolsAsync(cancellationToken);

        return new McpResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = new
            {
                tools = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = t.InputSchema
                })
            }
        };
    }

    private async Task<McpResponse> HandleCallToolAsync(McpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Parse tool call params from request (case-insensitive for JSON property names)
            var paramsJson = JsonSerializer.Serialize(request.Params);
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var callParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, jsonOptions);

            if (callParams == null || string.IsNullOrWhiteSpace(callParams.Name))
            {
                return new McpResponse
                {
                    JsonRpc = "2.0",
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32602,
                        Message = "Invalid params: 'name' is required"
                    }
                };
            }

            var result = await CallToolAsync(new McpToolCall { Name = callParams.Name, Arguments = callParams.Arguments }, cancellationToken);

            return new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Result = new
                {
                    content = result.Content,
                    isError = result.IsError
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call");
            return new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                }
            };
        }
    }

    private class ToolCallParams
    {
        public string Name { get; set; } = "";
        public Dictionary<string, object>? Arguments { get; set; }
    }
}
