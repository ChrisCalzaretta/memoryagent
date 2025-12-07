using DesignAgent.Server.Models;
using DesignAgent.Server.Models.Brand;
using DesignAgent.Server.Models.Questionnaire;

namespace DesignAgent.Server.Services;

/// <summary>
/// Service for managing brand definitions
/// </summary>
public interface IBrandService
{
    Task<BrandDefinition> CreateBrandAsync(ParsedBrandInput input, CancellationToken cancellationToken = default);
    Task<BrandDefinition?> GetBrandAsync(string context, CancellationToken cancellationToken = default);
    Task<BrandDefinition> UpdateBrandAsync(string context, BrandDefinition updates, CancellationToken cancellationToken = default);
    Task<bool> DeleteBrandAsync(string context, CancellationToken cancellationToken = default);
    Task<List<BrandSummary>> ListBrandsAsync(CancellationToken cancellationToken = default);
    Task<BrandDefinition> CloneBrandAsync(string fromContext, string toContext, Dictionary<string, object>? overrides = null, CancellationToken cancellationToken = default);
}

public class BrandSummary
{
    public string Context { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Service for generating design tokens from brand input
/// </summary>
public interface ITokenGeneratorService
{
    DesignTokens GenerateTokens(ParsedBrandInput input);
    ThemeDefinition GenerateThemes(ParsedBrandInput input, ColorTokens colors);
    string ExportToCss(BrandDefinition brand);
    string ExportToTailwindConfig(BrandDefinition brand);
    string ExportToJson(BrandDefinition brand);
    string ExportToScss(BrandDefinition brand);
}

/// <summary>
/// Service for generating component specifications
/// </summary>
public interface IComponentSpecService
{
    Dictionary<string, ComponentSpec> GenerateComponentSpecs(ParsedBrandInput input, DesignTokens tokens);
    ComponentSpec? GetComponentSpec(string componentName, string context);
    string GenerateComponentGuidance(string componentName, BrandDefinition brand);
}

/// <summary>
/// Service for validating designs against brand guidelines
/// </summary>
public interface IDesignValidationService
{
    Task<DesignValidationResult> ValidateAsync(string context, string code, CancellationToken cancellationToken = default);
    Task<DesignValidationResult> ValidateFilesAsync(string context, Dictionary<string, string> files, CancellationToken cancellationToken = default);
    List<DesignIssue> CheckColorCompliance(string code, BrandDefinition brand);
    List<DesignIssue> CheckTypographyCompliance(string code, BrandDefinition brand);
    List<DesignIssue> CheckSpacingCompliance(string code, BrandDefinition brand);
    List<DesignIssue> CheckComponentCompliance(string code, BrandDefinition brand);
}

/// <summary>
/// Service for the brand builder questionnaire
/// </summary>
public interface IQuestionnaireService
{
    BrandQuestionnaire GetQuestionnaire();
    ParsedBrandInput ParseAnswers(QuestionnaireAnswers answers);
    string GetQuestionnaireMarkdown();
}

/// <summary>
/// Service for accessibility validation
/// </summary>
public interface IAccessibilityService
{
    AccessibilityValidationResult ValidateAccessibility(string code, string wcagLevel = "AA");
    ContrastCheckResult CheckContrast(string foreground, string background);
    List<AccessibilityIssue> CheckAriaAttributes(string code);
    List<AccessibilityIssue> CheckKeyboardAccessibility(string code);
    List<AccessibilityIssue> CheckSemanticHtml(string code);
}

