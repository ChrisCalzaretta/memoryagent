using DesignAgent.Server.Models.DesignIntelligence;
using AgentContracts.Services;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for generating A2UI (Agent-to-User Interface) JSON from captured designs and patterns
/// </summary>
public class A2uiGeneratorService : IA2uiGeneratorService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IDesignIntelligenceStorage _storage;
    private readonly ILogger<A2uiGeneratorService> _logger;
    private readonly DesignIntelligenceOptions _options;

    public A2uiGeneratorService(
        IOllamaClient ollamaClient,
        IDesignIntelligenceStorage storage,
        ILogger<A2uiGeneratorService> logger,
        IOptions<DesignIntelligenceOptions> options)
    {
        _ollamaClient = ollamaClient;
        _storage = storage;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Generate A2UI JSON for a new design based on brand and patterns
    /// </summary>
    public async Task<A2uiOutput> GenerateA2uiAsync(string brandContext, string componentType, string requirements, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🎨 Generating A2UI: {Component} for {Brand}", componentType, brandContext);

        // Get top patterns for this component type
        var relevantPatterns = await _storage.GetTopPatternsAsync(10, cancellationToken);
        var filteredPatterns = relevantPatterns.Where(p => 
            p.Category.Contains(componentType, StringComparison.OrdinalIgnoreCase) ||
            p.Tags.Any(t => t.Contains(componentType, StringComparison.OrdinalIgnoreCase)))
            .Take(3)
            .ToList();

        // Build generation prompt
        var systemPrompt = await _storage.GetPromptAsync("a2ui_generation", cancellationToken)
            ?? GetFallbackA2uiGenerationPrompt();

        var userPrompt = BuildGenerationPrompt(brandContext, componentType, requirements, filteredPatterns);

        // Generate A2UI
        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("Failed to generate A2UI: {Error}", response.Error);
            throw new Exception($"A2UI generation failed: {response.Error}");
        }

        var a2uiJson = ExtractJsonFromResponse(response.Response);
        var parsedJson = JsonSerializer.Deserialize<JsonElement>(a2uiJson);

        // Extract design tokens
        var designTokens = ExtractDesignTokens(parsedJson);

        return new A2uiOutput
        {
            Brand = brandContext,
            DesignTokens = designTokens,
            A2uiJson = parsedJson,
            Metadata = new Dictionary<string, object>
            {
                ["componentType"] = componentType,
                ["requirements"] = requirements,
                ["patternsUsed"] = filteredPatterns.Count,
                ["generatedAt"] = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// Convert a learned pattern to A2UI format
    /// </summary>
    public async Task<A2uiOutput> ConvertPatternToA2uiAsync(string patternId, string brandContext, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🎨 Converting pattern {PatternId} to A2UI", patternId);

        var pattern = await _storage.GetPatternAsync(patternId, cancellationToken);
        if (pattern == null)
        {
            throw new ArgumentException($"Pattern not found: {patternId}");
        }

        var systemPrompt = await _storage.GetPromptAsync("a2ui_pattern_generation", cancellationToken)
            ?? GetFallbackPatternA2uiPrompt();

        var userPrompt = BuildPatternConversionPrompt(pattern, brandContext);

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        if (!response.Success)
        {
            throw new Exception($"Pattern A2UI generation failed: {response.Error}");
        }

        var a2uiJson = ExtractJsonFromResponse(response.Response);
        var parsedJson = JsonSerializer.Deserialize<JsonElement>(a2uiJson);
        var designTokens = ExtractDesignTokens(parsedJson);

        return new A2uiOutput
        {
            Brand = brandContext,
            DesignTokens = designTokens,
            A2uiJson = parsedJson,
            Metadata = new Dictionary<string, object>
            {
                ["patternId"] = patternId,
                ["patternName"] = pattern.Name,
                ["qualityScore"] = pattern.QualityScore
            }
        };
    }

    /// <summary>
    /// Generate A2UI similar to an existing design
    /// </summary>
    public async Task<A2uiOutput> GenerateSimilarA2uiAsync(string designId, string brandContext, string? variations = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🎨 Generating A2UI similar to {DesignId}", designId);

        var design = await _storage.GetDesignAsync(designId, cancellationToken);
        if (design == null)
        {
            throw new ArgumentException($"Design not found: {designId}");
        }

        var systemPrompt = await _storage.GetPromptAsync("a2ui_similar_generation", cancellationToken)
            ?? GetFallbackSimilarA2uiPrompt();

        var userPrompt = BuildSimilarGenerationPrompt(design, brandContext, variations);

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        if (!response.Success)
        {
            throw new Exception($"Similar A2UI generation failed: {response.Error}");
        }

        var a2uiJson = ExtractJsonFromResponse(response.Response);
        var parsedJson = JsonSerializer.Deserialize<JsonElement>(a2uiJson);
        var designTokens = ExtractDesignTokens(parsedJson);

        return new A2uiOutput
        {
            Brand = brandContext,
            DesignTokens = designTokens,
            A2uiJson = parsedJson,
            Metadata = new Dictionary<string, object>
            {
                ["referenceDesign"] = designId,
                ["referenceUrl"] = design.Url,
                ["variations"] = variations ?? "none"
            }
        };
    }

    /// <summary>
    /// Convert A2UI JSON to code (HTML/CSS or Blazor)
    /// </summary>
    public async Task<string> ConvertA2uiToCodeAsync(string a2uiJson, string targetFramework = "html", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📝 Converting A2UI to {Framework} code", targetFramework);

        var systemPrompt = $"You are a code generator. Convert A2UI JSON to {targetFramework.ToUpper()} code.";
        var userPrompt = $"Convert this A2UI JSON to {targetFramework} code:\n\n{a2uiJson}\n\nGenerate clean, production-ready code.";

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        return response.Response;
    }

    // ===== PRIVATE HELPERS =====

    private string BuildGenerationPrompt(string brandContext, string componentType, string requirements, List<DesignPattern> patterns)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate A2UI JSON for a {componentType} component:");
        sb.AppendLine();
        sb.AppendLine($"Brand: {brandContext}");
        sb.AppendLine($"Requirements: {requirements}");
        sb.AppendLine();

        if (patterns.Count > 0)
        {
            sb.AppendLine("Learned Patterns (use as inspiration):");
            foreach (var pattern in patterns)
            {
                sb.AppendLine($"- {pattern.Name} (Score: {pattern.QualityScore:F1}/10)");
                sb.AppendLine($"  {pattern.Description}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Generate A2UI JSON following Google's A2UI schema:");
        sb.AppendLine("1. Use semantic components (Grid, Card, Button, Text, etc.)");
        sb.AppendLine("2. Include design tokens in 'theme' section");
        sb.AppendLine("3. Structure content hierarchically");
        sb.AppendLine("4. Add accessibility attributes");
        sb.AppendLine();
        sb.AppendLine("Return ONLY valid A2UI JSON.");

        return sb.ToString();
    }

    private string BuildPatternConversionPrompt(DesignPattern pattern, string brandContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Convert this design pattern to A2UI JSON:");
        sb.AppendLine();
        sb.AppendLine($"Pattern: {pattern.Name}");
        sb.AppendLine($"Category: {pattern.Category}");
        sb.AppendLine($"Quality: {pattern.QualityScore:F1}/10");
        sb.AppendLine($"Brand Context: {brandContext}");
        sb.AppendLine();
        sb.AppendLine($"Description: {pattern.Description}");
        sb.AppendLine($"Tags: {string.Join(", ", pattern.Tags)}");
        sb.AppendLine();
        sb.AppendLine("Return ONLY valid A2UI JSON.");

        return sb.ToString();
    }

    private string BuildSimilarGenerationPrompt(CapturedDesign design, string brandContext, string? variations)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate A2UI similar to this design:");
        sb.AppendLine();
        sb.AppendLine($"Reference: {design.Url}");
        sb.AppendLine($"Score: {design.OverallScore:F1}/10");
        sb.AppendLine($"Brand Context: {brandContext}");
        sb.AppendLine();
        
        // Include high-scoring pages
        foreach (var page in design.Pages.OrderByDescending(p => p.OverallPageScore).Take(2))
        {
            sb.AppendLine($"Page: {page.PageType} (Score: {page.OverallPageScore:F1}/10)");
        }
        sb.AppendLine();

        if (!string.IsNullOrEmpty(variations))
        {
            sb.AppendLine($"Variations: {variations}");
            sb.AppendLine();
        }

        sb.AppendLine("Generate similar A2UI JSON maintaining the design philosophy.");
        sb.AppendLine("Return ONLY valid A2UI JSON.");

        return sb.ToString();
    }

    private Dictionary<string, Dictionary<string, string>> ExtractDesignTokens(JsonElement a2uiJson)
    {
        var tokens = new Dictionary<string, Dictionary<string, string>>();

        try
        {
            if (a2uiJson.TryGetProperty("theme", out var theme))
            {
                if (theme.TryGetProperty("colors", out var colors))
                {
                    tokens["colors"] = JsonSerializer.Deserialize<Dictionary<string, string>>(colors.GetRawText()) ?? new();
                }
                if (theme.TryGetProperty("fonts", out var fonts))
                {
                    tokens["fonts"] = JsonSerializer.Deserialize<Dictionary<string, string>>(fonts.GetRawText()) ?? new();
                }
                if (theme.TryGetProperty("spacing", out var spacing))
                {
                    tokens["spacing"] = JsonSerializer.Deserialize<Dictionary<string, string>>(spacing.GetRawText()) ?? new();
                }
            }
        }
        catch
        {
            // Return empty tokens if extraction fails
        }

        return tokens;
    }

    private string ExtractJsonFromResponse(string response)
    {
        // Try to extract JSON from markdown code blocks
        var jsonStart = response.IndexOf("```json");
        if (jsonStart >= 0)
        {
            jsonStart = response.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = response.IndexOf("```", jsonStart);
            if (jsonEnd > jsonStart)
            {
                return response.Substring(jsonStart, jsonEnd - jsonStart).Trim();
            }
        }

        // Try to find raw JSON
        jsonStart = response.IndexOf('{');
        if (jsonStart >= 0)
        {
            int depth = 0;
            for (int i = jsonStart; i < response.Length; i++)
            {
                if (response[i] == '{') depth++;
                else if (response[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return response.Substring(jsonStart, i - jsonStart + 1).Trim();
                    }
                }
            }
        }

        return response.Trim();
    }

    private string GetFallbackA2uiGenerationPrompt()
    {
        return @"You are an A2UI (Agent-to-User Interface) generator.

Generate A2UI JSON following Google's A2UI schema:
- Use semantic components (Grid, Card, Button, Text, Image, etc.)
- Include design tokens (colors, fonts, spacing) in 'theme' section
- Structure content hierarchically with proper nesting
- Add accessibility attributes (aria-label, role)
- Use props for styling (columns, gap, variant, size)

Generate clean, valid A2UI JSON.";
    }

    private string GetFallbackPatternA2uiPrompt()
    {
        return @"Convert design patterns into A2UI JSON components.
Focus on the pattern's structure and style, making it reusable.
Return ONLY valid A2UI JSON.";
    }

    private string GetFallbackSimilarA2uiPrompt()
    {
        return @"Generate A2UI JSON similar to the reference design.
Maintain the design philosophy while applying the specified brand and variations.
Return ONLY valid A2UI JSON.";
    }
}
