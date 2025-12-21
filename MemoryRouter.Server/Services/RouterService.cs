using System.Diagnostics;
using System.Text;
using System.Text.Json;
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
    // 🔥 REMOVED: ICodingOrchestratorClient - MemoryRouter now ONLY exposes MemoryAgent tools
    private readonly IHybridExecutionClassifier _executionClassifier;
    private readonly IBackgroundJobManager _jobManager;
    private readonly IPerformanceTracker _performanceTracker;
    private readonly ILogger<RouterService> _logger;

    public RouterService(
        IFunctionGemmaClient gemmaClient,
        IToolRegistry toolRegistry,
        IMemoryAgentClient memoryAgent,
        IHybridExecutionClassifier executionClassifier,
        IBackgroundJobManager jobManager,
        IPerformanceTracker performanceTracker,
        ILogger<RouterService> logger)
    {
        _gemmaClient = gemmaClient;
        _toolRegistry = toolRegistry;
        _memoryAgent = memoryAgent;
        _executionClassifier = executionClassifier;
        _jobManager = jobManager;
        _performanceTracker = performanceTracker;
        _logger = logger;
    }

    public async Task<WorkflowResult> ExecuteRequestAsync(
        string userRequest,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default,
        bool forceSync = false)
    {
        var requestId = Guid.NewGuid().ToString();
        var totalStopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("🧠 [Request {RequestId}] Processing: {Request}", requestId, userRequest);

        try
        {
            // Step 1: Use FunctionGemma to create an execution plan (with fallback to direct routing)
            _logger.LogInformation("📋 [Request {RequestId}] Asking FunctionGemma to plan workflow...", requestId);
            
            var availableTools = _toolRegistry.GetAllTools();
            
            WorkflowPlan plan;
            try
            {
                // Tier 1: Try FunctionGemma (Ollama)
                _logger.LogInformation("🤖 Tier 1: Trying FunctionGemma (Ollama)...");
                plan = await _gemmaClient.PlanWorkflowAsync(userRequest, availableTools, context, cancellationToken);
                _logger.LogInformation("✅ FunctionGemma succeeded");
            }
            catch (Exception gemmaEx)
            {
                _logger.LogWarning(gemmaEx, "⚠️ FunctionGemma failed, trying DeepSeek...");
                
                try
                {
                    // Tier 2: Try Phi4 AI (better for function calling)
                    _logger.LogInformation("🧠 Tier 2: Trying Phi4 AI...");
                    plan = await CreateDeepSeekRoutingPlanAsync(userRequest, availableTools, context ?? new Dictionary<string, object>(), cancellationToken);
                    _logger.LogInformation("✅ Phi4 succeeded");
                }
                catch (Exception phi4Ex)
                {
                    _logger.LogWarning(phi4Ex, "⚠️ Phi4 failed, using C# fallback");
                    
                    // Tier 3: C# keyword-based fallback
                    _logger.LogInformation("🔧 Tier 3: Using direct C# routing fallback");
                    plan = CreateDirectRoutingPlan(userRequest, context ?? new Dictionary<string, object>());
                }
            }
            
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

                    // ⚡ CRITICAL CHECK: If this is an index operation, FORCE async (bypass AI entirely)
                    bool forceIndexAsync = functionCall.Name.Contains("index", StringComparison.OrdinalIgnoreCase) && !forceSync;
                    if (forceIndexAsync)
                    {
                        _logger.LogWarning("🗂️🗂️🗂️ INDEX DETECTED - FORCING ASYNC MODE (BYPASSING AI)");
                    }

                    // 🧠 HYBRID INTELLIGENCE: Determine if this should run async (unless we're forcing)
                    ExecutionDecision executionDecision;
                    if (forceIndexAsync)
                    {
                        // Create a decision that forces async
                        executionDecision = new ExecutionDecision
                        {
                            ShouldRunAsync = true,
                            EstimatedDurationMs = 60000, // 1 minute minimum
                            ConfidencePercent = 100,
                            Reasoning = "INDEX OVERRIDE: All indexing operations MUST run in background",
                            DecisionSource = "Forced_Index_Override",
                            Complexity = TaskComplexity.High
                        };
                        _logger.LogWarning("📊 OVERRIDE DECISION: ASYNC=TRUE (forced for indexing)");
                    }
                    else
                    {
                        executionDecision = await _executionClassifier.DetermineExecutionModeAsync(
                            functionCall.Name,
                            userRequest,
                            processedArgs,
                            cancellationToken);
                    }

                    // Override async decision if execute_task is running in synchronous mode
                    var shouldRunAsync = executionDecision.ShouldRunAsync && !forceSync;
                    
                    _logger.LogWarning("🎯🎯🎯 FINAL: Tool={Tool}, shouldRunAsync={Async}, forceSync={Force}",
                        functionCall.Name, shouldRunAsync, forceSync);

                    _logger.LogInformation("🎯 Decision: {Mode} (est: {Ms}ms, confidence: {Conf}%, source: {Source}{Override})",
                        shouldRunAsync ? "ASYNC" : "SYNC",
                        executionDecision.EstimatedDurationMs,
                        executionDecision.ConfidencePercent,
                        executionDecision.DecisionSource,
                        forceSync ? ", FORCED SYNC" : "");
                    _logger.LogDebug("   Reasoning: {Reasoning}", executionDecision.Reasoning);

                    if (shouldRunAsync)
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
                                    // 🔥 REMOVED: coding-orchestrator - handled by orchestrator-mcp-wrapper.js
                                    _ => throw new InvalidOperationException($"Unknown service: {tool.Service} (MemoryRouter only routes to memory-agent)")
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
                            // 🔥 REMOVED: coding-orchestrator - handled by orchestrator-mcp-wrapper.js
                            _ => throw new InvalidOperationException($"Unknown service: {tool.Service} (MemoryRouter only routes to memory-agent)")
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

    /// <summary>
    /// Direct C# routing as fallback when FunctionGemma fails
    /// </summary>
    private WorkflowPlan CreateDirectRoutingPlan(string userRequest, Dictionary<string, object> context)
    {
        var lowerRequest = userRequest.ToLowerInvariant();
        string toolName;
        Dictionary<string, object> parameters = new();

        // Simple keyword-based routing
        if (lowerRequest.Contains("list") && (lowerRequest.Contains("task") || lowerRequest.Contains("job")))
        {
            toolName = "list_tasks";
        }
        else if (lowerRequest.Contains("workspace") && lowerRequest.Contains("status"))
        {
            toolName = "workspace_status";
        }
        // Check for "generated files" - route to get_generated_files
        else if (lowerRequest.Contains("generated") && lowerRequest.Contains("file"))
        {
            toolName = "get_generated_files";
            // Extract job ID (CodingOrchestrator format: job_YYYYMMDDHHMMSS_XXXXXXXX)
            var jobIdMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"(job_\d{14}_[a-f0-9]+)");
            if (jobIdMatch.Success)
            {
                parameters["jobId"] = jobIdMatch.Value;
            }
            // outputPath is required - default to current directory
            parameters["outputPath"] = context.ContainsKey("workspacePath") ? context["workspacePath"].ToString()! : ".";
        }
        // Check for "workflow status" specifically - route to get_workflow_status
        else if (lowerRequest.Contains("workflow") && (lowerRequest.Contains("status") || lowerRequest.Contains("progress")))
        {
            var uuidMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})");
            if (uuidMatch.Success)
            {
                toolName = "get_workflow_status";
                parameters["workflowId"] = uuidMatch.Value;
            }
            else
            {
                toolName = "list_workflows"; // No workflow ID = list all workflows
            }
        }
        // Check for "list workflows"
        else if (lowerRequest.Contains("list") && lowerRequest.Contains("workflow"))
        {
            toolName = "list_workflows";
        }
        // Check for "status" BEFORE "index" to handle "status on indexing"
        else if (lowerRequest.Contains("status") || lowerRequest.Contains("progress") || lowerRequest.Contains("check"))
        {
            // CodingOrchestrator jobs: job_YYYYMMDDHHMMSS_XXXXXXXX → get_task_status
            // MemoryRouter workflows: UUID format → get_workflow_status
            var codingOrchestratorMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"(job_\d{14}_[a-f0-9]+)");
            var uuidMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})");
            
            if (codingOrchestratorMatch.Success)
            {
                // CodingOrchestrator job → get_task_status
                toolName = "get_task_status";
                parameters["jobId"] = codingOrchestratorMatch.Value;
            }
            else if (uuidMatch.Success)
            {
                // UUID = MemoryRouter workflow → get_workflow_status (NOT get_task_status!)
                toolName = "get_workflow_status";
                parameters["workflowId"] = uuidMatch.Value;
            }
            else
            {
                toolName = "list_tasks";
            }
        }
        else if (lowerRequest.Contains("index"))
        {
            toolName = "index";
            parameters["path"] = context.ContainsKey("workspacePath") ? context["workspacePath"].ToString()! : "/workspace";
            parameters["scope"] = "directory";
            parameters["context"] = context.ContainsKey("context") ? context["context"].ToString()! : "default";
        }
        else if (lowerRequest.Contains("find") || lowerRequest.Contains("where") || lowerRequest.Contains("search") || lowerRequest.Contains("show me"))
        {
            toolName = "smartsearch";
            var query = lowerRequest.Replace("find", "").Replace("where", "").Replace("search", "").Replace("show me", "").Trim();
            parameters["query"] = query;
            parameters["context"] = context.ContainsKey("context") ? context["context"].ToString()! : "default";
        }
        else if ((lowerRequest.Contains("create") || lowerRequest.Contains("build") || lowerRequest.Contains("generate")) && lowerRequest.Contains("plan"))
        {
            toolName = "manage_plan";
            parameters["action"] = "create";
            parameters["name"] = userRequest;
        }
        else if (lowerRequest.Contains("create") || lowerRequest.Contains("build") || lowerRequest.Contains("generate") || lowerRequest.Contains("write"))
        {
            toolName = "orchestrate_task";
            parameters["task"] = userRequest;
        }
        else
        {
            // Default to smartsearch
            toolName = "smartsearch";
            parameters["query"] = userRequest;
            parameters["context"] = context.ContainsKey("context") ? context["context"].ToString()! : "default";
        }

        _logger.LogInformation("🎯 Direct routing selected: {Tool}", toolName);

        return new WorkflowPlan
        {
            Reasoning = $"Direct C# routing (FunctionGemma unavailable): {toolName}",
            FunctionCalls = new List<FunctionCall>
            {
                new FunctionCall
                {
                    Order = 1,
                    Name = toolName,
                    Arguments = parameters,
                    Reasoning = $"Selected {toolName} based on keywords"
                }
            }
        };
    }

    /// <summary>
    /// Tier 2: Phi4 AI-based routing (Microsoft's function-calling specialist)
    /// Phi4 is specifically trained for function calling and structured tasks
    /// </summary>
    private async Task<WorkflowPlan> CreateDeepSeekRoutingPlanAsync(
        string userRequest,
        IEnumerable<ToolDefinition> availableTools,
        Dictionary<string, object> context,
        CancellationToken cancellationToken)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://10.0.2.20:11434") };
        
        var toolsList = string.Join("\n", availableTools.Take(20).Select(t => $"- {t.Name}: {t.Description}"));
        
        var prompt = $@"You are a function router. Choose the correct tool.

Available tools:
{toolsList}

Request: ""{userRequest}""

Select ONE tool and return JSON:
{{""tool"":""tool_name"",""reason"":""why""}}

Quick rules:
- list tasks → list_tasks
- workspace status → workspace_status
- find/search → smartsearch
- index files → index
- create code → orchestrate_task
- task status + UUID → get_task_status

JSON only:";

        var request = new
        {
            model = "phi4:latest",  // Microsoft Phi4 - better for function calling
            prompt = prompt,
            format = "json",
            stream = false,
            options = new
            {
                temperature = 0.0,  // Deterministic for routing
                top_p = 0.9,
                num_predict = 128  // Short response needed
            }
        };

        var requestJson = JsonSerializer.Serialize(request);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("/api/generate", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var ollamaResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
        var aiResponse = ollamaResponse.GetProperty("response").GetString();

        if (string.IsNullOrEmpty(aiResponse))
        {
            throw new InvalidOperationException("DeepSeek returned empty response");
        }

        // Parse DeepSeek's response
        var parsed = JsonSerializer.Deserialize<JsonElement>(aiResponse);
        var toolName = parsed.GetProperty("tool").GetString();
        var reasoning = parsed.TryGetProperty("reasoning", out var reasoningProp) 
            ? reasoningProp.GetString() 
            : "DeepSeek selected this tool";

        // Build parameters based on tool and context
        var parameters = new Dictionary<string, object>();
        
        if (toolName == "index")
        {
            parameters["path"] = context.ContainsKey("workspacePath") ? context["workspacePath"].ToString()! : "/workspace";
            parameters["scope"] = "directory";
            parameters["context"] = context.ContainsKey("context") ? context["context"].ToString()! : "default";
        }
        else if (toolName == "smartsearch")
        {
            var query = userRequest.ToLowerInvariant()
                .Replace("find", "").Replace("where", "").Replace("search", "").Trim();
            parameters["query"] = query;
            parameters["context"] = context.ContainsKey("context") ? context["context"].ToString()! : "default";
        }
        else if (toolName == "orchestrate_task")
        {
            parameters["task"] = userRequest;
        }
        else if (toolName == "get_task_status")
        {
            // Only CodingOrchestrator jobs use get_task_status (job_YYYYMMDDHHMMSS_XXXXXXXX format)
            var codingOrchestratorMatch = System.Text.RegularExpressions.Regex.Match(userRequest.ToLowerInvariant(), @"(job_\d{14}_[a-f0-9]+)");
            if (codingOrchestratorMatch.Success)
            {
                parameters["jobId"] = codingOrchestratorMatch.Value;
            }
        }
        else if (toolName == "get_workflow_status")
        {
            // MemoryRouter workflows use UUID format
            var uuidMatch = System.Text.RegularExpressions.Regex.Match(userRequest.ToLowerInvariant(), @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})");
            if (uuidMatch.Success)
            {
                parameters["workflowId"] = uuidMatch.Value;
            }
        }
        else if (toolName == "get_generated_files")
        {
            // Extract job ID for get_generated_files
            var codingOrchestratorMatch = System.Text.RegularExpressions.Regex.Match(userRequest.ToLowerInvariant(), @"(job_\d{14}_[a-f0-9]+)");
            if (codingOrchestratorMatch.Success)
            {
                parameters["jobId"] = codingOrchestratorMatch.Value;
            }
            // outputPath is required - default to current directory
            parameters["outputPath"] = context.ContainsKey("workspacePath") ? context["workspacePath"].ToString()! : ".";
        }

        _logger.LogInformation("🧠 Phi4 selected: {Tool} - {Reasoning}", toolName, reasoning);

        return new WorkflowPlan
        {
            Reasoning = $"Phi4 AI routing: {reasoning}",
            FunctionCalls = new List<FunctionCall>
            {
                new FunctionCall
                {
                    Order = 1,
                    Name = toolName!,
                    Arguments = parameters,
                    Reasoning = reasoning ?? "Phi4 routing"
                }
            }
        };
    }
}


