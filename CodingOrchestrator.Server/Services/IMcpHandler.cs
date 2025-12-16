namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Handles MCP tool calls
/// </summary>
public interface IMcpHandler
{
    /// <summary>
    /// Get all available tool definitions
    /// </summary>
    IEnumerable<object> GetToolDefinitions();

    /// <summary>
    /// Handle a tool call
    /// </summary>
    Task<string> HandleToolCallAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken);
}








