using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Interface for MCP tool category handlers
/// Each category (indexing, search, validation, etc.) has its own handler
/// </summary>
public interface IMcpToolHandler
{
    /// <summary>
    /// Gets the tools this handler supports
    /// </summary>
    IEnumerable<McpTool> GetTools();

    /// <summary>
    /// Handles a tool call for this category
    /// </summary>
    Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default);
}

