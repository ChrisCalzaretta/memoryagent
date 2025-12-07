namespace DesignAgent.Server.Models.Brand;

/// <summary>
/// Complete brand definition including all design tokens, components, and guidelines
/// </summary>
public class BrandDefinition
{
    public string Context { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Core brand attributes
    public BrandIdentity Identity { get; set; } = new();
    public DesignTokens Tokens { get; set; } = new();
    public ThemeDefinition Themes { get; set; } = new();
    public VoiceGuidelines Voice { get; set; } = new();
    public AccessibilityRequirements Accessibility { get; set; } = new();
    public ResponsiveGuidelines Responsive { get; set; } = new();
    public MotionGuidelines Motion { get; set; } = new();
    public IconGuidelines Icons { get; set; } = new();
    
    // Component patterns
    public Dictionary<string, ComponentSpec> Components { get; set; } = new();
}

public class BrandIdentity
{
    public string[] PersonalityTraits { get; set; } = Array.Empty<string>();
    public string Archetype { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = string.Empty;
    public string[] Platforms { get; set; } = Array.Empty<string>();
    public string[] Frameworks { get; set; } = Array.Empty<string>();
}

public class DesignTokens
{
    public ColorTokens Colors { get; set; } = new();
    public TypographyTokens Typography { get; set; } = new();
    public SpacingTokens Spacing { get; set; } = new();
    public BorderTokens Borders { get; set; } = new();
    public ShadowTokens Shadows { get; set; } = new();
    public ZIndexTokens ZIndex { get; set; } = new();
}

public class ColorTokens
{
    // Primary brand colors
    public string Primary { get; set; } = "#3B82F6";
    public string PrimaryLight { get; set; } = "#60A5FA";
    public string PrimaryDark { get; set; } = "#2563EB";
    public string PrimarySubtle { get; set; } = "#EFF6FF";
    
    // Secondary
    public string Secondary { get; set; } = "#6366F1";
    public string SecondaryLight { get; set; } = "#818CF8";
    public string SecondaryDark { get; set; } = "#4F46E5";
    
    // Semantic colors
    public string Success { get; set; } = "#10B981";
    public string SuccessSubtle { get; set; } = "#D1FAE5";
    public string Warning { get; set; } = "#F59E0B";
    public string WarningSubtle { get; set; } = "#FEF3C7";
    public string Error { get; set; } = "#EF4444";
    public string ErrorSubtle { get; set; } = "#FEE2E2";
    public string Info { get; set; } = "#3B82F6";
    public string InfoSubtle { get; set; } = "#DBEAFE";
    
    // Neutral palette
    public Dictionary<string, string> Neutrals { get; set; } = new()
    {
        ["50"] = "#F9FAFB",
        ["100"] = "#F3F4F6",
        ["200"] = "#E5E7EB",
        ["300"] = "#D1D5DB",
        ["400"] = "#9CA3AF",
        ["500"] = "#6B7280",
        ["600"] = "#4B5563",
        ["700"] = "#374151",
        ["800"] = "#1F2937",
        ["900"] = "#111827",
        ["950"] = "#030712"
    };
    
    // Chart colors
    public string[] ChartPalette { get; set; } = new[]
    {
        "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899"
    };
}

public class TypographyTokens
{
    public string FontFamilySans { get; set; } = "'Inter', system-ui, sans-serif";
    public string FontFamilyMono { get; set; } = "'JetBrains Mono', monospace";
    public string FontFamilySerif { get; set; } = "'Merriweather', serif";
    
    // Font sizes (rem)
    public Dictionary<string, string> FontSizes { get; set; } = new()
    {
        ["xs"] = "0.75rem",
        ["sm"] = "0.875rem",
        ["base"] = "1rem",
        ["lg"] = "1.125rem",
        ["xl"] = "1.25rem",
        ["2xl"] = "1.5rem",
        ["3xl"] = "1.875rem",
        ["4xl"] = "2.25rem",
        ["5xl"] = "3rem",
        ["6xl"] = "3.75rem"
    };
    
    public Dictionary<string, string> FontWeights { get; set; } = new()
    {
        ["normal"] = "400",
        ["medium"] = "500",
        ["semibold"] = "600",
        ["bold"] = "700"
    };
    
    public Dictionary<string, string> LineHeights { get; set; } = new()
    {
        ["tight"] = "1.1",
        ["snug"] = "1.25",
        ["normal"] = "1.5",
        ["relaxed"] = "1.625",
        ["loose"] = "2"
    };
    
    public Dictionary<string, string> LetterSpacing { get; set; } = new()
    {
        ["tight"] = "-0.02em",
        ["normal"] = "0",
        ["wide"] = "0.05em",
        ["wider"] = "0.1em"
    };
}

public class SpacingTokens
{
    public string BaseUnit { get; set; } = "8px";
    
    public Dictionary<string, string> Scale { get; set; } = new()
    {
        ["0"] = "0",
        ["1"] = "0.25rem",   // 4px
        ["2"] = "0.5rem",    // 8px
        ["3"] = "0.75rem",   // 12px
        ["4"] = "1rem",      // 16px
        ["5"] = "1.25rem",   // 20px
        ["6"] = "1.5rem",    // 24px
        ["8"] = "2rem",      // 32px
        ["10"] = "2.5rem",   // 40px
        ["12"] = "3rem",     // 48px
        ["16"] = "4rem",     // 64px
        ["20"] = "5rem",     // 80px
        ["24"] = "6rem"      // 96px
    };
}

public class BorderTokens
{
    public Dictionary<string, string> Radius { get; set; } = new()
    {
        ["none"] = "0",
        ["sm"] = "0.25rem",    // 4px
        ["md"] = "0.375rem",   // 6px
        ["lg"] = "0.5rem",     // 8px
        ["xl"] = "0.75rem",    // 12px
        ["2xl"] = "1rem",      // 16px
        ["3xl"] = "1.5rem",    // 24px
        ["full"] = "9999px"
    };
    
    public Dictionary<string, string> Width { get; set; } = new()
    {
        ["0"] = "0",
        ["1"] = "1px",
        ["2"] = "2px",
        ["4"] = "4px"
    };
}

public class ShadowTokens
{
    public Dictionary<string, string> Shadows { get; set; } = new()
    {
        ["none"] = "none",
        ["sm"] = "0 1px 2px 0 rgb(0 0 0 / 0.05)",
        ["md"] = "0 4px 6px -1px rgb(0 0 0 / 0.1), 0 2px 4px -2px rgb(0 0 0 / 0.1)",
        ["lg"] = "0 10px 15px -3px rgb(0 0 0 / 0.1), 0 4px 6px -4px rgb(0 0 0 / 0.1)",
        ["xl"] = "0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.1)",
        ["2xl"] = "0 25px 50px -12px rgb(0 0 0 / 0.25)"
    };
}

public class ZIndexTokens
{
    public Dictionary<string, int> Layers { get; set; } = new()
    {
        ["base"] = 0,
        ["dropdown"] = 1000,
        ["sticky"] = 1020,
        ["fixed"] = 1030,
        ["modalBackdrop"] = 1040,
        ["modal"] = 1050,
        ["popover"] = 1060,
        ["tooltip"] = 1070,
        ["toast"] = 1080
    };
}

