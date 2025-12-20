using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Clients;

/// <summary>
/// Client for calling CodingOrchestrator MCP tools
/// </summary>
public interface ICodingOrchestratorClient
{
    /// <summary>
    /// Call a CodingOrchestrator tool
    /// </summary>
    Task<object> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all available tools from CodingOrchestrator
    /// </summary>
    Task<IEnumerable<McpToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default);
}


