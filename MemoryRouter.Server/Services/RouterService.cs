using System.Diagnostics;
using MemoryRouter.Server.Models;
using MemoryRouter.Server.Clients;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Smart routing service with Hybrid AI + Statistical Intelligence
/// - Uses FunctionGemma for tool selection
/// - Uses DeepSeek AI for complexity analysis
/// - Learns from actual performance over time
/// - Automatically runs long tasks in background
/// </summary>
public class RouterService : IRouterService
{
    private readonly IFunctionGemmaClient _gemmaClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ICodingOrchestratorClient _codingOrchestrator;
    private readonly IHybridExecutionClassifier _executionClassifier;
    private readonly IBackgroundJobManager _jobManager;
    private readonly IPerformanceTracker _performanceTracker;
    private readonly ILogger<RouterService> _logger;

    public RouterService(
        IFunctionGemmaClient gemmaClient,
        IToolRegistry toolRegistry,
        IMemoryAgentClient memoryAgent,
        ICodingOrchestratorClient codingOrchestrator,
        IHybridExecutionClassifier executionClassifier,
        IBackgroundJobManager jobManager,
        IPerformanceTracker performanceTracker,
        ILogger<RouterService> logger)
    {
        _gemmaClient = gemmaClient;
        _toolRegistry = toolRegistry;
        _memoryAgent = memoryAgent;
        _codingOrchestrator = codingOrchestrator;
        _executionClassifier = executionClassifier;
        _jobManager = jobManager;
        _performanceTracker = performanceTracker;
        _logger = logger;
    }

    public async Task<WorkflowResult> ExecuteRequestAsync(
        string userRequest,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var totalStopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("🧠 [Request {RequestId}] Processing: {Request}", requestId, userRequest);

        try
        {
            // Step 1: Use FunctionGemma to create an execution plan
            _logger.LogInformation("📋 [Request {RequestId}] Asking FunctionGemma to plan workflow...", requestId);
            
            var availableTools = _toolRegistry.GetAllTools();
            
            // Use FunctionGemma (now properly configured for Google's format)
            var plan = await _gemmaClient.PlanWorkflowAsync(userRequest, availableTools, context, cancellationToken);
            
            _logger.LogInformation("✅ [Request {RequestId}] Plan created with {StepCount} steps", 
                requestId, plan.FunctionCalls.Count);
            _logger.LogInformation("   💭 Reasoning: {Reasoning}", plan.Reasoning);

            // Step 2: Execute the plan step by step
            var steps = new List<StepResult>();
            var stepResults = new Dictionary<int, object>(); // Store results for use in later steps

            foreach (var functionCall in plan.FunctionCalls.OrderBy(fc => fc.Order))
            {
                _logger.LogInformation("▶️ [Request {RequestId}] Step {Order}: Executing {Tool}", 
                    requestId, functionCall.Order, functionCall.Name);
                _logger.LogInformation("   💭 {Reasoning}", functionCall.Reasoning);

                try
                {
                    // Get tool definition
                    var tool = _toolRegistry.GetTool(functionCall.Name);
                    if (tool == null)
                    {
                        throw new InvalidOperationException($"Tool '{functionCall.Name}' not found in registry");
                    }

                    // Process arguments (replace placeholders with previous results if needed)
                    var processedArgs = ProcessArguments(functionCall.Arguments, stepResults);

                    // 🧠 HYBRID INTELLIGENCE: Determine if this should run async
                    var executionDecision = await _executionClassifier.DetermineExecutionModeAsync(
                        functionCall.Name,
                        userRequest,
                        processedArgs,
                        cancellationToken);

                    _logger.LogInformation("🎯 Decision: {Mode} (est: {Ms}ms, confidence: {Conf}%, source: {Source})",
                        executionDecision.ShouldRunAsync ? "ASYNC" : "SYNC",
                        executionDecision.EstimatedDurationMs,
                        executionDecision.ConfidencePercent,
                        executionDecision.DecisionSource);
                    _logger.LogDebug("   Reasoning: {Reasoning}", executionDecision.Reasoning);

                    if (executionDecision.ShouldRunAsync)
                    {
                        // Run in background - return job ID immediately
                        var jobId = _jobManager.StartJob(
                            functionCall.Name,
                            async ct =>
                            {
                                var sw = Stopwatch.StartNew();
                                object result = tool.Service switch
                                {
                                    "memory-agent" => await _memoryAgent.CallToolAsync(functionCall.Name, processedArgs, ct),
                                    "coding-orchestrator" => await _codingOrchestrator.CallToolAsync(functionCall.Name, processedArgs, ct),
                                    _ => throw new InvalidOperationException($"Unknown service: {tool.Service}")
                                };
                                sw.Stop();
                                
                                // Record actual performance for learning
                                _performanceTracker.RecordExecution(functionCall.Name, sw.ElapsedMilliseconds, processedArgs);
                                
                                return result;
                            },
                            executionDecision.EstimatedDurationMs);

                        var stepResult = new StepResult
                        {
                            ToolName = functionCall.Name,
                            Success = true,
                            Result = new
                            {
                                jobId = jobId,
                                status = "started",
                                estimatedDurationMs = executionDecision.EstimatedDurationMs,
                                message = $"✅ Task started in background. Job ID: {jobId}. Use get_task_status with this job ID to check progress."
                            },
                            DurationMs = 0 // Immediate return
                        };

                        steps.Add(stepResult);
                        stepResults[functionCall.Order] = stepResult.Result!;

                        _logger.LogInformation("🚀 [Request {RequestId}] Step {Order} started in background (Job ID: {JobId})",
                            requestId, functionCall.Order, jobId);
                    }
                    else
                    {
                        // Run synchronously (fast task)
                        var stepStopwatch = Stopwatch.StartNew();

                        object result = tool.Service switch
                        {
                            "memory-agent" => await _memoryAgent.CallToolAsync(functionCall.Name, processedArgs, cancellationToken),
                            "coding-orchestrator" => await _codingOrchestrator.CallToolAsync(functionCall.Name, processedArgs, cancellationToken),
                            _ => throw new InvalidOperationException($"Unknown service: {tool.Service}")
                        };

                        stepStopwatch.Stop();

                        // Record actual performance for learning
                        _performanceTracker.RecordExecution(functionCall.Name, stepStopwatch.ElapsedMilliseconds, processedArgs);

                        // Store result for potential use in later steps
                        stepResults[functionCall.Order] = result;

                        var stepResult = new StepResult
                        {
                            ToolName = functionCall.Name,
                            Success = true,
                            Result = result,
                            DurationMs = stepStopwatch.ElapsedMilliseconds
                        };

                        steps.Add(stepResult);

                        _logger.LogInformation("✅ [Request {RequestId}] Step {Order} completed in {Duration}ms",
                            requestId, functionCall.Order, stepStopwatch.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ [Request {RequestId}] Step {Order} failed: {Error}", 
                        requestId, functionCall.Order, ex.Message);

                    var stepResult = new StepResult
                    {
                        ToolName = functionCall.Name,
                        Success = false,
                        Error = ex.Message,
                        DurationMs = 0
                    };

                    steps.Add(stepResult);
                    
                    // Decide whether to continue or abort
                    // For now, we abort on any error
                    totalStopwatch.Stop();
                    
                    return new WorkflowResult
                    {
                        RequestId = requestId,
                        OriginalRequest = userRequest,
                        Plan = plan,
                        Steps = steps,
                        Success = false,
                        Error = $"Workflow failed at step {functionCall.Order} ({functionCall.Name}): {ex.Message}",
                        TotalDurationMs = totalStopwatch.ElapsedMilliseconds
                    };
                }
            }

            totalStopwatch.Stop();

            // All steps completed successfully
            var finalResult = steps.LastOrDefault()?.Result?.ToString() ?? "Workflow completed successfully";
            
            _logger.LogInformation("🎉 [Request {RequestId}] Workflow completed successfully in {Duration}ms", 
                requestId, totalStopwatch.ElapsedMilliseconds);

            return new WorkflowResult
            {
                RequestId = requestId,
                OriginalRequest = userRequest,
                Plan = plan,
                Steps = steps,
                Success = true,
                FinalResult = finalResult,
                TotalDurationMs = totalStopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            
            _logger.LogError(ex, "❌ [Request {RequestId}] Workflow failed: {Error}", requestId, ex.Message);

            return new WorkflowResult
            {
                RequestId = requestId,
                OriginalRequest = userRequest,
                Plan = new WorkflowPlan { Reasoning = "Failed to create plan", FunctionCalls = new List<FunctionCall>() },
                Steps = new List<StepResult>(),
                Success = false,
                Error = $"Workflow execution failed: {ex.Message}",
                TotalDurationMs = totalStopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Process arguments, replacing placeholders like {{step_1_result}} with actual values
    /// </summary>
    private Dictionary<string, object> ProcessArguments(
        Dictionary<string, object> arguments,
        Dictionary<int, object> stepResults)
    {
        var processed = new Dictionary<string, object>();

        foreach (var (key, value) in arguments)
        {
            if (value is string strValue && strValue.Contains("{{") && strValue.Contains("}}"))
            {
                // Replace placeholder with actual result
                // Format: {{step_N_result}} or {{results_from_step_N}}
                var match = System.Text.RegularExpressions.Regex.Match(strValue, @"\{\{(?:step_|results_from_step_)?(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var stepNumber))
                {
                    if (stepResults.TryGetValue(stepNumber, out var stepResult))
                    {
                        processed[key] = stepResult;
                        _logger.LogDebug("   🔄 Replaced placeholder in '{Key}' with result from step {Step}", key, stepNumber);
                        continue;
                    }
                }
            }

            processed[key] = value;
        }

        return processed;
    }
}

