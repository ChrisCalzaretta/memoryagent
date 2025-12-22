using System.Text.Json;
using System.Text.RegularExpressions;
using AgentContracts.Services;
using AgentContracts.Models;
using AgentContracts.Requests;
using CodingAgent.Server.Templates;
using CodingAgent.Server.Clients; // For BrandSystem

namespace CodingAgent.Server.Services;

/// <summary>
/// Service that uses Phi4 for intelligent thinking/planning before code generation
/// </summary>
public interface IPhi4ThinkingService
{
    /// <summary>
    /// Create a detailed project plan from a user request and template
    /// </summary>
    Task<ProjectPlan> PlanProjectAsync(
        string userRequest,
        Templates.IProjectTemplate selectedTemplate,
        Dictionary<string, string>? existingContext = null,
        CancellationToken ct = default);
    
    /// <summary>
    /// Think about how to implement a specific file/step
    /// </summary>
    Task<ThinkingResult> ThinkAboutStepAsync(
        ThinkingContext context,
        CancellationToken ct = default);
    
    /// <summary>
    /// Analyze why previous attempts failed and suggest new approaches
    /// </summary>
    Task<FailureAnalysis> AnalyzeFailuresAsync(
        FailureAnalysisContext context,
        CancellationToken ct = default);
    
    /// <summary>
    /// Decide if we should build/compile now
    /// </summary>
    Task<BuildDecision> ShouldBuildNowAsync(
        BuildDecisionContext context,
        CancellationToken ct = default);
}

/// <summary>
/// Project plan with ordered file generation steps
/// </summary>
public record ProjectPlan
{
    public required string ProjectName { get; init; }
    public required string Language { get; init; }
    public required int TotalFiles { get; init; }
    public required int EstimatedComplexity { get; init; }
    public required List<PlanStep> Files { get; init; }
    public List<string>? Risks { get; init; }
    public List<string>? PatternsToApply { get; init; }
}

/// <summary>
/// A single file generation step in the plan
/// </summary>
public record PlanStep
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required string Purpose { get; init; }
    public List<string> Dependencies { get; init; } = new();
    public int EstimatedComplexity { get; init; }
    public int Priority { get; init; }
    public List<string>? Risks { get; init; }
    public List<string>? PatternsToApply { get; init; }
}

/// <summary>
/// Context for thinking about a step
/// </summary>
public record ThinkingContext
{
    public required string TaskDescription { get; init; }
    public required string FilePath { get; init; }
    public required string Language { get; init; }
    public string? ProjectType { get; init; }
    public Dictionary<string, string> ExistingFiles { get; init; } = new();
    public List<AttemptSummary> PreviousAttempts { get; init; } = new();
    public string[] AvailablePatterns { get; init; } = Array.Empty<string>();
    
    // 🔥 NEW: Multi-model support fields
    public string? LatestBuildErrors { get; init; }
    public int? LatestValidationScore { get; init; }
    public string[]? LatestValidationIssues { get; init; }
    public string? LatestValidationSummary { get; init; }
    
    // 🌐 NEW: Web research augmentation
    public List<WebSearchResult>? WebResearch { get; init; }
    
    // 🎨 NEW: Design/brand guidelines
    public BrandSystem? BrandGuidelines { get; init; }
}

/// <summary>
/// Summary of a previous attempt
/// </summary>
public record AttemptSummary
{
    public int AttemptNumber { get; init; }
    public string Model { get; init; } = "";
    public double Score { get; init; }
    public string[] Issues { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of thinking about a step
/// </summary>
public record ThinkingResult
{
    // Original Phi4 fields
    public required string Approach { get; init; }
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public string[] PatternsToUse { get; init; } = Array.Empty<string>();
    public string[] Risks { get; init; } = Array.Empty<string>();
    public string? Suggestions { get; init; }
    public int EstimatedComplexity { get; init; } = 5;
    public string? RecommendedModel { get; init; }
    
    // 🔥 NEW: Multi-Model Support (backward compatible - defaults provided)
    public List<string> ParticipatingModels { get; init; } = new();
    public string Strategy { get; init; } = "solo";
    public double Confidence { get; init; } = 1.0;
    public string Patterns { get; init; } = ""; // Comma-separated patterns
    public string Complexity { get; init; } = "moderate"; // low, moderate, high, critical
}

/// <summary>
/// Context for analyzing failures
/// </summary>
public record FailureAnalysisContext
{
    public required string FilePath { get; init; }
    public required string TaskDescription { get; init; }
    public required List<AttemptSummary> Attempts { get; init; }
    public Dictionary<string, string> ExistingFiles { get; init; } = new();
}

/// <summary>
/// Result of failure analysis
/// </summary>
public record FailureAnalysis
{
    public required string RootCause { get; init; }
    public string[] RecommendedActions { get; init; } = Array.Empty<string>();
    public string? AlternativeApproach { get; init; }
    public bool ShouldSplitFile { get; init; }
    public string[] SuggestedFiles { get; init; } = Array.Empty<string>();
    public double SuccessProbability { get; init; }
}

/// <summary>
/// Context for build decision
/// </summary>
public record BuildDecisionContext
{
    public int FilesGeneratedSinceLastBuild { get; init; }
    public int TotalFilesGenerated { get; init; }
    public int TotalFilesPlanned { get; init; }
    public string[] RecentFileTypes { get; init; } = Array.Empty<string>();
    public bool HadRecentFailures { get; init; }
}

/// <summary>
/// Result of build decision
/// </summary>
public record BuildDecision
{
    public bool ShouldBuild { get; init; }
    public string Reason { get; init; } = "";
}

/// <summary>
/// Implementation using Ollama with Phi4
/// </summary>
public class Phi4ThinkingService : IPhi4ThinkingService
{
    private readonly IOllamaClient _ollama;
    private readonly ILogger<Phi4ThinkingService> _logger;
    private const string Phi4Model = "phi4:latest";
    
    public Phi4ThinkingService(IOllamaClient ollama, ILogger<Phi4ThinkingService> logger)
    {
        _ollama = ollama;
        _logger = logger;
    }
    
    public async Task<ProjectPlan> PlanProjectAsync(
        string userRequest,
        Templates.IProjectTemplate selectedTemplate,
        Dictionary<string, string>? existingContext = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🧠 Phi4 creating project plan for: {ProjectType}", selectedTemplate.ProjectType);
        
        var prompt = $@"You are an expert project planner. Create a detailed file-by-file plan for:

USER REQUEST: {userRequest}
PROJECT TYPE: {selectedTemplate.Language} {selectedTemplate.ProjectType}
TEMPLATE: {selectedTemplate.Description}

The template provides these base files:
{string.Join("\n", selectedTemplate.Files.Select((f, i) => $"{i + 1}. {f.Key}"))}

Your task: Analyze the user request and determine what ADDITIONAL files are needed beyond the template.

Output ONLY valid JSON (no markdown, no explanation):
{{
  ""projectName"": ""SuggestedProjectName"",
  ""language"": ""{selectedTemplate.Language}"",
  ""totalFiles"": <total including template files>,
  ""estimatedComplexity"": <1-10>,
  ""files"": [
    {{
      ""fileName"": ""FileName.ext"",
      ""filePath"": ""Path/To/FileName.ext"",
      ""purpose"": ""What this file does"",
      ""dependencies"": [""Other/File.ext""],
      ""estimatedComplexity"": <1-10>,
      ""priority"": <1=first, higher=later>,
      ""risks"": [""Potential challenges""],
      ""patternsToApply"": [""Recommended patterns""]
    }}
  ],
  ""risks"": [""Overall risks""],
  ""patternsToApply"": [""Project-wide patterns""]
}}";

        try
        {
            var response = await _ollama.GenerateAsync(Phi4Model, prompt);
            var json = ExtractJson(response.Response ?? "");
            
            var plan = System.Text.Json.JsonSerializer.Deserialize<ProjectPlan>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (plan == null)
            {
                throw new InvalidOperationException("Failed to deserialize project plan");
            }
            
            _logger.LogInformation("🧠 Phi4 plan: {FileCount} files, complexity {Complexity}/10",
                plan.TotalFiles, plan.EstimatedComplexity);
            
            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "🧠 Phi4 planning failed, using template as-is");
            
            // Fallback: just use template files
            return new ProjectPlan
            {
                ProjectName = "GeneratedProject",
                Language = selectedTemplate.Language,
                TotalFiles = selectedTemplate.Files.Count,
                EstimatedComplexity = 3,
                Files = selectedTemplate.Files.Select((f, i) => new PlanStep
                {
                    FileName = Path.GetFileName(f.Key),
                    FilePath = f.Key,
                    Purpose = $"Template file from {selectedTemplate.TemplateId}",
                    EstimatedComplexity = 2,
                    Priority = i + 1
                }).ToList()
            };
        }
    }
    
    public async Task<ThinkingResult> ThinkAboutStepAsync(
        ThinkingContext context,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🧠 Phi4 thinking about: {File}", context.FilePath);
        
        var prompt = BuildThinkingPrompt(context);
        
        try
        {
            var ollamaResponse = await _ollama.GenerateAsync(Phi4Model, prompt);
            var result = ParseThinkingResult(ollamaResponse.Response ?? "");
            
            _logger.LogInformation(
                "🧠 Phi4 thinking complete: Approach='{Approach}', Complexity={Complexity}",
                result.Approach.Length > 50 ? result.Approach[..50] + "..." : result.Approach,
                result.EstimatedComplexity);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "🧠 Phi4 thinking failed, using default approach");
            
            return new ThinkingResult
            {
                Approach = "Generate standard implementation based on task description",
                EstimatedComplexity = 5
            };
        }
    }
    
    public async Task<FailureAnalysis> AnalyzeFailuresAsync(
        FailureAnalysisContext context,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "🔍 Phi4 analyzing failures for: {File} ({Attempts} attempts)",
            context.FilePath, context.Attempts.Count);
        
        var prompt = BuildFailureAnalysisPrompt(context);
        
        try
        {
            var ollamaResponse = await _ollama.GenerateAsync(Phi4Model, prompt);
            var result = ParseFailureAnalysis(ollamaResponse.Response ?? "");
            
            _logger.LogInformation(
                "🔍 Phi4 analysis complete: RootCause='{RootCause}', SuccessProb={Prob:P0}",
                result.RootCause.Length > 50 ? result.RootCause[..50] + "..." : result.RootCause,
                result.SuccessProbability);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "🔍 Phi4 analysis failed, using default");
            
            return new FailureAnalysis
            {
                RootCause = "Unable to determine root cause automatically",
                RecommendedActions = new[] { "Review validation errors", "Simplify requirements" },
                SuccessProbability = 0.5
            };
        }
    }
    
    public Task<BuildDecision> ShouldBuildNowAsync(
        BuildDecisionContext context,
        CancellationToken ct = default)
    {
        // Quick heuristic-based decision (no need to call Phi4 for this)
        
        // Build every 5 files
        if (context.FilesGeneratedSinceLastBuild >= 5)
        {
            return Task.FromResult(new BuildDecision
            {
                ShouldBuild = true,
                Reason = "Checkpoint: 5 files generated since last build"
            });
        }
        
        // Build at 25%, 50%, 75% completion
        if (context.TotalFilesPlanned > 0)
        {
            var progress = (double)context.TotalFilesGenerated / context.TotalFilesPlanned;
            if ((progress >= 0.25 && context.TotalFilesGenerated == (int)(context.TotalFilesPlanned * 0.25)) ||
                (progress >= 0.50 && context.TotalFilesGenerated == (int)(context.TotalFilesPlanned * 0.50)) ||
                (progress >= 0.75 && context.TotalFilesGenerated == (int)(context.TotalFilesPlanned * 0.75)))
            {
                return Task.FromResult(new BuildDecision
                {
                    ShouldBuild = true,
                    Reason = $"Checkpoint: {progress:P0} completion"
                });
            }
        }
        
        // Build after failures to validate architecture
        if (context.HadRecentFailures && context.FilesGeneratedSinceLastBuild >= 2)
        {
            return Task.FromResult(new BuildDecision
            {
                ShouldBuild = true,
                Reason = "Validation after recent failures"
            });
        }
        
        return Task.FromResult(new BuildDecision
        {
            ShouldBuild = false,
            Reason = "Continue generating"
        });
    }
    
    private string BuildThinkingPrompt(ThinkingContext context)
    {
        var existingFilesStr = context.ExistingFiles.Count > 0
            ? string.Join("\n", context.ExistingFiles.Keys.Select(f => $"- {f}"))
            : "None yet";
        
        var previousAttemptsStr = context.PreviousAttempts.Count > 0
            ? string.Join("\n", context.PreviousAttempts.Select(a => 
                $"- Attempt {a.AttemptNumber} ({a.Model}): Score {a.Score:F1}/10, Issues: {string.Join(", ", a.Issues.Take(2))}"))
            : "First attempt";
        
        return $@"You are a senior software architect planning code implementation.

TASK: {context.TaskDescription}
FILE TO GENERATE: {context.FilePath}
LANGUAGE: {context.Language}
PROJECT TYPE: {context.ProjectType ?? "Unknown"}

EXISTING FILES:
{existingFilesStr}

PREVIOUS ATTEMPTS:
{previousAttemptsStr}

Think carefully about how to implement this file. Consider:
1. What does this file need to do?
2. What dependencies does it need from existing files?
3. What patterns should be used?
4. What could go wrong?
5. How complex is this (1-10)?

Output your thinking as JSON:
{{
    ""approach"": ""Brief description of implementation approach"",
    ""dependencies"": [""list of files this depends on""],
    ""patternsToUse"": [""patterns like Repository, Factory, etc""],
    ""risks"": [""potential issues to watch for""],
    ""suggestions"": ""Specific suggestions for the code generator"",
    ""estimatedComplexity"": 5,
    ""recommendedModel"": ""deepseek or claude""
}}

Output ONLY the JSON, nothing else.";
    }
    
    private string BuildFailureAnalysisPrompt(FailureAnalysisContext context)
    {
        var attemptsStr = string.Join("\n", context.Attempts.Select(a =>
            $@"Attempt {a.AttemptNumber} ({a.Model}):
  - Score: {a.Score:F1}/10
  - Issues: {string.Join(", ", a.Issues)}"));
        
        return $@"You are analyzing why code generation is failing and how to fix it.

FILE: {context.FilePath}
TASK: {context.TaskDescription}

ATTEMPTS:
{attemptsStr}

Analyze:
1. What is the ROOT CAUSE of these failures?
2. Why are previous fixes not working?
3. What different approach should be tried?
4. Should this file be split into multiple files?
5. What is the probability the next attempt will succeed?

Output your analysis as JSON:
{{
    ""rootCause"": ""The fundamental reason for failure"",
    ""recommendedActions"": [""Action 1"", ""Action 2"", ""Action 3""],
    ""alternativeApproach"": ""A completely different way to solve this"",
    ""shouldSplitFile"": false,
    ""suggestedFiles"": [],
    ""successProbability"": 0.7
}}

Output ONLY the JSON, nothing else.";
    }
    
    private ThinkingResult ParseThinkingResult(string response)
    {
        try
        {
            // Try to extract JSON from response
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var json = JsonDocument.Parse(jsonMatch.Value);
                var root = json.RootElement;
                
                return new ThinkingResult
                {
                    Approach = root.TryGetProperty("approach", out var a) ? a.GetString() ?? "" : "",
                    Dependencies = ParseStringArray(root, "dependencies"),
                    PatternsToUse = ParseStringArray(root, "patternsToUse"),
                    Risks = ParseStringArray(root, "risks"),
                    Suggestions = root.TryGetProperty("suggestions", out var s) ? s.GetString() : null,
                    EstimatedComplexity = root.TryGetProperty("estimatedComplexity", out var c) ? c.GetInt32() : 5,
                    RecommendedModel = root.TryGetProperty("recommendedModel", out var m) ? m.GetString() : null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse thinking result as JSON");
        }
        
        // Fallback: extract key information from text
        return new ThinkingResult
        {
            Approach = response.Length > 200 ? response[..200] : response,
            EstimatedComplexity = 5
        };
    }
    
    private FailureAnalysis ParseFailureAnalysis(string response)
    {
        try
        {
            var jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
            if (jsonMatch.Success)
            {
                var json = JsonDocument.Parse(jsonMatch.Value);
                var root = json.RootElement;
                
                return new FailureAnalysis
                {
                    RootCause = root.TryGetProperty("rootCause", out var r) ? r.GetString() ?? "" : "",
                    RecommendedActions = ParseStringArray(root, "recommendedActions"),
                    AlternativeApproach = root.TryGetProperty("alternativeApproach", out var a) ? a.GetString() : null,
                    ShouldSplitFile = root.TryGetProperty("shouldSplitFile", out var s) && s.GetBoolean(),
                    SuggestedFiles = ParseStringArray(root, "suggestedFiles"),
                    SuccessProbability = root.TryGetProperty("successProbability", out var p) ? p.GetDouble() : 0.5
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse failure analysis as JSON");
        }
        
        return new FailureAnalysis
        {
            RootCause = response.Length > 200 ? response[..200] : response,
            SuccessProbability = 0.5
        };
    }
    
    private static string[] ParseStringArray(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            return prop.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToArray();
        }
        return Array.Empty<string>();
    }
    
    /// <summary>
    /// Extract JSON from LLM response (handles markdown code blocks)
    /// </summary>
    private static string ExtractJson(string response)
    {
        // Remove markdown code blocks if present
        var jsonMatch = Regex.Match(response, @"```(?:json)?\s*(\{.+\})\s*```", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            return jsonMatch.Groups[1].Value;
        }
        
        // Try to find JSON object directly
        var directMatch = Regex.Match(response, @"\{.+\}", RegexOptions.Singleline);
        if (directMatch.Success)
        {
            return directMatch.Value;
        }
        
        return response; // Hope for the best!
    }
}

