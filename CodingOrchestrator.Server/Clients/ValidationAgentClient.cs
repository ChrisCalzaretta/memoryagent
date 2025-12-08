using System.Text.Json;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// HTTP client for ValidationAgent.Server
/// </summary>
public class ValidationAgentClient : IValidationAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ValidationAgentClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ValidationAgentClient(HttpClient httpClient, ILogger<ValidationAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ValidateCodeResponse> ValidateAsync(ValidateCodeRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling ValidationAgent to validate {FileCount} files", request.Files.Count);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agent/validate", request, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ValidateCodeResponse>(JsonOptions, cancellationToken);
            return result ?? new ValidateCodeResponse { Passed = false, Score = 0, Summary = "Empty response from ValidationAgent" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ValidationAgent validate");
            return new ValidateCodeResponse { Passed = false, Score = 0, Summary = ex.Message };
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




