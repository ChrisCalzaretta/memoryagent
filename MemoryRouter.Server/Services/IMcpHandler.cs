namespace MemoryRouter.Server.Services;

/// <summary>
/// Handler for MCP protocol integration with Cursor
/// </summary>
public interface IMcpHandler
{
    /// <summary>
    /// Get all available tool definitions for tools/list
    /// </summary>
    IEnumerable<object> GetToolDefinitions();

    /// <summary>
    /// Handle a tool call from Cursor (tools/call)
    /// </summary>
    Task<string> HandleToolCallAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken);
}

