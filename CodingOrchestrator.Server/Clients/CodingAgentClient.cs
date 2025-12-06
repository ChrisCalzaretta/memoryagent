using System.Text.Json;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// HTTP client for CodingAgent.Server
/// </summary>
public class CodingAgentClient : ICodingAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CodingAgentClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public CodingAgentClient(HttpClient httpClient, ILogger<CodingAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<GenerateCodeResponse> GenerateAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling CodingAgent to generate code for: {Task}", request.Task);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agent/generate", request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GenerateCodeResponse>(JsonOptions, cancellationToken);
            return result ?? new GenerateCodeResponse { Success = false, Error = "Empty response from CodingAgent" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling CodingAgent generate");
            return new GenerateCodeResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<GenerateCodeResponse> FixAsync(GenerateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling CodingAgent to fix code for: {Task}", request.Task);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agent/fix", request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GenerateCodeResponse>(JsonOptions, cancellationToken);
            return result ?? new GenerateCodeResponse { Success = false, Error = "Empty response from CodingAgent" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling CodingAgent fix");
            return new GenerateCodeResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}



