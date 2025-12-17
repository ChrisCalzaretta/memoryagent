using Microsoft.AspNetCore.Mvc;
using CodingOrchestrator.Server.Services;
using System.Text.Json;

namespace CodingOrchestrator.Server.Controllers;

[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly IMcpHandler _mcpHandler;
    private readonly ILogger<McpController> _logger;

    public McpController(
        IMcpHandler mcpHandler,
        ILogger<McpController> logger)
    {
        _mcpHandler = mcpHandler;
        _logger = logger;
    }

    /// <summary>
    /// List available MCP tools
    /// </summary>
    [HttpGet("tools")]
    public ActionResult<object> ListTools()
    {
        var tools = _mcpHandler.GetToolDefinitions();
        return Ok(new { tools });
    }

    /// <summary>
    /// Call an MCP tool
    /// </summary>
    [HttpPost("call")]
    public async Task<ActionResult<object>> CallTool(
        [FromBody] McpToolCallRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP tool call: {Tool}", request.Name);

        try
        {
            var result = await _mcpHandler.HandleToolCallAsync(
                request.Name, 
                request.Arguments ?? new Dictionary<string, object>(),
                cancellationToken);

            return Ok(new
            {
                content = new[]
                {
                    new { type = "text", text = result }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool: {Tool}", request.Name);
            return Ok(new
            {
                content = new[]
                {
                    new { type = "text", text = $"Error: {ex.Message}" }
                },
                isError = true
            });
        }
    }
}

public class McpToolCallRequest
{
    public required string Name { get; set; }
    public Dictionary<string, object>? Arguments { get; set; }
}










