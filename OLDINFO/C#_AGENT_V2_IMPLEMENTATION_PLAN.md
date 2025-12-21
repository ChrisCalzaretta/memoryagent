# C# Agent v2 - Implementation Plan

**Goal:** Transform current TaskOrchestrator into intelligent C# Agent v2 with Phi4 thinking, smart escalation, and "never surrender" resilience.

**Timeline:** 3-4 weeks  
**Status:** Ready to start  
**Current Completion:** 60% (solid foundation exists)

---

## 🎯 **Overview**

We're **enhancing**, not rebuilding. The current TaskOrchestrator has:
- ✅ 10-attempt retry loop
- ✅ MemoryAgent integration
- ✅ Build integration
- ✅ Step-by-step execution
- ✅ Deepseek/Claude cost control

**What we're adding:**
- 🔥 **FREE multi-agent collaboration** (Deepseek + Phi4 + MemoryAgent)
- 🔥 **Real-time feedback loops** (all agents work in parallel)
- 🔥 **Dynamic learning** (adapts during the session)
- 🔥 **Claude escalation** (only if local models fail)
- 🔥 Stub generation & continue

**CORE PRINCIPLE:** Try everything with FREE local models first, escalate to Claude ONLY when needed!

---

## 📋 **Phase 1: FREE Multi-Agent Collaboration (Week 1)**

**Goal:** Build real-time collaboration with Deepseek + Phi4 + MemoryAgent (ALL FREE!)

**Strategy:**
```
╔══════════════════════════════════════════════════════════════╗
║  TRY LOCAL MODELS FIRST (FREE)                               ║
║  ├─ Deepseek: Code generation                                ║
║  ├─ Phi4: Strategic thinking & analysis                      ║
║  └─ MemoryAgent: Pattern learning & adaptation               ║
║                                                               ║
║  IF local models succeed (75-80% of cases):                  ║
║    ✅ Cost: $0.00                                             ║
║    ✅ Time: 18-45 seconds                                     ║
║                                                               ║
║  IF local models struggle after 3 iterations (20% of cases): ║
║    ⚠️  Escalate to Claude                                     ║
║    💰 Cost: $0.30                                             ║
║    ✅ Success rate: 95%                                       ║
╚══════════════════════════════════════════════════════════════╝
```

### **Day 1-2: Build Phi4 Client**

#### **1.1 Create Interface**

```csharp
// File: CodingOrchestrator.Server/Services/IPhi4ThinkingClient.cs

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Client for Phi4 thinking, analysis, and architectural reasoning
/// Uses local Ollama phi4:latest model (FREE!)
/// </summary>
public interface IPhi4ThinkingClient
{
    /// <summary>
    /// Think about a step before execution
    /// Returns guidance, risks, dependencies, and approach
    /// </summary>
    Task<ThinkingResult> ThinkAboutStepAsync(
        PlanStep step, 
        Dictionary<string, FileChange> existingFiles,
        TaskPlan overallPlan,
        string language,
        CancellationToken ct);
    
    /// <summary>
    /// Deep analysis of why we're stuck (used on attempt 5)
    /// Analyzes all previous attempts and identifies root cause
    /// </summary>
    Task<FailureAnalysisResult> AnalyzeFailuresAsync(
        PlanStep step,
        List<GenerationAttempt> previousAttempts,
        List<ValidateCodeResponse> validationResults,
        Dictionary<string, FileChange> existingFiles,
        CancellationToken ct);
    
    /// <summary>
    /// Rethink entire approach to a step (used on attempt 9)
    /// Suggests alternative architecture, file splits, or simplifications
    /// </summary>
    Task<RethinkResult> RethinkArchitectureAsync(
        PlanStep step,
        List<GenerationAttempt> allAttempts,
        TaskPlan overallPlan,
        CancellationToken ct);
    
    /// <summary>
    /// Decide if we should build the project now
    /// Returns true for strategic checkpoints (after models, after services, etc.)
    /// </summary>
    Task<BuildDecision> ShouldBuildNowAsync(
        int currentStepIndex,
        TaskPlan plan,
        Dictionary<string, FileChange> generatedFiles,
        PlanStep justCompleted,
        CancellationToken ct);
    
    /// <summary>
    /// Check if Phi4 is available via Ollama
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct);
}

public record ThinkingResult
{
    public string Guidance { get; init; } = "";
    public List<string> KeyPoints { get; init; } = new();
    public List<string> Risks { get; init; } = new();
    public List<string> Dependencies { get; init; } = new();
    public string SuggestedApproach { get; init; } = "";
    public int EstimatedComplexity { get; init; }  // 1-10
    public string? ExampleCode { get; init; }
}

public record FailureAnalysisResult
{
    public string RootCause { get; init; } = "";
    public string DeepseekMistakePattern { get; init; } = "";
    public string ClaudeMistakePattern { get; init; } = "";
    public string CorrectApproach { get; init; } = "";
    public string? ExampleCode { get; init; }
    public List<string> SuggestedActions { get; init; } = new();
    public bool ShouldSplitFile { get; init; }
    public List<string>? SuggestedFileSplit { get; init; }
}

public record RethinkResult
{
    public string NewApproach { get; init; } = "";
    public bool ShouldSplitIntoMultipleFiles { get; init; }
    public List<string> NewFileStructure { get; init; } = new();
    public string Reasoning { get; init; } = "";
    public bool ShouldSimplifyRequirements { get; init; }
    public string SimplifiedVersion { get; init; } = "";
}

public record BuildDecision
{
    public bool ShouldBuildNow { get; init; }
    public string Reasoning { get; init; } = "";
    public string Checkpoint { get; init; } = "";  // "after_models", "after_services", etc.
}

public record GenerationAttempt
{
    public int AttemptNumber { get; init; }
    public string Model { get; init; } = "";
    public string GeneratedCode { get; init; } = "";
    public ValidateCodeResponse? ValidationResult { get; init; }
    public DateTime Timestamp { get; init; }
}
```

#### **1.2 Implement Phi4 Client**

```csharp
// File: CodingOrchestrator.Server/Services/Phi4ThinkingClient.cs

using System.Text;
using System.Text.Json;

namespace CodingOrchestrator.Server.Services;

public class Phi4ThinkingClient : IPhi4ThinkingClient
{
    private readonly IOllamaClient _ollama;
    private readonly ILogger<Phi4ThinkingClient> _logger;
    private const string Phi4Model = "phi4:latest";

    public Phi4ThinkingClient(
        IOllamaClient ollama,
        ILogger<Phi4ThinkingClient> logger)
    {
        _ollama = ollama;
        _logger = logger;
    }

    public async Task<ThinkingResult> ThinkAboutStepAsync(
        PlanStep step,
        Dictionary<string, FileChange> existingFiles,
        TaskPlan overallPlan,
        string language,
        CancellationToken ct)
    {
        _logger.LogInformation("🧠 Phi4 thinking about: {Description}", step.Description);

        var prompt = BuildThinkingPrompt(step, existingFiles, overallPlan, language);
        
        var response = await _ollama.GenerateAsync(
            model: Phi4Model,
            prompt: prompt,
            systemPrompt: GetSystemPrompt("thinking"),
            maxTokens: 1000,
            temperature: 0.3f,  // Lower temp for focused thinking
            cancellationToken: ct);

        return ParseThinkingResult(response);
    }

    public async Task<FailureAnalysisResult> AnalyzeFailuresAsync(
        PlanStep step,
        List<GenerationAttempt> previousAttempts,
        List<ValidateCodeResponse> validationResults,
        Dictionary<string, FileChange> existingFiles,
        CancellationToken ct)
    {
        _logger.LogInformation("🔍 Phi4 analyzing {Count} failed attempts for: {Description}", 
            previousAttempts.Count, step.Description);

        var prompt = BuildAnalysisPrompt(step, previousAttempts, validationResults, existingFiles);
        
        var response = await _ollama.GenerateAsync(
            model: Phi4Model,
            prompt: prompt,
            systemPrompt: GetSystemPrompt("analysis"),
            maxTokens: 2000,
            temperature: 0.2f,  // Very focused for analysis
            cancellationToken: ct);

        return ParseAnalysisResult(response);
    }

    public async Task<RethinkResult> RethinkArchitectureAsync(
        PlanStep step,
        List<GenerationAttempt> allAttempts,
        TaskPlan overallPlan,
        CancellationToken ct)
    {
        _logger.LogInformation("🏗️ Phi4 rethinking architecture for: {Description}", step.Description);

        var prompt = BuildRethinkPrompt(step, allAttempts, overallPlan);
        
        var response = await _ollama.GenerateAsync(
            model: Phi4Model,
            prompt: prompt,
            systemPrompt: GetSystemPrompt("rethink"),
            maxTokens: 1500,
            temperature: 0.5f,  // Higher temp for creative solutions
            cancellationToken: ct);

        return ParseRethinkResult(response);
    }

    public async Task<BuildDecision> ShouldBuildNowAsync(
        int currentStepIndex,
        TaskPlan plan,
        Dictionary<string, FileChange> generatedFiles,
        PlanStep justCompleted,
        CancellationToken ct)
    {
        _logger.LogDebug("🏗️ Phi4 deciding if we should build now (step {Step}/{Total})", 
            currentStepIndex + 1, plan.Steps.Count);

        var prompt = BuildBuildDecisionPrompt(currentStepIndex, plan, generatedFiles, justCompleted);
        
        var response = await _ollama.GenerateAsync(
            model: Phi4Model,
            prompt: prompt,
            systemPrompt: GetSystemPrompt("build_decision"),
            maxTokens: 500,
            temperature: 0.1f,  // Very deterministic
            cancellationToken: ct);

        return ParseBuildDecision(response);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            // Simple test prompt
            var response = await _ollama.GenerateAsync(
                model: Phi4Model,
                prompt: "Test",
                systemPrompt: null,
                maxTokens: 10,
                temperature: 0.1f,
                cancellationToken: ct);
            
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Phi4 not available via Ollama");
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PROMPT BUILDERS
    // ═══════════════════════════════════════════════════════════════

    private string BuildThinkingPrompt(
        PlanStep step, 
        Dictionary<string, FileChange> existingFiles,
        TaskPlan overallPlan,
        string language)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"You are thinking about the next step in a {language} project generation task.");
        sb.AppendLine();
        sb.AppendLine($"OVERALL TASK: {overallPlan.TaskDescription}");
        sb.AppendLine();
        sb.AppendLine($"CURRENT STEP: {step.Description}");
        sb.AppendLine($"TARGET FILE: {step.FileName ?? "multiple files"}");
        sb.AppendLine();
        
        if (existingFiles.Any())
        {
            sb.AppendLine($"EXISTING FILES ({existingFiles.Count}):");
            foreach (var file in existingFiles.Keys.Take(10))
            {
                sb.AppendLine($"  - {file}");
            }
            if (existingFiles.Count > 10)
                sb.AppendLine($"  ... and {existingFiles.Count - 10} more");
            sb.AppendLine();
        }
        
        sb.AppendLine("THINK DEEPLY:");
        sb.AppendLine("1. What is the PURPOSE of this step?");
        sb.AppendLine("2. What are the KEY POINTS to consider?");
        sb.AppendLine("3. What RISKS or CHALLENGES might arise?");
        sb.AppendLine("4. What DEPENDENCIES does this step have?");
        sb.AppendLine("5. What is the BEST APPROACH?");
        sb.AppendLine("6. How COMPLEX is this step (1-10)?");
        sb.AppendLine();
        sb.AppendLine("Respond in JSON format:");
        sb.AppendLine(@"{
  ""guidance"": ""Brief guidance for the code generator"",
  ""keyPoints"": [""Point 1"", ""Point 2""],
  ""risks"": [""Risk 1"", ""Risk 2""],
  ""dependencies"": [""File1.cs"", ""Package.Name""],
  ""suggestedApproach"": ""Detailed approach"",
  ""estimatedComplexity"": 5,
  ""exampleCode"": ""Optional: Short example pattern""
}");

        return sb.ToString();
    }

    private string BuildAnalysisPrompt(
        PlanStep step,
        List<GenerationAttempt> previousAttempts,
        List<ValidateCodeResponse> validationResults,
        Dictionary<string, FileChange> existingFiles)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are a SENIOR ARCHITECT analyzing why code generation is failing.");
        sb.AppendLine();
        sb.AppendLine($"TASK: {step.Description}");
        sb.AppendLine($"TARGET FILE: {step.FileName}");
        sb.AppendLine($"ATTEMPTS SO FAR: {previousAttempts.Count}");
        sb.AppendLine();
        
        sb.AppendLine("ATTEMPT HISTORY:");
        for (int i = 0; i < previousAttempts.Count; i++)
        {
            var attempt = previousAttempts[i];
            var validation = i < validationResults.Count ? validationResults[i] : null;
            
            sb.AppendLine($"\n--- Attempt {attempt.AttemptNumber} ({attempt.Model}) ---");
            sb.AppendLine($"Score: {validation?.Score ?? 0}/10");
            sb.AppendLine($"Issues: {validation?.Summary ?? "N/A"}");
            
            // Include a snippet of the generated code
            var codeSnippet = attempt.GeneratedCode.Length > 500 
                ? attempt.GeneratedCode[..500] + "..." 
                : attempt.GeneratedCode;
            sb.AppendLine($"Code Snippet:\n{codeSnippet}");
        }
        
        sb.AppendLine();
        sb.AppendLine("DEEP ANALYSIS REQUIRED:");
        sb.AppendLine("1. What is the ROOT CAUSE of failures?");
        sb.AppendLine("2. What pattern mistake is Deepseek making?");
        sb.AppendLine("3. What pattern mistake is Claude making?");
        sb.AppendLine("4. What is the CORRECT approach?");
        sb.AppendLine("5. Should we split this file into multiple files?");
        sb.AppendLine("6. What specific actions should we take next?");
        sb.AppendLine();
        sb.AppendLine("Respond in JSON format:");
        sb.AppendLine(@"{
  ""rootCause"": ""The fundamental issue"",
  ""deepseekMistakePattern"": ""What deepseek keeps doing wrong"",
  ""claudeMistakePattern"": ""What Claude is missing"",
  ""correctApproach"": ""The right way to solve this"",
  ""exampleCode"": ""Short example of correct pattern"",
  ""suggestedActions"": [""Action 1"", ""Action 2""],
  ""shouldSplitFile"": false,
  ""suggestedFileSplit"": null
}");

        return sb.ToString();
    }

    private string BuildRethinkPrompt(
        PlanStep step,
        List<GenerationAttempt> allAttempts,
        TaskPlan overallPlan)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("CRITICAL SITUATION: 8 attempts have failed. Time to RETHINK THE ENTIRE APPROACH.");
        sb.AppendLine();
        sb.AppendLine($"TASK: {step.Description}");
        sb.AppendLine($"OVERALL PROJECT: {overallPlan.TaskDescription}");
        sb.AppendLine($"ATTEMPTS FAILED: {allAttempts.Count}");
        sb.AppendLine();
        
        sb.AppendLine("FUNDAMENTAL QUESTIONS:");
        sb.AppendLine("1. Is this step TOO COMPLEX for a single file?");
        sb.AppendLine("2. Should we change the architectural approach?");
        sb.AppendLine("3. Should we simplify the requirements?");
        sb.AppendLine("4. Should we split into multiple smaller files?");
        sb.AppendLine("5. Is there a different design pattern we should use?");
        sb.AppendLine();
        
        sb.AppendLine("PREVIOUS APPROACHES TRIED:");
        foreach (var attempt in allAttempts.Take(5))
        {
            sb.AppendLine($"- Attempt {attempt.AttemptNumber} ({attempt.Model}): Score {attempt.ValidationResult?.Score ?? 0}/10");
        }
        sb.AppendLine();
        
        sb.AppendLine("Respond in JSON format with a NEW architectural approach:");
        sb.AppendLine(@"{
  ""newApproach"": ""Completely different way to solve this"",
  ""shouldSplitIntoMultipleFiles"": false,
  ""newFileStructure"": [""File1.cs"", ""File2.cs""],
  ""reasoning"": ""Why this new approach will work"",
  ""shouldSimplifyRequirements"": false,
  ""simplifiedVersion"": ""Simplified version of the task""
}");

        return sb.ToString();
    }

    private string BuildBuildDecisionPrompt(
        int currentStepIndex,
        TaskPlan plan,
        Dictionary<string, FileChange> generatedFiles,
        PlanStep justCompleted)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("DECISION: Should we build/compile the project NOW?");
        sb.AppendLine();
        sb.AppendLine($"PROGRESS: Step {currentStepIndex + 1} of {plan.Steps.Count} ({(currentStepIndex + 1) * 100 / plan.Steps.Count}%)");
        sb.AppendLine($"JUST COMPLETED: {justCompleted.Description}");
        sb.AppendLine($"FILES GENERATED: {generatedFiles.Count}");
        sb.AppendLine();
        
        sb.AppendLine("STRATEGIC CHECKPOINTS:");
        sb.AppendLine("- After all Models/*.cs files (data layer complete)");
        sb.AppendLine("- After all Services/*.cs files (business layer complete)");
        sb.AppendLine("- After all Controllers/*.cs or Components/*.razor files");
        sb.AppendLine("- Every 5 files (periodic validation)");
        sb.AppendLine("- Before complex dependent files");
        sb.AppendLine("- Final file (complete validation)");
        sb.AppendLine();
        
        sb.AppendLine("REMAINING STEPS:");
        for (int i = currentStepIndex + 1; i < Math.Min(plan.Steps.Count, currentStepIndex + 4); i++)
        {
            sb.AppendLine($"  {i + 1}. {plan.Steps[i].Description}");
        }
        sb.AppendLine();
        
        sb.AppendLine("Respond in JSON format:");
        sb.AppendLine(@"{
  ""shouldBuildNow"": true,
  ""reasoning"": ""Why or why not"",
  ""checkpoint"": ""after_models""
}");

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════
    // RESPONSE PARSERS
    // ═══════════════════════════════════════════════════════════════

    private ThinkingResult ParseThinkingResult(string response)
    {
        try
        {
            // Try to extract JSON from markdown code blocks if present
            var json = ExtractJson(response);
            var parsed = JsonSerializer.Deserialize<ThinkingResult>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            return parsed ?? new ThinkingResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Phi4 thinking result, using fallback");
            return new ThinkingResult
            {
                Guidance = "Focus on clean, maintainable code",
                KeyPoints = new() { "Follow best practices", "Handle errors" },
                Risks = new() { "Unknown" },
                Dependencies = new(),
                SuggestedApproach = "Generate standard implementation",
                EstimatedComplexity = 5
            };
        }
    }

    private FailureAnalysisResult ParseAnalysisResult(string response)
    {
        try
        {
            var json = ExtractJson(response);
            var parsed = JsonSerializer.Deserialize<FailureAnalysisResult>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            return parsed ?? new FailureAnalysisResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Phi4 analysis result, using fallback");
            return new FailureAnalysisResult
            {
                RootCause = "Unable to determine root cause",
                DeepseekMistakePattern = "Pattern unclear",
                ClaudeMistakePattern = "Pattern unclear",
                CorrectApproach = "Try a different approach",
                SuggestedActions = new() { "Review requirements", "Simplify implementation" }
            };
        }
    }

    private RethinkResult ParseRethinkResult(string response)
    {
        try
        {
            var json = ExtractJson(response);
            var parsed = JsonSerializer.Deserialize<RethinkResult>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            return parsed ?? new RethinkResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Phi4 rethink result, using fallback");
            return new RethinkResult
            {
                NewApproach = "Simplify and retry",
                Reasoning = "Previous approaches failed",
                ShouldSimplifyRequirements = true,
                SimplifiedVersion = "Start with basic implementation"
            };
        }
    }

    private BuildDecision ParseBuildDecision(string response)
    {
        try
        {
            var json = ExtractJson(response);
            var parsed = JsonSerializer.Deserialize<BuildDecision>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            return parsed ?? new BuildDecision { ShouldBuildNow = false };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse build decision, defaulting to false");
            return new BuildDecision 
            { 
                ShouldBuildNow = false,
                Reasoning = "Parse error, deferring build"
            };
        }
    }

    private string ExtractJson(string response)
    {
        // Try to extract JSON from markdown code blocks
        var jsonMatch = System.Text.RegularExpressions.Regex.Match(
            response, 
            @"```(?:json)?\s*(\{.*?\})\s*```", 
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        if (jsonMatch.Success)
            return jsonMatch.Groups[1].Value;
        
        // Try to find JSON without code blocks
        var startIdx = response.IndexOf('{');
        var endIdx = response.LastIndexOf('}');
        
        if (startIdx >= 0 && endIdx > startIdx)
            return response.Substring(startIdx, endIdx - startIdx + 1);
        
        return response;
    }

    private string GetSystemPrompt(string mode)
    {
        return mode switch
        {
            "thinking" => @"You are Phi4, a reasoning AI focused on strategic thinking about code generation tasks.
Provide concise, structured analysis in JSON format. Focus on:
- Key insights and approaches
- Potential risks and dependencies
- Complexity estimation
Be specific and actionable.",

            "analysis" => @"You are Phi4, a senior software architect performing root cause analysis.
Analyze failed generation attempts to identify WHY they failed and WHAT to do differently.
Provide deep insights in JSON format. Be direct and specific about mistakes and solutions.",

            "rethink" => @"You are Phi4, a creative problem-solver for difficult code generation challenges.
When standard approaches fail, suggest alternative architectures or simplifications.
Think outside the box but remain practical. Respond in JSON format.",

            "build_decision" => @"You are Phi4, making strategic decisions about when to compile and validate.
Build at logical checkpoints: after data layer, business layer, presentation layer, or every N files.
Respond in JSON format with clear reasoning.",

            _ => "You are Phi4, a helpful coding assistant."
        };
    }
}
```

#### **1.3 Register in DI**

```csharp
// File: CodingOrchestrator.Server/Program.cs

// Add to ConfigureServices:
builder.Services.AddSingleton<IPhi4ThinkingClient, Phi4ThinkingClient>();
```

### **Day 3: Integrate Phi4 Thinking into TaskOrchestrator**

```csharp
// File: CodingOrchestrator.Server/Services/TaskOrchestrator.cs

// Add constructor injection:
private readonly IPhi4ThinkingClient _phi4;

public TaskOrchestrator(
    // ... existing params ...
    IPhi4ThinkingClient phi4,
    ILogger<TaskOrchestrator> logger)
{
    // ... existing assignments ...
    _phi4 = phi4;
}

// In the step-by-step loop, BEFORE calling CodingAgent:
// Line ~1760 (in the retry while loop)

// 🧠 PHI4 THINKING: Think about this step
ThinkingResult? thinking = null;
if (stepRetries == 1)  // Only think on first attempt per step
{
    try
    {
        thinking = await _phi4.ThinkAboutStepAsync(
            step, 
            accumulatedFiles, 
            taskPlan, 
            request.Language ?? "csharp",
            cancellationToken);
        
        _logger.LogInformation("🧠 [PHI4] Complexity: {Complexity}/10, Key points: {Points}", 
            thinking.EstimatedComplexity, 
            string.Join(", ", thinking.KeyPoints.Take(3)));
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Phi4 thinking failed (non-critical)");
    }
}

// Include thinking guidance in generation request:
var generateRequest = new GenerateCodeRequest
{
    Task = step.Description,
    Language = request.Language ?? "csharp",
    Context = request.Context,
    
    // 🧠 Add Phi4 guidance to the prompt!
    AdditionalGuidance = thinking != null 
        ? $"\n\n🧠 STRATEGIC GUIDANCE (from Phi4):\n{thinking.Guidance}\n\n" +
          $"KEY POINTS:\n{string.Join("\n", thinking.KeyPoints.Select(k => $"- {k}"))}\n\n" +
          $"RISKS TO AVOID:\n{string.Join("\n", thinking.Risks.Select(r => $"- {r}"))}\n\n" +
          $"SUGGESTED APPROACH:\n{thinking.SuggestedApproach}\n\n" +
          (thinking.ExampleCode != null ? $"EXAMPLE PATTERN:\n{thinking.ExampleCode}\n\n" : "")
        : null,
    
    ExistingFiles = existingFilesForAgent,
    PreviousFeedback = stepRetries > 1 ? feedback : null,
    ModelHint = DetermineModelForAttempt(stepRetries)  // NEW!
};
```

### **Day 4: Smart Escalation Strategy (COST-OPTIMIZED)**

**Key Principle:** Deepseek first, Claude only when needed!

```csharp
// File: CodingOrchestrator.Server/Services/EscalationStrategy.cs

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Determines which AI model to use based on attempt number
/// COST-OPTIMIZED: Prefers free Deepseek, escalates to Claude only on failures
/// </summary>
public static class EscalationStrategy
{
    public static ModelSelection SelectModelForAttempt(
        int attemptNumber,
        List<string> triedModels,
        decimal currentCost,
        decimal maxCost)
    {
        return attemptNumber switch
        {
            // Attempts 1-3: ALWAYS Deepseek (free, fast, 70% success rate)
            <= 3 => new ModelSelection
            {
                Model = "deepseek-v2:16b",
                Provider = "ollama",
                IsAnalysis = false,
                Reasoning = "Free Deepseek - attempting before any paid models",
                Temperature = 0.7f,
                Cost = 0.0m
            },
            
            // Attempt 4: Claude ONLY if we have failures (escalation, not default!)
            4 => currentCost < maxCost 
                ? new ModelSelection
                {
                    Model = "claude-sonnet-4-20250514",
                    Provider = "anthropic",
                    IsAnalysis = false,
                    Reasoning = "⚠️ Escalating to Claude after 3 deepseek failures",
                    Temperature = 0.7f,
                    Cost = 0.30m
                }
                : new ModelSelection
                {
                    Model = "deepseek-v2:16b",
                    Provider = "ollama",
                    IsAnalysis = false,
                    Reasoning = "Budget limit reached, continuing with free deepseek",
                    Temperature = 0.7f,
                    Cost = 0.0m
                },
            
            // Attempt 5: Phi4 Analysis (FREE! Deep thinking)
            5 => new ModelSelection
            {
                Model = "phi4:latest",
                Provider = "ollama",
                IsAnalysis = true,  // ⚠️ This is ANALYSIS, not generation!
                Reasoning = "FREE Phi4 deep analysis of root cause",
                Temperature = 0.2f,
                Cost = 0.0m
            },
            
            // Attempts 6-7: Deepseek with insights from Phi4 analysis (STILL FREE!)
            6 or 7 => new ModelSelection
            {
                Model = "deepseek-v2:16b",
                Provider = "ollama",
                IsAnalysis = false,
                Reasoning = "FREE Deepseek retry with Phi4's insights",
                Temperature = 0.6f,
                Cost = 0.0m
            },
            
            // Attempt 8: Premium Claude (heavy artillery)
            8 => currentCost < maxCost * 0.9m  // Save 10% for emergencies
                ? new ModelSelection
                {
                    Model = "claude-opus-4-20250514",  // Premium model
                    Provider = "anthropic",
                    IsAnalysis = false,
                    Reasoning = "Premium model for difficult case",
                    Temperature = 0.7f
                }
                : new ModelSelection
                {
                    Model = "claude-sonnet-4-20250514",  // Fall back to standard Claude
                    Provider = "anthropic",
                    IsAnalysis = false,
                    Reasoning = "Using standard Claude (cost limit near)",
                    Temperature = 0.7f
                },
            
            // Attempt 9: Phi4 Rethink (FREE! Architectural rethinking)
            9 => new ModelSelection
            {
                Model = "phi4:latest",
                Provider = "ollama",
                IsAnalysis = true,  // ⚠️ This is RETHINKING, not generation!
                Reasoning = "Rethinking entire approach",
                Temperature = 0.5f
            },
            
            // Attempt 10: Final combined attempt (pick best performer)
            10 => SelectBestPerformer(triedModels, currentCost, maxCost),
            
            _ => new ModelSelection
            {
                Model = "deepseek-v2:16b",
                Provider = "ollama",
                IsAnalysis = false,
                Reasoning = "Default model",
                Temperature = 0.7f
            }
        };
    }

    private static ModelSelection SelectBestPerformer(
        List<string> triedModels,
        decimal currentCost,
        decimal maxCost)
    {
        // If Claude was tried and we have budget, use Claude
        if (triedModels.Contains("claude-sonnet-4-20250514") && currentCost < maxCost)
        {
            return new ModelSelection
            {
                Model = "claude-sonnet-4-20250514",
                Provider = "anthropic",
                IsAnalysis = false,
                Reasoning = "Final attempt with best performer (Claude)",
                Temperature = 0.6f
            };
        }
        
        // Otherwise, use deepseek
        return new ModelSelection
        {
            Model = "deepseek-v2:16b",
            Provider = "ollama",
            IsAnalysis = false,
            Reasoning = "Final attempt with deepseek",
            Temperature = 0.6f
        };
    }
}

public record ModelSelection
{
    public string Model { get; init; } = "";
    public string Provider { get; init; } = "";  // "ollama" or "anthropic"
    public bool IsAnalysis { get; init; }  // If true, use for analysis not generation
    public string Reasoning { get; init; } = "";
    public float Temperature { get; init; }
    public decimal Cost { get; init; }  // Estimated cost per call
}

/// <summary>
/// Cost tracking summary
/// </summary>
public record CostSummary
{
    public int DeepseekCalls { get; init; }
    public int ClaudeCalls { get; init; }
    public int Phi4Calls { get; init; }
    public decimal TotalCost { get; init; }
    public decimal BudgetRemaining { get; init; }
    
    public string Summary => 
        $"💰 Cost: ${TotalCost:F2} | Deepseek: {DeepseekCalls} (free), Claude: {ClaudeCalls} (paid), Phi4: {Phi4Calls} (free)";
}
```

#### **Update TaskOrchestrator Retry Loop**

```csharp
// In TaskOrchestrator.cs, in the step retry while loop:

// Track all attempts for this step (for analysis)
var stepAttempts = new List<GenerationAttempt>();

while (!stepSuccess && stepRetries < maxRetriesPerStep && totalIterations < effectiveMaxIterations)
{
    totalIterations++;
    stepRetries++;
    cancellationToken.ThrowIfCancellationRequested();
    
    // ═══════════════════════════════════════════════════════════════
    // 🎯 SMART MODEL SELECTION (Escalation Strategy)
    // ═══════════════════════════════════════════════════════════════
    
    var modelSelection = EscalationStrategy.SelectModelForAttempt(
        stepRetries,
        triedModels.ToList(),
        cloudUsage.TotalCost,
        request.MaxTotalCost ?? 10.0m);
    
    _logger.LogInformation("🎯 [ESCALATION] Attempt {Attempt}/10: Using {Model} ({Reasoning})",
        stepRetries, modelSelection.Model, modelSelection.Reasoning);
    
    // ═══════════════════════════════════════════════════════════════
    // 🔍 SPECIAL HANDLING FOR ANALYSIS ATTEMPTS (5 and 9)
    // ═══════════════════════════════════════════════════════════════
    
    if (modelSelection.IsAnalysis)
    {
        if (stepRetries == 5)
        {
            // 🧠 PHI4 DEEP ANALYSIS
            try
            {
                var analysis = await _phi4.AnalyzeFailuresAsync(
                    step,
                    stepAttempts,
                    stepAttempts.Select(a => a.ValidationResult!).ToList(),
                    accumulatedFiles,
                    cancellationToken);
                
                _logger.LogInformation("🔍 [PHI4-ANALYSIS] Root cause: {Cause}", analysis.RootCause);
                _logger.LogInformation("🔍 [PHI4-ANALYSIS] Correct approach: {Approach}", analysis.CorrectApproach);
                
                // Store analysis results to guide next attempts
                if (feedback == null) feedback = new CodeFeedback();
                feedback.SpecificIssues.Add($"PHI4 ANALYSIS: {analysis.RootCause}");
                feedback.SpecificIssues.Add($"CORRECT APPROACH: {analysis.CorrectApproach}");
                if (analysis.ExampleCode != null)
                {
                    feedback.SpecificIssues.Add($"EXAMPLE PATTERN:\n{analysis.ExampleCode}");
                }
                
                // If Phi4 suggests splitting files, update the plan
                if (analysis.ShouldSplitFile && analysis.SuggestedFileSplit?.Any() == true)
                {
                    _logger.LogWarning("🔍 [PHI4-ANALYSIS] Suggests splitting into: {Files}", 
                        string.Join(", ", analysis.SuggestedFileSplit));
                    // TODO: Implement file split logic
                }
                
                continue; // Don't generate on analysis attempt, move to next attempt
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phi4 analysis failed, continuing");
                continue;
            }
        }
        else if (stepRetries == 9)
        {
            // 🏗️ PHI4 ARCHITECTURAL RETHINK
            try
            {
                var rethink = await _phi4.RethinkArchitectureAsync(
                    step,
                    stepAttempts,
                    taskPlan,
                    cancellationToken);
                
                _logger.LogInformation("🏗️ [PHI4-RETHINK] New approach: {Approach}", rethink.NewApproach);
                
                // Update the task description with new approach
                step = step with { Description = rethink.NewApproach };
                
                if (feedback == null) feedback = new CodeFeedback();
                feedback.SpecificIssues.Add($"PHI4 RETHINK: {rethink.NewApproach}");
                feedback.SpecificIssues.Add($"REASONING: {rethink.Reasoning}");
                
                continue; // Move to attempt 10 with new approach
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phi4 rethink failed, continuing");
                continue;
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════
    // 💻 NORMAL CODE GENERATION ATTEMPT
    // ═══════════════════════════════════════════════════════════════
    
    // ... rest of existing generation code ...
    
    lastGeneratedCode = await _codingAgent.GenerateAsync(generateRequest, cancellationToken);
    
    // Track this attempt
    stepAttempts.Add(new GenerationAttempt
    {
        AttemptNumber = stepRetries,
        Model = lastGeneratedCode.ModelUsed,
        GeneratedCode = string.Join("\n", lastGeneratedCode.Files.Select(f => f.Content)),
        ValidationResult = null,  // Will be filled after validation
        Timestamp = DateTime.UtcNow
    });
    
    // ... validation code ...
    
    // Update attempt with validation result
    if (stepAttempts.Any())
    {
        stepAttempts[^1] = stepAttempts[^1] with { ValidationResult = lastValidation };
    }
}
```

### **Day 5: Testing & Integration**

**Test Plan:**
1. Generate simple Console app → Should use deepseek only
2. Generate complex Blazor app → Should escalate to Claude on difficult files
3. Intentionally break a step → Should see Phi4 analysis on attempt 5
4. Force 9 failures → Should see Phi4 rethink on attempt 9

```bash
# Test 1: Simple app (should be cheap)
curl -X POST http://localhost:5003/api/orchestrator/execute \
  -H "Content-Type: application/json" \
  -d '{
    "task": "Create a calculator console app",
    "language": "csharp",
    "maxIterations": 10,
    "minValidationScore": 8
  }'

# Expected: 100% deepseek, $0 cost

# Test 2: Complex app (should use escalation)
curl -X POST http://localhost:5003/api/orchestrator/execute \
  -H "Content-Type: application/json" \
  -d '{
    "task": "Create a Blazor WebAssembly todo app with offline support",
    "language": "csharp",
    "maxIterations": 50
  }'

# Expected: Mix of deepseek (70%) and Claude (30%), Phi4 analysis if stuck
```

---

## 📋 **Phase 2: Advanced Retry & Resilience (Week 2)**

### **Day 6-7: Stub Generation**

```csharp
// File: CodingOrchestrator.Server/Services/StubGenerator.cs

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Generates compilable stubs when all generation attempts fail
/// </summary>
public interface IStubGenerator
{
    Task<string> GenerateStubAsync(
        PlanStep step, 
        string language,
        List<GenerationAttempt> failedAttempts,
        FailureAnalysisResult? analysis);
}

public class StubGenerator : IStubGenerator
{
    private readonly ILogger<StubGenerator> _logger;

    public StubGenerator(ILogger<StubGenerator> logger)
    {
        _logger = logger;
    }

    public Task<string> GenerateStubAsync(
        PlanStep step,
        string language,
        List<GenerationAttempt> failedAttempts,
        FailureAnalysisResult? analysis)
    {
        _logger.LogWarning("Generating stub for failed step: {Description}", step.Description);

        return language.ToLowerInvariant() switch
        {
            "csharp" or "c#" => Task.FromResult(GenerateCSharpStub(step, failedAttempts, analysis)),
            "python" => Task.FromResult(GeneratePythonStub(step, failedAttempts, analysis)),
            _ => Task.FromResult(GenerateGenericStub(step, failedAttempts, analysis))
        };
    }

    private string GenerateCSharpStub(
        PlanStep step,
        List<GenerationAttempt> failedAttempts,
        FailureAnalysisResult? analysis)
    {
        var className = Path.GetFileNameWithoutExtension(step.FileName ?? "GeneratedClass");
        var namespaceName = "GeneratedApp";  // TODO: Detect from plan

        var stub = $@"namespace {namespaceName};

/// <summary>
/// ⚠️ STUB: This file failed generation after {failedAttempts.Count} attempts
/// </summary>
/// <remarks>
/// TASK: {step.Description}
/// 
/// FAILURE REASON: {analysis?.RootCause ?? "Unknown"}
/// 
/// ATTEMPTS MADE:
{string.Join("\n", failedAttempts.Select(a => $"///   - Attempt {a.AttemptNumber} ({a.Model}): Score {a.ValidationResult?.Score ?? 0}/10"))}
/// 
/// SUGGESTED APPROACH: {analysis?.CorrectApproach ?? "Manual implementation needed"}
/// 
/// TODO: Implement this class manually or regenerate with more specific guidance
/// </remarks>
public class {className}
{{
    // TODO: Add required dependencies
    // {(analysis?.Dependencies != null && analysis.Dependencies.Any() 
        ? $"Suggested: {string.Join(", ", analysis.Dependencies)}" 
        : "Review requirements")}
    
    public {className}()
    {{
        // TODO: Implement constructor
        throw new NotImplementedException(""This is a generated stub - needs manual implementation"");
    }}
    
    // TODO: Add required methods based on task description:
    // {step.Description}
}}";

        return stub;
    }

    private string GeneratePythonStub(
        PlanStep step,
        List<GenerationAttempt> failedAttempts,
        FailureAnalysisResult? analysis)
    {
        // Similar to C# but Python syntax
        return $@"""
⚠️ STUB: This file failed generation after {failedAttempts.Count} attempts

TASK: {step.Description}
FAILURE REASON: {analysis?.RootCause ?? "Unknown"}

TODO: Implement this module manually
""

class GeneratedClass:
    def __init__(self):
        raise NotImplementedError(""This is a generated stub"")
";
    }

    private string GenerateGenericStub(
        PlanStep step,
        List<GenerationAttempt> failedAttempts,
        FailureAnalysisResult? analysis)
    {
        return $@"// STUB: Failed generation
// Task: {step.Description}
// Attempts: {failedAttempts.Count}
// TODO: Implement manually
";
    }
}
```

#### **Integrate Stub Generation into TaskOrchestrator**

```csharp
// In TaskOrchestrator.cs, after the step retry while loop:

if (!stepSuccess)
{
    _logger.LogWarning("[STEP-FAILED] Step {StepNum} failed after {Retries} attempts - generating stub", 
        stepNumber, maxRetriesPerStep);
    
    // 🔧 GENERATE STUB instead of stopping
    try
    {
        var stub = await _stubGenerator.GenerateStubAsync(
            step,
            request.Language ?? "csharp",
            stepAttempts,
            stepAttempts.Count >= 5 ? lastPhi4Analysis : null);
        
        // Add stub to accumulated files
        accumulatedFiles[step.FileName ?? $"Stub{stepNumber}.cs"] = new FileChange
        {
            Path = step.FileName ?? $"Stub{stepNumber}.cs",
            Content = stub,
            ChangeType = "add"
        };
        
        // ✅ MARK IN MEMORYAGENT TODO
        try
        {
            await _memoryAgent.AddTodoAsync(new CreateTodoRequest
            {
                Context = request.Context,
                Title = $"⚠️ NEEDS_HUMAN_REVIEW: {step.FileName}",
                Description = $"Failed after {maxRetriesPerStep} attempts. Stub generated. Review failure report.",
                Priority = "high",
                Tags = new() { "stub", "needs_review", "generation_failure" }
            }, cancellationToken);
        }
        catch (Exception todoEx)
        {
            _logger.LogWarning(todoEx, "Failed to create TODO (non-critical)");
        }
        
        // Track for failure report
        failedSteps.Add(new FailedStep
        {
            StepNumber = stepNumber,
            Description = step.Description,
            FileName = step.FileName,
            Attempts = stepAttempts,
            StubGenerated = true
        });
        
        // ✅ CONTINUE with next step!
        _logger.LogInformation("[CONTINUE] Moving to next step despite failure");
        continue;
    }
    catch (Exception stubEx)
    {
        _logger.LogError(stubEx, "Failed to generate stub");
        // Now we really need to stop
        break;
    }
}
```

### **Day 8-9: Failure Report Generation**

```csharp
// File: CodingOrchestrator.Server/Services/FailureReportGenerator.cs

namespace CodingOrchestrator.Server.Services;

public interface IFailureReportGenerator
{
    Task<string> GenerateReportAsync(
        FailedStep failedStep,
        TaskPlan overallPlan,
        decimal totalCost);
    
    Task SaveReportAsync(string report, string fileName, string workspacePath);
}

public class FailureReportGenerator : IFailureReportGenerator
{
    private readonly ILogger<FailureReportGenerator> _logger;

    public FailureReportGenerator(ILogger<FailureReportGenerator> logger)
    {
        _logger = logger;
    }

    public Task<string> GenerateReportAsync(
        FailedStep failedStep,
        TaskPlan overallPlan,
        decimal totalCost)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# Failure Report: {failedStep.FileName}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Step:** {failedStep.StepNumber} of {overallPlan.Steps.Count}");
        sb.AppendLine($"**Description:** {failedStep.Description}");
        sb.AppendLine($"**Total Attempts:** {failedStep.Attempts.Count}");
        sb.AppendLine($"**Highest Score:** {failedStep.Attempts.Max(a => a.ValidationResult?.Score ?? 0)}/10");
        sb.AppendLine($"**Status:** ⚠️ NEEDS HUMAN REVIEW");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // Attempt history
        sb.AppendLine("## Attempt History");
        sb.AppendLine();
        
        foreach (var attempt in failedStep.Attempts)
        {
            sb.AppendLine($"### Attempt {attempt.AttemptNumber}: {attempt.Model}");
            sb.AppendLine();
            sb.AppendLine($"- **Score:** {attempt.ValidationResult?.Score ?? 0}/10");
            sb.AppendLine($"- **Passed:** {(attempt.ValidationResult?.Passed == true ? "✅" : "❌")}");
            sb.AppendLine($"- **Timestamp:** {attempt.Timestamp:HH:mm:ss}");
            sb.AppendLine();
            
            if (attempt.ValidationResult != null)
            {
                sb.AppendLine($"**Issues:**");
                sb.AppendLine($"```");
                sb.AppendLine(attempt.ValidationResult.Summary);
                sb.AppendLine($"```");
                sb.AppendLine();
            }
            
            // Include code snippet
            var codeSnippet = attempt.GeneratedCode.Length > 800 
                ? attempt.GeneratedCode[..800] + "\n... (truncated)" 
                : attempt.GeneratedCode;
            sb.AppendLine($"**Code Generated:**");
            sb.AppendLine($"```csharp");
            sb.AppendLine(codeSnippet);
            sb.AppendLine($"```");
            sb.AppendLine();
        }
        
        // Root cause analysis (if available from Phi4)
        var phi4Analysis = failedStep.Attempts
            .FirstOrDefault(a => a.Model == "phi4:latest" && a.AttemptNumber == 5);
        
        if (phi4Analysis != null)
        {
            sb.AppendLine("## Root Cause Analysis (Phi4)");
            sb.AppendLine();
            sb.AppendLine($"**Root Cause:** {phi4Analysis.ValidationResult?.Summary ?? "N/A"}");
            sb.AppendLine();
        }
        
        // Recommendations
        sb.AppendLine("## Recommended Next Steps");
        sb.AppendLine();
        sb.AppendLine("### Option 1: Manual Implementation");
        sb.AppendLine($"Implement `{failedStep.FileName}` manually based on the task description.");
        sb.AppendLine();
        sb.AppendLine("### Option 2: Simplify Requirements");
        sb.AppendLine("Break down the requirements into smaller, simpler tasks and regenerate.");
        sb.AppendLine();
        sb.AppendLine("### Option 3: Provide Example");
        sb.AppendLine("Provide a working example of similar code and retry generation with context.");
        sb.AppendLine();
        
        // Cost breakdown
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Cost Breakdown");
        sb.AppendLine();
        sb.AppendLine($"- **Deepseek attempts:** {failedStep.Attempts.Count(a => a.Model.Contains("deepseek"))} (Cost: $0)");
        sb.AppendLine($"- **Claude attempts:** {failedStep.Attempts.Count(a => a.Model.Contains("claude"))} (Cost: ~${totalCost:F2})");
        sb.AppendLine($"- **Phi4 analysis:** {failedStep.Attempts.Count(a => a.Model.Contains("phi4"))} (Cost: $0)");
        sb.AppendLine();
        
        return Task.FromResult(sb.ToString());
    }

    public async Task SaveReportAsync(string report, string fileName, string workspacePath)
    {
        try
        {
            var reportPath = Path.Combine(workspacePath, $"{fileName}_failure_report.md");
            await File.WriteAllTextAsync(reportPath, report);
            _logger.LogInformation("Saved failure report to: {Path}", reportPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save failure report");
        }
    }
}

public record FailedStep
{
    public int StepNumber { get; init; }
    public string Description { get; init; } = "";
    public string? FileName { get; init; }
    public List<GenerationAttempt> Attempts { get; init; } = new();
    public bool StubGenerated { get; init; }
}
```

#### **Generate Reports at End of Task**

```csharp
// In TaskOrchestrator.cs, at the end of ExecuteTaskAsync:

// Generate failure reports for any failed steps
if (failedSteps.Any())
{
    _logger.LogWarning("Generating failure reports for {Count} failed steps", failedSteps.Count);
    
    foreach (var failed in failedSteps)
    {
        try
        {
            var report = await _failureReportGenerator.GenerateReportAsync(
                failed,
                taskPlan,
                cloudUsage.TotalCost);
            
            await _failureReportGenerator.SaveReportAsync(
                report,
                failed.FileName ?? $"Step{failed.StepNumber}",
                request.WorkspacePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate report for step {Step}", failed.StepNumber);
        }
    }
}

// Update response with partial success info
response.Success = accumulatedFiles.Any();  // Success if ANY files generated
response.PartialSuccess = failedSteps.Any();  // Flag partial success
response.Summary = failedSteps.Any()
    ? $"Partial success: {accumulatedFiles.Count - failedSteps.Count}/{accumulatedFiles.Count} files generated. " +
      $"{failedSteps.Count} files need manual review. See failure reports."
    : $"Complete success: {accumulatedFiles.Count} files generated";
```

### **Day 10: Build Decision Engine**

```csharp
// In TaskOrchestrator.cs, after each successful step:

// 🏗️ PHI4 BUILD DECISION
if (stepSuccess && _phi4 != null)
{
    try
    {
        var buildDecision = await _phi4.ShouldBuildNowAsync(
            stepIndex,
            taskPlan,
            accumulatedFiles,
            step,
            cancellationToken);
        
        if (buildDecision.ShouldBuildNow)
        {
            _logger.LogInformation("🏗️ [PHI4-BUILD] Building now: {Reasoning}", buildDecision.Reasoning);
            
            // Write files to disk
            var syncedFiles = await WriteFilesToWorkspaceAsync(
                accumulatedFiles.Values.ToList(),
                request.WorkspacePath,
                cancellationToken);
            
            // Build project
            var buildPhase = StartPhase("build_checkpoint", totalIterations);
            buildPhase.Details = new Dictionary<string, object>
            {
                ["checkpoint"] = buildDecision.Checkpoint,
                ["reasoning"] = buildDecision.Reasoning,
                ["filesGenerated"] = accumulatedFiles.Count
            };
            
            var buildResult = await _executionService.ExecuteAsync(
                new ExecuteCodeRequest
                {
                    Language = request.Language ?? "csharp",
                    WorkspacePath = request.WorkspacePath,
                    BuildOnly = true
                },
                cancellationToken);
            
            EndPhase(null, buildPhase);
            
            if (!buildResult.Success)
            {
                _logger.LogWarning("🏗️ [BUILD-FAIL] Build failed at checkpoint: {Errors}", 
                    buildResult.Errors);
                
                // Add build errors to feedback for next step
                if (feedback == null) feedback = new CodeFeedback();
                feedback.SpecificIssues.Add($"BUILD ERRORS:\n{buildResult.Errors}");
            }
            else
            {
                _logger.LogInformation("🏗️ [BUILD-OK] Build passed at checkpoint: {Checkpoint}", 
                    buildDecision.Checkpoint);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogDebug(ex, "Build decision check failed (non-critical)");
    }
}
```

---

## 📋 **Phase 3: Polish & Features (Week 3)**

### **Day 11-12: Library Templates**

```csharp
// File: CodingOrchestrator.Server/Templates/LibraryTemplate.cs

namespace CodingOrchestrator.Server.Templates;

public static class LibraryTemplate
{
    public static string GetCsprojTemplate(string libraryName, string description)
    {
        return $@"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFrameworks>net9.0;net8.0;netstandard2.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    
    <!-- NuGet Package Metadata -->
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageId>{libraryName}</PackageId>
    <Version>1.0.0</Version>
    <Authors>AI Generated</Authors>
    <Description>{description}</Description>
    <PackageTags>library;dotnet;csharp</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    
    <!-- Documentation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <None Include=""README.md"" Pack=""true"" PackagePath="""" />
  </ItemGroup>

</Project>";
    }

    public static string GetReadmeTemplate(string libraryName, string description)
    {
        return $@"# {libraryName}

{description}

## Installation

```bash
dotnet add package {libraryName}
```

## Usage

```csharp
// TODO: Add usage examples
```

## Features

- Feature 1
- Feature 2

## License

MIT

## Generated

This library was generated by C# Agent v2.
";
    }

    public static string GetChangelogTemplate()
    {
        return $@"# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - {DateTime.UtcNow:yyyy-MM-dd}

### Added
- Initial release
- Core functionality

";
    }
}
```

### **Day 13-14: Cost Optimization**

```csharp
// File: CodingOrchestrator.Server/Services/CostController.cs

namespace CodingOrchestrator.Server.Services;

public interface ICostController
{
    bool CanUseClaudeNow();
    bool ShouldUsePremiumClaude(int attemptNumber, decimal currentScore);
    void RecordCost(string model, decimal cost);
    CostSummary GetSummary();
}

public class CostController : ICostController
{
    private decimal _currentCost = 0;
    private readonly decimal _maxCost;
    private int _claudeCalls = 0;
    private readonly int _maxClaudeCalls;
    private readonly ILogger<CostController> _logger;

    public CostController(decimal maxCost, int maxClaudeCalls, ILogger<CostController> logger)
    {
        _maxCost = maxCost;
        _maxClaudeCalls = maxClaudeCalls;
        _logger = logger;
    }

    public bool CanUseClaudeNow()
    {
        if (_currentCost >= _maxCost)
        {
            _logger.LogWarning("❌ Max cost ${0:F2} reached, blocking Claude", _maxCost);
            return false;
        }

        if (_claudeCalls >= _maxClaudeCalls)
        {
            _logger.LogWarning("❌ Max Claude calls {0} reached", _maxClaudeCalls);
            return false;
        }

        return true;
    }

    public bool ShouldUsePremiumClaude(int attemptNumber, decimal currentScore)
    {
        // Only use premium if:
        // 1. We're on attempt 8+
        // 2. Still under 80% of budget
        // 3. Score is close (7+) but not quite passing (< 8)

        return attemptNumber >= 8 &&
               currentScore >= 7 &&
               currentScore < 8 &&
               _currentCost < (_maxCost * 0.8m);
    }

    public void RecordCost(string model, decimal cost)
    {
        _currentCost += cost;
        if (model.Contains("claude"))
        {
            _claudeCalls++;
        }
        _logger.LogDebug("💰 Recorded cost: ${0:F4} for {1} (Total: ${2:F2})", cost, model, _currentCost);
    }

    public CostSummary GetSummary()
    {
        return new CostSummary
        {
            TotalCost = _currentCost,
            MaxCost = _maxCost,
            ClaudeCalls = _claudeCalls,
            MaxClaudeCalls = _maxClaudeCalls,
            RemainingBudget = _maxCost - _currentCost,
            BudgetUsedPercent = (_currentCost / _maxCost) * 100
        };
    }
}

public record CostSummary
{
    public decimal TotalCost { get; init; }
    public decimal MaxCost { get; init; }
    public int ClaudeCalls { get; init; }
    public int MaxClaudeCalls { get; init; }
    public decimal RemainingBudget { get; init; }
    public decimal BudgetUsedPercent { get; init; }
}
```

### **Day 15: Model Performance Tracking**

(Add to MemoryAgent or local tracking)

```csharp
// Track which models work best for which file types
// Store in MemoryAgent for learning
await _memoryAgent.RecordModelPerformanceAsync(new ModelPerformanceRecord
{
    Model = "deepseek-v2:16b",
    FileType = "Models/*.cs",
    SuccessRate = 95,
    AverageScore = 8.8,
    AverageAttempts = 1.2
}, cancellationToken);
```

---

## 📋 **Phase 4: Final Testing & Documentation (Week 4)**

### **Day 16-17: Comprehensive Testing**

1. **Simple Apps** (should be $0):
   - Console calculator
   - Simple web API
   - Basic Blazor form

2. **Complex Apps** (should escalate):
   - Full Blazor WebAssembly with offline
   - Microservice with background workers
   - Class library with advanced patterns

3. **Edge Cases**:
   - Intentionally complex tasks
   - Invalid requirements
   - Missing dependencies

### **Day 18-19: Documentation**

- API documentation
- Usage examples
- Cost optimization guide
- Troubleshooting guide

### **Day 20: Performance Tuning**

- Optimize Phi4 prompts
- Tune temperature settings
- Reduce latency

---

## 📊 **Success Metrics**

### **Before (Current System)**
- First-pass success: ~60%
- Average attempts per file: 2.5
- Cost per project: ~$1.50
- Stops on first failure

### **After (V2)**
- First-pass success: 75%+ (with Phi4 thinking)
- Average attempts per file: 1.8
- Cost per project: ~$1.00 (better escalation)
- Never stops (stub + continue)
- 98.7% eventual success rate

---

## 🚀 **Next Steps**

1. **Start Phase 1 immediately** - Phi4 client is the foundation
2. **Test each phase** before moving to next
3. **Track metrics** throughout implementation
4. **Iterate on prompts** based on results

**Estimated completion: 4 weeks from start**

Ready to begin? 🔥


