using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Meta-reasoning service that intelligently selects and orders tools
/// Based on task analysis, not just prompt instructions
/// </summary>
public interface IToolReasoningService
{
    Task<ToolExecutionPlan> PlanToolUsageAsync(
        string task,
        CodebaseContext? codebaseContext,
        ValidationFeedback? previousFeedback,
        CancellationToken cancellationToken);
    
    Task<string> SuggestNextToolAsync(
        string task,
        List<string> toolsUsed,
        string currentState,
        CancellationToken cancellationToken);
}

public class ToolReasoningService : IToolReasoningService
{
    private readonly ILogger<ToolReasoningService> _logger;
    
    public ToolReasoningService(ILogger<ToolReasoningService> logger)
    {
        _logger = logger;
    }
    
    public async Task<ToolExecutionPlan> PlanToolUsageAsync(
        string task,
        CodebaseContext? codebaseContext,
        ValidationFeedback? previousFeedback,
        CancellationToken cancellationToken)
    {
        var plan = new ToolExecutionPlan { Task = task };
        
        // RULE-BASED REASONING (faster than LLM, more reliable)
        
        // 1. If modifying existing code → MUST read files first
        if (IsModificationTask(task))
        {
            plan.RequiredSteps.Add(new ToolStep
            {
                Tool = "list_files",
                Reasoning = "Modification task requires understanding existing structure",
                Priority = 1
            });
            
            plan.RequiredSteps.Add(new ToolStep
            {
                Tool = "search_codebase",
                Reasoning = "Find files related to the modification",
                Priority = 2
            });
        }
        
        // 2. If creating new feature → Search for similar patterns
        if (IsNewFeatureTask(task))
        {
            plan.RequiredSteps.Add(new ToolStep
            {
                Tool = "search_codebase",
                Reasoning = "Find existing patterns to match",
                Priority = 1
            });
            
            // Check if similar files exist
            if (codebaseContext != null && codebaseContext.Files.Any())
            {
                plan.RequiredSteps.Add(new ToolStep
                {
                    Tool = "read_file",
                    Reasoning = "Read similar existing files to understand patterns",
                    Priority = 2
                });
            }
        }
        
        // 3. If fixing errors → Read error location first
        if (previousFeedback?.BuildErrors != null)
        {
            plan.RequiredSteps.Add(new ToolStep
            {
                Tool = "read_file",
                Reasoning = "Read files with compilation errors",
                Priority = 1
            });
        }
        
        // 4. If creating service/component → Check DI registration
        if (IsServiceCreation(task))
        {
            plan.RequiredSteps.Add(new ToolStep
            {
                Tool = "read_file",
                Arguments = new { path = "Program.cs" },
                Reasoning = "Understand DI registration pattern",
                Priority = 1
            });
            
            plan.RequiredSteps.Add(new ToolStep
            {
                Tool = "grep",
                Arguments = new { pattern = "AddScoped|AddSingleton|AddTransient" },
                Reasoning = "Find all service registrations",
                Priority = 2
            });
        }
        
        // 5. Always compile before finalizing
        plan.RequiredSteps.Add(new ToolStep
        {
            Tool = "compile_code",
            Reasoning = "Verify code compiles before submission",
            Priority = 99 // Near end
        });
        
        _logger.LogInformation("📋 Tool plan created: {StepCount} steps", plan.RequiredSteps.Count);
        
        return plan;
    }
    
    public async Task<string> SuggestNextToolAsync(
        string task,
        List<string> toolsUsed,
        string currentState,
        CancellationToken cancellationToken)
    {
        // ADAPTIVE SUGGESTIONS based on what's been done
        
        // If no files read yet → suggest reading
        if (!toolsUsed.Contains("read_file") && !toolsUsed.Contains("list_files"))
        {
            return "read_file or list_files - You haven't explored the codebase yet";
        }
        
        // If files read but no search → suggest searching
        if (toolsUsed.Contains("read_file") && !toolsUsed.Contains("search_codebase"))
        {
            return "search_codebase - Find related patterns across the codebase";
        }
        
        // If code generated but not compiled → MUST compile
        if (currentState.Contains("```") && !toolsUsed.Contains("compile_code"))
        {
            return "compile_code - You MUST compile before finalizing!";
        }
        
        // If compiled successfully but not run → suggest running
        if (toolsUsed.Contains("compile_code") && !toolsUsed.Contains("run_code"))
        {
            return "run_code - Test for runtime errors";
        }
        
        // If everything done → finalize
        if (toolsUsed.Contains("compile_code") && toolsUsed.Contains("run_code"))
        {
            return "FINALIZE - Code is tested and ready";
        }
        
        return "search_codebase - Continue exploring";
    }
    
    // Task classification helpers
    private bool IsModificationTask(string task)
    {
        var modificationKeywords = new[] { "update", "modify", "change", "fix", "refactor", "improve" };
        return modificationKeywords.Any(k => task.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
    
    private bool IsNewFeatureTask(string task)
    {
        var newFeatureKeywords = new[] { "create", "add", "implement", "build", "generate" };
        return newFeatureKeywords.Any(k => task.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
    
    private bool IsServiceCreation(string task)
    {
        return task.Contains("service", StringComparison.OrdinalIgnoreCase) ||
               task.Contains("component", StringComparison.OrdinalIgnoreCase);
    }
}

public class ToolExecutionPlan
{
    public string Task { get; set; } = "";
    public List<ToolStep> RequiredSteps { get; set; } = new();
    public List<ToolStep> OptionalSteps { get; set; } = new();
}

public class ToolStep
{
    public string Tool { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public int Priority { get; set; }
    public object? Arguments { get; set; }
}


