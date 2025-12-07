using DesignAgent.Server.Models.Brand;
using DesignAgent.Server.Models.Questionnaire;
using System.Text;

namespace DesignAgent.Server.Services;

public class ComponentSpecService : IComponentSpecService
{
    private readonly ILogger<ComponentSpecService> _logger;

    public ComponentSpecService(ILogger<ComponentSpecService> logger)
    {
        _logger = logger;
    }

    public Dictionary<string, ComponentSpec> GenerateComponentSpecs(ParsedBrandInput input, DesignTokens tokens)
    {
        var components = new Dictionary<string, ComponentSpec>();
        
        // Always include core components
        components["Button"] = GenerateButtonSpec(tokens);
        components["Input"] = GenerateInputSpec(tokens);
        components["Card"] = GenerateCardSpec(tokens);
        components["Badge"] = GenerateBadgeSpec(tokens);
        components["Alert"] = GenerateAlertSpec(tokens);
        components["Modal"] = GenerateModalSpec(tokens);
        components["Tooltip"] = GenerateTooltipSpec(tokens);
        
        // Add based on component types requested
        if (input.ComponentTypes.Any(t => t.Contains("Dashboard") || t.Contains("Visualization")))
        {
            components["DataCard"] = GenerateDataCardSpec(tokens);
            components["Stat"] = GenerateStatSpec(tokens);
            components["Progress"] = GenerateProgressSpec(tokens);
        }
        
        if (input.ComponentTypes.Any(t => t.Contains("Table") || t.Contains("List")))
        {
            components["DataTable"] = GenerateDataTableSpec(tokens);
            components["List"] = GenerateListSpec(tokens);
        }
        
        if (input.ComponentTypes.Any(t => t.Contains("Form")))
        {
            components["Textarea"] = GenerateTextareaSpec(tokens);
            components["Select"] = GenerateSelectSpec(tokens);
            components["Checkbox"] = GenerateCheckboxSpec(tokens);
            components["Radio"] = GenerateRadioSpec(tokens);
            components["Switch"] = GenerateSwitchSpec(tokens);
        }
        
        if (input.ComponentTypes.Any(t => t.Contains("Navigation")))
        {
            components["Navbar"] = GenerateNavbarSpec(tokens);
            components["Sidebar"] = GenerateSidebarSpec(tokens);
            components["Tabs"] = GenerateTabsSpec(tokens);
            components["Breadcrumbs"] = GenerateBreadcrumbsSpec(tokens);
        }
        
        _logger.LogInformation("Generated {Count} component specs", components.Count);
        
        return components;
    }

    public ComponentSpec? GetComponentSpec(string componentName, string context)
    {
        // Would look up from stored brand
        return null;
    }

    public string GenerateComponentGuidance(string componentName, BrandDefinition brand)
    {
        if (!brand.Components.TryGetValue(componentName, out var spec))
        {
            return $"No specification found for component '{componentName}'";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine($"# {componentName} Component Specification\n");
        sb.AppendLine($"**Description:** {spec.Description}\n");
        sb.AppendLine($"**Category:** {spec.Category}\n");
        
        sb.AppendLine("## Styling");
        foreach (var (key, value) in spec.Styling.Base)
        {
            sb.AppendLine($"- `{key}`: {value}");
        }
        
        sb.AppendLine("\n## Variants");
        foreach (var (variantName, variant) in spec.Variants)
        {
            sb.AppendLine($"### {variantName}");
            foreach (var (key, value) in variant.Styles)
            {
                sb.AppendLine($"- `{key}`: {value}");
            }
        }
        
        sb.AppendLine("\n## Accessibility");
        sb.AppendLine($"- **Role:** {spec.Accessibility.Role}");
        foreach (var req in spec.Accessibility.Requirements)
        {
            sb.AppendLine($"- {req}");
        }
        
        return sb.ToString();
    }

    #region Component Generators

    private ComponentSpec GenerateButtonSpec(DesignTokens tokens)
    {
        return new ComponentSpec
        {
            Name = "Button",
            Description = "Clickable action trigger",
            Category = "action",
            Variants = new Dictionary<string, VariantSpec>
            {
                ["solid"] = new VariantSpec
                {
                    Name = "Solid",
                    Styles = new Dictionary<string, string>
                    {
                        ["background"] = "var(--color-primary)",
                        ["color"] = "white",
                        ["border"] = "none"
                    }
                },
                ["outline"] = new VariantSpec
                {
                    Name = "Outline",
                    Styles = new Dictionary<string, string>
                    {
                        ["background"] = "transparent",
                        ["color"] = "var(--color-primary)",
                        ["border"] = "1px solid var(--color-primary)"
                    }
                },
                ["ghost"] = new VariantSpec
                {
                    Name = "Ghost",
                    Styles = new Dictionary<string, string>
                    {
                        ["background"] = "transparent",
                        ["color"] = "var(--text-primary)",
                        ["border"] = "none"
                    }
                }
            },
            Sizes = new Dictionary<string, SizeSpec>
            {
                ["sm"] = new SizeSpec { Styles = new() { ["height"] = "32px", ["padding"] = "0 var(--space-3)", ["fontSize"] = "var(--text-sm)" } },
                ["md"] = new SizeSpec { Styles = new() { ["height"] = "40px", ["padding"] = "0 var(--space-4)", ["fontSize"] = "var(--text-sm)" } },
                ["lg"] = new SizeSpec { Styles = new() { ["height"] = "48px", ["padding"] = "0 var(--space-6)", ["fontSize"] = "var(--text-base)" } }
            },
            States = new Dictionary<string, StateSpec>
            {
                ["hover"] = new StateSpec { Styles = new() { ["filter"] = "brightness(0.9)" } },
                ["active"] = new StateSpec { Styles = new() { ["transform"] = "scale(0.98)" } },
                ["disabled"] = new StateSpec { Styles = new() { ["opacity"] = "0.5", ["cursor"] = "not-allowed" } },
                ["focus"] = new StateSpec { Styles = new() { ["outline"] = "2px solid var(--color-primary)", ["outlineOffset"] = "2px" } }
            },
            Styling = new ComponentStyling
            {
                Base = new Dictionary<string, string>
                {
                    ["fontWeight"] = "500",
                    ["borderRadius"] = "var(--radius-md)",
                    ["cursor"] = "pointer",
                    ["transition"] = "all 150ms ease",
                    ["display"] = "inline-flex",
                    ["alignItems"] = "center",
                    ["justifyContent"] = "center",
                    ["gap"] = "var(--space-2)"
                }
            },
            Accessibility = new ComponentAccessibility
            {
                Role = "button",
                AriaAttributes = new Dictionary<string, string>
                {
                    ["aria-label"] = "Required if icon-only",
                    ["aria-disabled"] = "true when disabled",
                    ["aria-pressed"] = "For toggle buttons"
                },
                KeyboardInteractions = new Dictionary<string, string>
                {
                    ["Enter"] = "Activate button",
                    ["Space"] = "Activate button"
                },
                Requirements = new[]
                {
                    "Minimum touch target 44x44px",
                    "Visible focus indicator",
                    "Color contrast 4.5:1 minimum"
                }
            }
        };
    }

    private ComponentSpec GenerateInputSpec(DesignTokens tokens)
    {
        return new ComponentSpec
        {
            Name = "Input",
            Description = "Text input field",
            Category = "form",
            Styling = new ComponentStyling
            {
                Base = new Dictionary<string, string>
                {
                    ["background"] = "var(--surface)",
                    ["border"] = "1px solid var(--border)",
                    ["borderRadius"] = "var(--radius-md)",
                    ["padding"] = "var(--space-3)",
                    ["fontSize"] = "var(--text-base)",
                    ["color"] = "var(--text-primary)",
                    ["width"] = "100%"
                }
            },
            States = new Dictionary<string, StateSpec>
            {
                ["focus"] = new StateSpec { Styles = new() { ["borderColor"] = "var(--color-primary)", ["outline"] = "none", ["boxShadow"] = "0 0 0 3px var(--color-primary-subtle)" } },
                ["error"] = new StateSpec { Styles = new() { ["borderColor"] = "var(--color-error)" } },
                ["disabled"] = new StateSpec { Styles = new() { ["opacity"] = "0.6", ["cursor"] = "not-allowed" } }
            },
            Accessibility = new ComponentAccessibility
            {
                Role = "textbox",
                AriaAttributes = new Dictionary<string, string>
                {
                    ["aria-label"] = "Required if no visible label",
                    ["aria-invalid"] = "true when error",
                    ["aria-describedby"] = "ID of help text"
                },
                Requirements = new[]
                {
                    "Associated label element",
                    "Clear error messages",
                    "Visible focus state"
                }
            }
        };
    }

    private ComponentSpec GenerateCardSpec(DesignTokens tokens)
    {
        return new ComponentSpec
        {
            Name = "Card",
            Description = "Contained content surface",
            Category = "layout",
            Variants = new Dictionary<string, VariantSpec>
            {
                ["elevated"] = new VariantSpec { Styles = new() { ["boxShadow"] = "var(--shadow-md)" } },
                ["outlined"] = new VariantSpec { Styles = new() { ["border"] = "1px solid var(--border)" } },
                ["filled"] = new VariantSpec { Styles = new() { ["background"] = "var(--surface)" } }
            },
            Styling = new ComponentStyling
            {
                Base = new Dictionary<string, string>
                {
                    ["background"] = "var(--surface)",
                    ["borderRadius"] = "var(--radius-lg)",
                    ["padding"] = "var(--space-6)"
                }
            },
            Accessibility = new ComponentAccessibility
            {
                Role = "article",
                Requirements = new[] { "Semantic HTML structure", "Heading if standalone content" }
            }
        };
    }

    private ComponentSpec GenerateBadgeSpec(DesignTokens tokens) => new()
    {
        Name = "Badge",
        Description = "Status or count indicator",
        Category = "feedback",
        Styling = new ComponentStyling
        {
            Base = new Dictionary<string, string>
            {
                ["borderRadius"] = "var(--radius-full)",
                ["padding"] = "0 var(--space-2)",
                ["fontSize"] = "var(--text-xs)",
                ["fontWeight"] = "500"
            }
        }
    };

    private ComponentSpec GenerateAlertSpec(DesignTokens tokens) => new()
    {
        Name = "Alert",
        Description = "Contextual feedback message",
        Category = "feedback",
        Variants = new Dictionary<string, VariantSpec>
        {
            ["info"] = new() { Styles = new() { ["background"] = "var(--color-info-subtle)", ["borderLeft"] = "4px solid var(--color-info)" } },
            ["success"] = new() { Styles = new() { ["background"] = "var(--color-success-subtle)", ["borderLeft"] = "4px solid var(--color-success)" } },
            ["warning"] = new() { Styles = new() { ["background"] = "var(--color-warning-subtle)", ["borderLeft"] = "4px solid var(--color-warning)" } },
            ["error"] = new() { Styles = new() { ["background"] = "var(--color-error-subtle)", ["borderLeft"] = "4px solid var(--color-error)" } }
        },
        Accessibility = new ComponentAccessibility
        {
            Role = "alert",
            AriaAttributes = new() { ["aria-live"] = "polite" }
        }
    };

    private ComponentSpec GenerateModalSpec(DesignTokens tokens) => new()
    {
        Name = "Modal",
        Description = "Overlay dialog window",
        Category = "overlay",
        Styling = new ComponentStyling
        {
            Base = new Dictionary<string, string>
            {
                ["background"] = "var(--surface)",
                ["borderRadius"] = "var(--radius-xl)",
                ["boxShadow"] = "var(--shadow-xl)",
                ["maxWidth"] = "560px",
                ["maxHeight"] = "90vh"
            }
        },
        Accessibility = new ComponentAccessibility
        {
            Role = "dialog",
            AriaAttributes = new() { ["aria-modal"] = "true", ["aria-labelledby"] = "title ID" },
            Requirements = new[] { "Focus trap", "Close on Escape", "Return focus on close" }
        }
    };

    private ComponentSpec GenerateTooltipSpec(DesignTokens tokens) => new()
    {
        Name = "Tooltip",
        Description = "Brief helper text on hover",
        Category = "overlay",
        Styling = new ComponentStyling
        {
            Base = new Dictionary<string, string>
            {
                ["background"] = "var(--text-primary)",
                ["color"] = "var(--bg)",
                ["padding"] = "var(--space-2) var(--space-3)",
                ["borderRadius"] = "var(--radius-md)",
                ["fontSize"] = "var(--text-sm)"
            }
        },
        Accessibility = new ComponentAccessibility
        {
            Role = "tooltip",
            Requirements = new[] { "Connected via aria-describedby" }
        }
    };

    private ComponentSpec GenerateDataCardSpec(DesignTokens tokens) => new()
    {
        Name = "DataCard",
        Description = "Dashboard metric card",
        Category = "data",
        Structure = new ComponentStructure
        {
            Elements = new Dictionary<string, StructureElement>
            {
                ["header"] = new() { Required = false, Contains = new[] { "icon", "title", "action" } },
                ["metric"] = new() { Required = true, Type = "text" },
                ["trend"] = new() { Required = false, Type = "indicator" }
            }
        }
    };

    private ComponentSpec GenerateStatSpec(DesignTokens tokens) => new()
    {
        Name = "Stat",
        Description = "Key metric display",
        Category = "data",
        Styling = new ComponentStyling
        {
            Base = new Dictionary<string, string>
            {
                ["value-fontSize"] = "var(--text-3xl)",
                ["value-fontWeight"] = "700",
                ["label-fontSize"] = "var(--text-sm)",
                ["label-color"] = "var(--text-secondary)"
            }
        }
    };

    private ComponentSpec GenerateProgressSpec(DesignTokens tokens) => new()
    {
        Name = "Progress",
        Description = "Progress indicator",
        Category = "feedback",
        Variants = new Dictionary<string, VariantSpec>
        {
            ["linear"] = new() { Styles = new() { ["height"] = "8px" } },
            ["circular"] = new() { Styles = new() { ["size"] = "40px" } }
        },
        Accessibility = new ComponentAccessibility
        {
            Role = "progressbar",
            AriaAttributes = new() { ["aria-valuenow"] = "current", ["aria-valuemin"] = "0", ["aria-valuemax"] = "100" }
        }
    };

    private ComponentSpec GenerateDataTableSpec(DesignTokens tokens) => new()
    {
        Name = "DataTable",
        Description = "Data table with sorting and pagination",
        Category = "data",
        Accessibility = new ComponentAccessibility
        {
            Role = "table",
            Requirements = new[] { "Sortable headers have aria-sort", "Pagination is keyboard accessible" }
        }
    };

    private ComponentSpec GenerateListSpec(DesignTokens tokens) => new()
    {
        Name = "List",
        Description = "Vertical list of items",
        Category = "data",
        Accessibility = new ComponentAccessibility { Role = "list" }
    };

    private ComponentSpec GenerateTextareaSpec(DesignTokens tokens) => new()
    {
        Name = "Textarea",
        Description = "Multi-line text input",
        Category = "form",
        Styling = new ComponentStyling
        {
            Base = new Dictionary<string, string>
            {
                ["minHeight"] = "80px",
                ["resize"] = "vertical"
            }
        }
    };

    private ComponentSpec GenerateSelectSpec(DesignTokens tokens) => new()
    {
        Name = "Select",
        Description = "Dropdown selection",
        Category = "form",
        Accessibility = new ComponentAccessibility
        {
            Role = "combobox",
            AriaAttributes = new() { ["aria-expanded"] = "true when open", ["aria-haspopup"] = "listbox" }
        }
    };

    private ComponentSpec GenerateCheckboxSpec(DesignTokens tokens) => new()
    {
        Name = "Checkbox",
        Description = "Boolean or multi-select option",
        Category = "form",
        Accessibility = new ComponentAccessibility
        {
            Role = "checkbox",
            AriaAttributes = new() { ["aria-checked"] = "true/false/mixed" }
        }
    };

    private ComponentSpec GenerateRadioSpec(DesignTokens tokens) => new()
    {
        Name = "Radio",
        Description = "Single selection from group",
        Category = "form",
        Accessibility = new ComponentAccessibility
        {
            Role = "radio",
            AriaAttributes = new() { ["aria-checked"] = "true/false" }
        }
    };

    private ComponentSpec GenerateSwitchSpec(DesignTokens tokens) => new()
    {
        Name = "Switch",
        Description = "On/off toggle",
        Category = "form",
        Accessibility = new ComponentAccessibility
        {
            Role = "switch",
            AriaAttributes = new() { ["aria-checked"] = "true/false" }
        }
    };

    private ComponentSpec GenerateNavbarSpec(DesignTokens tokens) => new()
    {
        Name = "Navbar",
        Description = "Top navigation bar",
        Category = "navigation",
        Accessibility = new ComponentAccessibility
        {
            Role = "navigation",
            AriaAttributes = new() { ["aria-label"] = "Main navigation" }
        }
    };

    private ComponentSpec GenerateSidebarSpec(DesignTokens tokens) => new()
    {
        Name = "Sidebar",
        Description = "Vertical side navigation",
        Category = "navigation",
        Accessibility = new ComponentAccessibility
        {
            Role = "navigation",
            AriaAttributes = new() { ["aria-label"] = "Sidebar navigation" }
        }
    };

    private ComponentSpec GenerateTabsSpec(DesignTokens tokens) => new()
    {
        Name = "Tabs",
        Description = "Tabbed content navigation",
        Category = "navigation",
        Accessibility = new ComponentAccessibility
        {
            Role = "tablist",
            KeyboardInteractions = new() { ["ArrowLeft"] = "Previous tab", ["ArrowRight"] = "Next tab" }
        }
    };

    private ComponentSpec GenerateBreadcrumbsSpec(DesignTokens tokens) => new()
    {
        Name = "Breadcrumbs",
        Description = "Navigation path indicator",
        Category = "navigation",
        Accessibility = new ComponentAccessibility
        {
            Role = "navigation",
            AriaAttributes = new() { ["aria-label"] = "Breadcrumb" }
        }
    };

    #endregion
}

