using System.Text.RegularExpressions;
using System.Text.Json;
using System.Diagnostics;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Transforms and modernizes CSS
/// Now with evolving prompts via PromptService
/// </summary>
public class CSSTransformationService : ICSSTransformationService
{
    private readonly ILLMService _llmService;
    private readonly IPromptService _promptService;
    private readonly IPathTranslationService _pathTranslation;
    private readonly ILogger<CSSTransformationService> _logger;
    
    public CSSTransformationService(
        ILLMService llmService,
        IPromptService promptService,
        IPathTranslationService pathTranslation,
        ILogger<CSSTransformationService> logger)
    {
        _llmService = llmService;
        _promptService = promptService;
        _pathTranslation = pathTranslation;
        _logger = logger;
    }
    
    public async Task<CSSTransformation> TransformCSSAsync(
        string sourceFilePath,
        CSSTransformationOptions options,
        CancellationToken cancellationToken = default)
    {
        var transformation = new CSSTransformation
        {
            SourceFilePath = sourceFilePath,
            Context = ExtractContextFromPath(sourceFilePath)
        };
        
        // Translate Windows path to container path
        var containerPath = _pathTranslation.TranslateToContainerPath(sourceFilePath);
        _logger.LogDebug("Path translation: {OriginalPath} -> {ContainerPath}", sourceFilePath, containerPath);
        
        var sourceCode = await File.ReadAllTextAsync(containerPath, cancellationToken);
        
        _logger.LogInformation("Transforming CSS for {FilePath}", sourceFilePath);
        
        // 1. Extract inline styles
        var inlineStyles = ExtractInlineStyles(sourceCode);
        transformation.InlineStyleCount = inlineStyles.Count;
        
        _logger.LogInformation("Found {Count} inline styles", inlineStyles.Count);
        
        // 2. Analyze existing CSS
        var analysis = await AnalyzeCSSAsync(sourceFilePath, cancellationToken);
        transformation.CSSIssues = analysis.Issues;
        transformation.DetectedPatterns = analysis.Recommendations;
        
        // 3. Generate modern CSS with LLM
        if (inlineStyles.Any() || analysis.Issues.Any())
        {
            var modernCSS = await GenerateModernCSSAsync(
                inlineStyles,
                analysis,
                options,
                cancellationToken);
            
            transformation.GeneratedCSS = modernCSS.CSS;
            transformation.CSSVariables = modernCSS.Variables;
            transformation.UsesVariables = modernCSS.Variables.Any();
            transformation.UsesModernLayout = modernCSS.UsesModernLayout;
            transformation.IsResponsive = modernCSS.IsResponsive;
            transformation.HasAccessibility = modernCSS.HasAccessibility;
            
            _logger.LogInformation("Generated modern CSS with {VarCount} variables",
                modernCSS.Variables.Count);
        }
        
        return transformation;
    }
    
    public async Task<CSSAnalysisResult> AnalyzeCSSAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        var result = new CSSAnalysisResult
        {
            IssueBreakdown = new Dictionary<string, int>()
        };
        
        // Translate Windows path to container path
        var containerPath = _pathTranslation.TranslateToContainerPath(sourceFilePath);
        _logger.LogDebug("CSS Analysis path translation: {OriginalPath} -> {ContainerPath}", sourceFilePath, containerPath);
        
        var sourceCode = await File.ReadAllTextAsync(containerPath, cancellationToken);
        
        // Check inline styles
        var inlineStyles = Regex.Matches(sourceCode, @"style=""[^""]+""");
        result.InlineStyleCount = inlineStyles.Count;
        result.IssueBreakdown["inline_styles"] = inlineStyles.Count;
        
        if (inlineStyles.Count > 0)
        {
            result.Issues.Add($"{inlineStyles.Count} inline styles should be extracted to external CSS");
            result.Recommendations.Add("Extract inline styles to component-scoped CSS or site.css");
        }
        
        // Check for CSS variables
        if (!sourceCode.Contains("var(--") && !sourceCode.Contains(":root"))
        {
            result.Issues.Add("No CSS variables detected");
            result.Recommendations.Add("Use CSS variables for colors, spacing, and fonts for easier theming");
            result.IssueBreakdown["no_variables"] = 1;
        }
        
        // Check for modern layout
        var hasFloat = sourceCode.Contains("float:");
        var hasGrid = sourceCode.Contains("display: grid") || sourceCode.Contains("display:grid");
        var hasFlex = sourceCode.Contains("display: flex") || sourceCode.Contains("display:flex");
        
        if (hasFloat && !hasGrid && !hasFlex)
        {
            result.Issues.Add("Uses float-based layout (legacy pattern)");
            result.Recommendations.Add("Modernize layout using CSS Grid or Flexbox");
            result.IssueBreakdown["float_layout"] = 1;
        }
        
        // Check for responsive design
        if (!sourceCode.Contains("@media"))
        {
            result.Issues.Add("No responsive design detected");
            result.Recommendations.Add("Add media queries for mobile (480px), tablet (768px), desktop (1024px+)");
            result.IssueBreakdown["no_responsive"] = 1;
        }
        
        // Check for accessibility
        var hasFocusStyles = sourceCode.Contains(":focus") || sourceCode.Contains(":focus-visible");
        if (!hasFocusStyles)
        {
            result.Issues.Add("No focus styles detected");
            result.Recommendations.Add("Add :focus and :focus-visible styles for keyboard navigation");
            result.IssueBreakdown["no_focus_styles"] = 1;
        }
        
        // Check for vendor prefixes (outdated)
        if (sourceCode.Contains("-webkit-") || sourceCode.Contains("-moz-"))
        {
            result.Issues.Add("Manual vendor prefixes detected (use PostCSS/autoprefixer instead)");
            result.Recommendations.Add("Remove manual vendor prefixes and use build-time autoprefixer");
        }
        
        // Calculate quality score
        result.QualityScore = CalculateCSSQualityScore(result);
        
        return result;
    }
    
    public List<InlineStyle> ExtractInlineStyles(string code)
    {
        var inlineStyles = new List<InlineStyle>();
        var matches = Regex.Matches(code, @"<(\w+)[^>]*style=""([^""]+)""[^>]*>", RegexOptions.Multiline);
        
        int lineNumber = 1;
        foreach (Match match in matches)
        {
            var element = match.Groups[1].Value;
            var stylesStr = match.Groups[2].Value;
            
            var styles = new Dictionary<string, string>();
            var stylePairs = stylesStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var pair in stylePairs)
            {
                var parts = pair.Split(':', 2);
                if (parts.Length == 2)
                {
                    styles[parts[0].Trim()] = parts[1].Trim();
                }
            }
            
            inlineStyles.Add(new InlineStyle
            {
                Element = element,
                Styles = styles,
                LineNumber = lineNumber,
                Context = match.Value
            });
            
            lineNumber++;
        }
        
        return inlineStyles;
    }
    
    public async Task<ModernCSSResult> GenerateModernCSSAsync(
        List<InlineStyle> inlineStyles,
        CSSAnalysisResult analysis,
        CSSTransformationOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        string prompt;
        string? promptId = null;
        
        try
        {
            // Try to use versioned prompt
            var promptTemplate = await _promptService.GetPromptAsync("css_transformation", allowTestVariant: true, cancellationToken);
            promptId = promptTemplate.Id;
            
            var inlineStylesDesc = inlineStyles.Any()
                ? string.Join("\n", inlineStyles.Take(10).Select(s => 
                    $"- {s.Element}: {string.Join("; ", s.Styles.Select(kv => $"{kv.Key}: {kv.Value}"))}"))
                : "No inline styles";
            
            var goals = new List<string>();
            if (options.ExtractInlineStyles) goals.Add("✅ Extract ALL inline styles to external CSS");
            if (options.GenerateVariables) goals.Add("✅ Generate CSS variables for colors, spacing, fonts");
            if (options.ModernizeLayout) goals.Add("✅ Use modern layout (Grid/Flexbox instead of floats)");
            if (options.AddResponsive) goals.Add("✅ Add responsive design (mobile-first)");
            if (options.AddAccessibility) goals.Add("✅ Add accessibility (focus states, high contrast)");
            
            var variables = new Dictionary<string, string>
            {
                ["inlineStyleCount"] = inlineStyles.Count.ToString(),
                ["inlineStyles"] = inlineStylesDesc + (inlineStyles.Count > 10 ? $"\n...and {inlineStyles.Count - 10} more" : ""),
                ["issues"] = string.Join("\n", analysis.Issues.Select(i => $"- {i}")),
                ["recommendations"] = string.Join("\n", analysis.Recommendations.Select(r => $"- {r}")),
                ["qualityScore"] = analysis.QualityScore.ToString("F1"),
                ["goals"] = string.Join("\n", goals)
            };
            
            prompt = await _promptService.RenderPromptAsync("css_transformation", variables, cancellationToken);
        }
        catch
        {
            // Fallback to hardcoded prompt
            prompt = BuildCSSTransformationPrompt(inlineStyles, analysis, options);
        }
        
        // Call LLM (DeepSeek Coder)
        var response = await _llmService.GenerateAsync(prompt, cancellationToken);
        stopwatch.Stop();
        
        // Parse response
        var result = ParseModernCSSResult(response);
        
        // Record execution for learning
        if (promptId != null)
        {
            try
            {
                await _promptService.RecordExecutionAsync(
                    promptId,
                    prompt.Substring(0, Math.Min(2000, prompt.Length)),
                    new Dictionary<string, string> { ["inlineStyleCount"] = inlineStyles.Count.ToString() },
                    response.Substring(0, Math.Min(2000, response.Length)),
                    stopwatch.ElapsedMilliseconds,
                    parseSuccess: result.CSS != null,
                    cancellationToken: cancellationToken);
            }
            catch { }
        }
        
        return result;
    }
    
    // === PRIVATE METHODS ===
    
    private string ExtractContextFromPath(string filePath)
    {
        // Extract project/context from path
        var parts = filePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "default";
    }
    
    private float CalculateCSSQualityScore(CSSAnalysisResult result)
    {
        float score = 100f;
        
        // Deduct for issues
        score -= result.InlineStyleCount * 1.5f;  // -1.5 points per inline style
        score -= result.Issues.Count * 5f;        // -5 points per issue
        
        // Bonus for good practices
        if (!result.IssueBreakdown.ContainsKey("no_variables"))
            score += 10f;
        
        if (!result.IssueBreakdown.ContainsKey("no_responsive"))
            score += 10f;
        
        return Math.Max(0, Math.Min(100, score));
    }
    
    private string BuildCSSTransformationPrompt(
        List<InlineStyle> inlineStyles,
        CSSAnalysisResult analysis,
        CSSTransformationOptions options)
    {
        var inlineStylesDescription = inlineStyles.Any()
            ? string.Join("\n", inlineStyles.Take(10).Select(s => 
                $"- {s.Element}: {string.Join("; ", s.Styles.Select(kv => $"{kv.Key}: {kv.Value}"))}"))
            : "No inline styles";
        
        return $@"
You are a CSS modernization expert. Transform this CSS to modern best practices.

INLINE STYLES DETECTED ({inlineStyles.Count}):
═══════════════════════════════════════
{inlineStylesDescription}
{(inlineStyles.Count > 10 ? $"...and {inlineStyles.Count - 10} more" : "")}

CSS ANALYSIS:
═════════════
Issues:
{string.Join("\n", analysis.Issues.Select(i => $"- {i}"))}

Recommendations:
{string.Join("\n", analysis.Recommendations.Select(r => $"- {r}"))}

Quality Score: {analysis.QualityScore:F1}/100

TRANSFORMATION GOALS:
═════════════════════
{(options.ExtractInlineStyles ? "✅ Extract ALL inline styles to external CSS" : "")}
{(options.GenerateVariables ? "✅ Generate CSS variables for colors, spacing, fonts" : "")}
{(options.ModernizeLayout ? "✅ Use modern layout (Grid/Flexbox instead of floats)" : "")}
{(options.AddResponsive ? "✅ Add responsive design (mobile-first)" : "")}
{(options.AddAccessibility ? "✅ Add accessibility (focus states, high contrast)" : "")}

REQUIREMENTS:
═════════════
1. Create CSS variables in :root for:
   - Colors (primary, secondary, success, danger, etc.)
   - Spacing (xs, sm, md, lg, xl)
   - Fonts (sizes, families, weights)
   - Borders (radius, widths)
   - Shadows

2. Modern layout patterns:
   - CSS Grid for page layouts
   - Flexbox for component layouts
   - NO floats or tables for layout

3. Responsive breakpoints:
   - Mobile: 320px - 767px
   - Tablet: 768px - 1023px
   - Desktop: 1024px+
   - Use mobile-first approach

4. Accessibility:
   - Focus styles (:focus, :focus-visible)
   - High contrast mode support
   - Reduced motion support
   - Color contrast WCAG AA minimum

Return VALID JSON:
{{
  ""css"": ""...full modern CSS..."",
  ""variables"": {{
    ""--color-primary"": ""#007bff"",
    ""--spacing-sm"": ""0.5rem"",
    ...
  }},
  ""uses_modern_layout"": true,
  ""is_responsive"": true,
  ""has_accessibility"": true,
  ""improvements"": [
    ""Extracted 47 inline styles to external CSS"",
    ""Added 15 CSS variables for consistent theming"",
    ""Converted float layout to CSS Grid"",
    ""Added mobile, tablet, desktop breakpoints""
  ]
}}
";
    }
    
    private ModernCSSResult ParseModernCSSResult(string llmResponse)
    {
        try
        {
            // Clean up markdown
            var json = llmResponse.Trim();
            if (json.StartsWith("```"))
            {
                json = Regex.Replace(json, @"```(?:json)?\s*", "");
                json = json.TrimEnd('`').Trim();
            }
            
            // Try to find JSON object in the response if it doesn't start with {
            if (!json.StartsWith("{"))
            {
                // Look for JSON embedded in text response
                var jsonMatch = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Singleline);
                if (jsonMatch.Success)
                {
                    json = jsonMatch.Value;
                    _logger.LogWarning("LLM returned text with embedded JSON, extracting...");
                }
                else
                {
                    // No JSON found - LLM returned explanation text (e.g., "This file has no CSS")
                    _logger.LogWarning("LLM returned text instead of JSON: {Response}", 
                        llmResponse.Length > 200 ? llmResponse[..200] + "..." : llmResponse);
                    
                    // Return empty result with the LLM's explanation
                    return new ModernCSSResult
                    {
                        CSS = "",
                        UsesModernLayout = false,
                        IsResponsive = false,
                        HasAccessibility = false,
                        Improvements = { $"LLM Note: {llmResponse.Trim()}" }
                    };
                }
            }
            
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var result = new ModernCSSResult
            {
                CSS = root.GetProperty("css").GetString() ?? "",
                UsesModernLayout = root.TryGetProperty("uses_modern_layout", out var modern) && modern.GetBoolean(),
                IsResponsive = root.TryGetProperty("is_responsive", out var responsive) && responsive.GetBoolean(),
                HasAccessibility = root.TryGetProperty("has_accessibility", out var access) && access.GetBoolean()
            };
            
            // Variables
            if (root.TryGetProperty("variables", out var vars))
            {
                foreach (var prop in vars.EnumerateObject())
                {
                    result.Variables[prop.Name] = prop.Value.GetString() ?? "";
                }
            }
            
            // Improvements
            if (root.TryGetProperty("improvements", out var improvements))
            {
                foreach (var imp in improvements.EnumerateArray())
                {
                    result.Improvements.Add(imp.GetString() ?? "");
                }
            }
            
            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse CSS transformation response: {Response}", 
                llmResponse.Length > 500 ? llmResponse[..500] + "..." : llmResponse);
            
            // Return empty result instead of throwing
            return new ModernCSSResult
            {
                CSS = "",
                UsesModernLayout = false,
                IsResponsive = false,
                HasAccessibility = false,
                Improvements = { $"CSS transformation failed: {ex.Message}" }
            };
        }
    }
}

