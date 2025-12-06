using System.Text.Json;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services.Mcp;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Orchestrator for all MCP tools - delegates to specialized handlers
/// Refactored from 2979 lines to ~150 lines using Strategy Pattern
/// </summary>
public class McpService : IMcpService
{
    private readonly IEnumerable<IMcpToolHandler> _handlers;
    private readonly ILogger<McpService> _logger;
    private readonly Dictionary<string, IMcpToolHandler> _toolHandlerMap;

    public McpService(
        IEnumerable<IMcpToolHandler> handlers,
        ILogger<McpService> logger)
    {
        _handlers = handlers;
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

        _logger.LogInformation("🎯 MCP Service initialized with {HandlerCount} handlers and {ToolCount} tools",
            handlers.Count(), _toolHandlerMap.Count);
    }

    public async Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔧 MCP Tool Call: {ToolName}", toolCall.Name);

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
            _logger.LogInformation("✅ MCP Tool '{ToolName}' completed {Status}",
                toolCall.Name, result.IsError ? "with errors" : "successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ MCP Tool '{ToolName}' failed with exception", toolCall.Name);
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
            // Parse tool call params from request
            var paramsJson = JsonSerializer.Serialize(request.Params);
            var callParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson);

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
