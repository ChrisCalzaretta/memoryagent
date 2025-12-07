namespace DesignAgent.Server.Models.Brand;

/// <summary>
/// Component specification defining structure, variants, states, and accessibility
/// </summary>
public class ComponentSpec
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // layout, navigation, form, feedback, etc.
    
    public ComponentStructure Structure { get; set; } = new();
    public Dictionary<string, VariantSpec> Variants { get; set; } = new();
    public Dictionary<string, SizeSpec> Sizes { get; set; } = new();
    public Dictionary<string, StateSpec> States { get; set; } = new();
    public ComponentStyling Styling { get; set; } = new();
    public ComponentAccessibility Accessibility { get; set; } = new();
    public ComponentBehavior Behavior { get; set; } = new();
}

public class ComponentStructure
{
    public Dictionary<string, StructureElement> Elements { get; set; } = new();
}

public class StructureElement
{
    public bool Required { get; set; }
    public string Type { get; set; } = string.Empty; // text, icon, slot, etc.
    public string[] Contains { get; set; } = Array.Empty<string>();
    public string Position { get; set; } = string.Empty;
}

public class VariantSpec
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Styles { get; set; } = new();
}

public class SizeSpec
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Styles { get; set; } = new();
}

public class StateSpec
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Styles { get; set; } = new();
    public string? Condition { get; set; }
}

public class ComponentStyling
{
    public Dictionary<string, string> Base { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> ByElement { get; set; } = new();
}

public class ComponentAccessibility
{
    public string Role { get; set; } = string.Empty;
    public Dictionary<string, string> AriaAttributes { get; set; } = new();
    public Dictionary<string, string> KeyboardInteractions { get; set; } = new();
    public string[] Requirements { get; set; } = Array.Empty<string>();
}

public class ComponentBehavior
{
    public Dictionary<string, string> Interactions { get; set; } = new();
    public Dictionary<string, string> Animations { get; set; } = new();
    public string[] Features { get; set; } = Array.Empty<string>();
}

