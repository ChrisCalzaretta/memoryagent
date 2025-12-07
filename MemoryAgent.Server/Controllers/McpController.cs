using System.Text;
using System.Text.Json;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MemoryAgent.Server.Controllers;

[ApiController]
[Route("")]
public class McpController : ControllerBase
{
    private readonly IMcpService _mcpService;
    private readonly ILogger<McpController> _logger;

    public McpController(IMcpService mcpService, ILogger<McpController> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    /// <summary>
    /// MCP Server-Sent Events endpoint for Cursor integration
    /// </summary>
    [HttpPost("sse")]
    public async Task SseEndpoint(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        _logger.LogInformation("SSE connection established");

        try
        {
            // Read the entire request body
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync(cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                _logger.LogInformation("Received MCP request: {Request}", requestBody);

                try
                {
                    var mcpRequest = JsonSerializer.Deserialize<McpRequest>(requestBody);
                    if (mcpRequest != null)
                    {
                        var response = await _mcpService.HandleRequestAsync(mcpRequest, cancellationToken);
                        
                        // Only send a response if not null (notifications return null)
                        if (response != null)
                        {
                            // Send JSON-RPC response as SSE data event
                            var json = JsonSerializer.Serialize(response);
                            var message = $"data: {json}\n\n";
                            var bytes = Encoding.UTF8.GetBytes(message);
                            
                            await Response.Body.WriteAsync(bytes, cancellationToken);
                            await Response.Body.FlushAsync(cancellationToken);
                            
                            _logger.LogInformation("Sent MCP response: {Response}", json);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing MCP request");
                    
                    var errorResponse = new McpResponse
                    {
                        Id = 0,
                        Error = new McpError
                        {
                            Code = -32603,
                            Message = $"Internal error: {ex.Message}"
                        }
                    };
                    
                    var json = JsonSerializer.Serialize(errorResponse);
                    var message = $"data: {json}\n\n";
                    var bytes = Encoding.UTF8.GetBytes(message);
                    
                    await Response.Body.WriteAsync(bytes, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSE connection error");
        }
        finally
        {
            _logger.LogInformation("SSE connection closed");
        }
    }

    /// <summary>
    /// HTTP POST endpoint for MCP requests (alternative to SSE)
    /// Also available at /api/mcp/call for backward compatibility
    /// </summary>
    [HttpPost("mcp")]
    [HttpPost("api/mcp/call")]
    public async Task<ActionResult> McpEndpoint(
        [FromBody] McpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _mcpService.HandleRequestAsync(request, cancellationToken);
            
            // Notifications return null and get no response
            if (response == null)
            {
                return NoContent();
            }
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            return StatusCode(500, new McpResponse
            {
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
    /// Get available MCP tools
    /// </summary>
    [HttpGet("tools")]
    public async Task<ActionResult<List<McpTool>>> GetTools(CancellationToken cancellationToken)
    {
        var tools = await _mcpService.GetToolsAsync(cancellationToken);
        return Ok(tools);
    }

    private async Task SendSseEvent(string eventType, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var message = $"event: {eventType}\ndata: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(message);
        
        await Response.Body.WriteAsync(bytes);
        await Response.Body.FlushAsync();
    }
}


