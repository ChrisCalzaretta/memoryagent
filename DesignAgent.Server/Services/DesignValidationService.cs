using DesignAgent.Server.Models;
using DesignAgent.Server.Models.Brand;
using System.Text.RegularExpressions;

namespace DesignAgent.Server.Services;

public class DesignValidationService : IDesignValidationService
{
    private readonly IBrandService _brandService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly ILogger<DesignValidationService> _logger;

    public DesignValidationService(
        IBrandService brandService,
        IAccessibilityService accessibilityService,
        ILogger<DesignValidationService> logger)
    {
        _brandService = brandService;
        _accessibilityService = accessibilityService;
        _logger = logger;
    }

    public async Task<DesignValidationResult> ValidateAsync(string context, string code, CancellationToken cancellationToken = default)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return new DesignValidationResult
            {
                IsCompliant = false,
                Score = 0,
                Grade = "F",
                Issues = new List<DesignIssue>
                {
                    new() { Type = "brand", Severity = IssueSeverity.Critical, Message = $"Brand context '{context}' not found" }
                }
            };
        }

        var issues = new List<DesignIssue>();
        
        // Check all compliance areas
        issues.AddRange(CheckColorCompliance(code, brand));
        issues.AddRange(CheckTypographyCompliance(code, brand));
        issues.AddRange(CheckSpacingCompliance(code, brand));
        issues.AddRange(CheckComponentCompliance(code, brand));
        
        // Check accessibility
        var a11yResult = _accessibilityService.ValidateAccessibility(code, brand.Accessibility.Level);
        issues.AddRange(a11yResult.Issues.Select(i => new DesignIssue
        {
            Type = "accessibility",
            Severity = i.Severity,
            Message = i.Message,
            Fix = i.Fix
        }));

        // Calculate score
        var score = CalculateScore(issues);
        var grade = CalculateGrade(score);

        return new DesignValidationResult
        {
            IsCompliant = score >= 8,
            Score = score,
            Grade = grade,
            Issues = issues,
            Summary = new ValidationSummary
            {
                TotalIssues = issues.Count,
                Critical = issues.Count(i => i.Severity == IssueSeverity.Critical),
                High = issues.Count(i => i.Severity == IssueSeverity.High),
                Medium = issues.Count(i => i.Severity == IssueSeverity.Medium),
                Low = issues.Count(i => i.Severity == IssueSeverity.Low),
                ByType = issues.GroupBy(i => i.Type).ToDictionary(g => g.Key, g => g.Count())
            }
        };
    }

    public async Task<DesignValidationResult> ValidateFilesAsync(string context, Dictionary<string, string> files, CancellationToken cancellationToken = default)
    {
        var allIssues = new List<DesignIssue>();
        
        foreach (var (filePath, code) in files)
        {
            var result = await ValidateAsync(context, code, cancellationToken);
            foreach (var issue in result.Issues)
            {
                issue.FilePath = filePath;
                allIssues.Add(issue);
            }
        }
        
        var score = CalculateScore(allIssues);
        
        return new DesignValidationResult
        {
            IsCompliant = score >= 8,
            Score = score,
            Grade = CalculateGrade(score),
            Issues = allIssues,
            Summary = new ValidationSummary
            {
                TotalIssues = allIssues.Count,
                Critical = allIssues.Count(i => i.Severity == IssueSeverity.Critical),
                High = allIssues.Count(i => i.Severity == IssueSeverity.High),
                Medium = allIssues.Count(i => i.Severity == IssueSeverity.Medium),
                Low = allIssues.Count(i => i.Severity == IssueSeverity.Low)
            }
        };
    }

    public List<DesignIssue> CheckColorCompliance(string code, BrandDefinition brand)
    {
        var issues = new List<DesignIssue>();
        
        // Check for hardcoded colors that should use variables
        var hardcodedColors = FindHardcodedColors(code);
        foreach (var (color, lineNum) in hardcodedColors)
        {
            var suggestion = SuggestColorVariable(color, brand);
            if (suggestion != null)
            {
                issues.Add(new DesignIssue
                {
                    Type = "color",
                    Severity = IssueSeverity.Medium,
                    Message = $"Hardcoded color '{color}' should use design token",
                    LineNumber = lineNum,
                    Fix = $"Replace '{color}' with 'var({suggestion})'",
                    FixCode = $"var({suggestion})"
                });
            }
        }
        
        // Check for non-brand colors
        var grayColors = Regex.Matches(code, @"(bg|text|border)-gray-\d+", RegexOptions.IgnoreCase);
        foreach (Match match in grayColors)
        {
            issues.Add(new DesignIssue
            {
                Type = "color",
                Severity = IssueSeverity.Medium,
                Message = $"Using generic gray '{match.Value}', consider using brand neutrals",
                Fix = $"Replace with slate or brand neutral equivalent"
            });
        }
        
        return issues;
    }

    public List<DesignIssue> CheckTypographyCompliance(string code, BrandDefinition brand)
    {
        var issues = new List<DesignIssue>();
        
        // Check for non-brand fonts
        var fontMatches = Regex.Matches(code, @"font-family:\s*([^;]+)", RegexOptions.IgnoreCase);
        foreach (Match match in fontMatches)
        {
            var fontValue = match.Groups[1].Value.Trim();
            if (!fontValue.Contains("var(--font-") && 
                !fontValue.Contains(brand.Tokens.Typography.FontFamilySans) &&
                !fontValue.Contains("system-ui"))
            {
                issues.Add(new DesignIssue
                {
                    Type = "typography",
                    Severity = IssueSeverity.Low,
                    Message = $"Non-brand font family detected: '{fontValue}'",
                    Fix = $"Use var(--font-sans) or var(--font-mono)"
                });
            }
        }
        
        // Check for off-scale font sizes
        var fontSizes = Regex.Matches(code, @"font-size:\s*(\d+)px", RegexOptions.IgnoreCase);
        foreach (Match match in fontSizes)
        {
            var size = int.Parse(match.Groups[1].Value);
            if (!IsOnScale(size, new[] { 12, 14, 16, 18, 20, 24, 30, 36, 48, 60 }))
            {
                issues.Add(new DesignIssue
                {
                    Type = "typography",
                    Severity = IssueSeverity.Low,
                    Message = $"Font size {size}px is off the type scale",
                    Fix = $"Use a scale value: var(--text-sm), var(--text-base), etc."
                });
            }
        }
        
        return issues;
    }

    public List<DesignIssue> CheckSpacingCompliance(string code, BrandDefinition brand)
    {
        var issues = new List<DesignIssue>();
        
        // Check for off-scale spacing values
        var spacingProps = new[] { "padding", "margin", "gap" };
        foreach (var prop in spacingProps)
        {
            var matches = Regex.Matches(code, $@"{prop}[^:]*:\s*(\d+)px", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var value = int.Parse(match.Groups[1].Value);
                if (!IsOnScale(value, new[] { 0, 4, 8, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96 }))
                {
                    issues.Add(new DesignIssue
                    {
                        Type = "spacing",
                        Severity = IssueSeverity.Low,
                        Message = $"Spacing value {value}px is off the 8px scale",
                        Fix = $"Use a scale value like var(--space-2), var(--space-4), etc."
                    });
                }
            }
        }
        
        return issues;
    }

    public List<DesignIssue> CheckComponentCompliance(string code, BrandDefinition brand)
    {
        var issues = new List<DesignIssue>();
        
        // Check for buttons without proper styling
        if (code.Contains("<button") && !code.Contains("btn") && !code.Contains("class=\""))
        {
            issues.Add(new DesignIssue
            {
                Type = "component",
                Severity = IssueSeverity.Medium,
                Message = "Button element found without styling class",
                Fix = "Add appropriate button styling class (btn-primary, btn-secondary, etc.)"
            });
        }
        
        // Check for interactive elements without focus styles
        if (Regex.IsMatch(code, @"<(button|a|input|select)", RegexOptions.IgnoreCase))
        {
            if (!code.Contains("focus:") && !code.Contains(":focus"))
            {
                issues.Add(new DesignIssue
                {
                    Type = "accessibility",
                    Severity = IssueSeverity.High,
                    Message = "Interactive elements detected without focus styles",
                    Fix = "Add focus:ring or :focus styles for keyboard users"
                });
            }
        }
        
        return issues;
    }

    private List<(string color, int line)> FindHardcodedColors(string code)
    {
        var results = new List<(string, int)>();
        var lines = code.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var hexMatches = Regex.Matches(lines[i], @"#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})\b");
            foreach (Match match in hexMatches)
            {
                results.Add((match.Value, i + 1));
            }
            
            var rgbMatches = Regex.Matches(lines[i], @"rgb\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*\)");
            foreach (Match match in rgbMatches)
            {
                results.Add((match.Value, i + 1));
            }
        }
        
        return results;
    }

    private string? SuggestColorVariable(string color, BrandDefinition brand)
    {
        var normalizedColor = color.ToUpper();
        
        // Check against brand colors
        if (normalizedColor == brand.Tokens.Colors.Primary.ToUpper())
            return "--color-primary";
        if (normalizedColor == brand.Tokens.Colors.Success.ToUpper())
            return "--color-success";
        if (normalizedColor == brand.Tokens.Colors.Warning.ToUpper())
            return "--color-warning";
        if (normalizedColor == brand.Tokens.Colors.Error.ToUpper())
            return "--color-error";
        
        // Check neutrals
        foreach (var (key, value) in brand.Tokens.Colors.Neutrals)
        {
            if (normalizedColor == value.ToUpper())
                return $"--color-neutral-{key}";
        }
        
        // Generic suggestion for any hardcoded color
        return "--color-[appropriate-token]";
    }

    private bool IsOnScale(int value, int[] scale)
    {
        return scale.Contains(value) || value == 0;
    }

    private int CalculateScore(List<DesignIssue> issues)
    {
        if (issues.Count == 0) return 10;
        
        var deductions = 0.0;
        foreach (var issue in issues)
        {
            deductions += issue.Severity switch
            {
                IssueSeverity.Critical => 3.0,
                IssueSeverity.High => 1.5,
                IssueSeverity.Medium => 0.5,
                IssueSeverity.Low => 0.2,
                _ => 0.1
            };
        }
        
        return Math.Max(0, (int)Math.Round(10 - deductions));
    }

    private string CalculateGrade(int score)
    {
        return score switch
        {
            >= 9 => "A",
            >= 8 => "B",
            >= 7 => "C",
            >= 6 => "D",
            _ => "F"
        };
    }
}

