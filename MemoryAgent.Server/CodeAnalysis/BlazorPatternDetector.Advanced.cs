using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Advanced Blazor pattern detection - covers additional patterns not in main detector
/// Covers: CascadingValue, ErrorBoundary, Virtualize, Layouts, Generic Components, Authorization, Streaming Rendering
/// </summary>
public partial class BlazorPatternDetector
{
    private const string BlazorCascadingUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters";
    private const string BlazorErrorHandlingUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/handle-errors";
    private const string BlazorVirtualizationUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/virtualization";
    private const string BlazorLayoutsUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/layouts";
    private const string BlazorAuthUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/security/";
    private const string BlazorStreamingUrl = "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/rendering";

    /// <summary>
    /// Detects advanced Blazor patterns
    /// </summary>
    private List<CodePattern> DetectAdvancedPatterns(string sourceCode, string filePath, string? context, SyntaxNode root)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        patterns.AddRange(DetectCascadingPatterns(lines, root, sourceCode, filePath, context));
        patterns.AddRange(DetectErrorBoundaries(lines, filePath, context));
        patterns.AddRange(DetectVirtualization(lines, filePath, context));
        patterns.AddRange(DetectLayoutPatterns(lines, filePath, context));
        patterns.AddRange(DetectGenericComponents(lines, root, sourceCode, filePath, context));
        patterns.AddRange(DetectAuthorizationPatterns(lines, filePath, context));
        patterns.AddRange(DetectRenderingOptimizations(lines, filePath, context));
        patterns.AddRange(DetectStreamingRendering(root, sourceCode, filePath, context));

        return patterns;
    }

    /// <summary>
    /// Detects CascadingValue and CascadingParameter patterns
    /// </summary>
    private List<CodePattern> DetectCascadingPatterns(
        string[] lines,
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // Detect <CascadingValue> component
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Regex.IsMatch(line, @"<CascadingValue", RegexOptions.IgnoreCase))
            {
                var hasName = line.Contains("Name=");
                var isFixed = line.Contains("IsFixed=\"true\"");
                var valueMatch = Regex.Match(line, @"Value=\""@([^\""]+)\""");
                var value = valueMatch.Success ? valueMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_CascadingValue",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.StateManagement,
                    Implementation = "CascadingValue",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 5),
                    BestPractice = "CascadingValue component for passing data down component tree. " +
                        (hasName ? "Uses named cascading value for multiple values of same type." : "Unnamed cascading value.") +
                        (isFixed ? " IsFixed=true for performance optimization (value won't change)." : " Value can change (consider IsFixed=true if immutable)."),
                    AzureBestPracticeUrl = BlazorCascadingUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["HasName"] = hasName,
                        ["IsFixed"] = isFixed,
                        ["Value"] = value,
                        ["PatternSubType"] = "CascadingValue"
                    }
                });
            }
        }

        // Detect [CascadingParameter] attribute
        var cascadingParams = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(p => p.AttributeLists.Any(al => al.ToString().Contains("CascadingParameter")));

        foreach (var param in cascadingParams)
        {
            var lineNumber = sourceCode.Take(param.SpanStart).Count(c => c == '\n') + 1;
            var paramName = param.Identifier.ToString();
            var paramType = param.Type.ToString();
            
            var nameAttr = param.AttributeLists
                .SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.ToString().Contains("CascadingParameter"));
            var hasName = nameAttr?.ArgumentList?.Arguments.Any(arg => arg.ToString().Contains("Name")) == true;

            patterns.Add(new CodePattern
            {
                Name = "Blazor_CascadingParameter",
                Type = PatternType.Blazor,
                Category = PatternCategory.StateManagement,
                Implementation = $"[CascadingParameter] {paramType}",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                BestPractice = $"Cascading parameter '{paramName}' receives value from ancestor CascadingValue. " +
                    (hasName ? "Uses named parameter to match specific CascadingValue." : "Matches by type. Consider Name attribute if multiple values of same type."),
                AzureBestPracticeUrl = BlazorCascadingUrl,
                Confidence = 0.95f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["ParameterName"] = paramName,
                    ["ParameterType"] = paramType,
                    ["HasName"] = hasName,
                    ["PatternSubType"] = "CascadingParameter"
                }
            });
        }

        return patterns;
    }

    /// <summary>
    /// Detects ErrorBoundary component usage
    /// </summary>
    private List<CodePattern> DetectErrorBoundaries(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Regex.IsMatch(line, @"<ErrorBoundary", RegexOptions.IgnoreCase))
            {
                // Look ahead for ErrorContent
                var hasErrorContent = false;
                for (int j = i; j < Math.Min(i + 10, lines.Length); j++)
                {
                    if (lines[j].Contains("<ErrorContent>"))
                    {
                        hasErrorContent = true;
                        break;
                    }
                }

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_ErrorBoundary",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Reliability,
                    Implementation = "ErrorBoundary",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 8),
                    BestPractice = "ErrorBoundary component catches unhandled exceptions and prevents app crashes. " +
                        (hasErrorContent ? "Includes custom ErrorContent for user-friendly error display." : "Uses default error UI. Consider custom ErrorContent for better UX."),
                    AzureBestPracticeUrl = BlazorErrorHandlingUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["HasErrorContent"] = hasErrorContent,
                        ["PatternSubType"] = "ErrorBoundary"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects Virtualize component for performance optimization
    /// </summary>
    private List<CodePattern> DetectVirtualization(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Regex.IsMatch(line, @"<Virtualize", RegexOptions.IgnoreCase))
            {
                var itemsMatch = Regex.Match(line, @"Items=""@([^""]+)""");
                var itemsSizeMatch = Regex.Match(line, @"ItemSize=""(\d+)""");
                var overscanMatch = Regex.Match(line, @"OverscanCount=""(\d+)""");
                
                var items = itemsMatch.Success ? itemsMatch.Groups[1].Value : "";
                var itemSize = itemsSizeMatch.Success ? int.Parse(itemsSizeMatch.Groups[1].Value) : 0;
                var overscanCount = overscanMatch.Success ? int.Parse(overscanMatch.Groups[1].Value) : 3;

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_Virtualize",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Performance,
                    Implementation = "Virtualize",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 7),
                    BestPractice = $"Virtualize component for rendering large lists efficiently. Only renders visible items. " +
                        (itemSize > 0 ? $"Uses fixed ItemSize={itemSize}px for optimal performance." : "WARNING: Set ItemSize for best performance with fixed-height items.") +
                        $" OverscanCount={overscanCount} items buffered.",
                    AzureBestPracticeUrl = BlazorVirtualizationUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Items"] = items,
                        ["ItemSize"] = itemSize,
                        ["OverscanCount"] = overscanCount,
                        ["PatternSubType"] = "Virtualization"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects layout patterns
    /// </summary>
    private List<CodePattern> DetectLayoutPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // @layout directive
            if (Regex.IsMatch(line, @"^@layout\s+\w+", RegexOptions.IgnoreCase))
            {
                var layoutMatch = Regex.Match(line, @"@layout\s+([\w\.]+)");
                var layoutName = layoutMatch.Success ? layoutMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_LayoutDirective",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.ComponentModel,
                    Implementation = $"@layout {layoutName}",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = $"Component uses {layoutName} layout. Layouts provide consistent page structure across multiple pages.",
                    AzureBestPracticeUrl = BlazorLayoutsUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["LayoutName"] = layoutName,
                        ["PatternSubType"] = "LayoutDirective"
                    }
                });
            }

            // @Body in layouts
            if (Regex.IsMatch(line, @"@Body", RegexOptions.IgnoreCase))
            {
                patterns.Add(new CodePattern
                {
                    Name = "Blazor_LayoutBody",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.ComponentModel,
                    Implementation = "@Body",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 3),
                    BestPractice = "@Body placeholder in layout component. Renders the content of the page using this layout.",
                    AzureBestPracticeUrl = BlazorLayoutsUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["PatternSubType"] = "LayoutBody"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects generic component patterns
    /// </summary>
    private List<CodePattern> DetectGenericComponents(
        string[] lines,
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // @typeparam directive
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Regex.IsMatch(line, @"^@typeparam\s+\w+", RegexOptions.IgnoreCase))
            {
                var typeParamMatch = Regex.Match(line, @"@typeparam\s+(\w+)");
                var typeParam = typeParamMatch.Success ? typeParamMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_TypeParam",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.ComponentModel,
                    Implementation = $"@typeparam {typeParam}",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = $"Generic component with type parameter {typeParam}. Enables reusable components for multiple data types.",
                    AzureBestPracticeUrl = BlazorComponentsUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["TypeParameter"] = typeParam,
                        ["PatternSubType"] = "GenericComponent"
                    }
                });
            }
        }

        // @attribute directive
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Regex.IsMatch(line, @"^@attribute\s+\[", RegexOptions.IgnoreCase))
            {
                var attrMatch = Regex.Match(line, @"@attribute\s+\[([^\]]+)\]");
                var attribute = attrMatch.Success ? attrMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_AttributeDirective",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.ComponentModel,
                    Implementation = $"@attribute [{attribute}]",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = $"Component-level attribute [{attribute}]. Applies attribute to generated component class.",
                    AzureBestPracticeUrl = BlazorComponentsUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Attribute"] = attribute,
                        ["PatternSubType"] = "AttributeDirective"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects authorization patterns
    /// </summary>
    private List<CodePattern> DetectAuthorizationPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // <AuthorizeView>
            if (Regex.IsMatch(line, @"<AuthorizeView", RegexOptions.IgnoreCase))
            {
                var hasPolicy = line.Contains("Policy=");
                var hasRoles = line.Contains("Roles=");
                
                // Look for <Authorized> and <NotAuthorized> sections
                var hasAuthorized = false;
                var hasNotAuthorized = false;
                for (int j = i; j < Math.Min(i + 20, lines.Length); j++)
                {
                    if (lines[j].Contains("<Authorized>")) hasAuthorized = true;
                    if (lines[j].Contains("<NotAuthorized>")) hasNotAuthorized = true;
                }

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_AuthorizeView",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Security,
                    Implementation = "AuthorizeView",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 10),
                    BestPractice = "AuthorizeView for conditional UI based on authorization. " +
                        (hasPolicy ? "Uses authorization policy." : "") +
                        (hasRoles ? "Uses role-based authorization." : "") +
                        (hasAuthorized && hasNotAuthorized ? " Includes both Authorized and NotAuthorized content (best practice)." : " Consider adding NotAuthorized content for better UX."),
                    AzureBestPracticeUrl = BlazorAuthUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["HasPolicy"] = hasPolicy,
                        ["HasRoles"] = hasRoles,
                        ["HasAuthorized"] = hasAuthorized,
                        ["HasNotAuthorized"] = hasNotAuthorized,
                        ["PatternSubType"] = "Authorization"
                    }
                });
            }

            // [Authorize] attribute
            if (Regex.IsMatch(line, @"@attribute\s+\[Authorize", RegexOptions.IgnoreCase))
            {
                var policyMatch = Regex.Match(line, @"Policy\s*=\s*""([^""]+)""");
                var rolesMatch = Regex.Match(line, @"Roles\s*=\s*""([^""]+)""");
                
                var policy = policyMatch.Success ? policyMatch.Groups[1].Value : "";
                var roles = rolesMatch.Success ? rolesMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_AuthorizeAttribute",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Security,
                    Implementation = "[Authorize]",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = "Component-level [Authorize] attribute. Requires authentication to access component. " +
                        (string.IsNullOrEmpty(policy) ? "" : $"Policy: {policy}. ") +
                        (string.IsNullOrEmpty(roles) ? "" : $"Roles: {roles}."),
                    AzureBestPracticeUrl = BlazorAuthUrl,
                    Confidence = 0.98f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Policy"] = policy,
                        ["Roles"] = roles,
                        ["PatternSubType"] = "AuthorizeAttribute"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects rendering optimization patterns
    /// </summary>
    private List<CodePattern> DetectRenderingOptimizations(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // @key directive for render optimization
            if (Regex.IsMatch(line, @"@key=""", RegexOptions.IgnoreCase))
            {
                var keyMatch = Regex.Match(line, @"@key=""@?([^""]+)""");
                var keyValue = keyMatch.Success ? keyMatch.Groups[1].Value : "";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_KeyDirective",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Performance,
                    Implementation = "@key",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 3),
                    BestPractice = $"@key directive for render optimization. Helps Blazor preserve element/component identity during re-rendering. Key: {keyValue}. Use in loops to improve diff algorithm performance.",
                    AzureBestPracticeUrl = BlazorStreamingUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["KeyValue"] = keyValue,
                        ["PatternSubType"] = "RenderOptimization"
                    }
                });
            }

            // @preservewhitespace
            if (Regex.IsMatch(line, @"@preservewhitespace\s+(true|false)", RegexOptions.IgnoreCase))
            {
                var preserveMatch = Regex.Match(line, @"@preservewhitespace\s+(true|false)", RegexOptions.IgnoreCase);
                var preserve = preserveMatch.Success && preserveMatch.Groups[1].Value.ToLower() == "true";

                patterns.Add(new CodePattern
                {
                    Name = "Blazor_PreserveWhitespace",
                    Type = PatternType.Blazor,
                    Category = PatternCategory.Rendering,
                    Implementation = "@preservewhitespace",
                    Language = "C#",
                    FilePath = filePath,
                    LineNumber = i + 1,
                    EndLineNumber = i + 1,
                    Content = GetContext(lines, i, 2),
                    BestPractice = preserve 
                        ? "Preserves whitespace in component. Useful for pre-formatted content."
                        : "Whitespace minimization enabled (default). Improves performance and reduces payload size.",
                    AzureBestPracticeUrl = BlazorStreamingUrl,
                    Confidence = 0.95f,
                    Context = context ?? string.Empty,
                    Metadata = new Dictionary<string, object>
                    {
                        ["Preserve"] = preserve,
                        ["PatternSubType"] = "WhitespaceHandling"
                    }
                });
            }
        }

        return patterns;
    }

    /// <summary>
    /// Detects streaming rendering patterns (Blazor 8.0+)
    /// </summary>
    private List<CodePattern> DetectStreamingRendering(
        SyntaxNode root,
        string sourceCode,
        string filePath,
        string? context)
    {
        var patterns = new List<CodePattern>();

        // Detect streaming rendering attributes
        var streamingAttrs = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(a => a.ToString().Contains("StreamRendering"));

        foreach (var attr in streamingAttrs)
        {
            var lineNumber = sourceCode.Take(attr.SpanStart).Count(c => c == '\n') + 1;
            var enabled = attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Contains("true") != false;

            patterns.Add(new CodePattern
            {
                Name = "Blazor_StreamRendering",
                Type = PatternType.Blazor,
                Category = PatternCategory.Performance,
                Implementation = "[StreamRendering]",
                Language = "C#",
                FilePath = filePath,
                LineNumber = lineNumber,
                EndLineNumber = lineNumber,
                Content = GetCodeSnippet(sourceCode, lineNumber, 3),
                BestPractice = enabled
                    ? "Streaming rendering enabled (Blazor 8.0+). Improves perceived performance by streaming UI updates as they become available."
                    : "Streaming rendering disabled. Component waits for all async operations before rendering.",
                AzureBestPracticeUrl = BlazorStreamingUrl,
                Confidence = 0.95f,
                Context = context ?? string.Empty,
                Metadata = new Dictionary<string, object>
                {
                    ["Enabled"] = enabled,
                    ["PatternSubType"] = "StreamRendering"
                }
            });
        }

        return patterns;
    }

}

