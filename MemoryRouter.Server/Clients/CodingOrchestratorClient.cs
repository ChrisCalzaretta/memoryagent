using System.Text;
using System.Text.Json;
using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Clients;

/// <summary>
/// HTTP client for calling CodingOrchestrator MCP tools
/// </summary>
public class CodingOrchestratorClient : ICodingOrchestratorClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CodingOrchestratorClient> _logger;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CodingOrchestratorClient(HttpClient httpClient, ILogger<CodingOrchestratorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<McpToolDefinition>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Fetching tools from CodingOrchestrator...");

        try
        {
            // CodingOrchestrator uses REST endpoints, not JSON-RPC for tools/list
            var response = await _httpClient.GetAsync("/api/mcp/tools", cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<McpToolsListResponse>(responseJson, _jsonOptions);

            if (result?.Tools == null)
            {
                throw new InvalidOperationException("CodingOrchestrator returned null tools list");
            }

            _logger.LogInformation("✅ Fetched {Count} tools from CodingOrchestrator", result.Tools.Count);
            return result.Tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to fetch tools from CodingOrchestrator");
            throw;
        }
    }

    public async Task<object> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📞 Calling CodingOrchestrator tool: {Tool}", toolName);

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

            var response = await _httpClient.PostAsync("/api/mcp/call", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<McpResponse>(responseJson, _jsonOptions);

            if (result?.Result == null)
            {
                throw new InvalidOperationException($"CodingOrchestrator returned null result for {toolName}");
            }

            _logger.LogInformation("✅ CodingOrchestrator {Tool} completed successfully", toolName);
            return result.Result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to call CodingOrchestrator tool: {Tool}", toolName);
            throw;
        }
    }

    private class McpToolsListResponse
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

