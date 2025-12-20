using System.Text;
using System.Text.Json;
using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Clients;

/// <summary>
/// HTTP client for calling MemoryAgent MCP tools
/// </summary>
public class MemoryAgentClient : IMemoryAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MemoryAgentClient> _logger;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MemoryAgentClient(HttpClient httpClient, ILogger<MemoryAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<McpToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Fetching tools from MemoryAgent...");

        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/list",
                @params = new { }
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/mcp", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<McpToolsListResponse>(responseJson, _jsonOptions);

            if (result?.Result?.Tools == null)
            {
                throw new InvalidOperationException("MemoryAgent returned null tools list");
            }

            _logger.LogInformation("✅ Fetched {Count} tools from MemoryAgent", result.Result.Tools.Count);
            return result.Result.Tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to fetch tools from MemoryAgent");
            throw;
        }
    }

    public async Task<object> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📞 Calling MemoryAgent tool: {Tool}", toolName);

        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = arguments
                }
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/mcp", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<McpResponse>(responseJson, _jsonOptions);

            if (result?.Result == null)
            {
                throw new InvalidOperationException($"MemoryAgent returned null result for {toolName}");
            }

            _logger.LogInformation("✅ MemoryAgent {Tool} completed successfully", toolName);
            return result.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to call MemoryAgent tool: {Tool}", toolName);
            throw;
        }
    }

    private class McpToolsListResponse
    {
        public McpToolsListResult? Result { get; set; }
    }

    private class McpToolsListResult
    {
        public List<McpToolDefinition> Tools { get; set; } = new();
    }

    private class McpResponse
    {
        public object? Result { get; set; }
        public McpError? Error { get; set; }
    }

    private class McpError
    {
        public int Code { get; set; }
        public string? Message { get; set; }
    }
}


