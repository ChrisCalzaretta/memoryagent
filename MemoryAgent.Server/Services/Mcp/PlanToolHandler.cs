using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// Handles MCP tools for development plan management
/// Tools: create_plan, get_plan_status, update_task_status, complete_plan, search_plans, validate_task
/// </summary>
public class PlanToolHandler : IMcpToolHandler
{
    private readonly IPlanService _planService;
    private readonly IRecommendationService _recommendationService;
    private readonly ITaskValidationService _validationService;
    private readonly IIntentClassificationService _intentClassifier;
    private readonly ILogger<PlanToolHandler> _logger;

    public PlanToolHandler(
        IPlanService planService,
        IRecommendationService recommendationService,
        ITaskValidationService validationService,
        IIntentClassificationService intentClassifier,
        ILogger<PlanToolHandler> logger)
    {
        _planService = planService;
        _recommendationService = recommendationService;
        _validationService = validationService;
        _intentClassifier = intentClassifier;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "create_plan",
                Description = "Create a development plan with tasks and dependencies. Optionally include AI-recommended architecture tasks.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context" },
                        name = new { type = "string", description = "Plan name" },
                        description = new { type = "string", description = "Plan description" },
                        tasks = new { type = "array", items = new { type = "object" }, description = "Array of tasks" },
                        include_recommendations = new { type = "boolean", description = "Auto-generate tasks from architecture recommendations", @default = false },
                        max_recommendations = new { type = "number", description = "Max recommended tasks to add", @default = 10 },
                        recommendation_categories = new { type = "array", items = new { type = "string" }, description = "Specific categories to recommend (optional)" }
                    },
                    required = new[] { "context", "name", "tasks" }
                }
            },
            new McpTool
            {
                Name = "get_plan_status",
                Description = "Get the status and progress of a development plan",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" }
                    },
                    required = new[] { "planId" }
                }
            },
            new McpTool
            {
                Name = "update_task_status",
                Description = "Update the status of a task in a plan",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" },
                        taskId = new { type = "string", description = "Task ID" },
                        status = new { type = "string", description = "New status: Pending, InProgress, Blocked, Completed, Cancelled" }
                    },
                    required = new[] { "planId", "taskId", "status" }
                }
            },
            new McpTool
            {
                Name = "complete_plan",
                Description = "Mark a development plan as completed",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" }
                    },
                    required = new[] { "planId" }
                }
            },
            new McpTool
            {
                Name = "search_plans",
                Description = "Search and filter development plans",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Filter by context" },
                        status = new { type = "string", description = "Filter by status: Draft, Active, Completed, Cancelled, OnHold" }
                    }
                }
            },
            new McpTool
            {
                Name = "validate_task",
                Description = "Validate a task against its rules before completion. Checks for required tests, files, code quality, etc. Can auto-fix validation failures if enabled.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" },
                        taskId = new { type = "string", description = "Task ID to validate" },
                        autoFix = new { type = "boolean", description = "Automatically fix validation failures (e.g., create missing tests)", @default = false }
                    },
                    required = new[] { "planId", "taskId" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "create_plan" => await CreatePlanToolAsync(args, cancellationToken),
            "get_plan_status" => await GetPlanStatusToolAsync(args, cancellationToken),
            "update_task_status" => await UpdateTaskStatusToolAsync(args, cancellationToken),
            "complete_plan" => await CompletePlanToolAsync(args, cancellationToken),
            "search_plans" => await SearchPlansToolAsync(args, cancellationToken),
            "validate_task" => await ValidateTaskToolAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    private async Task<McpToolResult> CreatePlanToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";
        var name = args?.GetValueOrDefault("name")?.ToString() ?? "";
        var description = args?.GetValueOrDefault("description")?.ToString() ?? "";
        var includeRecommendations = args?.TryGetValue("include_recommendations", out var incRec) == true && SafeParseBool(incRec, false);
        var maxRecommendations = args?.TryGetValue("max_recommendations", out var maxRec) == true ? SafeParseInt(maxRec, 10) : 10;
        
        var tasks = new List<PlanTaskRequest>();
        
        // Add manual tasks if provided
        if (args?.TryGetValue("tasks", out var tasksArr) == true && tasksArr is IEnumerable<object> tasksList)
        {
            int index = 0;
            foreach (var taskElem in tasksList)
            {
                if (taskElem is Dictionary<string, object> taskDict)
                {
                    tasks.Add(new PlanTaskRequest
                    {
                        Title = taskDict.GetValueOrDefault("title")?.ToString() ?? "",
                        Description = taskDict.GetValueOrDefault("description")?.ToString() ?? "",
                        OrderIndex = (taskDict.GetValueOrDefault("orderIndex") as int?) ?? index++,
                        Dependencies = new List<string>()
                    });
                }
            }
        }

        // Auto-generate recommended tasks based on architecture patterns
        if (includeRecommendations)
        {
            _logger.LogInformation("🎯 Analyzing {Context} for architecture recommendations...", context);
            
            // 🧠 STEP 1: Classify user intent using LLM
            var userRequest = $"{name}. {description}";
            var intent = await _intentClassifier.ClassifyIntentAsync(userRequest, context, cancellationToken);
            
            _logger.LogInformation("🎯 Intent classified: {ProjectType} / {Goal} / Tech: [{Technologies}] / Confidence: {Confidence:P0}",
                intent.ProjectType, intent.PrimaryGoal, string.Join(", ", intent.Technologies), intent.Confidence);
            
            // 🎯 STEP 2: Get smart category suggestions from intent
            var suggestedCategories = await _intentClassifier.SuggestPatternCategoriesAsync(intent, cancellationToken);
            
            // Parse user-provided recommendation categories filter
            var categories = new List<PatternCategory>();
            if (args?.TryGetValue("recommendation_categories", out var catObj) == true && catObj is IEnumerable<object> catList)
            {
                foreach (var cat in catList)
                {
                    if (Enum.TryParse<PatternCategory>(cat.ToString(), out var category))
                        categories.Add(category);
                }
            }
            
            // Merge user categories with AI-suggested categories
            if (!categories.Any())
            {
                categories.AddRange(suggestedCategories);
                _logger.LogInformation("🤖 Using AI-suggested categories: {Categories}", string.Join(", ", categories));
            }

            // 📋 STEP 3: Get architecture recommendations
            var recRequest = new RecommendationRequest
            {
                Context = context,
                Categories = categories.Any() ? categories : null,
                IncludeLowPriority = false,
                MaxRecommendations = maxRecommendations
            };

            var recommendations = await _recommendationService.AnalyzeAndRecommendAsync(recRequest, cancellationToken);

            // Convert high/critical priority recommendations to plan tasks
            int taskIndex = tasks.Count;
            foreach (var recommendation in recommendations.Recommendations
                .Where(r => r.Priority == "HIGH" || r.Priority == "CRITICAL")
                .Take(maxRecommendations))
            {
                tasks.Add(new PlanTaskRequest
                {
                    Title = $"[{recommendation.Category}] {recommendation.Issue}",
                    Description = $"{recommendation.Recommendation}\n\n" +
                                 $"🎯 Impact: {recommendation.Impact}\n" +
                                 $"🔗 Docs: {recommendation.AzureUrl ?? "N/A"}\n" +
                                 (recommendation.CodeExample != null ? $"\n📝 Example:\n```\n{recommendation.CodeExample}\n```" : ""),
                    OrderIndex = taskIndex++,
                    Dependencies = new List<string>()
                });
            }

            if (tasks.Count > 0)
            {
                _logger.LogInformation("✅ Added {Count} architecture-recommended tasks from {Total} total recommendations", 
                    tasks.Count, recommendations.Recommendations.Count);
            }
        }

        var request = new AddPlanRequest
        {
            Context = context,
            Name = name,
            Description = description,
            Tasks = tasks
        };

        var plan = await _planService.AddPlanAsync(request, cancellationToken);

        var resultText = $"✅ Development Plan created!\n\n" +
                        $"ID: {plan.Id}\n" +
                        $"Name: {plan.Name}\n" +
                        $"Status: {plan.Status}\n" +
                        $"Tasks: {plan.Tasks.Count}\n" +
                        $"Created: {plan.CreatedAt:yyyy-MM-dd HH:mm}";

        if (includeRecommendations && tasks.Any())
        {
            var recommendedCount = tasks.Count(t => t.Title.StartsWith("["));
            resultText += $"\n\n🎯 Architecture Recommendations: {recommendedCount} tasks auto-generated based on detected patterns";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = resultText
                }
            }
        };
    }

    private async Task<McpToolResult> GetPlanStatusToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString() ?? "";
        var plan = await _planService.GetPlanAsync(planId, cancellationToken);

        if (plan == null)
            return ErrorResult($"Plan not found: {planId}");

        var total = plan.Tasks.Count;
        var completed = plan.Tasks.Count(t => t.Status == Models.TaskStatus.Completed);
        var inProgress = plan.Tasks.Count(t => t.Status == Models.TaskStatus.InProgress);
        var pending = plan.Tasks.Count(t => t.Status == Models.TaskStatus.Pending);
        var progress = total > 0 ? (double)completed / total * 100 : 0;

        var text = $"📋 {plan.Name}\n\n" +
                   $"Status: {plan.Status}\n" +
                   $"Progress: {progress:F1}% ({completed}/{total} tasks completed)\n\n" +
                   $"Tasks:\n" +
                   string.Join("\n", plan.Tasks.OrderBy(t => t.OrderIndex).Select(t =>
                       $"  {(t.Status == Models.TaskStatus.Completed ? "✅" : t.Status == Models.TaskStatus.InProgress ? "🔄" : t.Status == Models.TaskStatus.Blocked ? "🚫" : "⏳")} {t.Title} ({t.Status})"));

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> UpdateTaskStatusToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString() ?? "";
        var taskId = args?.GetValueOrDefault("taskId")?.ToString() ?? "";
        var statusStr = args?.GetValueOrDefault("status")?.ToString() ?? "";
        var status = Enum.Parse<Models.TaskStatus>(statusStr);

        var plan = await _planService.UpdateTaskStatusAsync(planId, taskId, status, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Task status updated!\n\n" +
                           $"Plan: {plan.Name}\n" +
                           $"Progress: {(double)plan.Tasks.Count(t => t.Status == Models.TaskStatus.Completed) / plan.Tasks.Count * 100:F1}%"
                }
            }
        };
    }

    private async Task<McpToolResult> CompletePlanToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString() ?? "";
        var plan = await _planService.CompletePlanAsync(planId, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Plan completed!\n\n" +
                           $"Name: {plan.Name}\n" +
                           $"Completed: {plan.CompletedAt:yyyy-MM-dd HH:mm}\n" +
                           $"Total tasks: {plan.Tasks.Count}"
                }
            }
        };
    }

    private async Task<McpToolResult> SearchPlansToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.TryGetValue("context", out var ctx) == true ? ctx?.ToString()?.ToLowerInvariant() : null;
        var statusStr = args?.TryGetValue("status", out var stat) == true ? stat?.ToString() : null;
        PlanStatus? status = statusStr != null ? Enum.Parse<PlanStatus>(statusStr) : null;

        var plans = await _planService.GetPlansAsync(context, status, cancellationToken);

        var text = plans.Any()
            ? $"Found {plans.Count} plan(s):\n\n" +
              string.Join("\n\n", plans.Select(p =>
              {
                  var total = p.Tasks.Count;
                  var completed = p.Tasks.Count(t => t.Status == Models.TaskStatus.Completed);
                  var progress = total > 0 ? (double)completed / total * 100 : 0;
                  return $"📋 {p.Name}\n" +
                         $"   ID: {p.Id}\n" +
                         $"   Status: {p.Status}\n" +
                         $"   Progress: {progress:F1}% ({completed}/{total})\n" +
                         $"   Created: {p.CreatedAt:yyyy-MM-dd}";
              }))
            : "No plans found.";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateTaskToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString()!;
        var taskId = args?.GetValueOrDefault("taskId")?.ToString()!;
        var autoFix = args?.TryGetValue("autoFix", out var fix) == true && (fix as bool?) == true;

        var plan = await _planService.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
            return ErrorResult($"Plan not found: {planId}");

        var task = plan.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null)
            return ErrorResult($"Task not found: {taskId}");

        var validationResult = await _validationService.ValidateTaskAsync(task, plan.Context, cancellationToken);

        if (validationResult.IsValid)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"✅ Task '{task.Title}' passed all validation rules!\n\n" +
                               "Task is ready to be marked as completed."
                    }
                }
            };
        }

        // Build failure text with actionable context
        var failureText = $"❌ Task '{task.Title}' failed validation:\n\n";
        foreach (var failure in validationResult.Failures)
        {
            failureText += $"• {failure.RuleType}: {failure.Message}\n";
            if (failure.CanAutoFix)
                failureText += $"  💡 Auto-fix available: {failure.FixDescription}\n";
            
            if (failure.ActionableContext.Any())
            {
                failureText += AddActionableContext(failure.ActionableContext);
            }
            failureText += "\n";
        }

        // Auto-fix if requested
        if (autoFix)
        {
            failureText += "\n🔧 Attempting auto-fix...\n";
            var wasFixed = await _validationService.AutoFixValidationFailuresAsync(task, validationResult, plan.Context, cancellationToken);
            failureText += wasFixed ? "✅ Auto-fix completed! Please re-validate to confirm.\n" : "❌ Auto-fix failed. Manual intervention required.\n";
        }
        else if (validationResult.Suggestions.Any())
        {
            failureText += "\n💡 Suggestions:\n";
            foreach (var suggestion in validationResult.Suggestions)
                failureText += $"• {suggestion}\n";
            failureText += "\nRun with autoFix: true to automatically fix these issues.\n";
        }

        return new McpToolResult
        {
            IsError = !autoFix,
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = failureText }
            }
        };
    }

    private string AddActionableContext(Dictionary<string, object> context)
    {
        var text = "  📋 Details:\n";
        
        if (context.ContainsKey("suggestion"))
            text += $"     {context["suggestion"]}\n";
        
        if (context.ContainsKey("methods_to_test") && context["methods_to_test"] is IEnumerable<object> methodList)
        {
            text += "\n     Methods needing tests:\n";
            var methodArray = methodList.Take(5).ToArray();
            for (int i = 0; i < methodArray.Length && i < 5; i++)
            {
                var method = methodArray[i];
                var nameProperty = method.GetType().GetProperty("Name");
                if (nameProperty != null)
                {
                    var methodName = nameProperty.GetValue(method)?.ToString();
                    text += $"       - {methodName}\n";
                }
            }
            
            var totalCount = context.ContainsKey("method_count") ? SafeParseInt(context["method_count"], 0) : 0;
            if (totalCount > 5)
                text += $"       ... and {totalCount - 5} more\n";
        }
        
        if (context.ContainsKey("example_test_names") && context["example_test_names"] is IEnumerable<object> exampleList)
        {
            text += "\n     Example test names:\n";
            foreach (var example in exampleList.Take(3))
                text += $"       - {example}\n";
        }
        
        return text;
    }

    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            _ => defaultValue
        };

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, out var i) => i,
            _ => defaultValue
        };

    private McpToolResult ErrorResult(string error) => new McpToolResult
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };
}

