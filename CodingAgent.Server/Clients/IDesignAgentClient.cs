using System.Text.Json.Serialization;

namespace CodingAgent.Server.Clients;

/// <summary>
/// 🎨 DESIGN AGENT CLIENT - Brand system management and UI validation
/// </summary>
public interface IDesignAgentClient
{
    /// <summary>
    /// Get brand questionnaire questions
    /// </summary>
    Task<DesignQuestionnaire> GetQuestionnaireAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Create a brand system from questionnaire answers
    /// </summary>
    Task<BrandSystem> CreateBrandAsync(
        Dictionary<string, string> answers,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get an existing brand by context name
    /// </summary>
    Task<BrandSystem?> GetBrandAsync(string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate UI code against brand guidelines
    /// </summary>
    Task<DesignValidationResult> ValidateDesignAsync(
        string context,
        List<FileContent> files,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Design questionnaire with questions
/// </summary>
public class DesignQuestionnaire
{
    public string Version { get; set; } = "1.0";
    public List<QuestionSection> Sections { get; set; } = new();
}

public class QuestionSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<QuestionnaireQuestion> Questions { get; set; } = new();
}

public class QuestionnaireQuestion
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    
    // Design Agent sends this as an enum (int), we need to convert it
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuestionType Type { get; set; } = QuestionType.Text;
    
    public bool Required { get; set; }
    public string? Example { get; set; }
    public string? HelpText { get; set; }
    public List<string>? Options { get; set; }
    public string? DefaultValue { get; set; }
}

public enum QuestionType
{
    Text = 0,
    TextArea = 1,
    Select = 2,
    MultiSelect = 3,
    Radio = 4,
    Checkbox = 5,
    Color = 6,
    Number = 7
}

/// <summary>
/// Brand system with design guidelines
/// </summary>
public record BrandSystem
{
    public required string Name { get; init; }
    public required string Context { get; init; }
    public required BrandColors Colors { get; init; }
    public required BrandTypography Typography { get; init; }
    public required BrandSpacing Spacing { get; init; }
    public required string Tone { get; init; }
    public required string Accessibility { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record BrandColors
{
    public required string Primary { get; init; }
    public required string Secondary { get; init; }
    public string? Accent { get; init; }
    public string? Background { get; init; }
    public string? Text { get; init; }
}

public record BrandTypography
{
    public required string HeadingFont { get; init; }
    public required string BodyFont { get; init; }
    public string? MonoFont { get; init; }
}

public record BrandSpacing
{
    public required string BaseUnit { get; init; } // "4px", "8px", etc.
    public required string Scale { get; init; } // "linear", "fibonacci"
}

public record FileContent
{
    public required string Path { get; init; }
    public required string Content { get; init; }
}

/// <summary>
/// Design validation result
/// </summary>
public record DesignValidationResult
{
    public required int Score { get; init; } // 0-10
    public required List<DesignIssue> Issues { get; init; }
    public required string Summary { get; init; }
}

public record DesignIssue
{
    public required string Severity { get; init; } // "error", "warning", "info"
    public required string Message { get; init; }
    public required string FilePath { get; init; }
    public int? LineNumber { get; init; }
    public string? SuggestedFix { get; init; }
}

