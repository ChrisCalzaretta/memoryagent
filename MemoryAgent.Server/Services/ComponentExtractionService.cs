using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Detects and extracts reusable component patterns
/// Now with evolving prompts via PromptService
/// </summary>
public class ComponentExtractionService : IComponentExtractionService
{
    private readonly ILLMService _llmService;
    private readonly IPromptService _promptService;
    private readonly IPathTranslationService _pathTranslation;
    private readonly ILogger<ComponentExtractionService> _logger;
    
    public ComponentExtractionService(
        ILLMService llmService,
        IPromptService promptService,
        IPathTranslationService pathTranslation,
        ILogger<ComponentExtractionService> logger)
    {
        _llmService = llmService;
        _promptService = promptService;
        _pathTranslation = pathTranslation;
        _logger = logger;
    }
    
    public async Task<List<ComponentCandidate>> DetectReusableComponentsAsync(
        string projectPath,
        int minOccurrences = 2,
        float minSimilarity = 0.7f,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Detecting reusable components in {ProjectPath}", projectPath);
        
        // Translate Windows path to container path
        var containerPath = _pathTranslation.TranslateToContainerPath(projectPath);
        _logger.LogDebug("Path translation: {OriginalPath} -> {ContainerPath}", projectPath, containerPath);
        
        // 1. Scan all .razor files
        var razorFiles = Directory.GetFiles(containerPath, "*.razor", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} Razor files to analyze", razorFiles.Length);
        
        // 2. Extract HTML blocks from each file
        var allBlocks = new List<HTMLBlock>();
        foreach (var file in razorFiles)
        {
            var blocks = await ExtractHTMLBlocksFromFileAsync(file, cancellationToken);
            allBlocks.AddRange(blocks);
        }
        
        _logger.LogInformation("Extracted {Count} HTML blocks", allBlocks.Count);
        
        // 3. Find similar blocks (static analysis)
        var similarGroups = await FindSimilarBlocksAsync(allBlocks, minSimilarity);
        _logger.LogInformation("Found {Count} groups of similar blocks", similarGroups.Count);
        
        // 4. Filter by occurrence count
        var candidates = new List<ComponentCandidate>();
        foreach (var group in similarGroups.Where(g => g.Count >= minOccurrences))
        {
            // 5. Use LLM to analyze if this should be a component
            var candidate = await AnalyzeCandidateWithLLMAsync(group, cancellationToken);
            if (candidate != null)
            {
                candidates.Add(candidate);
            }
        }
        
        // 6. Sort by value score
        candidates = candidates.OrderByDescending(c => c.ValueScore).ToList();
        
        _logger.LogInformation("Identified {Count} reusable component candidates", candidates.Count);
        
        return candidates;
    }
    
    public async Task<ExtractedComponent> ExtractComponentAsync(
        ComponentCandidate candidate,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting component: {Name}", candidate.Name);
        
        var stopwatch = Stopwatch.StartNew();
        string prompt;
        string? promptId = null;
        
        try
        {
            // Try to use versioned prompt
            var promptTemplate = await _promptService.GetPromptAsync("component_extraction", allowTestVariant: true, cancellationToken);
            promptId = promptTemplate.Id;
            
            var example = candidate.Locations.First();
            var parametersDesc = string.Join("\n", candidate.ProposedInterface.Parameters.Select(p => 
                $"- {p.Name} ({p.Type}){(p.Required ? " [Required]" : "")} - {p.Description}"));
            var eventsDesc = string.Join("\n", candidate.ProposedInterface.Events.Select(e => 
                $"- {e.Name} ({e.Type}) - {e.Description}"));
            
            var variables = new Dictionary<string, string>
            {
                ["componentName"] = candidate.Name,
                ["description"] = candidate.Description,
                ["occurrences"] = candidate.Occurrences.ToString(),
                ["exampleCode"] = example.Code,
                ["parameters"] = parametersDesc,
                ["events"] = eventsDesc,
                ["filePath"] = example.FilePath,
                ["lineStart"] = example.LineStart.ToString(),
                ["lineEnd"] = example.LineEnd.ToString()
            };
            
            prompt = await _promptService.RenderPromptAsync("component_extraction", variables, cancellationToken);
        }
        catch
        {
            // Fallback to hardcoded prompt
            prompt = BuildComponentExtractionPrompt(candidate);
        }
        
        var response = await _llmService.GenerateAsync(prompt, cancellationToken);
        stopwatch.Stop();
        
        var component = ParseExtractedComponent(response, candidate.Name, outputPath);
        
        // Record execution for learning
        if (promptId != null)
        {
            try
            {
                await _promptService.RecordExecutionAsync(
                    promptId,
                    prompt.Substring(0, Math.Min(2000, prompt.Length)),
                    new Dictionary<string, string> { ["componentName"] = candidate.Name },
                    response.Substring(0, Math.Min(2000, response.Length)),
                    stopwatch.ElapsedMilliseconds,
                    parseSuccess: component.Code != null,
                    cancellationToken: cancellationToken);
            }
            catch { }
        }
        
        _logger.LogInformation("Generated component {Name} with {ParamCount} parameters",
            component.Name, component.Interface.Parameters.Count);
        
        return component;
    }
    
    public async Task<List<List<HTMLBlock>>> FindSimilarBlocksAsync(
        List<HTMLBlock> blocks,
        float minSimilarity = 0.7f)
    {
        var similarGroups = new List<List<HTMLBlock>>();
        var processed = new HashSet<HTMLBlock>();
        
        foreach (var block in blocks)
        {
            if (processed.Contains(block)) continue;
            
            var similar = new List<HTMLBlock> { block };
            
            foreach (var otherBlock in blocks)
            {
                if (processed.Contains(otherBlock) || block == otherBlock) continue;
                
                var similarity = CalculateBlockSimilarity(block, otherBlock);
                if (similarity >= minSimilarity)
                {
                    similar.Add(otherBlock);
                    processed.Add(otherBlock);
                }
            }
            
            if (similar.Count >= 2)
            {
                similarGroups.Add(similar);
            }
            
            processed.Add(block);
        }
        
        return similarGroups;
    }
    
    // === PRIVATE METHODS ===
    
    private async Task<List<HTMLBlock>> ExtractHTMLBlocksFromFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var blocks = new List<HTMLBlock>();
        var code = await File.ReadAllTextAsync(filePath, cancellationToken);
        var lines = code.Split('\n');
        
        // Simple block extraction - look for div/component patterns
        var divPattern = new Regex(@"<div[^>]*class=""([^""]+)""[^>]*>", RegexOptions.Multiline);
        var matches = divPattern.Matches(code);
        
        int lineNumber = 0;
        foreach (Match match in matches)
        {
            // Find the line number
            var matchIndex = match.Index;
            var currentIndex = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                currentIndex += lines[i].Length + 1; // +1 for newline
                if (currentIndex >= matchIndex)
                {
                    lineNumber = i + 1;
                    break;
                }
            }
            
            // Extract context (10 lines around the match)
            var startLine = Math.Max(0, lineNumber - 5);
            var endLine = Math.Min(lines.Length - 1, lineNumber + 5);
            var contextLines = lines.Skip(startLine).Take(endLine - startLine + 1);
            var context = string.Join("\n", contextLines);
            
            var block = new HTMLBlock
            {
                FilePath = filePath,
                LineStart = startLine,
                LineEnd = endLine,
                HTML = context,
                ElementCount = CountElements(context),
                HasImage = context.Contains("<img"),
                HasButton = context.Contains("<button") || context.Contains("@onclick"),
                HasForm = context.Contains("<EditForm") || context.Contains("<form"),
                HasPrice = context.Contains("price", StringComparison.OrdinalIgnoreCase)
            };
            
            blocks.Add(block);
        }
        
        return blocks;
    }
    
    private int CountElements(string html)
    {
        // Count opening tags
        var tagPattern = new Regex(@"<(\w+)", RegexOptions.Multiline);
        return tagPattern.Matches(html).Count;
    }
    
    private float CalculateBlockSimilarity(HTMLBlock a, HTMLBlock b)
    {
        float score = 0f;
        float totalChecks = 0f;
        
        // Element count similarity
        totalChecks++;
        if (Math.Abs(a.ElementCount - b.ElementCount) <= 2)
            score += 1f;
        
        // Feature similarity
        totalChecks++;
        if (a.HasImage == b.HasImage)
            score += 1f;
        
        totalChecks++;
        if (a.HasButton == b.HasButton)
            score += 1f;
        
        totalChecks++;
        if (a.HasForm == b.HasForm)
            score += 1f;
        
        totalChecks++;
        if (a.HasPrice == b.HasPrice)
            score += 1f;
        
        // Text similarity (simplified Levenshtein)
        totalChecks++;
        var textSimilarity = CalculateTextSimilarity(a.HTML, b.HTML);
        if (textSimilarity > 0.6f)
            score += 1f;
        
        return score / totalChecks;
    }
    
    private float CalculateTextSimilarity(string a, string b)
    {
        // Simplified similarity - in production use proper Levenshtein distance
        var aWords = a.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var bWords = b.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        var commonWords = aWords.Intersect(bWords).Count();
        var totalWords = Math.Max(aWords.Length, bWords.Length);
        
        return totalWords > 0 ? (float)commonWords / totalWords : 0f;
    }
    
    private async Task<ComponentCandidate?> AnalyzeCandidateWithLLMAsync(
        List<HTMLBlock> group,
        CancellationToken cancellationToken)
    {
        // Take up to 3 examples
        var examples = group.Take(3).ToList();
        
        var exampleTexts = new System.Text.StringBuilder();
        for (int i = 0; i < Math.Min(3, examples.Count); i++)
        {
            exampleTexts.AppendLine($@"
OCCURRENCE {i + 1}:
═════════════
File: {examples[i].FilePath}
{examples[i].HTML}
");
        }
        
        var prompt = $@"
Analyze these similar HTML blocks and determine if they should be extracted into a reusable component.

{exampleTexts}

Total occurrences: {group.Count}

Questions:
1. Should this be extracted into a reusable component?
2. What should the component be called?
3. What parameters should it accept?
4. What events should it expose?
5. What's the value score (0-100)?

Return JSON:
{{{{
  ""should_extract"": true,
  ""component_name"": ""ComponentName"",
  ""description"": ""Brief description"",
  ""priority"": ""High"",
  ""value_score"": 85,
  ""parameters"": [
    {{{{
      ""name"": ""Product"",
      ""type"": ""Product"",
      ""required"": true,
      ""description"": ""Parameter description""
    }}}}
  ],
  ""events"": [
    {{{{
      ""name"": ""OnClick"",
      ""type"": ""EventCallback<Product>"",
      ""description"": ""Event description""
    }}}}
  ]
}}}}";
        
        var response = await _llmService.GenerateAsync(prompt, cancellationToken);
        
        try
        {
            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                json = Regex.Replace(json, @"```(?:json)?\s*", "");
                json = json.TrimEnd('`').Trim();
            }
            
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (!root.TryGetProperty("should_extract", out var shouldExtract) || !shouldExtract.GetBoolean())
            {
                return null;
            }
            
            var candidate = new ComponentCandidate
            {
                Name = root.GetProperty("component_name").GetString() ?? "UnnamedComponent",
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                Occurrences = group.Count,
                Similarity = group.Count > 1 ? CalculateBlockSimilarity(group[0], group[1]) : 1.0f,
                ValueScore = root.TryGetProperty("value_score", out var score) ? (float)score.GetDouble() : 50f,
                Priority = ParsePriority(root.TryGetProperty("priority", out var pri) ? pri.GetString() : "Medium"),
                LinesSaved = group.Count * 10 // Estimate
            };
            
            // Parse parameters
            if (root.TryGetProperty("parameters", out var params_))
            {
                foreach (var param in params_.EnumerateArray())
                {
                    candidate.ProposedInterface.Parameters.Add(new ComponentParameter
                    {
                        Name = param.GetProperty("name").GetString() ?? "",
                        Type = param.GetProperty("type").GetString() ?? "",
                        Required = param.TryGetProperty("required", out var req) && req.GetBoolean(),
                        Description = param.TryGetProperty("description", out var descr) ? descr.GetString() ?? "" : ""
                    });
                }
            }
            
            // Parse events
            if (root.TryGetProperty("events", out var events))
            {
                foreach (var evt in events.EnumerateArray())
                {
                    candidate.ProposedInterface.Events.Add(new ComponentEvent
                    {
                        Name = evt.GetProperty("name").GetString() ?? "",
                        Type = evt.GetProperty("type").GetString() ?? "",
                        Description = evt.TryGetProperty("description", out var descr) ? descr.GetString() ?? "" : ""
                    });
                }
            }
            
            // Store locations
            foreach (var block in group)
            {
                candidate.Locations.Add(new ComponentOccurrence
                {
                    FilePath = block.FilePath,
                    LineStart = block.LineStart,
                    LineEnd = block.LineEnd,
                    Code = block.HTML
                });
            }
            
            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response for component candidate");
            return null;
        }
    }
    
    private Priority ParsePriority(string? priority)
    {
        return priority?.ToLower() switch
        {
            "critical" => Priority.Critical,
            "high" => Priority.High,
            "medium" => Priority.Medium,
            "low" => Priority.Low,
            _ => Priority.Medium
        };
    }
    
    private string BuildComponentExtractionPrompt(ComponentCandidate candidate)
    {
        var example = candidate.Locations.First();
        var parametersDesc = string.Join("\n", candidate.ProposedInterface.Parameters.Select(p => 
            $"- {p.Name} ({p.Type}){(p.Required ? " [Required]" : "")} - {p.Description}"));
        var eventsDesc = string.Join("\n", candidate.ProposedInterface.Events.Select(e => 
            $"- {e.Name} ({e.Type}) - {e.Description}"));
        
        return $@"
Generate a reusable Blazor component based on this pattern.

COMPONENT NAME: {candidate.Name}
DESCRIPTION: {candidate.Description}
OCCURRENCES: {candidate.Occurrences}

EXAMPLE CODE:
═════════════
{example.Code}

PROPOSED INTERFACE:
═══════════════════
Parameters:
{parametersDesc}

Events:
{eventsDesc}

Generate a complete, production-ready Blazor component.

Return JSON:
{{{{
  ""component_code"": ""...full .razor code..."",
  ""component_css"": ""...component-scoped CSS..."",
  ""refactorings"": [
    {{{{
      ""file_path"": ""{example.FilePath}"",
      ""line_start"": {example.LineStart},
      ""line_end"": {example.LineEnd},
      ""old_code"": ""..."",
      ""new_code"": ""<{candidate.Name} ... />""
    }}}}
  ]
}}}}";
    }
    
    private ExtractedComponent ParseExtractedComponent(string llmResponse, string name, string filePath)
    {
        try
        {
            var json = llmResponse.Trim();
            if (json.StartsWith("```"))
            {
                json = Regex.Replace(json, @"```(?:json)?\s*", "");
                json = json.TrimEnd('`').Trim();
            }
            
            // Try to find JSON object in the response if it doesn't start with {
            if (!json.StartsWith("{"))
            {
                var jsonMatch = Regex.Match(json, @"\{[\s\S]*\}", RegexOptions.Singleline);
                if (jsonMatch.Success)
                {
                    json = jsonMatch.Value;
                    _logger.LogWarning("LLM returned text with embedded JSON, extracting...");
                }
                else
                {
                    _logger.LogWarning("LLM returned text instead of JSON: {Response}", 
                        llmResponse.Length > 200 ? llmResponse[..200] + "..." : llmResponse);
                    
                    // Return component with the LLM's explanation as the code
                    return new ExtractedComponent
                    {
                        Name = name,
                        FilePath = filePath,
                        Code = $"// LLM Note: {llmResponse.Trim()}",
                        CSS = null
                    };
                }
            }
            
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var component = new ExtractedComponent
            {
                Name = name,
                FilePath = filePath,
                Code = root.GetProperty("component_code").GetString() ?? "",
                CSS = root.TryGetProperty("component_css", out var css) ? css.GetString() : null
            };
            
            // Parse refactorings
            if (root.TryGetProperty("refactorings", out var refactorings))
            {
                foreach (var refactor in refactorings.EnumerateArray())
                {
                    component.Refactorings.Add(new ComponentRefactoring
                    {
                        FilePath = refactor.GetProperty("file_path").GetString() ?? "",
                        LineStart = refactor.GetProperty("line_start").GetInt32(),
                        LineEnd = refactor.GetProperty("line_end").GetInt32(),
                        OldCode = refactor.GetProperty("old_code").GetString() ?? "",
                        NewCode = refactor.GetProperty("new_code").GetString() ?? ""
                    });
                }
            }
            
            return component;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse component extraction response: {Response}", 
                llmResponse.Length > 500 ? llmResponse[..500] + "..." : llmResponse);
            
            return new ExtractedComponent
            {
                Name = name,
                FilePath = filePath,
                Code = $"// Component extraction failed: {ex.Message}\n// LLM Response: {llmResponse}",
                CSS = null
            };
        }
    }
}

