namespace DesignAgent.Server.Models.Questionnaire;

/// <summary>
/// Brand builder questionnaire structure
/// </summary>
public class BrandQuestionnaire
{
    public string Version { get; set; } = "1.0";
    public List<QuestionSection> Sections { get; set; } = new();
}

public class QuestionSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<Question> Questions { get; set; } = new();
}

public class Question
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public bool Required { get; set; }
    public string? Example { get; set; }
    public string? HelpText { get; set; }
    public List<string>? Options { get; set; }
    public int? MinSelect { get; set; }
    public int? MaxSelect { get; set; }
    public string? DefaultValue { get; set; }
}

public enum QuestionType
{
    Text,
    TextArea,
    Select,
    MultiSelect,
    Radio,
    Checkbox,
    Color,
    Number
}

/// <summary>
/// User's answers to the questionnaire
/// </summary>
public class QuestionnaireAnswers
{
    public Dictionary<string, object> Answers { get; set; } = new();
}

/// <summary>
/// Parsed questionnaire responses for brand generation
/// </summary>
public class ParsedBrandInput
{
    // Identity
    public string BrandName { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string Description { get; set; } = string.Empty;
    
    // Audience
    public string TargetAudience { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    
    // Personality
    public string[] PersonalityTraits { get; set; } = Array.Empty<string>();
    public string VoiceArchetype { get; set; } = string.Empty;
    
    // Visual
    public string ThemePreference { get; set; } = "dark";
    public string? PreferredColors { get; set; }
    public string? AvoidColors { get; set; }
    public string VisualStyle { get; set; } = string.Empty;
    public string CornerStyle { get; set; } = "rounded";
    
    // Typography
    public string FontPreference { get; set; } = string.Empty;
    public string? SpecificFonts { get; set; }
    
    // Platforms
    public string[] Platforms { get; set; } = Array.Empty<string>();
    public string[] Frameworks { get; set; } = Array.Empty<string>();
    public string CssFramework { get; set; } = string.Empty;
    
    // Components
    public string[] ComponentTypes { get; set; } = Array.Empty<string>();
    
    // Motion
    public string MotionPreference { get; set; } = "moderate";
    
    // Inspiration
    public string? InspirationSites { get; set; }
    public string? AvoidStyles { get; set; }
    
    // Accessibility
    public string AccessibilityLevel { get; set; } = "AA";
}

