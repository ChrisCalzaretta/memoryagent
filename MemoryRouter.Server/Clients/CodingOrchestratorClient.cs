using System.Text;
using System.Text.Json;
using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Clients;

/// <summary>
/// HTTP client for calling CodingAgent MCP tools (was CodingOrchestrator)
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
        _logger.LogInformation("🔍 Fetching tools from CodingAgent...");

        try
        {
            // CodingAgent uses REST endpoints, not JSON-RPC for tools/list
            var response = await _httpClient.GetAsync("/api/mcp/tools", cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<McpToolsListResponse>(responseJson, _jsonOptions);

            if (result?.Tools == null)
            {
                throw new InvalidOperationException("CodingAgent returned null tools list");
            }

            _logger.LogInformation("✅ Fetched {Count} tools from CodingAgent", result.Tools.Count);
            return result.Tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to fetch tools from CodingAgent");
            throw;
        }
    }

    public async Task<object> CallToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📞 Calling CodingAgent tool: {Tool}", toolName);

        try
        {
            // CodingAgent expects simple format: { "Name": "tool", "Arguments": {...} }
            var request = new
            {
                Name = toolName,
                Arguments = arguments
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/mcp/call", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Response format: { "content": [{ "type": "text", "text": "..." }] }
            var result = JsonSerializer.Deserialize<CodingOrchestratorResponse>(responseJson, _jsonOptions);

            if (result?.Content == null || result.Content.Length == 0)
            {
                throw new InvalidOperationException($"CodingAgent returned null result for {toolName}");
            }

            _logger.LogInformation("✅ CodingAgent {Tool} completed successfully", toolName);
            return result.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to call CodingAgent tool: {Tool}", toolName);
            throw;
        }
    }

    private class McpToolsListResponse
    {
        public List<McpToolDefinition> Tools { get; set; } = new();
    }

    private class CodingOrchestratorResponse
    {
        public ContentItem[] Content { get; set; } = Array.Empty<ContentItem>();
        public bool? IsError { get; set; }
    }

    private class ContentItem
    {
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
    }
}


