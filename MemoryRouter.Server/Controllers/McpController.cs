using Microsoft.AspNetCore.Mvc;
using MemoryRouter.Server.Services;
using System.Text.Json;

namespace MemoryRouter.Server.Controllers;

/// <summary>
/// MCP protocol controller for Cursor IDE integration
/// </summary>
[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly IMcpHandler _mcpHandler;
    private readonly ILogger<McpController> _logger;

    public McpController(IMcpHandler mcpHandler, ILogger<McpController> logger)
    {
        _mcpHandler = mcpHandler;
        _logger = logger;
    }

    /// <summary>
    /// Generic MCP endpoint - routes based on method in JSON-RPC request
    /// This is the main entry point for MCP clients
    /// </summary>
    [HttpPost("")]
    public async Task<ActionResult<McpResponse>> HandleMcpRequest(
        [FromBody] McpRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("📞 MCP request: {Method}", request.Method);

        try
        {
            return request.Method switch
            {
                "initialize" => Ok(new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id,
                    Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { tools = new { } },
                        serverInfo = new
                        {
                            name = "memory-router",
                            version = "1.0.0"
                        }
                    }
                }),

                "tools/list" => ListTools(request),

                "tools/call" => await CallTool(request, cancellationToken),

                _ => BadRequest(new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32601,
                        Message = $"Method not found: {request.Method}"
                    }
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle MCP request: {Method}", request.Method);
            return StatusCode(500, new McpResponse
            {
                Jsonrpc = "2.0",
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                }
            });
        }
    }

    /// <summary>
    /// MCP tools/list endpoint - returns available tools
    /// </summary>
    [HttpPost("tools/list")]
    public ActionResult<McpResponse> ListTools([FromBody] McpRequest request)
    {
        _logger.LogInformation("📋 MCP tools/list called");

        try
        {
            var tools = _mcpHandler.GetToolDefinitions();

            return Ok(new McpResponse
            {
                Jsonrpc = "2.0",
                Id = request.Id,
                Result = new { tools }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list tools");
            return StatusCode(500, new McpResponse
            {
                Jsonrpc = "2.0",
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                }
            });
        }
    }

    /// <summary>
    /// MCP tools/call endpoint - executes a tool
    /// </summary>
    [HttpPost("tools/call")]
    public async Task<ActionResult<McpResponse>> CallTool(
        [FromBody] McpRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔧 MCP tools/call: {Method}", request.Method);

        try
        {
            if (request.Params?.Name == null)
            {
                return BadRequest(new McpResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32602,
                        Message = "Missing tool name"
                    }
                });
            }

            var result = await _mcpHandler.HandleToolCallAsync(
                request.Params.Name,
                request.Params.Arguments ?? new Dictionary<string, object>(),
                cancellationToken
            );

            return Ok(new McpResponse
            {
                Jsonrpc = "2.0",
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = result
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call tool");
            return StatusCode(500, new McpResponse
            {
                Jsonrpc = "2.0",
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                }
            });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("/health")]
    public ActionResult Health()
    {
        return Ok(new { status = "healthy", service = "MemoryRouter" });
    }
}

// MCP protocol models
public class McpRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public object? Id { get; set; }  // Can be string or number
    public string? Method { get; set; }
    public McpParams? Params { get; set; }
}

public class McpParams
{
    public string? Name { get; set; }
    public Dictionary<string, object>? Arguments { get; set; }
}

public class McpResponse
{
    public string Jsonrpc { get; set; } = "2.0";
    public object? Id { get; set; }  // Can be string or number
    public object? Result { get; set; }
    public McpError? Error { get; set; }
}

public class McpError
{
    public int Code { get; set; }
    public string? Message { get; set; }
}

