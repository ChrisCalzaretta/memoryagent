using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for handling MCP protocol operations
/// </summary>
public interface IMcpService
{
    /// <summary>
    /// Get list of available MCP tools
    /// </summary>
    Task<List<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Call an MCP tool
    /// </summary>
    Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handle an MCP request (returns null for notifications)
    /// </summary>
    Task<McpResponse?> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken = default);
}


