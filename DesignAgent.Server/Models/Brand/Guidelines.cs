namespace DesignAgent.Server.Models.Brand;

/// <summary>
/// Voice and tone guidelines for copy/content
/// </summary>
public class VoiceGuidelines
{
    public string Personality { get; set; } = "Friendly helper";
    public string Archetype { get; set; } = "The Coach";
    
    public ToneByContext Tone { get; set; } = new();
    public WritingRules Writing { get; set; } = new();
    public ContentExamples Examples { get; set; } = new();
}

public class ToneByContext
{
    public string Default { get; set; } = "Friendly, professional";
    public string Error { get; set; } = "Helpful, not blaming";
    public string Success { get; set; } = "Celebratory but not over the top";
    public string Empty { get; set; } = "Encouraging, action-oriented";
    public string Loading { get; set; } = "Brief, informative";
    public string Onboarding { get; set; } = "Welcoming, simple";
}

public class WritingRules
{
    public bool UseContractions { get; set; } = true;
    public string Person { get; set; } = "second"; // you/your
    public string Voice { get; set; } = "active";
    public string SentenceLength { get; set; } = "short";
    public bool UseJargon { get; set; } = false;
    public bool UseExclamationMarks { get; set; } = false;
}

public class ContentExamples
{
    public Dictionary<string, string[]> Good { get; set; } = new()
    {
        ["button_primary"] = new[] { "Get started", "Save changes", "Continue" },
        ["button_secondary"] = new[] { "Cancel", "Skip for now", "Maybe later" },
        ["error"] = new[] { "Couldn't save that. Try again?", "Something went wrong." },
        ["empty"] = new[] { "No items yet. Ready to add one?", "Nothing here yet." },
        ["success"] = new[] { "Done!", "Changes saved.", "All set!" }
    };
    
    public string[] Avoid { get; set; } = new[]
    {
        "Error 500",
        "Invalid input",
        "Operation failed",
        "AMAZING!",
        "Congratulations on your achievement milestone!"
    };
}

/// <summary>
/// Accessibility requirements (WCAG compliance)
/// </summary>
public class AccessibilityRequirements
{
    public string Level { get; set; } = "AA"; // AA or AAA
    
    public ContrastRequirements Contrast { get; set; } = new();
    public KeyboardRequirements Keyboard { get; set; } = new();
    public ScreenReaderRequirements ScreenReader { get; set; } = new();
    public MotionAccessibility Motion { get; set; } = new();
    public FormAccessibility Forms { get; set; } = new();
    public ColorBlindness ColorBlindness { get; set; } = new();
}

public class ContrastRequirements
{
    public double NormalTextMinimum { get; set; } = 4.5;
    public double LargeTextMinimum { get; set; } = 3.0;
    public double UIComponentsMinimum { get; set; } = 3.0;
    public double GraphicalObjectsMinimum { get; set; } = 3.0;
}

public class KeyboardRequirements
{
    public bool AllInteractiveFocusable { get; set; } = true;
    public bool FocusIndicatorVisible { get; set; } = true;
    public string FocusIndicatorStyle { get; set; } = "2px solid var(--primary)";
    public bool LogicalTabOrder { get; set; } = true;
    public bool NoKeyboardTraps { get; set; } = true;
    public bool SkipLinks { get; set; } = true;
}

public class ScreenReaderRequirements
{
    public bool SemanticHTML { get; set; } = true;
    public bool AriaLabelsRequired { get; set; } = true;
    public bool LiveRegionsForDynamic { get; set; } = true;
    public bool LogicalHeadingHierarchy { get; set; } = true;
    public bool LandmarkRegions { get; set; } = true;
}

public class MotionAccessibility
{
    public bool RespectReducedMotion { get; set; } = true;
    public bool NoAutoPlaying { get; set; } = true;
    public bool PauseableAnimations { get; set; } = true;
    public bool NoFlashing { get; set; } = true; // No flashing > 3 times/sec
}

public class FormAccessibility
{
    public bool VisibleLabels { get; set; } = true;
    public bool ClearErrorIdentification { get; set; } = true;
    public bool RequiredIndicators { get; set; } = true;
    public bool InputInstructions { get; set; } = true;
    public bool ErrorSuggestions { get; set; } = true;
}

public class ColorBlindness
{
    public bool ConsiderDeuteranopia { get; set; } = true;
    public bool ConsiderProtanopia { get; set; } = true;
    public bool ConsiderTritanopia { get; set; } = true;
    public bool NeverRelyOnColorAlone { get; set; } = true;
}

/// <summary>
/// Responsive design guidelines
/// </summary>
public class ResponsiveGuidelines
{
    public Dictionary<string, string> Breakpoints { get; set; } = new()
    {
        ["sm"] = "640px",
        ["md"] = "768px",
        ["lg"] = "1024px",
        ["xl"] = "1280px",
        ["2xl"] = "1536px"
    };
    
    public LayoutRules Layout { get; set; } = new();
    public TypographyScaling TypographyScale { get; set; } = new();
    public TouchTargets TouchTargets { get; set; } = new();
}

public class LayoutRules
{
    public string MobileNavigation { get; set; } = "bottom-tabs";
    public string DesktopNavigation { get; set; } = "sidebar";
    public Dictionary<string, int> GridColumns { get; set; } = new()
    {
        ["mobile"] = 1,
        ["tablet"] = 2,
        ["desktop"] = 3
    };
}

public class TypographyScaling
{
    public Dictionary<string, string> HeadingSize { get; set; } = new()
    {
        ["mobile"] = "1.5rem",
        ["desktop"] = "2.25rem"
    };
    public Dictionary<string, string> BodySize { get; set; } = new()
    {
        ["mobile"] = "0.875rem",
        ["desktop"] = "1rem"
    };
}

public class TouchTargets
{
    public string MinimumSize { get; set; } = "44px";
    public string MinimumSpacing { get; set; } = "8px";
}

/// <summary>
/// Animation and motion guidelines
/// </summary>
public class MotionGuidelines
{
    public string Preference { get; set; } = "moderate"; // minimal, moderate, rich, none
    
    public Dictionary<string, string> Duration { get; set; } = new()
    {
        ["instant"] = "100ms",
        ["fast"] = "200ms",
        ["normal"] = "300ms",
        ["slow"] = "500ms"
    };
    
    public Dictionary<string, string> Easing { get; set; } = new()
    {
        ["default"] = "cubic-bezier(0.4, 0, 0.2, 1)",
        ["enter"] = "cubic-bezier(0, 0, 0.2, 1)",
        ["exit"] = "cubic-bezier(0.4, 0, 1, 1)",
        ["bounce"] = "cubic-bezier(0.68, -0.55, 0.265, 1.55)"
    };
    
    public Dictionary<string, AnimationPattern> Patterns { get; set; } = new();
}

public class AnimationPattern
{
    public string Duration { get; set; } = "normal";
    public string Easing { get; set; } = "default";
    public string Keyframes { get; set; } = string.Empty;
}

/// <summary>
/// Icon and imagery guidelines
/// </summary>
public class IconGuidelines
{
    public string Library { get; set; } = "lucide";
    public string Style { get; set; } = "outlined";
    public double StrokeWidth { get; set; } = 1.5;
    public int DefaultSize { get; set; } = 24;
    public int[] AvailableSizes { get; set; } = new[] { 16, 20, 24, 32 };
    
    public ImageryGuidelines Imagery { get; set; } = new();
}

public class ImageryGuidelines
{
    public string Style { get; set; } = "minimal illustrations, data visualizations";
    public string[] Avoid { get; set; } = new[] { "stock photos", "3D renders", "busy patterns" };
    public string[] PreferredFormats { get; set; } = new[] { "svg", "webp", "png" };
    public Dictionary<string, string> AspectRatios { get; set; } = new()
    {
        ["hero"] = "16:9",
        ["card"] = "4:3",
        ["avatar"] = "1:1",
        ["thumbnail"] = "1:1"
    };
}

