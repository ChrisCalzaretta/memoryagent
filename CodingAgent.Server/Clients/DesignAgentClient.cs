using System.Text;
using System.Text.Json;

namespace CodingAgent.Server.Clients;

/// <summary>
/// 🎨 DESIGN AGENT CLIENT - HTTP client for Design Agent service
/// </summary>
public class DesignAgentClient : IDesignAgentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DesignAgentClient> _logger;
    private readonly string _designAgentUrl;
    
    public DesignAgentClient(
        HttpClient httpClient,
        ILogger<DesignAgentClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _designAgentUrl = configuration["DesignAgent:BaseUrl"] ?? "http://design-agent:5004";
        
        _logger.LogInformation("DesignAgentClient configured with URL: {Url}", _designAgentUrl);
    }
    
    public async Task<DesignQuestionnaire> GetQuestionnaireAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📋 Fetching design questionnaire");
            
            var response = await _httpClient.GetAsync(
                $"{_designAgentUrl}/api/design/brand/questionnaire",
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DesignQuestionnaire>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (result == null)
                throw new InvalidOperationException("Failed to deserialize questionnaire");
            
            var totalQuestions = result.Sections.Sum(s => s.Questions.Count);
            _logger.LogInformation("✅ Fetched {Sections} sections with {Count} total questions", result.Sections.Count, totalQuestions);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to fetch design questionnaire");
            throw;
        }
    }
    
    public async Task<BrandSystem> CreateBrandAsync(
        Dictionary<string, string> answers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🎨 Creating brand system from {Count} answers", answers.Count);
            
            // Convert Dictionary<string, string> to Dictionary<string, object> for Design Agent
            var answersDict = answers.ToDictionary(kv => kv.Key, kv => (object)kv.Value);
            var json = JsonSerializer.Serialize(new { answers = answersDict });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(
                $"{_designAgentUrl}/api/design/brand/create",
                content,
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Design Agent returns BrandDefinition - deserialize it fully
            var brandDef = JsonSerializer.Deserialize<BrandDefinition>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (brandDef == null)
                throw new InvalidOperationException("Failed to deserialize brand definition");
            
            var result = MapToBrandSystem(brandDef);
            
            _logger.LogInformation("✅ Created brand: {Name}", result.Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to create brand system");
            throw;
        }
    }
    
    public async Task<BrandSystem?> GetBrandAsync(string context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📖 Fetching brand for context: {Context}", context);
            
            var response = await _httpClient.GetAsync(
                $"{_designAgentUrl}/api/design/brand/{context}",
                cancellationToken);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("⚠️ No brand found for context: {Context}", context);
                return null;
            }
            
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Design Agent returns BrandDefinition - deserialize it fully
            var brandDef = JsonSerializer.Deserialize<BrandDefinition>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (brandDef == null)
                throw new InvalidOperationException("Failed to deserialize brand definition");
            
            var result = MapToBrandSystem(brandDef);
            
            _logger.LogInformation("✅ Fetched brand: {Name}", result.Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to fetch brand (non-fatal)");
            return null;
        }
    }
    
    public async Task<DesignValidationResult> ValidateDesignAsync(
        string context,
        List<FileContent> files,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🎨 Validating {Count} files against brand: {Context}", files.Count, context);
            
            var json = JsonSerializer.Serialize(new { context, files });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(
                $"{_designAgentUrl}/api/design/validate",
                content,
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DesignValidationResult>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (result == null)
                throw new InvalidOperationException("Failed to deserialize validation result");
            
            _logger.LogInformation("✅ Design validation: {Score}/10, {Issues} issues", 
                result.Score, result.Issues.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Design validation failed");
            throw;
        }
    }
    
    private BrandSystem MapToBrandSystem(BrandDefinition brandDef)
    {
        return new BrandSystem
        {
            Name = brandDef.Name,
            Context = brandDef.Context,
            Tone = brandDef.Voice?.Tone ?? "Professional",
            Accessibility = brandDef.Accessibility?.Level ?? "WCAG AA",
            Colors = new BrandColors
            {
                Primary = brandDef.Tokens.Colors.Primary,
                Secondary = brandDef.Tokens.Colors.Secondary,
                Accent = brandDef.Tokens.Colors.Accent ?? "#f59e0b",
                Background = brandDef.Tokens.Colors.Background,
                Text = brandDef.Tokens.Colors.Text
            },
            Typography = new BrandTypography
            {
                HeadingFont = brandDef.Tokens.Typography.Families?.Heading ?? "Inter",
                BodyFont = brandDef.Tokens.Typography.Families?.Body ?? "Inter",
                MonoFont = brandDef.Tokens.Typography.Families?.Monospace
            },
            Spacing = new BrandSpacing
            {
                BaseUnit = brandDef.Tokens.Spacing.Base,
                Scale = brandDef.Tokens.Spacing.Scale ?? "linear"
            },
            CreatedAt = brandDef.CreatedAt
        };
    }
}

// ===========================================================================================
// Design Agent DTOs (minimal subset needed for deserialization)
// ===========================================================================================

internal class BrandDefinition
{
    public string Context { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DesignTokensDto Tokens { get; set; } = new();
    public VoiceGuidelinesDto? Voice { get; set; }
    public AccessibilityRequirementsDto? Accessibility { get; set; }
}

internal class DesignTokensDto
{
    public ColorTokensDto Colors { get; set; } = new();
    public TypographyTokensDto Typography { get; set; } = new();
    public SpacingTokensDto Spacing { get; set; } = new();
}

internal class ColorTokensDto
{
    public string Primary { get; set; } = "#3B82F6";
    public string Secondary { get; set; } = "#6366F1";
    public string? Accent { get; set; }
    public string Background { get; set; } = "#FFFFFF";
    public string Text { get; set; } = "#1F2937";
}

internal class TypographyTokensDto
{
    public FontFamiliesDto? Families { get; set; }
}

internal class FontFamiliesDto
{
    public string? Heading { get; set; }
    public string? Body { get; set; }
    public string? Monospace { get; set; }
}

internal class SpacingTokensDto
{
    public string Base { get; set; } = "8px";
    public string? Scale { get; set; }
}

internal class VoiceGuidelinesDto
{
    public string? Tone { get; set; }
}

internal class AccessibilityRequirementsDto
{
    public string? Level { get; set; }
}

