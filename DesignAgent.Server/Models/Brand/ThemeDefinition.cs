namespace DesignAgent.Server.Models.Brand;

/// <summary>
/// Theme definitions for dark/light modes
/// </summary>
public class ThemeDefinition
{
    public string DefaultTheme { get; set; } = "dark";
    public bool SupportsBothThemes { get; set; } = true;
    public bool AutoSwitchWithSystem { get; set; } = true;
    
    public ThemeColors Dark { get; set; } = new()
    {
        Background = "#0F172A",
        Surface = "#1E293B",
        SurfaceHover = "#334155",
        SurfaceActive = "#475569",
        Border = "#334155",
        BorderHover = "#475569",
        TextPrimary = "#F8FAFC",
        TextSecondary = "#94A3B8",
        TextMuted = "#64748B",
        TextInverted = "#0F172A"
    };
    
    public ThemeColors Light { get; set; } = new()
    {
        Background = "#FFFFFF",
        Surface = "#F8FAFC",
        SurfaceHover = "#F1F5F9",
        SurfaceActive = "#E2E8F0",
        Border = "#E2E8F0",
        BorderHover = "#CBD5E1",
        TextPrimary = "#0F172A",
        TextSecondary = "#475569",
        TextMuted = "#94A3B8",
        TextInverted = "#F8FAFC"
    };
}

public class ThemeColors
{
    public string Background { get; set; } = string.Empty;
    public string Surface { get; set; } = string.Empty;
    public string SurfaceHover { get; set; } = string.Empty;
    public string SurfaceActive { get; set; } = string.Empty;
    public string SurfaceElevated { get; set; } = string.Empty;
    public string Border { get; set; } = string.Empty;
    public string BorderHover { get; set; } = string.Empty;
    public string TextPrimary { get; set; } = string.Empty;
    public string TextSecondary { get; set; } = string.Empty;
    public string TextMuted { get; set; } = string.Empty;
    public string TextInverted { get; set; } = string.Empty;
}

