using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Registry of all available tools from MemoryAgent and CodingOrchestrator
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Initialize the registry by discovering tools from all services
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available tools
    /// </summary>
    IEnumerable<ToolDefinition> GetAllTools();

    /// <summary>
    /// Get a specific tool by name
    /// </summary>
    ToolDefinition? GetTool(string name);

    /// <summary>
    /// Search tools by keywords or description
    /// </summary>
    IEnumerable<ToolDefinition> SearchTools(string query);
}

