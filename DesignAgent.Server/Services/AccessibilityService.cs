using DesignAgent.Server.Models;
using System.Text.RegularExpressions;

namespace DesignAgent.Server.Services;

public class AccessibilityService : IAccessibilityService
{
    public AccessibilityValidationResult ValidateAccessibility(string code, string wcagLevel = "AA")
    {
        var issues = new List<AccessibilityIssue>();
        
        issues.AddRange(CheckAriaAttributes(code));
        issues.AddRange(CheckKeyboardAccessibility(code));
        issues.AddRange(CheckSemanticHtml(code));
        issues.AddRange(CheckImageAlt(code));
        issues.AddRange(CheckFormLabels(code));
        issues.AddRange(CheckHeadingHierarchy(code));
        
        var score = CalculateA11yScore(issues);
        
        return new AccessibilityValidationResult
        {
            WcagLevel = wcagLevel,
            Passes = score >= 8,
            Score = score,
            Issues = issues
        };
    }

    public ContrastCheckResult CheckContrast(string foreground, string background)
    {
        var fgLuminance = CalculateLuminance(foreground);
        var bgLuminance = CalculateLuminance(background);
        
        var lighter = Math.Max(fgLuminance, bgLuminance);
        var darker = Math.Min(fgLuminance, bgLuminance);
        
        var ratio = (lighter + 0.05) / (darker + 0.05);
        
        return new ContrastCheckResult
        {
            Foreground = foreground,
            Background = background,
            Ratio = Math.Round(ratio, 2),
            PassesAA = ratio >= 4.5,
            PassesAAA = ratio >= 7.0,
            PassesLargeTextAA = ratio >= 3.0,
            RequiredRatio = "4.5:1 for normal text, 3:1 for large text"
        };
    }

    public List<AccessibilityIssue> CheckAriaAttributes(string code)
    {
        var issues = new List<AccessibilityIssue>();
        
        // Check buttons without aria-label
        var iconButtons = Regex.Matches(code, @"<button[^>]*>\s*<(svg|i|span class=""icon|Icon)[^>]*>[^<]*</(svg|i|span|Icon)>\s*</button>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match match in iconButtons)
        {
            if (!match.Value.Contains("aria-label"))
            {
                issues.Add(new AccessibilityIssue
                {
                    Criterion = "4.1.2 Name, Role, Value",
                    Level = "A",
                    Severity = IssueSeverity.High,
                    Message = "Icon-only button missing aria-label",
                    Element = match.Value.Substring(0, Math.Min(50, match.Value.Length)) + "...",
                    Fix = "Add aria-label describing the button's action"
                });
            }
        }
        
        // Check for aria-hidden on focusable elements
        var hiddenFocusable = Regex.Matches(code, @"<(button|a|input)[^>]*aria-hidden=""true""", RegexOptions.IgnoreCase);
        foreach (Match match in hiddenFocusable)
        {
            issues.Add(new AccessibilityIssue
            {
                Criterion = "4.1.2 Name, Role, Value",
                Level = "A",
                Severity = IssueSeverity.Critical,
                Message = "Focusable element has aria-hidden='true'",
                Element = match.Value,
                Fix = "Remove aria-hidden or make element non-focusable"
            });
        }
        
        return issues;
    }

    public List<AccessibilityIssue> CheckKeyboardAccessibility(string code)
    {
        var issues = new List<AccessibilityIssue>();
        
        // Check for click handlers without keyboard handlers
        var clickOnly = Regex.Matches(code, @"@onclick=""[^""]*""(?![^>]*@onkeydown)", RegexOptions.IgnoreCase);
        foreach (Match match in clickOnly)
        {
            // Check if it's on an inherently interactive element
            var context = GetSurroundingContext(code, match.Index);
            if (!Regex.IsMatch(context, @"<(button|a|input|select)", RegexOptions.IgnoreCase))
            {
                issues.Add(new AccessibilityIssue
                {
                    Criterion = "2.1.1 Keyboard",
                    Level = "A",
                    Severity = IssueSeverity.High,
                    Message = "Click handler on non-interactive element without keyboard support",
                    Element = context,
                    Fix = "Add @onkeydown handler or use a <button> element"
                });
            }
        }
        
        // Check for tabindex > 0
        var posTabindex = Regex.Matches(code, @"tabindex=""([2-9]|\d{2,})""", RegexOptions.IgnoreCase);
        foreach (Match match in posTabindex)
        {
            issues.Add(new AccessibilityIssue
            {
                Criterion = "2.4.3 Focus Order",
                Level = "A",
                Severity = IssueSeverity.Medium,
                Message = $"Positive tabindex ({match.Groups[1].Value}) disrupts natural focus order",
                Element = match.Value,
                Fix = "Use tabindex='0' or restructure DOM for natural focus order"
            });
        }
        
        return issues;
    }

    public List<AccessibilityIssue> CheckSemanticHtml(string code)
    {
        var issues = new List<AccessibilityIssue>();
        
        // Check for divs that should be buttons
        var clickableDivs = Regex.Matches(code, @"<div[^>]*@onclick", RegexOptions.IgnoreCase);
        foreach (Match match in clickableDivs)
        {
            issues.Add(new AccessibilityIssue
            {
                Criterion = "4.1.2 Name, Role, Value",
                Level = "A",
                Severity = IssueSeverity.High,
                Message = "Clickable div should be a button for accessibility",
                Element = match.Value.Substring(0, Math.Min(50, match.Value.Length)),
                Fix = "Replace <div @onclick> with <button>"
            });
        }
        
        // Check for missing landmark regions
        if (!code.Contains("<main") && !code.Contains("<Main") && code.Length > 500)
        {
            issues.Add(new AccessibilityIssue
            {
                Criterion = "1.3.1 Info and Relationships",
                Level = "A",
                Severity = IssueSeverity.Low,
                Message = "Page appears to lack <main> landmark",
                Fix = "Wrap main content in <main> element"
            });
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckImageAlt(string code)
    {
        var issues = new List<AccessibilityIssue>();
        
        // Check for images without alt
        var imagesNoAlt = Regex.Matches(code, @"<img(?![^>]*alt=)[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match match in imagesNoAlt)
        {
            issues.Add(new AccessibilityIssue
            {
                Criterion = "1.1.1 Non-text Content",
                Level = "A",
                Severity = IssueSeverity.Critical,
                Message = "Image missing alt attribute",
                Element = match.Value,
                Fix = "Add alt attribute (use alt='' for decorative images)"
            });
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckFormLabels(string code)
    {
        var issues = new List<AccessibilityIssue>();
        
        // Check for inputs without labels
        var inputs = Regex.Matches(code, @"<input[^>]*id=""([^""]*)""[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match match in inputs)
        {
            var inputId = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(inputId))
            {
                // Check if there's a label for this input
                if (!Regex.IsMatch(code, $@"<label[^>]*for=""{inputId}""", RegexOptions.IgnoreCase) &&
                    !match.Value.Contains("aria-label"))
                {
                    issues.Add(new AccessibilityIssue
                    {
                        Criterion = "1.3.1 Info and Relationships",
                        Level = "A",
                        Severity = IssueSeverity.High,
                        Message = $"Input '{inputId}' has no associated label",
                        Element = match.Value,
                        Fix = $"Add <label for=\"{inputId}\"> or aria-label attribute"
                    });
                }
            }
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckHeadingHierarchy(string code)
    {
        var issues = new List<AccessibilityIssue>();
        
        var headings = Regex.Matches(code, @"<h(\d)", RegexOptions.IgnoreCase);
        var levels = new List<int>();
        
        foreach (Match match in headings)
        {
            levels.Add(int.Parse(match.Groups[1].Value));
        }
        
        // Check for skipped levels
        for (int i = 1; i < levels.Count; i++)
        {
            if (levels[i] > levels[i - 1] + 1)
            {
                issues.Add(new AccessibilityIssue
                {
                    Criterion = "1.3.1 Info and Relationships",
                    Level = "A",
                    Severity = IssueSeverity.Medium,
                    Message = $"Heading hierarchy skips from h{levels[i - 1]} to h{levels[i]}",
                    Fix = "Ensure heading levels are sequential (h1 → h2 → h3)"
                });
            }
        }
        
        // Check for multiple h1s
        var h1Count = levels.Count(l => l == 1);
        if (h1Count > 1)
        {
            issues.Add(new AccessibilityIssue
            {
                Criterion = "1.3.1 Info and Relationships",
                Level = "A",
                Severity = IssueSeverity.Low,
                Message = $"Multiple h1 elements found ({h1Count})",
                Fix = "Use only one h1 per page"
            });
        }
        
        return issues;
    }

    private double CalculateLuminance(string hexColor)
    {
        // Remove # if present
        hexColor = hexColor.TrimStart('#');
        
        // Handle 3-digit hex
        if (hexColor.Length == 3)
        {
            hexColor = $"{hexColor[0]}{hexColor[0]}{hexColor[1]}{hexColor[1]}{hexColor[2]}{hexColor[2]}";
        }
        
        if (hexColor.Length != 6) return 0;
        
        var r = int.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255.0;
        
        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
        
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private string GetSurroundingContext(string code, int index)
    {
        var start = Math.Max(0, code.LastIndexOf('<', index));
        var end = Math.Min(code.Length, code.IndexOf('>', index) + 1);
        return code.Substring(start, end - start);
    }

    private int CalculateA11yScore(List<AccessibilityIssue> issues)
    {
        if (issues.Count == 0) return 10;
        
        var deductions = issues.Sum(i => i.Severity switch
        {
            IssueSeverity.Critical => 3.0,
            IssueSeverity.High => 1.5,
            IssueSeverity.Medium => 0.5,
            IssueSeverity.Low => 0.2,
            _ => 0.1
        });
        
        return Math.Max(0, (int)Math.Round(10 - deductions));
    }
}

