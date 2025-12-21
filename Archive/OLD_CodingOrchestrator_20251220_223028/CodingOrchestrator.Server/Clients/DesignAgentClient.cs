using System.Text.Json;

namespace CodingOrchestrator.Server.Clients;

public interface IDesignAgentClient
{
    Task<BrandContext?> GetBrandAsync(string context, CancellationToken cancellationToken = default);
    Task<List<BrandSummary>> ListBrandsAsync(CancellationToken cancellationToken = default);
    Task<DesignValidationResult> ValidateAsync(string context, string code, CancellationToken cancellationToken = default);
    Task<DesignValidationResult> ValidateFilesAsync(string context, Dictionary<string, string> files, CancellationToken cancellationToken = default);
    Task<string> GetQuestionnaireMarkdownAsync(CancellationToken cancellationToken = default);
    Task<BrandContext> CreateBrandAsync(Dictionary<string, object> answers, CancellationToken cancellationToken = default);
    Task<BrandContext> UpdateBrandAsync(string context, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
}

public class DesignAgentClient : IDesignAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DesignAgentClient> _logger;

    public DesignAgentClient(HttpClient httpClient, ILogger<DesignAgentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BrandContext?> GetBrandAsync(string context, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/design/brand/{context}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get brand {Context}: {Status}", context, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<BrandContext>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting brand {Context}", context);
            return null;
        }
    }

    public async Task<DesignValidationResult> ValidateAsync(string context, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/design/validate", new
            {
                context,
                code
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new DesignValidationResult
                {
                    IsCompliant = true, // Graceful degradation - don't block on validation failure
                    Score = 0,
                    Grade = "?",
                    Issues = new List<DesignIssue>
                    {
                        new() { Type = "error", Message = $"Validation service returned {response.StatusCode}" }
                    }
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<DesignValidationResult>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            }) ?? new DesignValidationResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating design for {Context}", context);
            return new DesignValidationResult
            {
                IsCompliant = true, // Graceful degradation
                Score = 0,
                Issues = new List<DesignIssue>
                {
                    new() { Type = "error", Message = ex.Message }
                }
            };
        }
    }

    public async Task<DesignValidationResult> ValidateFilesAsync(string context, Dictionary<string, string> files, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/design/validate/files", new
            {
                context,
                files
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new DesignValidationResult { IsCompliant = true, Score = 0 };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<DesignValidationResult>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            }) ?? new DesignValidationResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating files for {Context}", context);
            return new DesignValidationResult { IsCompliant = true, Score = 0 };
        }
    }

    public async Task<string> GetQuestionnaireMarkdownAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/design/brand/questionnaire/markdown", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            return "Error loading questionnaire";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting questionnaire");
            return "Error loading questionnaire";
        }
    }

    public async Task<BrandContext> CreateBrandAsync(Dictionary<string, object> answers, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/design/brand/create", new
        {
            answers
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<BrandContext>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        }) ?? throw new InvalidOperationException("Failed to create brand");
    }

    public async Task<List<BrandSummary>> ListBrandsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/design/brand", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<BrandSummary>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<List<BrandSummary>>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            }) ?? new List<BrandSummary>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing brands");
            return new List<BrandSummary>();
        }
    }

    public async Task<BrandContext> UpdateBrandAsync(string context, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/design/brand/{context}", updates, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<BrandContext>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        }) ?? throw new InvalidOperationException("Failed to update brand");
    }
}

// DTOs for Design Agent communication - using JsonElement for flexibility
public class BrandContext
{
    public string Context { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public JsonElement? Tokens { get; set; }
    public JsonElement? Components { get; set; }
    public JsonElement? Voice { get; set; }
    public JsonElement? Accessibility { get; set; }
    public JsonElement? Themes { get; set; }
}

// Helper to extract common values from BrandContext
public static class BrandContextExtensions
{
    public static string GetPrimaryColor(this BrandContext brand)
    {
        try
        {
            if (brand.Tokens?.TryGetProperty("colors", out var colors) == true &&
                colors.TryGetProperty("primary", out var primary))
            {
                return primary.GetString() ?? "";
            }
        }
        catch { }
        return "";
    }

    public static string GetFontFamily(this BrandContext brand)
    {
        try
        {
            if (brand.Tokens?.TryGetProperty("typography", out var typography) == true &&
                typography.TryGetProperty("fontFamilySans", out var font))
            {
                return font.GetString() ?? "";
            }
        }
        catch { }
        return "";
    }
}

public class DesignValidationResult
{
    public bool IsCompliant { get; set; }
    public int Score { get; set; }
    public string Grade { get; set; } = string.Empty;
    public List<DesignIssue> Issues { get; set; } = new();
}

public class DesignIssue
{
    public string Type { get; set; } = string.Empty;
    public int Severity { get; set; }  // 0=Low, 1=Medium, 2=High, 3=Critical
    public string Message { get; set; } = string.Empty;
    public string? Fix { get; set; }
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
    
    public string SeverityName => Severity switch
    {
        3 => "Critical",
        2 => "High",
        1 => "Medium",
        _ => "Low"
    };
}

public class BrandSummary
{
    public string Context { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

