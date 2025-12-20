using System.Text.Json;
using System.Text.RegularExpressions;
using MemoryRouter.Server.Models;
using static MemoryRouter.Server.Models.ToolCategory;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Tracks workflow execution with nested job IDs for Cursor auto-polling
/// </summary>
public class WorkflowTracker
{
    public string WorkflowId { get; set; } = "";
    public string Request { get; set; } = "";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running"; // running, completed, failed
    public int Progress { get; set; } = 0; // 0-100
    public string CurrentStep { get; set; } = "initializing";
    public List<NestedJob> NestedJobs { get; set; } = new();
    public string? FinalResult { get; set; }
    public string? Error { get; set; }
    public long EstimatedDurationMs { get; set; } = 60000;
}

public class NestedJob
{
    public string JobId { get; set; } = "";
    public string Type { get; set; } = ""; // orchestrate_task, index, etc.
    public string Status { get; set; } = "running";
    public int Progress { get; set; } = 0;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// MCP handler that exposes MemoryRouter to Cursor IDE
/// Single entry point: execute_task (FunctionGemma figures out the rest)
/// </summary>
public class McpHandler : IMcpHandler
{
    private readonly IRouterService _routerService;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<McpHandler> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public McpHandler(
        IRouterService routerService,
        IToolRegistry toolRegistry,
        ILogger<McpHandler> logger)
    {
        _routerService = routerService;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public IEnumerable<object> GetToolDefinitions()
    {
        var tools = new List<object>
        {
            // Primary entry point - FunctionGemma-powered smart routing
            new Dictionary<string, object>
            {
                ["name"] = "execute_task",
                ["description"] = @"🧠 **Smart AI Router** - Single entry point for ANY development task. 

Uses FunctionGemma to automatically figure out which tools to call and in what order.

**What it does:**
- Analyzes your natural language request
- Searches for existing code/patterns when needed
- Generates code in any language
- Validates and checks quality
- Creates designs and brands
- Plans and breaks down complex tasks

**Examples:**
- ""Create a REST API for users with authentication""
- ""Find all code that handles database transactions""
- ""Generate a React dashboard with charts""
- ""Design a brand system for my fintech app""
- ""Explain how the authentication system works""

Just describe what you want - the router figures out the rest!",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["request"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Your task in natural language (e.g., 'Create a user service', 'Find authentication code', 'Design a dark mode theme')"
                        },
                        ["context"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Optional project context name for memory/continuity"
                        },
                        ["workspacePath"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Optional workspace path for file operations"
                        },
                        ["background"] = new Dictionary<string, object>
                        {
                            ["type"] = "boolean",
                            ["description"] = "Run workflow in background and return immediately with workflow ID (default: true for long tasks)",
                            ["default"] = true
                        }
                    },
                    ["required"] = new[] { "request" }
                }
            },

            // Discovery tool - see what's available
            new Dictionary<string, object>
            {
                ["name"] = "list_available_tools",
                ["description"] = "List all tools that MemoryRouter can use (from MemoryAgent and CodingOrchestrator). Shows capabilities of the system.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["category"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Optional: filter by category",
                            ["enum"] = new[] { "all", "search", "index", "analysis", "validation", "planning", "todo", "codegen", "design", "knowledge", "status", "control", "other" }
                        }
                    }
                }
            },

            // 🚀 NEW: Workflow status tracking for Cursor auto-polling
            new Dictionary<string, object>
            {
                ["name"] = "get_workflow_status",
                ["description"] = @"📊 **Get Workflow Status** - Track background workflow progress and nested jobs.

Use this to check the status of background workflows started by `execute_task`.

**Returns:**
- Workflow status (running/completed/failed)
- Progress percentage (0-100)
- Current step being executed
- Nested job IDs (e.g., CodingOrchestrator jobs)
- Final result when complete

**Auto-polling:** Call every 5 seconds to track progress of long-running tasks.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["workflowId"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The workflow ID returned by execute_task (UUID format)"
                        }
                    },
                    ["required"] = new[] { "workflowId" }
                }
            },

            // 🔄 List all active workflows
            new Dictionary<string, object>
            {
                ["name"] = "list_workflows",
                ["description"] = "List all active and recent workflows. Shows workflow IDs, status, and progress for tracking.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["includeCompleted"] = new Dictionary<string, object>
                        {
                            ["type"] = "boolean",
                            ["description"] = "Include completed workflows (default: false, only show running)",
                            ["default"] = false
                        }
                    }
                }
            }
        };

        return tools;
    }

    public async Task<string> HandleToolCallAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🎯 MCP tool call: {Tool}", toolName);

        return toolName switch
        {
            "execute_task" => await HandleExecuteTaskAsync(arguments, cancellationToken),
            "list_available_tools" => HandleListAvailableTools(arguments),
            "get_workflow_status" => HandleGetWorkflowStatus(arguments),
            "list_workflows" => HandleListWorkflows(arguments),
            _ => $"❌ Unknown tool: {toolName}"
        };
    }

    // Static dictionaries for workflow tracking (in production, use Redis or similar)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, WorkflowResult> _workflowResults = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _runningWorkflows = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, WorkflowTracker> _workflowTrackers = new();

    private async Task<string> HandleExecuteTaskAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken)
    {
        var request = GetStringArg(arguments, "request");
        if (string.IsNullOrEmpty(request))
        {
            return "❌ Error: 'request' parameter is required";
        }

        // ⚡ INTERCEPT: Handle workflow-related requests directly (don't route through AI)
        var interceptResult = TryInterceptLocalTools(request);
        if (interceptResult != null)
        {
            _logger.LogInformation("⚡ Intercepted local tool request: {Request}", request);
            return interceptResult;
        }

        var context = new Dictionary<string, object>();
        
        if (arguments.TryGetValue("context", out var ctxValue) && ctxValue != null)
        {
            context["context"] = ctxValue.ToString() ?? string.Empty;
        }
        
        if (arguments.TryGetValue("workspacePath", out var wsValue) && wsValue != null)
        {
            context["workspacePath"] = wsValue.ToString() ?? string.Empty;
        }

        // Check if user wants to run in background
        // ⚡ SMART DEFAULT: Detect slow operations that DON'T need real-time results
        // NOTE: Search operations are SYNCHRONOUS - users need results immediately!
        var requestLower = request.ToLowerInvariant();
        var smartDefaultBackground = 
            (requestLower.Contains("index") && !requestLower.Contains("status")) ||  // Indexing (Fix #23)
            requestLower.Contains("workspace") ||                                     // Workspace analysis (Fix #24)
            (requestLower.Contains("list") && requestLower.Contains("task"));        // List tasks (Fix #24)
        
        var runInBackground = GetBoolArg(arguments, "background", smartDefaultBackground);

        _logger.LogInformation("🚀 Executing task: {Request} (background: {Background}, smart default: {Smart})", 
            request, runInBackground, smartDefaultBackground);
        _logger.LogInformation("📊 Arguments received: {Args}", System.Text.Json.JsonSerializer.Serialize(arguments));

        try
        {
            if (runInBackground)
            {
                // Start workflow in background and return immediately with workflow ID
                var workflowId = Guid.NewGuid().ToString();
                
                // Create workflow tracker for auto-polling
                var tracker = new WorkflowTracker
                {
                    WorkflowId = workflowId,
                    Request = request,
                    StartedAt = DateTime.UtcNow,
                    Status = "running",
                    Progress = 0,
                    CurrentStep = "analyzing_request",
                    EstimatedDurationMs = EstimateWorkflowDuration(request)
                };
                _workflowTrackers[workflowId] = tracker;
                
                var workflowTask = Task.Run(async () =>
                {
                    try
                    {
                        _logger.LogInformation("🔄 [Workflow {WorkflowId}] Started in background", workflowId);
                        tracker.CurrentStep = "executing_workflow";
                        tracker.Progress = 10;
                        
                        var result = await _routerService.ExecuteRequestAsync(request, context, CancellationToken.None);
                        
                        _logger.LogInformation("✅ [Workflow {WorkflowId}] Completed: {Success}", workflowId, result.Success);
                        
                        // Extract nested job IDs from the result
                        ExtractNestedJobIds(result, tracker);
                        
                        // Update tracker
                        tracker.Status = result.Success ? "completed" : "failed";
                        tracker.Progress = 100;
                        tracker.CompletedAt = DateTime.UtcNow;
                        tracker.FinalResult = result.FinalResult;
                        tracker.Error = result.Error;
                        tracker.CurrentStep = "completed";
                        
                        // Store result for later retrieval
                        _workflowResults[workflowId] = result;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ [Workflow {WorkflowId}] Failed", workflowId);
                        
                        tracker.Status = "failed";
                        tracker.Error = ex.Message;
                        tracker.CompletedAt = DateTime.UtcNow;
                        tracker.CurrentStep = "failed";
                        
                        _workflowResults[workflowId] = new WorkflowResult
                        {
                            RequestId = workflowId,
                            OriginalRequest = request,
                            Plan = new WorkflowPlan(),
                            Steps = new List<StepResult>(),
                            Success = false,
                            Error = ex.Message,
                            TotalDurationMs = 0
                        };
                    }
                    finally
                    {
                        _runningWorkflows.TryRemove(workflowId, out _);
                    }
                }, CancellationToken.None);

                _runningWorkflows[workflowId] = workflowTask;

                _logger.LogInformation("✅ Returning immediately to caller with workflow ID: {WorkflowId}", workflowId);

                // Return structured response with job tracking metadata
                var jobTracking = new
                {
                    workflowId = workflowId,
                    pollTool = "get_workflow_status",
                    pollIntervalMs = 5000,
                    estimatedDurationMs = tracker.EstimatedDurationMs,
                    canPoll = true
                };
                var jobTrackingJson = JsonSerializer.Serialize(jobTracking, _jsonOptions);

                return $@"🚀 **Workflow Started in Background**

**Workflow ID:** `{workflowId}`
**Request:** {request}

✅ The AI is now analyzing your request and executing the workflow in the background.

**This returns immediately** - you can continue working in Cursor without waiting!

---

## 📊 Job Tracking (for Cursor auto-polling)

To check status, call: `get_workflow_status` with `workflowId: ""{workflowId}""`

<!-- MCP_JOB_TRACKING
{jobTrackingJson}
-->

**Estimated duration:** ~{tracker.EstimatedDurationMs / 1000} seconds

💡 **Tip:** Use `list_workflows` to see all active workflows.";
            }
            else
            {
                // Run synchronously and wait for result (forceSync=true ensures all steps also run synchronously)
                _logger.LogInformation("⏳ Waiting for workflow to complete synchronously (forceSync=true)...");
                
                var result = await _routerService.ExecuteRequestAsync(request, context, cancellationToken, forceSync: true);
                
                _logger.LogInformation("✅ Workflow completed, formatting result...");
                
                var formattedResult = FormatWorkflowResult(result);
                
                _logger.LogInformation("📤 Returning formatted result to Cursor ({Length} chars)", formattedResult.Length);
                return formattedResult;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to execute task");
            return $"❌ Error executing task: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
        }
    }

    private string FormatWorkflowResult(WorkflowResult result)
    {
        var output = new System.Text.StringBuilder();
        
        if (result.Success)
        {
            output.AppendLine("# ✅ Task Completed Successfully");
            output.AppendLine();
            output.AppendLine($"**Request:** {result.OriginalRequest}");
            output.AppendLine($"**Duration:** {result.TotalDurationMs}ms");
            output.AppendLine();
            
            output.AppendLine("## 🤖 FunctionGemma's Plan");
            output.AppendLine($"> {result.Plan.Reasoning}");
            output.AppendLine();
            
            output.AppendLine("## 📋 Execution Steps");
            output.AppendLine();
            foreach (var step in result.Steps)
            {
                var icon = step.Success ? "✅" : "❌";
                output.AppendLine($"{icon} **{step.ToolName}** ({step.DurationMs}ms)");
                
                if (!step.Success && step.Error != null)
                {
                    output.AppendLine($"   ❌ Error: {step.Error}");
                }
            }
            output.AppendLine();
            
            output.AppendLine("## 🎯 Final Result");
            output.AppendLine();
            output.AppendLine(result.FinalResult ?? "Task completed");
        }
        else
        {
            output.AppendLine("# ❌ Task Failed");
            output.AppendLine();
            output.AppendLine($"**Request:** {result.OriginalRequest}");
            output.AppendLine($"**Error:** {result.Error}");
            output.AppendLine();
            
            if (result.Steps.Any())
            {
                output.AppendLine("## Completed Steps:");
                foreach (var step in result.Steps.Where(s => s.Success))
                {
                    output.AppendLine($"- ✅ {step.ToolName}");
                }
            }
        }

        return output.ToString();
    }

    private string HandleListAvailableTools(Dictionary<string, object> arguments)
    {
        var categoryStr = GetStringArg(arguments, "category", "all").ToLowerInvariant();
        
        IEnumerable<ToolDefinition> tools;
        
        // Filter by category if specified
        if (categoryStr == "all")
        {
            tools = _toolRegistry.GetAllTools();
        }
        else
        {
            var toolCategory = categoryStr switch
            {
                "search" => ToolCategory.Search,
                "index" => ToolCategory.Index,
                "analysis" => ToolCategory.Analysis,
                "validation" => ToolCategory.Validation,
                "planning" => ToolCategory.Planning,
                "todo" => ToolCategory.Todo,
                "codegen" => ToolCategory.CodeGen,
                "design" => ToolCategory.Design,
                "knowledge" => ToolCategory.Knowledge,
                "status" => ToolCategory.Status,
                "control" => ToolCategory.Control,
                "other" => ToolCategory.Other,
                _ => (ToolCategory?)null
            };
            
            tools = toolCategory.HasValue 
                ? _toolRegistry.GetToolsByCategory(toolCategory.Value)
                : _toolRegistry.GetAllTools();
        }

        var output = new System.Text.StringBuilder();
        var toolCount = tools.Count();
        output.AppendLine($"# 🛠️ Available Tools ({toolCount})");
        output.AppendLine();
        
        // ⚠️ IMPORTANT: Limit response size for Cursor MCP client (max ~5KB)
        const int MAX_RESPONSE_SIZE = 5000;
        bool truncated = false;

        // Group by category first, then by service
        var groupedTools = tools.GroupBy(t => t.Category);
        
        foreach (var categoryGroup in groupedTools.OrderBy(g => g.Key))
        {
            var categoryIcon = GetCategoryIcon(categoryGroup.Key);
            output.AppendLine($"## {categoryIcon} {categoryGroup.Key} ({categoryGroup.Count()} tools)");
            output.AppendLine();

            var serviceGroups = categoryGroup.GroupBy(t => t.Service);
            
            foreach (var serviceGroup in serviceGroups)
            {
                var serviceIcon = serviceGroup.Key == "memory-agent" ? "🧠" : "🎯";
                output.AppendLine($"### {serviceIcon} {serviceGroup.Key}");
                output.AppendLine();

                foreach (var tool in serviceGroup.OrderBy(t => t.Name))
                {
                    // Check size limit before adding more content
                    if (output.Length > MAX_RESPONSE_SIZE)
                    {
                        truncated = true;
                        break;
                    }
                    
                    output.AppendLine($"#### `{tool.Name}`");
                    output.AppendLine(tool.Description);
                    
                    // Only include use cases if we have room
                    if (tool.UseCases.Any() && output.Length < MAX_RESPONSE_SIZE - 500)
                    {
                        output.AppendLine($"**Use Cases:** {string.Join(", ", tool.UseCases.Take(3))}");
                    }
                    
                    output.AppendLine();
                }
                
                if (truncated) break;
            }
            
            if (truncated) break;
        }
        
        if (truncated)
        {
            output.AppendLine();
            output.AppendLine("---");
            output.AppendLine($"⚠️ **Response truncated** - showing partial list due to size limits.");
            output.AppendLine($"💡 Use category filter for specific tools: `list_available_tools` with `category` parameter");
            output.AppendLine($"   Available categories: search, planning, codegen, design, validation, status");
        }

        return output.ToString();
    }

    private static string GetCategoryIcon(ToolCategory category) => category switch
    {
        ToolCategory.Search => "🔍",
        ToolCategory.Index => "📦",
        ToolCategory.Analysis => "🔬",
        ToolCategory.Validation => "✅",
        ToolCategory.Planning => "📋",
        ToolCategory.Todo => "📝",
        ToolCategory.CodeGen => "🚀",
        ToolCategory.Design => "🎨",
        ToolCategory.Knowledge => "🧠",
        ToolCategory.Status => "📊",
        ToolCategory.Control => "🛑",
        ToolCategory.Other => "🔧",
        _ => "❓"
    };

    private static string GetStringArg(Dictionary<string, object> args, string key, string defaultValue = "")
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.GetString() ?? defaultValue;
            }
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    private static bool GetBoolArg(Dictionary<string, object> args, string key, bool defaultValue = false)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind == JsonValueKind.True || 
                       (jsonElement.ValueKind == JsonValueKind.String && 
                        bool.TryParse(jsonElement.GetString(), out var boolValue) && boolValue);
            }
            if (value is bool boolVal)
            {
                return boolVal;
            }
            if (value is string strVal && bool.TryParse(strVal, out var parsedBool))
            {
                return parsedBool;
            }
        }
        return defaultValue;
    }

    #region Workflow Tracking

    /// <summary>
    /// Get status of a specific workflow for Cursor auto-polling
    /// </summary>
    private string HandleGetWorkflowStatus(Dictionary<string, object> arguments)
    {
        var workflowId = GetStringArg(arguments, "workflowId");
        
        if (string.IsNullOrEmpty(workflowId))
        {
            return "❌ Error: 'workflowId' parameter is required";
        }

        _logger.LogInformation("📊 Getting workflow status for: {WorkflowId}", workflowId);

        // Check if we have a tracker
        if (!_workflowTrackers.TryGetValue(workflowId, out var tracker))
        {
            // Check if we have a completed result
            if (_workflowResults.TryGetValue(workflowId, out var result))
            {
                return FormatCompletedWorkflowStatus(workflowId, result);
            }
            
            return $"❌ Workflow `{workflowId}` not found. Use `list_workflows` to see available workflows.";
        }

        var output = new System.Text.StringBuilder();
        var statusIcon = tracker.Status switch
        {
            "running" => "🔄",
            "completed" => "✅",
            "failed" => "❌",
            _ => "❓"
        };

        output.AppendLine($"# {statusIcon} Workflow Status: **{tracker.Status.ToUpper()}**");
        output.AppendLine();
        output.AppendLine($"| Field | Value |");
        output.AppendLine($"|-------|-------|");
        output.AppendLine($"| **Workflow ID** | `{tracker.WorkflowId}` |");
        output.AppendLine($"| **Request** | {tracker.Request} |");
        output.AppendLine($"| **Progress** | {tracker.Progress}% |");
        output.AppendLine($"| **Current Step** | {tracker.CurrentStep} |");
        output.AppendLine($"| **Started** | {tracker.StartedAt:HH:mm:ss} UTC |");
        
        var elapsed = (DateTime.UtcNow - tracker.StartedAt).TotalSeconds;
        output.AppendLine($"| **Elapsed** | {elapsed:F1}s |");
        
        if (tracker.EstimatedDurationMs > 0)
        {
            var remaining = Math.Max(0, (tracker.EstimatedDurationMs / 1000.0) - elapsed);
            output.AppendLine($"| **Est. Remaining** | ~{remaining:F0}s |");
        }
        
        output.AppendLine();

        // Show nested jobs if any
        if (tracker.NestedJobs.Any())
        {
            output.AppendLine("## 🔗 Nested Jobs");
            output.AppendLine();
            output.AppendLine("| Job ID | Type | Status | Progress |");
            output.AppendLine("|--------|------|--------|----------|");
            
            foreach (var job in tracker.NestedJobs)
            {
                var jobIcon = job.Status switch
                {
                    "running" => "🔄",
                    "completed" => "✅",
                    "failed" => "❌",
                    _ => "❓"
                };
                output.AppendLine($"| `{job.JobId}` | {job.Type} | {jobIcon} {job.Status} | {job.Progress}% |");
            }
            output.AppendLine();
            output.AppendLine($"💡 **Tip:** Use `get_task_status` with a job ID to get detailed progress for code generation jobs.");
            output.AppendLine();
        }

        // Show final result if completed
        if (tracker.Status == "completed" && !string.IsNullOrEmpty(tracker.FinalResult))
        {
            output.AppendLine("## 🎯 Final Result");
            output.AppendLine();
            output.AppendLine(tracker.FinalResult);
        }
        else if (tracker.Status == "failed" && !string.IsNullOrEmpty(tracker.Error))
        {
            output.AppendLine("## ❌ Error");
            output.AppendLine();
            output.AppendLine(tracker.Error);
        }

        // Include structured JSON for programmatic parsing
        var statusJson = new
        {
            workflowId = tracker.WorkflowId,
            status = tracker.Status,
            progress = tracker.Progress,
            currentStep = tracker.CurrentStep,
            elapsedMs = (long)(DateTime.UtcNow - tracker.StartedAt).TotalMilliseconds,
            nestedJobs = tracker.NestedJobs.Select(j => new { j.JobId, j.Type, j.Status, j.Progress }),
            canPoll = tracker.Status == "running"
        };
        var json = JsonSerializer.Serialize(statusJson, _jsonOptions);
        
        output.AppendLine();
        output.AppendLine("<!-- MCP_WORKFLOW_STATUS");
        output.AppendLine(json);
        output.AppendLine("-->");

        return output.ToString();
    }

    /// <summary>
    /// List all active and recent workflows
    /// </summary>
    private string HandleListWorkflows(Dictionary<string, object> arguments)
    {
        var includeCompleted = GetBoolArg(arguments, "includeCompleted", false);
        
        _logger.LogInformation("📋 Listing workflows (includeCompleted: {Include})", includeCompleted);

        var output = new System.Text.StringBuilder();
        output.AppendLine("# 📋 Active Workflows");
        output.AppendLine();

        var trackers = _workflowTrackers.Values
            .Where(t => includeCompleted || t.Status == "running")
            .OrderByDescending(t => t.StartedAt)
            .Take(20)
            .ToList();

        if (!trackers.Any())
        {
            output.AppendLine("No active workflows. Start a new task with `execute_task`!");
            return output.ToString();
        }

        output.AppendLine("| Status | Workflow ID | Request | Progress | Started |");
        output.AppendLine("|--------|-------------|---------|----------|---------|");

        foreach (var t in trackers)
        {
            var icon = t.Status switch
            {
                "running" => "🔄",
                "completed" => "✅",
                "failed" => "❌",
                _ => "❓"
            };
            var requestPreview = t.Request.Length > 40 ? t.Request.Substring(0, 37) + "..." : t.Request;
            output.AppendLine($"| {icon} | `{t.WorkflowId.Substring(0, 8)}...` | {requestPreview} | {t.Progress}% | {t.StartedAt:HH:mm:ss} |");
        }

        output.AppendLine();
        output.AppendLine($"**Total:** {trackers.Count} workflow(s)");
        output.AppendLine();
        output.AppendLine("💡 Use `get_workflow_status` with a workflow ID to get detailed status.");

        return output.ToString();
    }

    /// <summary>
    /// Format status for a completed workflow from stored results
    /// </summary>
    private string FormatCompletedWorkflowStatus(string workflowId, WorkflowResult result)
    {
        var output = new System.Text.StringBuilder();
        var icon = result.Success ? "✅" : "❌";
        
        output.AppendLine($"# {icon} Workflow Completed");
        output.AppendLine();
        output.AppendLine($"| Field | Value |");
        output.AppendLine($"|-------|-------|");
        output.AppendLine($"| **Workflow ID** | `{workflowId}` |");
        output.AppendLine($"| **Request** | {result.OriginalRequest} |");
        output.AppendLine($"| **Status** | {(result.Success ? "✅ Success" : "❌ Failed")} |");
        output.AppendLine($"| **Duration** | {result.TotalDurationMs}ms |");
        output.AppendLine();

        if (result.Success && !string.IsNullOrEmpty(result.FinalResult))
        {
            output.AppendLine("## 🎯 Final Result");
            output.AppendLine();
            output.AppendLine(result.FinalResult);
        }
        else if (!result.Success && !string.IsNullOrEmpty(result.Error))
        {
            output.AppendLine("## ❌ Error");
            output.AppendLine();
            output.AppendLine(result.Error);
        }

        return output.ToString();
    }

    /// <summary>
    /// Estimate workflow duration based on request content
    /// </summary>
    private static long EstimateWorkflowDuration(string request)
    {
        var lower = request.ToLowerInvariant();
        
        // Code generation is slowest (1-2 minutes)
        if (lower.Contains("create") || lower.Contains("generate") || lower.Contains("build"))
            return 90000; // 90 seconds
        
        // Indexing depends on scope
        if (lower.Contains("index"))
            return lower.Contains("file") ? 30000 : 120000; // 30s for file, 2min for directory
        
        // Search operations are fast
        if (lower.Contains("search") || lower.Contains("find"))
            return 10000; // 10 seconds
        
        // Default
        return 30000; // 30 seconds
    }

    /// <summary>
    /// Extract nested job IDs from workflow result (e.g., CodingOrchestrator jobs)
    /// </summary>
    private void ExtractNestedJobIds(WorkflowResult result, WorkflowTracker tracker)
    {
        if (result.FinalResult == null) return;

        // Look for CodingOrchestrator job IDs: job_YYYYMMDDHHMMSS_XXXXXXXX
        var jobIdPattern = new Regex(@"job_\d{14}_[a-f0-9]+", RegexOptions.IgnoreCase);
        var matches = jobIdPattern.Matches(result.FinalResult);

        foreach (Match match in matches)
        {
            var jobId = match.Value;
            if (!tracker.NestedJobs.Any(j => j.JobId == jobId))
            {
                tracker.NestedJobs.Add(new NestedJob
                {
                    JobId = jobId,
                    Type = "orchestrate_task",
                    Status = "running",
                    Progress = 0,
                    StartedAt = DateTime.UtcNow
                });
                
                _logger.LogInformation("🔗 [Workflow {WorkflowId}] Found nested job: {JobId}", 
                    tracker.WorkflowId, jobId);
            }
        }

        // Also look in step results
        foreach (var step in result.Steps)
        {
            if (step.Result is string stepResult)
            {
                var stepMatches = jobIdPattern.Matches(stepResult);
                foreach (Match match in stepMatches)
                {
                    var jobId = match.Value;
                    if (!tracker.NestedJobs.Any(j => j.JobId == jobId))
                    {
                        tracker.NestedJobs.Add(new NestedJob
                        {
                            JobId = jobId,
                            Type = step.ToolName,
                            Status = "running",
                            Progress = 0,
                            StartedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Intercept requests that should be handled locally (not routed through AI)
    /// Returns null if not intercepted, otherwise returns the result
    /// </summary>
    private string? TryInterceptLocalTools(string request)
    {
        var lowerRequest = request.ToLowerInvariant();
        
        // Check for workflow status with UUID
        if (lowerRequest.Contains("workflow") && (lowerRequest.Contains("status") || lowerRequest.Contains("progress")))
        {
            var uuidMatch = Regex.Match(request, @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})", RegexOptions.IgnoreCase);
            if (uuidMatch.Success)
            {
                return HandleGetWorkflowStatus(new Dictionary<string, object> { ["workflowId"] = uuidMatch.Value });
            }
        }
        
        // Check for UUID status (without "workflow" keyword)
        if (lowerRequest.Contains("status"))
        {
            var uuidMatch = Regex.Match(request, @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})", RegexOptions.IgnoreCase);
            // Only intercept if it's a UUID (workflow) and NOT a job_ ID (CodingOrchestrator)
            if (uuidMatch.Success && !Regex.IsMatch(request, @"job_\d{14}_[a-f0-9]+", RegexOptions.IgnoreCase))
            {
                return HandleGetWorkflowStatus(new Dictionary<string, object> { ["workflowId"] = uuidMatch.Value });
            }
        }
        
        // Check for list workflows
        if (lowerRequest.Contains("list") && lowerRequest.Contains("workflow"))
        {
            return HandleListWorkflows(new Dictionary<string, object> { ["includeCompleted"] = true });
        }
        
        // Not intercepted - let the AI router handle it
        return null;
    }

    #endregion
}


