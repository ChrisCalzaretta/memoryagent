using System.Text.RegularExpressions;
using System.Text.Json;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Transforms Blazor/Razor pages to modern architecture
/// </summary>
public class PageTransformationService : IPageTransformationService
{
    private readonly ILLMService _llmService;
    private readonly RazorParser _razorParser;
    private readonly ICSSTransformationService _cssService;
    private readonly IComponentExtractionService _componentService;
    private readonly ILogger<PageTransformationService> _logger;
    
    // In-memory pattern storage (in production, use database)
    private static readonly List<TransformationPattern> _patterns = new();
    
    public PageTransformationService(
        ILLMService llmService,
        RazorParser razorParser,
        ICSSTransformationService cssService,
        IComponentExtractionService componentService,
        ILogger<PageTransformationService> logger)
    {
        _llmService = llmService;
        _razorParser = razorParser;
        _cssService = cssService;
        _componentService = componentService;
        _logger = logger;
    }
    
    public async Task<PageTransformation> TransformPageAsync(
        string sourceFilePath,
        TransformationOptions options,
        CancellationToken cancellationToken = default)
    {
        var transformation = new PageTransformation
        {
            SourceFilePath = sourceFilePath,
            Type = TransformationType.FullTransformation,
            Status = TransformationStatus.Analyzing
        };
        
        try
        {
            _logger.LogInformation("Starting transformation for {FilePath}", sourceFilePath);
            
            // 1. Parse source file
            var sourceCode = await File.ReadAllTextAsync(sourceFilePath, cancellationToken);
            var parsed = RazorParser.ParseRazorFile(sourceFilePath, "default", null);
            
            transformation.OriginalLines = sourceCode.Split('\n').Length;
            
            // 2. Analyze what needs transformation
            var analysis = await AnalyzePageAsync(parsed, sourceCode, options, cancellationToken);
            transformation.IssuesDetected = analysis.Issues;
            
            // 3. Generate transformation plan with LLM
            transformation.Status = TransformationStatus.Transforming;
            var plan = await GenerateTransformationPlanAsync(
                parsed, 
                sourceCode,
                analysis, 
                options,
                cancellationToken);
            
            // 4. Execute transformations
            var results = await ExecuteTransformationPlanAsync(plan, options, cancellationToken);
            
            // 5. Update transformation record
            transformation.GeneratedFiles = results.Files;
            transformation.ComponentsExtracted = results.ComponentsExtracted;
            transformation.InlineStylesRemoved = results.InlineStylesRemoved;
            transformation.ImprovementsMade = results.Improvements;
            transformation.TransformedLines = results.TotalLines;
            transformation.Status = TransformationStatus.Completed;
            transformation.CompletedAt = DateTime.UtcNow;
            transformation.LLMModel = "deepseek-coder-v2:16b";
            transformation.Confidence = results.Confidence;
            transformation.Reasoning = results.Reasoning;
            
            _logger.LogInformation("Transformation completed: {ComponentsExtracted} components, {StylesRemoved} styles",
                results.ComponentsExtracted, results.InlineStylesRemoved);
            
            return transformation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transformation failed for {FilePath}", sourceFilePath);
            transformation.Status = TransformationStatus.Failed;
            transformation.IssuesDetected.Add($"Error: {ex.Message}");
            throw;
        }
    }
    
    public async Task<TransformationPattern> LearnPatternAsync(
        string exampleOldPath,
        string exampleNewPath,
        string patternName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Learning transformation pattern from {OldPath} → {NewPath}", 
            exampleOldPath, exampleNewPath);
        
        // Read both files
        var oldCode = await File.ReadAllTextAsync(exampleOldPath, cancellationToken);
        var newCode = await File.ReadAllTextAsync(exampleNewPath, cancellationToken);
        
        // Parse both
        var oldParsed = RazorParser.ParseRazorFile(exampleOldPath, "default", null);
        var newParsed = RazorParser.ParseRazorFile(exampleNewPath, "default", null);
        
        // Use LLM to learn transformation
        var pattern = await LearnTransformationPatternWithLLMAsync(
            oldCode,
            newCode,
            oldParsed,
            newParsed,
            patternName,
            exampleOldPath,
            exampleNewPath,
            cancellationToken);
        
        // Store pattern
        _patterns.Add(pattern);
        
        _logger.LogInformation("Learned pattern '{PatternName}' with {RuleCount} rules",
            patternName, pattern.Rules.Count);
        
        return pattern;
    }
    
    public async Task<PageTransformation> ApplyPatternAsync(
        string patternId,
        string targetFilePath,
        CancellationToken cancellationToken = default)
    {
        var pattern = _patterns.FirstOrDefault(p => p.Id == patternId || p.Name == patternId);
        if (pattern == null)
        {
            throw new InvalidOperationException($"Pattern '{patternId}' not found");
        }
        
        _logger.LogInformation("Applying pattern '{PatternName}' to {FilePath}",
            pattern.Name, targetFilePath);
        
        // Read target file
        var targetCode = await File.ReadAllTextAsync(targetFilePath, cancellationToken);
        var parsed = RazorParser.ParseRazorFile(targetFilePath, "default", null);
        
        // Apply pattern with LLM
        var transformation = await ApplyPatternWithLLMAsync(
            pattern,
            targetCode,
            parsed,
            targetFilePath,
            cancellationToken);
        
        pattern.TimesApplied++;
        pattern.LastUsedAt = DateTime.UtcNow;
        
        return transformation;
    }
    
    public Task<List<TransformationPattern>> GetPatternsAsync(string? context = null)
    {
        var patterns = context == null 
            ? _patterns 
            : _patterns.Where(p => p.Context == context).ToList();
        
        return Task.FromResult(patterns);
    }
    
    // === PRIVATE METHODS ===
    
    private async Task<PageAnalysis> AnalyzePageAsync(
        ParseResult parsed,
        string sourceCode,
        TransformationOptions options,
        CancellationToken cancellationToken)
    {
        var analysis = new PageAnalysis
        {
            FileSize = sourceCode.Length
        };
        
        var lines = sourceCode.Split('\n');
        
        // Check file size
        if (lines.Length > 200)
        {
            analysis.Issues.Add($"Component too large ({lines.Length} lines) - should be split into smaller components");
        }
        
        // Check for inline styles
        var inlineStyleMatches = Regex.Matches(sourceCode, @"style=""[^""]+""");
        if (inlineStyleMatches.Count > 0)
        {
            analysis.Issues.Add($"{inlineStyleMatches.Count} inline styles detected - should be moved to CSS");
            analysis.HasInlineStyles = true;
        }
        
        // Check for error handling
        if (!sourceCode.Contains("try") && !sourceCode.Contains("ErrorBoundary"))
        {
            analysis.Issues.Add("No error handling detected - should add try/catch or ErrorBoundary");
            analysis.NeedsErrorHandling = true;
        }
        
        // Check for loading states
        if (!sourceCode.Contains("isLoading") && !sourceCode.Contains("LoadingSpinner"))
        {
            analysis.Issues.Add("No loading states detected - should add loading indicators");
            analysis.NeedsLoadingStates = true;
        }
        
        // Check for repeated HTML patterns
        var htmlBlocks = ExtractHTMLBlocks(sourceCode);
        var repeated = FindRepeatedBlocks(htmlBlocks);
        if (repeated.Any())
        {
            analysis.Issues.Add($"{repeated.Count} repeated HTML patterns detected - candidates for component extraction");
            // Would populate ComponentCandidates here with full detection
        }
        
        return analysis;
    }
    
    private List<HTMLBlock> ExtractHTMLBlocks(string code)
    {
        // Simple extraction - in production, use proper HTML parser
        var blocks = new List<HTMLBlock>();
        var lines = code.Split('\n');
        
        // Look for div/component blocks
        var divPattern = new Regex(@"<div[^>]*>", RegexOptions.Multiline);
        var matches = divPattern.Matches(code);
        
        // Simplified block extraction
        foreach (Match match in matches)
        {
            blocks.Add(new HTMLBlock
            {
                HTML = match.Value,
                ElementCount = 1,
                HasButton = match.Value.Contains("button"),
                HasImage = match.Value.Contains("img")
            });
        }
        
        return blocks;
    }
    
    private List<HTMLBlock> FindRepeatedBlocks(List<HTMLBlock> blocks)
    {
        // Simple similarity check - in production, use better algorithm
        var repeated = new List<HTMLBlock>();
        
        for (int i = 0; i < blocks.Count; i++)
        {
            int similarCount = 0;
            for (int j = i + 1; j < blocks.Count; j++)
            {
                if (blocks[i].ElementCount == blocks[j].ElementCount &&
                    blocks[i].HasButton == blocks[j].HasButton &&
                    blocks[i].HasImage == blocks[j].HasImage)
                {
                    similarCount++;
                }
            }
            
            if (similarCount >= 1 && !repeated.Contains(blocks[i]))
            {
                repeated.Add(blocks[i]);
            }
        }
        
        return repeated;
    }
    
    private async Task<TransformationPlan> GenerateTransformationPlanAsync(
        ParseResult parsed,
        string sourceCode,
        PageAnalysis analysis,
        TransformationOptions options,
        CancellationToken cancellationToken)
    {
        var prompt = BuildTransformationPrompt(parsed, sourceCode, analysis, options);
        
        // Call LLM (DeepSeek Coder)
        var response = await _llmService.GenerateAsync(prompt, cancellationToken);
        
        // Parse response
        var plan = ParseTransformationPlan(response);
        
        return plan;
    }
    
    private string BuildTransformationPrompt(
        ParseResult parsed,
        string sourceCode,
        PageAnalysis analysis,
        TransformationOptions options)
    {
        var fileName = parsed.CodeElements.FirstOrDefault()?.FilePath ?? "Unknown";
        return $@"
You are a Blazor refactoring expert. Transform this component to modern best practices.

SOURCE FILE: {fileName}
═══════════════════════════════════════════════════════

{sourceCode}

ANALYSIS:
═════════
Issues Detected:
{string.Join("\n", analysis.Issues.Select(i => $"- {i}"))}

TRANSFORMATION GOALS:
═════════════════════
{(options.ExtractComponents ? "✅ Extract reusable components from repeated patterns" : "⏭️  Skip component extraction")}
{(options.ModernizeCSS ? "✅ Modernize CSS (extract inline styles, use CSS variables)" : "⏭️  Skip CSS modernization")}
{(options.AddErrorHandling ? "✅ Add comprehensive error handling" : "⏭️  Skip error handling")}
{(options.AddLoadingStates ? "✅ Add loading states and indicators" : "⏭️  Skip loading states")}
{(options.AddAccessibility ? "✅ Add accessibility (ARIA labels, keyboard nav)" : "⏭️  Skip accessibility")}

REQUIREMENTS:
═════════════
1. Preserve ALL existing functionality
2. Use modern Blazor patterns (.NET 8+)
3. Extract components for repeated patterns (ProductCard, FormField, etc.)
4. Move ALL inline styles to component-scoped CSS
5. Use CSS variables for colors, spacing, fonts
6. Add error boundaries and loading states
7. Keep each component under 150 lines
8. Use descriptive, meaningful names
9. Add XML documentation comments
10. Add render modes where appropriate

OUTPUT INSTRUCTIONS:
═══════════════════
Return ONLY valid JSON. Use double quotes for all strings and property names. NO markdown, NO code blocks, NO explanatory text.

Example JSON structure:
{{
  ""main_component"": {{
    ""file_path"": ""Pages/YourPage.razor"",
    ""code"": ""...full razor component code...""
  }},
  ""extracted_components"": [
    {{
      ""name"": ""ComponentName"",
      ""file_path"": ""Components/ComponentName.razor"",
      ""code"": ""...full component code..."",
      ""css"": ""...component-scoped CSS...""
    }}
  ],
  ""css_variables"": {{
    ""--color-primary"": ""#007bff"",
    ""--color-success"": ""#28a745"",
    ""--spacing-sm"": ""0.5rem"",
    ""--spacing-md"": ""1rem""
  }},
  ""site_css"": ""...main site CSS with variables..."",
  ""improvements"": [
    ""Extracted ProductCard component (12 occurrences → 1 reusable component)"",
    ""Moved 47 inline styles to external CSS"",
    ""Added error boundary wrapper"",
    ""Added loading state management""
  ],
  ""confidence"": 0.95,
  ""reasoning"": ""Transformation follows Blazor best practices...""
}}
";
    }
    
    private TransformationPlan ParseTransformationPlan(string llmResponse)
    {
        try
        {
            _logger.LogDebug("Parsing LLM response (length: {Length}): {Response}", 
                llmResponse.Length, llmResponse.Substring(0, Math.Min(200, llmResponse.Length)));
            
            // Clean up markdown code blocks if present
            var json = llmResponse.Trim();
            if (json.StartsWith("```"))
            {
                json = Regex.Replace(json, @"```(?:json)?\s*", "");
                json = json.TrimEnd('`').Trim();
            }
            
            // If response has text before JSON, extract just the JSON
            if (!json.StartsWith("{") && !json.StartsWith("["))
            {
                var jsonStart = json.IndexOf('{');
                if (jsonStart > 0)
                {
                    _logger.LogDebug("Stripping {Chars} chars of text before JSON", jsonStart);
                    json = json.Substring(jsonStart);
                }
            }
            
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var plan = new TransformationPlan();
            
            // Main component
            if (root.TryGetProperty("main_component", out var mainComp))
            {
                plan.MainComponentPath = mainComp.GetProperty("file_path").GetString() ?? "";
                plan.MainComponentCode = mainComp.GetProperty("code").GetString() ?? "";
            }
            
            // Extracted components
            if (root.TryGetProperty("extracted_components", out var components))
            {
                foreach (var comp in components.EnumerateArray())
                {
                    plan.ExtractedComponents.Add(new ExtractedComponent
                    {
                        Name = comp.GetProperty("name").GetString() ?? "",
                        FilePath = comp.GetProperty("file_path").GetString() ?? "",
                        Code = comp.GetProperty("code").GetString() ?? "",
                        CSS = comp.TryGetProperty("css", out var css) ? css.GetString() : null
                    });
                }
            }
            
            // CSS
            if (root.TryGetProperty("site_css", out var siteCss))
            {
                plan.CSS = siteCss.GetString() ?? "";
            }
            
            // CSS Variables
            if (root.TryGetProperty("css_variables", out var vars))
            {
                foreach (var prop in vars.EnumerateObject())
                {
                    plan.CSSVariables[prop.Name] = prop.Value.GetString() ?? "";
                }
            }
            
            // Improvements
            if (root.TryGetProperty("improvements", out var improvements))
            {
                foreach (var imp in improvements.EnumerateArray())
                {
                    plan.Improvements.Add(imp.GetString() ?? "");
                }
            }
            
            // Metadata
            plan.Confidence = root.TryGetProperty("confidence", out var conf) 
                ? (float)conf.GetDouble() 
                : 0.9f;
            plan.Reasoning = root.TryGetProperty("reasoning", out var reason)
                ? reason.GetString() ?? ""
                : "";
            
            return plan;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse LLM response as JSON: {Response}", llmResponse);
            throw new InvalidOperationException($"LLM returned invalid JSON: {ex.Message}");
        }
    }
    
    private async Task<TransformationExecutionResult> ExecuteTransformationPlanAsync(
        TransformationPlan plan,
        TransformationOptions options,
        CancellationToken cancellationToken)
    {
        var result = new TransformationExecutionResult
        {
            Confidence = plan.Confidence,
            Reasoning = plan.Reasoning,
            Improvements = plan.Improvements
        };
        
        // Main component
        result.Files.Add(new GeneratedFile
        {
            FilePath = plan.MainComponentPath,
            Content = plan.MainComponentCode,
            Type = FileType.Component,
            LineCount = plan.MainComponentCode.Split('\n').Length,
            Description = "Main transformed component"
        });
        result.TotalLines += plan.MainComponentCode.Split('\n').Length;
        
        // Extracted components
        foreach (var comp in plan.ExtractedComponents)
        {
            result.Files.Add(new GeneratedFile
            {
                FilePath = comp.FilePath,
                Content = comp.Code,
                Type = FileType.Component,
                LineCount = comp.Code.Split('\n').Length,
                Description = $"Extracted component: {comp.Name}"
            });
            result.TotalLines += comp.Code.Split('\n').Length;
            result.ComponentsExtracted++;
            
            // Component CSS
            if (!string.IsNullOrEmpty(comp.CSS))
            {
                var cssPath = comp.FilePath + ".css";
                result.Files.Add(new GeneratedFile
                {
                    FilePath = cssPath,
                    Content = comp.CSS,
                    Type = FileType.CSS,
                    LineCount = comp.CSS.Split('\n').Length,
                    Description = $"Component-scoped CSS for {comp.Name}"
                });
            }
        }
        
        // Site CSS
        if (!string.IsNullOrEmpty(plan.CSS))
        {
            var cssPath = options.CSSOutputPath ?? "wwwroot/css/site.css";
            result.Files.Add(new GeneratedFile
            {
                FilePath = cssPath,
                Content = plan.CSS,
                Type = FileType.CSS,
                LineCount = plan.CSS.Split('\n').Length,
                Description = "Main site CSS with variables"
            });
            result.InlineStylesRemoved = Regex.Matches(plan.MainComponentCode, @"style=""").Count;
        }
        
        return result;
    }
    
    private async Task<TransformationPattern> LearnTransformationPatternWithLLMAsync(
        string oldCode,
        string newCode,
        ParseResult oldParsed,
        ParseResult newParsed,
        string patternName,
        string exampleOldPath,
        string exampleNewPath,
        CancellationToken cancellationToken)
    {
        var prompt = $@"
You are learning a transformation pattern by comparing an old and new version of a file.

OLD VERSION:
════════════
{oldCode}

NEW VERSION:
════════════
{newCode}

Analyze the differences and create a reusable transformation pattern.

Return JSON:
{{
  ""pattern_name"": ""{patternName}"",
  ""description"": ""Brief description of transformation"",
  ""rules"": [
    {{
      ""type"": ""ExtractComponent"",
      ""description"": ""What was extracted"",
      ""parameters"": {{}}
    }}
  ]
}}
";
        
        var response = await _llmService.GenerateAsync(prompt, cancellationToken);
        
        // Parse pattern (simplified)  
        var pattern = new TransformationPattern
        {
            Name = patternName,
            Description = $"Learned from {Path.GetFileName(exampleOldPath)} transformation",
            ExampleOldFilePath = exampleOldPath,
            ExampleNewFilePath = exampleNewPath
        };
        
        return pattern;
    }
    
    private async Task<PageTransformation> ApplyPatternWithLLMAsync(
        TransformationPattern pattern,
        string targetCode,
        ParseResult parsed,
        string targetFilePath,
        CancellationToken cancellationToken)
    {
        var prompt = $@"
Apply this learned transformation pattern to a new file.

PATTERN: {pattern.Name}
{pattern.Description}

TARGET FILE:
════════════
{targetCode}

Apply the same transformations as in the pattern.

Return the same JSON format as transform_page.
";
        
        var response = await _llmService.GenerateAsync(prompt, cancellationToken);
        var plan = ParseTransformationPlan(response);
        var results = await ExecuteTransformationPlanAsync(plan, new TransformationOptions(), cancellationToken);
        
        return new PageTransformation
        {
            SourceFilePath = targetFilePath,
            Type = TransformationType.FullTransformation,
            Status = TransformationStatus.Completed,
            GeneratedFiles = results.Files,
            ComponentsExtracted = results.ComponentsExtracted,
            InlineStylesRemoved = results.InlineStylesRemoved,
            ImprovementsMade = results.Improvements,
            Confidence = results.Confidence,
            Reasoning = results.Reasoning,
            CompletedAt = DateTime.UtcNow
        };
    }
}

