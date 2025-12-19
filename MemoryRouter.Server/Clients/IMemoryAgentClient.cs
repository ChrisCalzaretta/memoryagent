using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Clients;

/// <summary>
/// Client for calling MemoryAgent MCP tools
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Call a MemoryAgent tool
    /// </summary>
    Task<object> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all available tools from MemoryAgent
    /// </summary>
    Task<IEnumerable<McpToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default);
}

