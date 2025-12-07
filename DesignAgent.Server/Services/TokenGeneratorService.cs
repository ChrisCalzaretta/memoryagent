using DesignAgent.Server.Models.Brand;
using DesignAgent.Server.Models.Questionnaire;
using System.Text;
using System.Text.Json;

namespace DesignAgent.Server.Services;

public class TokenGeneratorService : ITokenGeneratorService
{
    private static readonly Dictionary<string, ColorPalette> PresetPalettes = new()
    {
        ["green"] = new ColorPalette("#10B981", "#34D399", "#059669", "#D1FAE5"),
        ["blue"] = new ColorPalette("#3B82F6", "#60A5FA", "#2563EB", "#DBEAFE"),
        ["purple"] = new ColorPalette("#8B5CF6", "#A78BFA", "#7C3AED", "#EDE9FE"),
        ["red"] = new ColorPalette("#EF4444", "#F87171", "#DC2626", "#FEE2E2"),
        ["orange"] = new ColorPalette("#F97316", "#FB923C", "#EA580C", "#FED7AA"),
        ["teal"] = new ColorPalette("#14B8A6", "#2DD4BF", "#0D9488", "#CCFBF1"),
        ["indigo"] = new ColorPalette("#6366F1", "#818CF8", "#4F46E5", "#E0E7FF"),
        ["pink"] = new ColorPalette("#EC4899", "#F472B6", "#DB2777", "#FCE7F3"),
        ["amber"] = new ColorPalette("#F59E0B", "#FBBF24", "#D97706", "#FEF3C7"),
        ["cyan"] = new ColorPalette("#06B6D4", "#22D3EE", "#0891B2", "#CFFAFE")
    };

    private static readonly Dictionary<string, string[]> FontPairings = new()
    {
        ["sans-serif"] = new[] { "'Inter', system-ui, sans-serif", "'Inter', system-ui, sans-serif" },
        ["geometric"] = new[] { "'Space Grotesk', system-ui, sans-serif", "'Inter', system-ui, sans-serif" },
        ["humanist"] = new[] { "'Source Sans 3', system-ui, sans-serif", "'Source Sans 3', system-ui, sans-serif" },
        ["serif"] = new[] { "'Merriweather', Georgia, serif", "'Source Sans 3', system-ui, sans-serif" },
        ["modern"] = new[] { "'DM Sans', system-ui, sans-serif", "'DM Sans', system-ui, sans-serif" }
    };

    public DesignTokens GenerateTokens(ParsedBrandInput input)
    {
        var tokens = new DesignTokens();
        
        // Generate colors based on preferences
        tokens.Colors = GenerateColorTokens(input);
        
        // Generate typography
        tokens.Typography = GenerateTypographyTokens(input);
        
        // Generate spacing
        tokens.Spacing = GenerateSpacingTokens(input);
        
        // Generate borders based on corner style
        tokens.Borders = GenerateBorderTokens(input);
        
        // Generate shadows based on visual style
        tokens.Shadows = GenerateShadowTokens(input);
        
        // Z-index layers (standard)
        tokens.ZIndex = new ZIndexTokens();
        
        return tokens;
    }

    public ThemeDefinition GenerateThemes(ParsedBrandInput input, ColorTokens colors)
    {
        var themes = new ThemeDefinition
        {
            DefaultTheme = input.ThemePreference == "light" ? "light" : "dark",
            SupportsBothThemes = input.ThemePreference == "both",
            AutoSwitchWithSystem = input.ThemePreference == "both"
        };
        
        // Dark theme
        themes.Dark = new ThemeColors
        {
            Background = "#0F172A",
            Surface = "#1E293B",
            SurfaceHover = "#334155",
            SurfaceActive = "#475569",
            SurfaceElevated = "#1E293B",
            Border = "#334155",
            BorderHover = "#475569",
            TextPrimary = "#F8FAFC",
            TextSecondary = "#94A3B8",
            TextMuted = "#64748B",
            TextInverted = "#0F172A"
        };
        
        // Light theme
        themes.Light = new ThemeColors
        {
            Background = "#FFFFFF",
            Surface = "#F8FAFC",
            SurfaceHover = "#F1F5F9",
            SurfaceActive = "#E2E8F0",
            SurfaceElevated = "#FFFFFF",
            Border = "#E2E8F0",
            BorderHover = "#CBD5E1",
            TextPrimary = "#0F172A",
            TextSecondary = "#475569",
            TextMuted = "#94A3B8",
            TextInverted = "#F8FAFC"
        };
        
        return themes;
    }

    public string ExportToCss(BrandDefinition brand)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("/* Auto-generated Design Tokens */");
        sb.AppendLine($"/* Brand: {brand.Name} */");
        sb.AppendLine($"/* Generated: {DateTime.UtcNow:yyyy-MM-dd} */\n");
        
        sb.AppendLine(":root {");
        
        // Colors
        sb.AppendLine("  /* Colors - Primary */");
        sb.AppendLine($"  --color-primary: {brand.Tokens.Colors.Primary};");
        sb.AppendLine($"  --color-primary-light: {brand.Tokens.Colors.PrimaryLight};");
        sb.AppendLine($"  --color-primary-dark: {brand.Tokens.Colors.PrimaryDark};");
        sb.AppendLine($"  --color-primary-subtle: {brand.Tokens.Colors.PrimarySubtle};");
        sb.AppendLine();
        
        sb.AppendLine("  /* Colors - Semantic */");
        sb.AppendLine($"  --color-success: {brand.Tokens.Colors.Success};");
        sb.AppendLine($"  --color-success-subtle: {brand.Tokens.Colors.SuccessSubtle};");
        sb.AppendLine($"  --color-warning: {brand.Tokens.Colors.Warning};");
        sb.AppendLine($"  --color-warning-subtle: {brand.Tokens.Colors.WarningSubtle};");
        sb.AppendLine($"  --color-error: {brand.Tokens.Colors.Error};");
        sb.AppendLine($"  --color-error-subtle: {brand.Tokens.Colors.ErrorSubtle};");
        sb.AppendLine($"  --color-info: {brand.Tokens.Colors.Info};");
        sb.AppendLine($"  --color-info-subtle: {brand.Tokens.Colors.InfoSubtle};");
        sb.AppendLine();
        
        // Neutrals
        sb.AppendLine("  /* Colors - Neutrals */");
        foreach (var (key, value) in brand.Tokens.Colors.Neutrals)
        {
            sb.AppendLine($"  --color-neutral-{key}: {value};");
        }
        sb.AppendLine();
        
        // Typography
        sb.AppendLine("  /* Typography */");
        sb.AppendLine($"  --font-sans: {brand.Tokens.Typography.FontFamilySans};");
        sb.AppendLine($"  --font-mono: {brand.Tokens.Typography.FontFamilyMono};");
        sb.AppendLine();
        
        foreach (var (key, value) in brand.Tokens.Typography.FontSizes)
        {
            sb.AppendLine($"  --text-{key}: {value};");
        }
        sb.AppendLine();
        
        // Spacing
        sb.AppendLine("  /* Spacing */");
        foreach (var (key, value) in brand.Tokens.Spacing.Scale)
        {
            sb.AppendLine($"  --space-{key}: {value};");
        }
        sb.AppendLine();
        
        // Border radius
        sb.AppendLine("  /* Border Radius */");
        foreach (var (key, value) in brand.Tokens.Borders.Radius)
        {
            sb.AppendLine($"  --radius-{key}: {value};");
        }
        sb.AppendLine();
        
        // Shadows
        sb.AppendLine("  /* Shadows */");
        foreach (var (key, value) in brand.Tokens.Shadows.Shadows)
        {
            sb.AppendLine($"  --shadow-{key}: {value};");
        }
        sb.AppendLine();
        
        // Theme colors (dark by default)
        sb.AppendLine("  /* Theme - Dark (default) */");
        var theme = brand.Themes.DefaultTheme == "dark" ? brand.Themes.Dark : brand.Themes.Light;
        sb.AppendLine($"  --bg: {theme.Background};");
        sb.AppendLine($"  --surface: {theme.Surface};");
        sb.AppendLine($"  --surface-hover: {theme.SurfaceHover};");
        sb.AppendLine($"  --border: {theme.Border};");
        sb.AppendLine($"  --text-primary: {theme.TextPrimary};");
        sb.AppendLine($"  --text-secondary: {theme.TextSecondary};");
        sb.AppendLine($"  --text-muted: {theme.TextMuted};");
        
        sb.AppendLine("}");
        
        // Light theme override
        if (brand.Themes.SupportsBothThemes)
        {
            sb.AppendLine();
            sb.AppendLine("[data-theme=\"light\"], .light {");
            sb.AppendLine($"  --bg: {brand.Themes.Light.Background};");
            sb.AppendLine($"  --surface: {brand.Themes.Light.Surface};");
            sb.AppendLine($"  --surface-hover: {brand.Themes.Light.SurfaceHover};");
            sb.AppendLine($"  --border: {brand.Themes.Light.Border};");
            sb.AppendLine($"  --text-primary: {brand.Themes.Light.TextPrimary};");
            sb.AppendLine($"  --text-secondary: {brand.Themes.Light.TextSecondary};");
            sb.AppendLine($"  --text-muted: {brand.Themes.Light.TextMuted};");
            sb.AppendLine("}");
            
            sb.AppendLine();
            sb.AppendLine("@media (prefers-color-scheme: light) {");
            sb.AppendLine("  :root:not([data-theme]) {");
            sb.AppendLine($"    --bg: {brand.Themes.Light.Background};");
            sb.AppendLine($"    --surface: {brand.Themes.Light.Surface};");
            sb.AppendLine($"    --text-primary: {brand.Themes.Light.TextPrimary};");
            sb.AppendLine("  }");
            sb.AppendLine("}");
        }
        
        return sb.ToString();
    }

    public string ExportToTailwindConfig(BrandDefinition brand)
    {
        var config = new
        {
            theme = new
            {
                extend = new
                {
                    colors = new
                    {
                        primary = new
                        {
                            DEFAULT = brand.Tokens.Colors.Primary,
                            light = brand.Tokens.Colors.PrimaryLight,
                            dark = brand.Tokens.Colors.PrimaryDark,
                            subtle = brand.Tokens.Colors.PrimarySubtle
                        },
                        success = brand.Tokens.Colors.Success,
                        warning = brand.Tokens.Colors.Warning,
                        error = brand.Tokens.Colors.Error
                    },
                    fontFamily = new
                    {
                        sans = brand.Tokens.Typography.FontFamilySans,
                        mono = brand.Tokens.Typography.FontFamilyMono
                    },
                    borderRadius = brand.Tokens.Borders.Radius
                }
            }
        };
        
        return $"// tailwind.config.js - {brand.Name}\nmodule.exports = {JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true })}";
    }

    public string ExportToJson(BrandDefinition brand)
    {
        return JsonSerializer.Serialize(brand.Tokens, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ExportToScss(BrandDefinition brand)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"// SCSS Variables - {brand.Name}");
        sb.AppendLine();
        
        sb.AppendLine("// Colors");
        sb.AppendLine($"$color-primary: {brand.Tokens.Colors.Primary};");
        sb.AppendLine($"$color-primary-light: {brand.Tokens.Colors.PrimaryLight};");
        sb.AppendLine($"$color-primary-dark: {brand.Tokens.Colors.PrimaryDark};");
        sb.AppendLine();
        
        sb.AppendLine("// Typography");
        sb.AppendLine($"$font-sans: {brand.Tokens.Typography.FontFamilySans};");
        sb.AppendLine($"$font-mono: {brand.Tokens.Typography.FontFamilyMono};");
        
        return sb.ToString();
    }

    private ColorTokens GenerateColorTokens(ParsedBrandInput input)
    {
        var colors = new ColorTokens();
        
        // Determine primary color from preferences
        var primaryPalette = DeterminePrimaryColor(input.PreferredColors, input.AvoidColors, input.PersonalityTraits);
        
        colors.Primary = primaryPalette.Primary;
        colors.PrimaryLight = primaryPalette.Light;
        colors.PrimaryDark = primaryPalette.Dark;
        colors.PrimarySubtle = primaryPalette.Subtle;
        
        // Set chart colors avoiding the avoided colors
        colors.ChartPalette = GenerateChartPalette(input.AvoidColors);
        
        return colors;
    }

    private TypographyTokens GenerateTypographyTokens(ParsedBrandInput input)
    {
        var typography = new TypographyTokens();
        
        // If specific fonts provided, use them
        if (!string.IsNullOrEmpty(input.SpecificFonts))
        {
            var fonts = input.SpecificFonts.Split(',').Select(f => f.Trim()).ToArray();
            if (fonts.Length > 0)
                typography.FontFamilySans = $"'{fonts[0]}', system-ui, sans-serif";
        }
        else
        {
            // Choose based on font preference
            var fontKey = input.FontPreference.ToLower() switch
            {
                var s when s.Contains("geometric") => "geometric",
                var s when s.Contains("serif") => "serif",
                var s when s.Contains("humanist") => "humanist",
                var s when s.Contains("modern") || s.Contains("dm sans") => "modern",
                _ => "sans-serif"
            };
            
            if (FontPairings.TryGetValue(fontKey, out var pairing))
            {
                typography.FontFamilySans = pairing[0];
            }
        }
        
        return typography;
    }

    private SpacingTokens GenerateSpacingTokens(ParsedBrandInput input)
    {
        // Standard 8px based spacing works for most cases
        return new SpacingTokens();
    }

    private BorderTokens GenerateBorderTokens(ParsedBrandInput input)
    {
        var borders = new BorderTokens();
        
        // Adjust radius based on corner style preference
        borders.Radius = input.CornerStyle switch
        {
            "sharp" => new Dictionary<string, string>
            {
                ["none"] = "0", ["sm"] = "2px", ["md"] = "2px", ["lg"] = "4px",
                ["xl"] = "4px", ["2xl"] = "6px", ["3xl"] = "8px", ["full"] = "9999px"
            },
            "slight" => new Dictionary<string, string>
            {
                ["none"] = "0", ["sm"] = "4px", ["md"] = "6px", ["lg"] = "8px",
                ["xl"] = "10px", ["2xl"] = "12px", ["3xl"] = "16px", ["full"] = "9999px"
            },
            "very-rounded" => new Dictionary<string, string>
            {
                ["none"] = "0", ["sm"] = "8px", ["md"] = "12px", ["lg"] = "16px",
                ["xl"] = "20px", ["2xl"] = "24px", ["3xl"] = "32px", ["full"] = "9999px"
            },
            "pill" => new Dictionary<string, string>
            {
                ["none"] = "0", ["sm"] = "9999px", ["md"] = "9999px", ["lg"] = "9999px",
                ["xl"] = "9999px", ["2xl"] = "9999px", ["3xl"] = "9999px", ["full"] = "9999px"
            },
            _ => borders.Radius // rounded (default)
        };
        
        return borders;
    }

    private ShadowTokens GenerateShadowTokens(ParsedBrandInput input)
    {
        var shadows = new ShadowTokens();
        
        // Minimal style = subtle shadows
        if (input.VisualStyle.Contains("Minimal", StringComparison.OrdinalIgnoreCase))
        {
            shadows.Shadows["md"] = "0 2px 4px -1px rgb(0 0 0 / 0.06)";
            shadows.Shadows["lg"] = "0 4px 6px -2px rgb(0 0 0 / 0.05)";
        }
        // Bold style = stronger shadows
        else if (input.VisualStyle.Contains("Bold", StringComparison.OrdinalIgnoreCase))
        {
            shadows.Shadows["md"] = "0 6px 12px -2px rgb(0 0 0 / 0.15)";
            shadows.Shadows["lg"] = "0 12px 24px -4px rgb(0 0 0 / 0.15)";
        }
        
        return shadows;
    }

    private ColorPalette DeterminePrimaryColor(string? preferred, string? avoid, string[] traits)
    {
        // Parse avoided colors
        var avoidLower = avoid?.ToLower() ?? "";
        
        // Try to find a color mentioned in preferences
        if (!string.IsNullOrEmpty(preferred))
        {
            var prefLower = preferred.ToLower();
            foreach (var (colorName, palette) in PresetPalettes)
            {
                if (prefLower.Contains(colorName) && !avoidLower.Contains(colorName))
                    return palette;
            }
        }
        
        // Choose based on personality traits
        if (traits.Any(t => t.Contains("Trust", StringComparison.OrdinalIgnoreCase)))
            return PresetPalettes["blue"];
        if (traits.Any(t => t.Contains("Energy", StringComparison.OrdinalIgnoreCase)))
            return PresetPalettes["orange"];
        if (traits.Any(t => t.Contains("Calm", StringComparison.OrdinalIgnoreCase)))
            return PresetPalettes["teal"];
        if (traits.Any(t => t.Contains("Luxur", StringComparison.OrdinalIgnoreCase)))
            return PresetPalettes["purple"];
        if (traits.Any(t => t.Contains("Play", StringComparison.OrdinalIgnoreCase)))
            return PresetPalettes["pink"];
        
        // Default to blue (universally professional)
        return PresetPalettes["blue"];
    }

    private string[] GenerateChartPalette(string? avoid)
    {
        var palette = new List<string>();
        var avoidLower = avoid?.ToLower() ?? "";
        
        foreach (var (colorName, p) in PresetPalettes)
        {
            if (!avoidLower.Contains(colorName))
                palette.Add(p.Primary);
            
            if (palette.Count >= 6) break;
        }
        
        return palette.ToArray();
    }

    private record ColorPalette(string Primary, string Light, string Dark, string Subtle);
}

