using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Services;
using DesignAgent.Server.Clients;
using DesignAgent.Server.Models;
using DesignAgent.Server.Models.Brand;
using DesignAgent.Server.Models.Questionnaire;

namespace DesignAgent.Server.Services;

/// <summary>
/// 🧠 LLM-enhanced design service for creative brand generation and intelligent validation
/// - Uses Lightning prompts for all LLM calls
/// - Smart model selection for optimal performance
/// - Records model performance for learning
/// </summary>
public interface ILlmDesignService
{
    /// <summary>
    /// Generate creative brand suggestions using LLM
    /// </summary>
    Task<LlmBrandSuggestions> GenerateBrandSuggestionsAsync(
        ParsedBrandInput input, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate intelligent validation feedback with explanations
    /// </summary>
    Task<LlmValidationFeedback> GenerateValidationFeedbackAsync(
        string code, 
        BrandDefinition brand, 
        List<DesignIssue> issues,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate fix suggestions for design issues
    /// </summary>
    Task<string> GenerateFixSuggestionsAsync(
        DesignIssue issue, 
        BrandDefinition brand,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM-generated brand suggestions
/// </summary>
public class LlmBrandSuggestions
{
    public string? CreativeTagline { get; set; }
    public List<string> ColorSuggestions { get; set; } = new();
    public List<string> FontSuggestions { get; set; } = new();
    public string? BrandStory { get; set; }
    public List<string> PersonalityTraits { get; set; } = new();
    public string? VoiceTone { get; set; }
    public Dictionary<string, string> ComponentIdeas { get; set; } = new();
}

/// <summary>
/// LLM-generated validation feedback
/// </summary>
public class LlmValidationFeedback
{
    public string Summary { get; set; } = "";
    public List<LlmIssueExplanation> IssueExplanations { get; set; } = new();
    public string OverallRecommendation { get; set; } = "";
    public List<string> QuickWins { get; set; } = new();
}

public class LlmIssueExplanation
{
    public string Issue { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public string HowToFix { get; set; } = "";
    public string FixCode { get; set; } = "";
}

public class LlmDesignService : ILlmDesignService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IDesignModelSelector _modelSelector;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<LlmDesignService> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public LlmDesignService(
        IOllamaClient ollamaClient,
        IDesignModelSelector modelSelector,
        IMemoryAgentClient memoryAgent,
        ILogger<LlmDesignService> logger)
    {
        _ollamaClient = ollamaClient;
        _modelSelector = modelSelector;
        _memoryAgent = memoryAgent;
        _logger = logger;
    }

    public async Task<LlmBrandSuggestions> GenerateBrandSuggestionsAsync(
        ParsedBrandInput input, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var selection = await _modelSelector.SelectModelAsync(
            $"Generate creative brand suggestions for {input.BrandName}",
            "brand_generation",
            cancellationToken);
        
        _logger.LogInformation("🎨 Generating brand suggestions with {Model}", selection.Model);
        
        // Get prompt from Lightning
        var systemPrompt = await GetPromptAsync("brand_generation", GetDefaultBrandPrompt(), cancellationToken);
        var userPrompt = BuildBrandPrompt(input);
        
        var response = await _ollamaClient.GenerateAsync(
            selection.Model,
            userPrompt,
            systemPrompt,
            selection.Port,
            cancellationToken);
        
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        if (!response.Success)
        {
            _logger.LogWarning("LLM brand generation failed: {Error}", response.Error);
            await RecordPerformanceAsync(selection.Model, "brand_generation", false, 0, duration, response.Error, cancellationToken);
            return new LlmBrandSuggestions();
        }
        
        var suggestions = ParseBrandSuggestions(response.Response);
        await RecordPerformanceAsync(selection.Model, "brand_generation", true, 8, duration, null, cancellationToken);
        
        _logger.LogInformation("✨ Generated brand suggestions: {Tagline}", suggestions.CreativeTagline);
        return suggestions;
    }

    public async Task<LlmValidationFeedback> GenerateValidationFeedbackAsync(
        string code, 
        BrandDefinition brand, 
        List<DesignIssue> issues,
        CancellationToken cancellationToken = default)
    {
        if (!issues.Any())
        {
            return new LlmValidationFeedback
            {
                Summary = "✅ Great work! Your code follows the brand guidelines.",
                OverallRecommendation = "No issues found - your implementation is brand-compliant!"
            };
        }
        
        var startTime = DateTime.UtcNow;
        var selection = await _modelSelector.SelectModelAsync(
            $"Validate {issues.Count} design issues against {brand.Name} brand guidelines",
            "validation",
            cancellationToken);
        
        _logger.LogInformation("🔍 Generating validation feedback with {Model}", selection.Model);
        
        var systemPrompt = await GetPromptAsync("design_validation", GetDefaultValidationPrompt(), cancellationToken);
        var userPrompt = BuildValidationPrompt(code, brand, issues);
        
        var response = await _ollamaClient.GenerateAsync(
            selection.Model,
            userPrompt,
            systemPrompt,
            selection.Port,
            cancellationToken);
        
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        if (!response.Success)
        {
            _logger.LogWarning("LLM validation feedback failed: {Error}", response.Error);
            await RecordPerformanceAsync(selection.Model, "validation", false, 0, duration, response.Error, cancellationToken);
            return GenerateFallbackFeedback(issues);
        }
        
        var feedback = ParseValidationFeedback(response.Response, issues);
        await RecordPerformanceAsync(selection.Model, "validation", true, 8, duration, null, cancellationToken);
        
        return feedback;
    }

    public async Task<string> GenerateFixSuggestionsAsync(
        DesignIssue issue, 
        BrandDefinition brand,
        CancellationToken cancellationToken = default)
    {
        var selection = await _modelSelector.SelectModelAsync(
            $"Generate fix for {issue.Type} issue",
            "fix_suggestion",
            cancellationToken);
        
        var systemPrompt = await GetPromptAsync("design_fix", GetDefaultFixPrompt(), cancellationToken);
        var userPrompt = $@"## Design Issue to Fix

**Issue Type:** {issue.Type}
**Severity:** {issue.Severity}
**Message:** {issue.Message}
**Code:** 
```
{issue.CodeSnippet}
```

**Brand Guidelines:**
- Primary Color: {brand.Tokens.Colors.Primary}
- Secondary Color: {brand.Tokens.Colors.Secondary}
- Font Family: {brand.Tokens.Typography.FontFamilySans}
- Base Size: {brand.Tokens.Typography.FontSizes.GetValueOrDefault("base", "1rem")}

Generate the corrected code that follows the brand guidelines.";
        
        var response = await _ollamaClient.GenerateAsync(
            selection.Model,
            userPrompt,
            systemPrompt,
            selection.Port,
            cancellationToken);
        
        return response.Success ? response.Response : issue.Fix ?? "Unable to generate fix suggestion";
    }

    #region Prompt Building

    private string BuildBrandPrompt(ParsedBrandInput input)
    {
        return $@"## Brand Information

**Brand Name:** {input.BrandName}
**Industry:** {input.Industry}
**Target Audience:** {input.TargetAudience}
**Description:** {input.Description}
**Tagline (user provided):** {input.Tagline}

**User Preferences:**
- Style: {input.VisualStyle}
- Preferred Colors: {input.PreferredColors ?? "none specified"}
- Theme: {input.ThemePreference}
- Personality Traits: {string.Join(", ", input.PersonalityTraits)}
- Voice Archetype: {input.VoiceArchetype}

**Target Platforms:** {string.Join(", ", input.Platforms)}
**Target Frameworks:** {string.Join(", ", input.Frameworks)}

Please generate creative brand suggestions including:
1. A creative tagline (improve on or complement the user's)
2. Color palette suggestions (hex codes)
3. Font pairing suggestions
4. A brief brand story
5. Personality trait recommendations
6. Voice and tone guidelines
7. Key component styling ideas

Return as JSON.";
    }

    private string BuildValidationPrompt(string code, BrandDefinition brand, List<DesignIssue> issues)
    {
        var issueList = string.Join("\n", issues.Select((i, idx) => 
            $"{idx + 1}. [{i.Severity}] {i.Type}: {i.Message}"));
        
        return $@"## Design Validation Request

**Brand:** {brand.Name}
**Brand Style:** {brand.Identity.Archetype}

**Code to Validate:**
```
{(code.Length > 2000 ? code[..2000] + "..." : code)}
```

**Issues Found ({issues.Count}):**
{issueList}

**Brand Guidelines:**
- Primary Color: {brand.Tokens.Colors.Primary}
- Secondary Color: {brand.Tokens.Colors.Secondary}
- Font Family: {brand.Tokens.Typography.FontFamilySans}
- Spacing Base: {brand.Tokens.Spacing.BaseUnit}

Please provide:
1. A summary of the validation results
2. For each issue, explain WHY it matters and HOW to fix it
3. An overall recommendation
4. Quick wins (easy fixes that improve compliance)

Return as JSON.";
    }

    #endregion

    #region Response Parsing

    /// <summary>
    /// Extract JSON from LLM response - handles markdown code blocks, plain JSON, etc.
    /// </summary>
    private string? ExtractJsonFromResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;
        
        // Try 1: JSON in markdown code block (```json ... ``` or ``` ... ```)
        var codeBlockMatch = Regex.Match(response, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        if (codeBlockMatch.Success)
        {
            var jsonCandidate = codeBlockMatch.Groups[1].Value.Trim();
            if (jsonCandidate.StartsWith("{") || jsonCandidate.StartsWith("["))
            {
                _logger.LogDebug("Extracted JSON from markdown code block");
                return jsonCandidate;
            }
        }
        
        // Try 2: Find first complete JSON object { ... }
        var braceMatch = Regex.Match(response, @"\{(?:[^{}]|(?:\{[^{}]*\}))*\}", RegexOptions.Singleline);
        if (braceMatch.Success)
        {
            _logger.LogDebug("Extracted JSON using brace matching");
            return braceMatch.Value;
        }
        
        // Try 3: Response is already JSON
        var trimmed = response.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            _logger.LogDebug("Response is already JSON");
            return trimmed;
        }
        
        _logger.LogWarning("Could not extract JSON from response (length: {Length})", response.Length);
        return null;
    }

    private LlmBrandSuggestions ParseBrandSuggestions(string response)
    {
        var json = ExtractJsonFromResponse(response);
        
        if (json != null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<LlmBrandSuggestions>(json, JsonOptions);
                if (parsed != null) 
                {
                    _logger.LogDebug("✅ Successfully parsed brand suggestions JSON");
                    return parsed;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("JSON deserialization failed: {Error}. JSON: {Json}", 
                    ex.Message, json.Length > 200 ? json[..200] + "..." : json);
            }
        }
        
        // Fallback: Extract what we can from plain text
        _logger.LogInformation("Using text extraction fallback for brand suggestions");
        return new LlmBrandSuggestions
        {
            CreativeTagline = ExtractSection(response, "tagline") ?? ExtractQuotedText(response, "tagline"),
            BrandStory = ExtractSection(response, "story") ?? ExtractSection(response, "brand story"),
            ColorSuggestions = ExtractListItems(response, "color")
        };
    }

    private LlmValidationFeedback ParseValidationFeedback(string response, List<DesignIssue> issues)
    {
        var json = ExtractJsonFromResponse(response);
        
        if (json != null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<LlmValidationFeedback>(json, JsonOptions);
                if (parsed != null)
                {
                    _logger.LogDebug("✅ Successfully parsed validation feedback JSON");
                    return parsed;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("JSON deserialization failed: {Error}", ex.Message);
            }
        }
        
        _logger.LogInformation("Using fallback feedback generation");
        return GenerateFallbackFeedback(issues);
    }
    
    private string? ExtractQuotedText(string text, string near)
    {
        // Find text in quotes near a keyword
        var nearIndex = text.IndexOf(near, StringComparison.OrdinalIgnoreCase);
        if (nearIndex == -1) return null;
        
        var searchArea = text.Substring(nearIndex, Math.Min(200, text.Length - nearIndex));
        var quoteMatch = Regex.Match(searchArea, @"""([^""]+)""");
        return quoteMatch.Success ? quoteMatch.Groups[1].Value : null;
    }
    
    private List<string> ExtractListItems(string text, string section)
    {
        var items = new List<string>();
        
        // Find section and extract bullet points or numbered items
        var sectionStart = text.IndexOf(section, StringComparison.OrdinalIgnoreCase);
        if (sectionStart == -1) return items;
        
        var searchArea = text.Substring(sectionStart, Math.Min(500, text.Length - sectionStart));
        var listMatches = Regex.Matches(searchArea, @"[-•*]\s*([^\n]+)");
        
        foreach (Match match in listMatches)
        {
            var item = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(item) && item.Length < 100)
                items.Add(item);
        }
        
        return items;
    }

    private LlmValidationFeedback GenerateFallbackFeedback(List<DesignIssue> issues)
    {
        return new LlmValidationFeedback
        {
            Summary = $"Found {issues.Count} design issues that need attention.",
            IssueExplanations = issues.Select(i => new LlmIssueExplanation
            {
                Issue = i.Message,
                WhyItMatters = GetGenericExplanation(i.Type),
                HowToFix = i.Fix ?? "Review the brand guidelines and update accordingly.",
                FixCode = i.FixCode ?? ""
            }).ToList(),
            OverallRecommendation = issues.Any(i => i.Severity == IssueSeverity.Critical)
                ? "Critical issues found - fix before deployment!"
                : "Review and fix issues to ensure brand consistency.",
            QuickWins = issues.Where(i => i.Severity == IssueSeverity.Low)
                .Select(i => i.Fix ?? i.Message).Take(3).ToList()
        };
    }

    private string GetGenericExplanation(string issueType)
    {
        return issueType.ToLowerInvariant() switch
        {
            "color" => "Colors reinforce brand identity and user recognition. Inconsistent colors confuse users and weaken brand perception.",
            "typography" => "Consistent typography improves readability and establishes visual hierarchy. It's crucial for professional appearance.",
            "spacing" => "Proper spacing creates visual rhythm and improves content digestibility. It affects how 'polished' the UI feels.",
            "accessibility" => "Accessibility ensures all users can use your application. It's also a legal requirement in many jurisdictions.",
            "component" => "Consistent components create a cohesive user experience and reduce cognitive load.",
            _ => "Following brand guidelines ensures consistency and professionalism across your application."
        };
    }

    private string? ExtractSection(string text, string sectionName)
    {
        var match = Regex.Match(text, $@"{sectionName}[:\s]+([^\n]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    #endregion

    #region Lightning Prompts

    private async Task<string> GetPromptAsync(string promptName, string defaultPrompt, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = await _memoryAgent.GetPromptAsync(promptName, cancellationToken);
            if (prompt != null && !string.IsNullOrEmpty(prompt.Content))
            {
                _logger.LogDebug("Using Lightning prompt: {Name} v{Version}", prompt.Name, prompt.Version);
                return prompt.Content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get prompt from Lightning, using default");
        }
        return defaultPrompt;
    }

    private string GetDefaultBrandPrompt()
    {
        return @"You are a creative brand strategist and UX designer.

Your job is to generate creative, cohesive brand suggestions that:
1. Align with the industry and target audience
2. Create emotional resonance
3. Are practical for UI implementation
4. Stand out from competitors

Be creative but practical. Suggest specific hex colors, real font names, and actionable guidelines.

CRITICAL: Return ONLY valid JSON. No markdown, no code blocks, no explanatory text before or after.

Return this exact JSON structure:
{
    ""creativeTagline"": ""A memorable tagline"",
    ""colorSuggestions"": [""#HEX1"", ""#HEX2""],
    ""fontSuggestions"": [""Font Name 1"", ""Font Name 2""],
    ""brandStory"": ""Brief brand narrative"",
    ""personalityTraits"": [""trait1"", ""trait2""],
    ""voiceTone"": ""Description of voice and tone"",
    ""componentIdeas"": {""buttons"": ""style description"", ""cards"": ""style description""}
}";
    }

    private string GetDefaultValidationPrompt()
    {
        return @"You are a UX design expert reviewing code against brand guidelines.

Your job is to:
1. Explain WHY each design issue matters (user impact, brand impact)
2. Provide specific, actionable fixes
3. Prioritize issues by impact
4. Suggest quick wins for immediate improvement

Be constructive and educational. Help developers understand design principles.

CRITICAL: Return ONLY valid JSON. No markdown, no code blocks, no explanatory text before or after.

Return this exact JSON structure:
{
    ""summary"": ""Overall assessment"",
    ""issueExplanations"": [
        {
            ""issue"": ""Issue description"",
            ""whyItMatters"": ""Why this matters for users/brand"",
            ""howToFix"": ""Step-by-step fix instructions"",
            ""fixCode"": ""Corrected code snippet""
        }
    ],
    ""overallRecommendation"": ""Final recommendation"",
    ""quickWins"": [""Easy fix 1"", ""Easy fix 2""]
}";
    }

    private string GetDefaultFixPrompt()
    {
        return @"You are a CSS/UI expert who generates brand-compliant code fixes.

Your job is to:
1. Take the problematic code
2. Apply the brand guidelines
3. Generate corrected code
4. Keep the fix minimal and focused

Return ONLY the corrected code, properly formatted.";
    }

    #endregion

    #region Performance Recording

    private async Task RecordPerformanceAsync(
        string model, string taskType, bool success, double score, 
        double durationMs, string? errorType, CancellationToken cancellationToken)
    {
        try
        {
            await _memoryAgent.RecordModelPerformanceAsync(
                model, taskType, success, score,
                "design", "moderate", 1, (long)durationMs, errorType,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to record model performance");
        }
    }

    #endregion
}

