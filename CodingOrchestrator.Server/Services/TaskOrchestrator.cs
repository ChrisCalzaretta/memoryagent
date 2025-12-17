using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;
using CodingOrchestrator.Server.Clients;
using System.Diagnostics;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Orchestrates the multi-agent coding workflow
/// NOW WITH DOCKER EXECUTION! Code is actually run before validation.
/// </summary>
public class TaskOrchestrator : ITaskOrchestrator
{
    private readonly ICodingAgentClient _codingAgent;
    private readonly IValidationAgentClient _validationAgent;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly IExecutionService _executionService;
    private readonly IPathTranslationService _pathTranslation;
    private readonly IDotnetScaffoldService _scaffoldService;
    private readonly ILogger<TaskOrchestrator> _logger;
    private IJobManager? _jobManager;
    
    // 📊 OpenTelemetry ActivitySource for distributed tracing
    private static readonly ActivitySource _activitySource = new("CodingOrchestrator.TaskOrchestrator");

    public TaskOrchestrator(
        ICodingAgentClient codingAgent,
        IValidationAgentClient validationAgent,
        IMemoryAgentClient memoryAgent,
        IExecutionService executionService,
        IPathTranslationService pathTranslation,
        IDotnetScaffoldService scaffoldService,
        ILogger<TaskOrchestrator> logger)
    {
        _codingAgent = codingAgent;
        _validationAgent = validationAgent;
        _memoryAgent = memoryAgent;
        _executionService = executionService;
        _pathTranslation = pathTranslation;
        _scaffoldService = scaffoldService;
        _logger = logger;
        
        _logger.LogInformation("🔧 TaskOrchestrator v2025.12.16.C - with null-safe lastValidation");
    }

    /// <summary>
    /// Set the job manager (used to break circular dependency)
    /// </summary>
    public void SetJobManager(IJobManager jobManager)
    {
        _jobManager = jobManager;
    }

    public Task<TaskStatusResponse> ExecuteTaskAsync(OrchestrateTaskRequest request, CancellationToken cancellationToken)
    {
        return ExecuteTaskAsync(request, null, cancellationToken);
    }

    public async Task<TaskStatusResponse> ExecuteTaskAsync(OrchestrateTaskRequest request, string? jobId, CancellationToken cancellationToken)
    {
        // 📊 Start a trace for this task execution
        using var activity = _activitySource.StartActivity("ExecuteTask", ActivityKind.Server);
        activity?.SetTag("task.id", jobId ?? "inline");
        activity?.SetTag("task.language", request.Language ?? "auto");
        activity?.SetTag("task.context", request.Context);
        activity?.SetTag("task.max_iterations", request.MaxIterations);
        activity?.SetTag("task.min_score", request.MinValidationScore);
        
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting orchestration for task: {Task}", request.Task);

        var response = new TaskStatusResponse
        {
            JobId = jobId ?? Guid.NewGuid().ToString("N"),
            Status = TaskState.Running,
            MaxIterations = request.MaxIterations,
            Timeline = new List<PhaseInfo>()
        };

        try
        {
            // 🧠 Phase 0: Estimate complexity and adjust iterations dynamically
            UpdateProgress(jobId, TaskState.Running, 5, "Estimating complexity", 0);
            var estimatePhase = StartPhase("complexity_estimation");
            
            var estimateRequest = new EstimateComplexityRequest
            {
                Task = request.Task,
                Language = request.Language,
                Context = request.Context
            };
            
            var complexity = await _codingAgent.EstimateComplexityAsync(estimateRequest, cancellationToken);
            
            // Use user's max iterations as hard cap - don't let LLM override it
            // The user knows their time/resource budget
            var effectiveMaxIterations = request.MaxIterations;
            if (complexity.Success && complexity.RecommendedIterations > request.MaxIterations)
            {
                _logger.LogWarning("🧠 LLM recommended {Recommended} iterations but user set max={Max}. Respecting user's limit.",
                    complexity.RecommendedIterations, request.MaxIterations);
            }
            
            // Update response with effective iterations
            response.MaxIterations = effectiveMaxIterations;
            
            estimatePhase.Details = new Dictionary<string, object>
            {
                ["complexityLevel"] = complexity.ComplexityLevel,
                ["recommendedIterations"] = complexity.RecommendedIterations,
                ["effectiveIterations"] = effectiveMaxIterations,
                ["estimatedFiles"] = complexity.EstimatedFiles,
                ["reasoning"] = complexity.Reasoning
            };
            EndPhase(jobId, estimatePhase);
            response.Timeline.Add(estimatePhase);

            // Phase 1: Get context from Lightning (with graceful degradation)
            UpdateProgress(jobId, TaskState.Running, 10, "Gathering context", 0);
            var contextPhase = StartPhase("context_gathering");
            
            // 🛡️ GRACEFUL DEGRADATION: Continue without context if Memory Agent is unavailable
            CodeContext? context = null;
            try
            {
                context = await _memoryAgent.GetContextAsync(request.Task, request.Context, cancellationToken);
                _logger.LogInformation("✅ Retrieved context from Memory Agent");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "⚠️ Memory Agent unavailable - proceeding without context (degraded mode)");
                contextPhase.Details = new Dictionary<string, object>
                {
                    ["status"] = "degraded",
                    ["error"] = ex.Message,
                    ["fallback"] = "Proceeding without historical context"
                };
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "⚠️ Memory Agent timed out - proceeding without context");
                contextPhase.Details = new Dictionary<string, object>
                {
                    ["status"] = "timeout",
                    ["fallback"] = "Proceeding without historical context"
                };
            }
            
            EndPhase(jobId, contextPhase);
            response.Timeline.Add(contextPhase);

            // Phase 2-N: Coding and Validation loop
            GenerateCodeResponse? lastGeneratedCode = null;
            ValidateCodeResponse? lastValidation = null;
            var iteration = 0;
            
            // Track which models have been tried (for smart rotation)
            var triedModels = new HashSet<string>();
            var modelsUsedDuringTask = new List<string>();  // For failure recording
            var approachesTried = new List<string>();  // Track approaches for failure learning
            
            // 🔄 STAGNATION DETECTION: Track repeated errors to prevent infinite loops
            var lastErrorSignature = "";
            var sameErrorCount = 0;
            const int MaxSameErrors = 3;  // Break after 3 identical errors
            
            // 📁 ACCUMULATE files across iterations (not replace!)
            var accumulatedFiles = new Dictionary<string, FileChange>();
            
            // ☁️ Track cloud LLM usage (Anthropic) across iterations
            var cloudUsage = new CloudUsage
            {
                Provider = "anthropic",
                Note = "Check console.anthropic.com for actual balance"
            };
            
            // 🧠 TASK LEARNING: Query lessons from similar failed tasks
            var taskLessons = await QueryLessonsForTaskAsync(request, cancellationToken);
            
            // 📋 SMART CODEGEN Phase: Generate plan before coding
            UpdateProgress(jobId, TaskState.Running, 12, "Generating task plan", 0);
            var planPhase = StartPhase("plan_generation");
            TaskPlan? taskPlan = null;
            ProjectSymbols? projectSymbols = null;
            SimilarTasksResult? similarTasks = null;
            DesignContext? designContext = null;
            
            try
            {
                // 📋 Generate execution plan with checklist
                taskPlan = await _memoryAgent.GeneratePlanAsync(
                    request.Task, 
                    request.Language ?? "python", 
                    request.Context, 
                    cancellationToken);
                _logger.LogInformation("[PLAN] Generated plan with {Steps} steps", taskPlan.Steps.Count);
                
                // 🔍 Get project symbols for context
                projectSymbols = await _memoryAgent.GetProjectSymbolsAsync(request.Context, cancellationToken);
                _logger.LogInformation("🔍 Retrieved {Classes} classes, {Functions} functions", 
                    projectSymbols.Classes.Count, projectSymbols.Functions.Count);
                
                // 🔎 Query similar successful tasks
                similarTasks = await _memoryAgent.QuerySimilarSuccessfulTasksAsync(
                    request.Task, 
                    request.Language ?? "python", 
                    cancellationToken);
                if (similarTasks.FoundTasks > 0)
                {
                    _logger.LogInformation("🔎 Found {Count} similar successful tasks", similarTasks.FoundTasks);
                }
                
                // 🎨 Get design context for UI tasks
                if (IsUITask(request.Task))
                {
                    designContext = await _memoryAgent.GetDesignContextAsync(request.Context, cancellationToken);
                    if (designContext != null)
                    {
                        _logger.LogInformation("🎨 Retrieved design context: {Brand}", designContext.BrandName);
                    }
                }
                
                planPhase.Details = new Dictionary<string, object>
                {
                    ["planId"] = taskPlan.PlanId,
                    ["stepsCount"] = taskPlan.Steps.Count,
                    ["requiredClasses"] = taskPlan.RequiredClasses,
                    ["dependencyOrder"] = taskPlan.DependencyOrder,
                    ["symbolsCount"] = (projectSymbols?.Classes.Count ?? 0) + (projectSymbols?.Functions.Count ?? 0),
                    ["similarTasksFound"] = similarTasks?.FoundTasks ?? 0,
                    ["hasDesignContext"] = designContext != null
                };
                
                // 📋 Set the plan on the job status for display
                if (_jobManager != null)
                {
                    _jobManager.SetJobPlan(jobId, new TaskPlanInfo
                    {
                        RequiredClasses = taskPlan.RequiredClasses,
                        DependencyOrder = taskPlan.DependencyOrder,
                        SemanticBreakdown = taskPlan.SemanticBreakdown,
                        Steps = taskPlan.Steps.Select(s => new PlanStepInfo
                        {
                            Order = s.Order,
                            Description = s.Description,
                            FileName = s.FileName,
                            Status = s.Status
                        }).ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Plan generation failed - proceeding without plan (degraded mode)");
                planPhase.Details = new Dictionary<string, object>
                {
                    ["status"] = "degraded",
                    ["error"] = ex.Message
                };
            }
            EndPhase(jobId, planPhase);
            response.Timeline.Add(planPhase);
            
            // 📦 SCAFFOLD PHASE: Use dotnet new for C# projects to get perfect structure
            ScaffoldResult? scaffoldResult = null;
            var isCSharpProject = request.Language?.ToLowerInvariant() is "csharp" or "cs" or "c#" 
                                  || request.Task.Contains(".NET", StringComparison.OrdinalIgnoreCase)
                                  || request.Task.Contains("C#", StringComparison.OrdinalIgnoreCase);
            
            if (isCSharpProject)
            {
                var scaffoldPhase = StartPhase("project_scaffolding");
                try
                {
                    var projectType = _scaffoldService.DetectProjectType(request.Task);
                    // ALWAYS use "GeneratedApp" - matches what Claude generates by default
                    // This prevents namespace mismatches between scaffold and Claude code
                    var projectName = "GeneratedApp";
                    
                    _logger.LogInformation("📦 Scaffolding {ProjectType} project: {ProjectName}", projectType, projectName);
                    UpdateProgress(jobId, TaskState.Running, 15, $"Scaffolding {projectType} project", 0);
                    
                    scaffoldResult = await _scaffoldService.ScaffoldProjectAsync(projectType, projectName, cancellationToken);
                    
                    if (scaffoldResult.Success)
                    {
                        _logger.LogInformation("✅ Scaffolded {Count} files using dotnet new {Template}", 
                            scaffoldResult.Files.Count, scaffoldResult.TemplateName);
                        
                        // 🎯 Override language for specific project types to use specialized prompts
                        if (scaffoldResult.ProjectType == DotnetProjectType.Blazor || 
                            scaffoldResult.ProjectType == DotnetProjectType.BlazorWasm)
                        {
                            request.Language = "blazor";
                            _logger.LogInformation("🎨 Using Blazor-specific prompt for {ProjectType}", scaffoldResult.ProjectType);
                        }
                        
                        // Pre-populate accumulated files with scaffolded structure
                        foreach (var file in scaffoldResult.Files)
                        {
                            accumulatedFiles[file.Path] = new AgentContracts.Responses.FileChange
                            {
                                Path = file.Path,
                                Content = file.Content,
                                Type = AgentContracts.Responses.FileChangeType.Created
                            };
                        }
                        
                        scaffoldPhase.Details = new Dictionary<string, object>
                        {
                            ["projectType"] = scaffoldResult.ProjectType.ToString(),
                            ["template"] = scaffoldResult.TemplateName,
                            ["filesScaffolded"] = scaffoldResult.Files.Count,
                            ["projectName"] = scaffoldResult.ProjectName
                        };
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Scaffolding failed: {Error} - proceeding without scaffold", scaffoldResult.Error);
                        scaffoldPhase.Details = new Dictionary<string, object>
                        {
                            ["status"] = "failed",
                            ["error"] = scaffoldResult.Error ?? "Unknown error"
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Scaffolding error - proceeding without scaffold");
                    scaffoldPhase.Details = new Dictionary<string, object>
                    {
                        ["status"] = "error",
                        ["error"] = ex.Message
                    };
                }
                EndPhase(jobId, scaffoldPhase);
                response.Timeline.Add(scaffoldPhase);
            }
            
            // 🔀 EXECUTION MODE: Choose between batch (default) or step-by-step
            if (request.ExecutionMode?.ToLowerInvariant() == "stepbystep" && taskPlan?.Steps.Count > 1)
            {
                _logger.LogInformation("[STEP-BY-STEP] Using step-by-step execution mode ({Steps} steps)", taskPlan.Steps.Count);
                return await ExecuteStepByStepAsync(
                    request, jobId, response, taskPlan, projectSymbols, similarTasks, 
                    designContext, context, taskLessons, effectiveMaxIterations, startTime, cancellationToken);
            }
            
            _logger.LogInformation("📦 Using BATCH execution mode (all files at once){ScaffoldInfo}", 
                scaffoldResult?.Success == true ? $" (with {scaffoldResult.Files.Count} scaffolded files)" : "");

            while (iteration < effectiveMaxIterations)
            {
                iteration++;
                cancellationToken.ThrowIfCancellationRequested();

                // Coding Agent phase
                var codingProgress = 10 + (iteration * 80 / effectiveMaxIterations);
                UpdateProgress(jobId, TaskState.Running, codingProgress, $"Coding Agent (iteration {iteration})", iteration);
                
                var codingPhase = StartPhase("coding_agent", iteration);

                // Build feedback with tried models so CodingAgent can rotate
                var feedback = lastValidation?.ToFeedback();
                
                _logger.LogInformation("🔍 Iteration {Iteration}: lastValidation is {NullOrPresent}, feedback is {FeedbackStatus}",
                    iteration, 
                    lastValidation == null ? "NULL" : "present",
                    feedback == null ? "NULL" : "present");
                
                // 🔧 FIX: If we don't have feedback but it's not iteration 1, create a dummy feedback
                // This prevents 400 errors when calling FixAsync with null PreviousFeedback
                if (feedback == null && iteration > 1)
                {
                    _logger.LogWarning("⚠️ No previous validation on iteration {Iteration} - creating dummy feedback", iteration);
                    feedback = new ValidationFeedback
                    {
                        Score = 0,
                        TriedModels = triedModels,
                        Summary = "Previous iteration did not complete validation"
                    };
                }
                
                if (feedback != null)
                {
                    feedback.TriedModels = triedModels;
                    
                    // ✅ BuildErrors is now properly set in ValidateCodeResponse.ToFeedback()
                    // No need for hacky extraction logic here!
                }

                // 📁 Tell the LLM what files already exist WITH their signatures
                var existingFilesList = "";
                if (accumulatedFiles.Any())
                {
                    existingFilesList = $"\n\n📁 FILES ALREADY GENERATED ({accumulatedFiles.Count}):\n";
                    foreach (var file in accumulatedFiles)
                    {
                        existingFilesList += $"\n### {file.Key}\n";
                        // Extract class/interface/function signatures from the content
                        var signatures = ExtractSignatures(file.Value.Content, request.Language ?? "csharp");
                        if (!string.IsNullOrEmpty(signatures))
                        {
                            existingFilesList += $"```\n{signatures}\n```\n";
                        }
                    }
                    existingFilesList += "\n⚠️ Generate the MISSING files. Reference the classes/methods above. You may also update existing files if needed.";
                }

                // 🧠 Include lessons learned in the task description
                var lessonsSection = taskLessons.FoundLessons > 0 
                    ? $"\n\n{taskLessons.AvoidanceAdvice}\n\n✅ SUGGESTED APPROACHES:\n{string.Join("\n", taskLessons.SuggestedApproaches.Select(a => $"- {a}"))}"
                    : "";
                
                // 📋 Include plan context
                var planSection = "";
                if (taskPlan != null && taskPlan.Steps.Any())
                {
                    planSection = $"\n\n📋 EXECUTION PLAN:\n";
                    planSection += $"  Required Classes: {string.Join(", ", taskPlan.RequiredClasses)}\n";
                    planSection += $"  File Order: {string.Join(" → ", taskPlan.DependencyOrder)}\n";
                    planSection += $"  Semantic Breakdown:\n{taskPlan.SemanticBreakdown}\n";
                }
                
                // 🔍 Include project symbols context (DISABLED - causes context pollution)
                // The symbols from the indexed workspace (MemoryAgent) pollute generation
                // of new, unrelated code (e.g., Calculator app gets CodingAgent namespaces)
                // TODO: Re-enable when we can scope symbols to the task, not the whole workspace
                var symbolsSection = "";
                // if (projectSymbols != null && (projectSymbols.Classes.Any() || projectSymbols.Functions.Any()))
                // {
                //     symbolsSection = "\n\n🔍 AVAILABLE SYMBOLS IN PROJECT:\n";
                //     foreach (var cls in projectSymbols.Classes.Take(10))
                //     {
                //         symbolsSection += $"  • {cls.Name}: {cls.ImportStatement}\n";
                //         if (cls.Methods.Any())
                //             symbolsSection += $"    Methods: {string.Join(", ", cls.Methods.Take(5))}\n";
                //     }
                // }
                
                // 🔎 Include similar task guidance
                var similarTasksSection = "";
                if (similarTasks != null && similarTasks.FoundTasks > 0)
                {
                    similarTasksSection = $"\n\n🔎 SIMILAR SUCCESSFUL TASK APPROACH:\n";
                    if (!string.IsNullOrEmpty(similarTasks.SuggestedApproach))
                        similarTasksSection += $"  Approach: {similarTasks.SuggestedApproach}\n";
                    if (!string.IsNullOrEmpty(similarTasks.SuggestedStructure))
                        similarTasksSection += $"  Structure: {similarTasks.SuggestedStructure}\n";
                }
                
                // 🎨 Include design context for UI tasks
                var designSection = "";
                if (designContext != null)
                {
                    designSection = $"\n\n🎨 DESIGN SYSTEM ({designContext.BrandName}):\n";
                    if (designContext.Colors.Any())
                        designSection += $"  Colors: {string.Join(", ", designContext.Colors.Take(5).Select(c => $"{c.Key}={c.Value}"))}\n";
                    if (designContext.AccessibilityRules.Any())
                        designSection += $"  Accessibility: {string.Join(", ", designContext.AccessibilityRules.Take(3))}\n";
                }
                
                // Convert accumulated files to ExistingFile format for the coding agent
                var existingFilesForAgent = accumulatedFiles.Any()
                    ? accumulatedFiles.Select(kv => new ExistingFile 
                      { 
                          Path = kv.Key, 
                          Content = kv.Value.Content 
                      }).ToList()
                    : null;
                
                var generateRequest = new GenerateCodeRequest
                {
                    Task = request.Task + existingFilesList + lessonsSection + planSection + symbolsSection + similarTasksSection + designSection,
                    Language = request.Language,
                    Context = context,
                    WorkspacePath = request.WorkspacePath,
                    PreviousFeedback = feedback,
                    ExistingFiles = existingFilesForAgent  // 🔧 Include existing files for build error fix prompt
                };
                
                _logger.LogInformation("📤 Sending {FileCount} existing files to coding agent (iteration {Iteration})",
                    existingFilesForAgent?.Count ?? 0, iteration);

                try
                {
                    lastGeneratedCode = iteration == 1 
                        ? await _codingAgent.GenerateAsync(generateRequest, cancellationToken)
                        : await _codingAgent.FixAsync(generateRequest, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    _logger.LogError(ex, "❌ Coding agent returned 400 on iteration {Iteration} - ensuring feedback for next iteration", iteration);
                    // Create a fake failure response so we have lastValidation for next iteration
                    lastGeneratedCode = new GenerateCodeResponse
                    {
                        Success = false,
                        Error = $"HTTP 400: {ex.Message}",
                        ModelUsed = "error"
                    };
                }
                
                // Track which model was used
                if (!string.IsNullOrEmpty(lastGeneratedCode.ModelUsed))
                {
                    triedModels.Add(lastGeneratedCode.ModelUsed);
                    modelsUsedDuringTask.Add(lastGeneratedCode.ModelUsed);  // For failure learning
                    _logger.LogDebug("Added {Model} to tried models. Total tried: {Count}",
                        lastGeneratedCode.ModelUsed, triedModels.Count);
                }
                
                // ☁️ Accumulate cloud usage if Claude was used
                if (lastGeneratedCode.CloudUsage != null)
                {
                    cloudUsage.Model = lastGeneratedCode.CloudUsage.Model;
                    cloudUsage.InputTokens += lastGeneratedCode.CloudUsage.InputTokens;
                    cloudUsage.OutputTokens += lastGeneratedCode.CloudUsage.OutputTokens;
                    cloudUsage.EstimatedCost += lastGeneratedCode.CloudUsage.Cost;
                    cloudUsage.TokensRemaining = lastGeneratedCode.CloudUsage.TokensRemaining;
                    cloudUsage.RequestsRemaining = lastGeneratedCode.CloudUsage.RequestsRemaining;
                    cloudUsage.ApiCalls++;
                    
                    _logger.LogInformation("[CLAUDE] Total usage: {Calls} calls, {Tokens} tokens, ${Cost:F4}",
                        cloudUsage.ApiCalls, cloudUsage.InputTokens + cloudUsage.OutputTokens, cloudUsage.EstimatedCost);
                }
                
                // Track approaches tried
                var approachKey = $"Iteration {iteration}: {(iteration == 1 ? "Initial generation" : "Fix attempt")} with {lastGeneratedCode.ModelUsed}";
                approachesTried.Add(approachKey);

                EndPhase(jobId, codingPhase);
                response.Timeline.Add(codingPhase);

                if (!lastGeneratedCode.Success)
                {
                    _logger.LogWarning("Coding agent failed on iteration {Iteration}: {Error}", 
                        iteration, lastGeneratedCode.Error);
                    
                    // Set lastValidation so next iteration's PreviousFeedback isn't null
                    lastValidation = new ValidateCodeResponse
                    {
                        Score = 0,
                        Passed = false,
                        Issues = new List<ValidationIssue>
                        {
                            new ValidationIssue
                            {
                                Severity = "critical",
                                Message = "Code generation failed",
                                Suggestion = lastGeneratedCode.Error ?? "Unknown error from code generation"
                            }
                        },
                        Summary = $"Code generation failed: {lastGeneratedCode.Error}"
                    };
                    continue;
                }

                // 📁 ACCUMULATE files - merge new files with existing ones
                foreach (var file in lastGeneratedCode.FileChanges)
                {
                    accumulatedFiles[file.Path] = file;
                    _logger.LogDebug("📁 Accumulated file: {Path} (total: {Count})", file.Path, accumulatedFiles.Count);
                    
                    // 📊 Track generated file in job status
                    _jobManager?.AddGeneratedFile(jobId, file.Path);
                    
                    // 📊 INDEX IMMEDIATELY: Add to Qdrant+Neo4j for context awareness
                    try
                    {
                        await _memoryAgent.IndexFileAsync(
                            file.Path, 
                            file.Content, 
                            request.Language ?? "python", 
                            request.Context, 
                            cancellationToken);
                        _logger.LogDebug("📊 Indexed file: {Path}", file.Path);
                    }
                    catch (Exception indexEx)
                    {
                        _logger.LogWarning(indexEx, "⚠️ Failed to index file (non-critical): {Path}", file.Path);
                    }
                }
                
                _logger.LogInformation("📁 Iteration {Iteration}: Generated {NewFiles} files, Total accumulated: {Total}",
                    iteration, lastGeneratedCode.FileChanges.Count, accumulatedFiles.Count);

                // ✅ IMPORT VALIDATION: Check imports before Docker execution
                var importValidationPhase = StartPhase("import_validation", iteration);
                var allCode = string.Join("\n\n", accumulatedFiles.Values.Select(f => f.Content));
                
                try
                {
                    var importValidation = await _memoryAgent.ValidateImportsAsync(
                        allCode,
                        request.Language ?? "python",
                        request.Context,
                        cancellationToken);
                    
                    importValidationPhase.Details = new Dictionary<string, object>
                    {
                        ["isValid"] = importValidation.IsValid,
                        ["importsChecked"] = importValidation.Imports.Count,
                        ["summary"] = importValidation.Summary
                    };
                    
                    if (!importValidation.IsValid)
                    {
                        var invalidImports = importValidation.Imports
                            .Where(i => !i.IsValid)
                            .Select(i => $"{i.Module}: {i.Reason}. {i.Suggestion}")
                            .ToList();
                        
                        _logger.LogWarning("⚠️ Invalid imports detected: {Imports}", string.Join(", ", invalidImports.Take(5)));
                        
                        // Create feedback with import errors and loop back
                        lastValidation = new ValidateCodeResponse
                        {
                            Score = 2,
                            Passed = false,
                            Issues = invalidImports.Select(i => new ValidationIssue
                            {
                                Severity = "critical",
                                Message = $"Import error: {i}",
                                File = accumulatedFiles.Keys.FirstOrDefault() ?? "unknown",
                                Line = 1,
                                Suggestion = "Fix the import statement"
                            }).ToList(),
                            Summary = $"Import validation failed:\n{string.Join("\n", invalidImports)}"
                        };
                        
                        EndPhase(jobId, importValidationPhase);
                        response.Timeline.Add(importValidationPhase);
                        
                        _logger.LogInformation("Iteration {Iteration}: Import validation FAILED, looping back", iteration);
                        continue;
                    }
                }
                catch (Exception importEx)
                {
                    _logger.LogWarning(importEx, "⚠️ Import validation failed (proceeding anyway)");
                    importValidationPhase.Details = new Dictionary<string, object>
                    {
                        ["status"] = "skipped",
                        ["error"] = importEx.Message
                    };
                }
                EndPhase(jobId, importValidationPhase);
                response.Timeline.Add(importValidationPhase);
                
                // 🐳 EXECUTION PHASE: Actually run the code in Docker!
                var executionProgress = codingProgress + 5;
                UpdateProgress(jobId, TaskState.Running, executionProgress, $"Docker Execution (iteration {iteration})", iteration);
                
                var executionPhase = StartPhase("docker_execution", iteration);
                
                // 🧹 CLEAN UP C# FILES BEFORE DOCKER BUILD
                // Fixes MSB1011 (multiple .csproj) and duplicate file issues
                if (request.Language?.ToLowerInvariant() is "csharp" or "cs" or "c#")
                {
                    CleanAccumulatedFilesForCSharp(accumulatedFiles);
                }
                
                // Use ALL accumulated files, not just last iteration's!
                var executionFiles = accumulatedFiles.Values.Select(f => new ExecutionFile
                {
                    Path = f.Path,
                    Content = f.Content,
                    ChangeType = (int)f.Type,
                    Reason = f.Reason
                }).ToList();
                
                // 🧠 Pass LLM's execution instructions (if provided)
                var executionResult = await _executionService.ExecuteAsync(
                    request.Language ?? "python",
                    executionFiles,
                    request.WorkspacePath,
                    lastGeneratedCode.Execution,  // LLM tells us how to run it!
                    cancellationToken);
                
                executionPhase.Details = new Dictionary<string, object>
                {
                    ["success"] = executionResult.Success,
                    ["buildPassed"] = executionResult.BuildPassed,
                    ["executionPassed"] = executionResult.ExecutionPassed,
                    ["exitCode"] = executionResult.ExitCode,
                    ["durationMs"] = executionResult.DurationMs,
                    ["dockerImage"] = executionResult.DockerImage,
                    // 📝 Capture errors for status display
                    ["error"] = !executionResult.Success 
                        ? (executionResult.Errors?.Length > 1000 
                            ? executionResult.Errors.Substring(0, 1000) + "..." 
                            : executionResult.Errors ?? "Unknown error")
                        : null,
                    ["output"] = executionResult.Success 
                        ? (executionResult.Output?.Length > 500 
                            ? executionResult.Output.Substring(0, 500) + "..." 
                            : executionResult.Output)
                        : null
                };
                EndPhase(jobId, executionPhase);
                response.Timeline.Add(executionPhase);
                
                // If execution failed, create feedback with REAL errors and loop back
                if (!executionResult.Success)
                {
                    _logger.LogWarning("🐳 Execution failed on iteration {Iteration}: {Errors}", 
                        iteration, executionResult.Errors);
                    
                    // 🔄 STAGNATION DETECTION: Check if we're seeing the same error repeatedly
                    var currentErrorSignature = GetErrorSignature(executionResult.Errors);
                    if (currentErrorSignature == lastErrorSignature)
                    {
                        sameErrorCount++;
                        _logger.LogWarning("⚠️ Same error detected {Count}/{Max} times: {Signature}", 
                            sameErrorCount, MaxSameErrors, currentErrorSignature);
                        
                        if (sameErrorCount >= MaxSameErrors)
                        {
                            _logger.LogError("🛑 STAGNATION DETECTED: Breaking loop after {Count} identical errors", sameErrorCount);
                            lastValidation = new ValidateCodeResponse
                            {
                                Score = 0,
                                Passed = false,
                                BuildErrors = executionResult.Errors, // 🔧 Set build errors for stagnation too!
                                Issues = new List<ValidationIssue>
                                {
                                    new ValidationIssue
                                    {
                                        Severity = "critical",
                                        Message = $"Stagnation detected: Same error repeated {sameErrorCount} times",
                                        Suggestion = $"The LLM cannot fix this error. Manual intervention needed:\n\n{executionResult.Errors}"
                                    }
                                },
                                Summary = $"STAGNATION: Same error repeated {sameErrorCount} times. LLM cannot resolve.\n\nError: {currentErrorSignature}"
                            };
                            break; // Exit the loop - we're stuck
                        }
                    }
                    else
                    {
                        // New error - reset counter
                        lastErrorSignature = currentErrorSignature;
                        sameErrorCount = 1;
                    }
                    
                    // Create feedback with REAL execution errors (not LLM guesses!)
                    lastValidation = new ValidateCodeResponse
                    {
                        Score = 0,
                        Passed = false,
                        BuildErrors = executionResult.Errors, // 🔧 CRITICAL: Set this so focused build error prompt is used!
                        Issues = new List<ValidationIssue>
                        {
                            new ValidationIssue
                            {
                                Severity = "critical",
                                Message = executionResult.BuildPassed 
                                    ? "Code compiled but failed to execute" 
                                    : "Code failed to compile/build",
                                File = executionFiles.FirstOrDefault()?.Path ?? "unknown",
                                Line = 1,
                                Suggestion = $"Fix the following errors:\n\n{executionResult.Errors}"
                            }
                        },
                        Summary = $"Execution failed in Docker ({executionResult.DockerImage}). " +
                                  $"Build: {(executionResult.BuildPassed ? "✅" : "❌")}, " +
                                  $"Run: {(executionResult.ExecutionPassed ? "✅" : "❌")}\n\n" +
                                  $"Error output:\n{executionResult.CombinedOutput}"
                    };
                    
                    _logger.LogInformation("Iteration {Iteration}: Execution FAILED, looping back to CodingAgent with real errors", iteration);
                    continue; // Loop back to CodingAgent with real error messages!
                }
                
                _logger.LogInformation("🐳 Execution passed on iteration {Iteration}! Output: {Output}", 
                    iteration, 
                    executionResult.Output.Length > 100 
                        ? executionResult.Output[..100] + "..." 
                        : executionResult.Output);

                // ✅ CODE WORKS! Now validate for patterns/quality with ValidationAgent
                var validationProgress = executionProgress + 5;
                UpdateProgress(jobId, TaskState.Running, validationProgress, $"Validation Agent (iteration {iteration})", iteration);
                
                var validationPhase = StartPhase("validation_agent", iteration);

                // Validate ALL accumulated files, not just last iteration's!
                var validateRequest = new ValidateCodeRequest
                {
                    Files = accumulatedFiles.Values.Select(f => new CodeFile
                    {
                        Path = f.Path,
                        Content = f.Content,
                        IsNew = f.Type == FileChangeType.Created
                    }).ToList(),
                    Context = request.Context,
                    Language = request.Language,
                    OriginalTask = request.Task,
                    WorkspacePath = request.WorkspacePath,
                    ValidationMode = request.ValidationMode // "standard" (default) or "enterprise"
                };

                lastValidation = await _validationAgent.ValidateAsync(validateRequest, cancellationToken);
                
                // Add execution output to validation context
                lastValidation.Summary = $"✅ Code executed successfully!\nOutput: {executionResult.Output}\n\n{lastValidation.Summary}";
                
                validationPhase.Details = new Dictionary<string, object>
                {
                    ["score"] = lastValidation.Score,
                    ["passed"] = lastValidation.Passed,
                    ["issueCount"] = lastValidation.Issues.Count,
                    ["executionOutput"] = executionResult.Output?.Length > 200 
                        ? executionResult.Output.Substring(0, 200) + "..." 
                        : executionResult.Output,
                    // 📝 Capture feedback for status display
                    ["feedback"] = lastValidation.Summary?.Length > 500 
                        ? lastValidation.Summary.Substring(0, 500) + "..." 
                        : lastValidation.Summary,
                    ["topIssues"] = lastValidation.Issues.Take(3).Select(i => $"{i.Severity}: {i.Message}").ToList()
                };
                EndPhase(jobId, validationPhase);
                response.Timeline.Add(validationPhase);

                _logger.LogInformation("Iteration {Iteration}: Score {Score}/10, Passed: {Passed}", 
                    iteration, lastValidation.Score, lastValidation.Passed);

                // Check if we passed
                if (lastValidation.Passed && lastValidation.Score >= request.MinValidationScore)
                {
                    _logger.LogInformation("Task passed validation on iteration {Iteration}", iteration);
                    break;
                }
            }

            // Build result using ALL accumulated files
            if (accumulatedFiles.Any() && lastValidation != null)
            {
                var files = accumulatedFiles.Values.Select(f => new GeneratedFile
                {
                    Path = f.Path,
                    Content = f.Content,
                    ChangeType = f.Type,
                    Reason = f.Reason
                }).ToList();
                
                _logger.LogInformation("📁 Final result contains {Count} accumulated files", files.Count);
                
                // 📝 AUTO-WRITE: Write files to workspace if enabled
                var filesWritten = new List<string>();
                var meetsMinScore = lastValidation.Score >= request.MinValidationScore;
                if (request.AutoWriteFiles && meetsMinScore && !string.IsNullOrEmpty(request.WorkspacePath))
                {
                    filesWritten = await WriteFilesToWorkspaceAsync(files, request.WorkspacePath, cancellationToken);
                    _logger.LogInformation("📝 Auto-wrote {Count} files to workspace", filesWritten.Count);
                }
                
                var autoWriteStatus = request.AutoWriteFiles && meetsMinScore
                    ? $" Files written to: {string.Join(", ", filesWritten)}"
                    : " Files returned for manual review.";
                
                // meetsMinScore already defined above
                var result = new TaskResult
                {
                    Success = meetsMinScore,
                    Files = files,
                    ValidationScore = lastValidation.Score,
                    TotalIterations = iteration,
                    TotalDurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Summary = meetsMinScore 
                        ? $"Successfully generated code with score {lastValidation.Score}/10 in {iteration} iteration(s).{autoWriteStatus}"
                        : $"Code generated but validation score {lastValidation.Score}/10 did not meet minimum {request.MinValidationScore}",
                    ModelsUsed = modelsUsedDuringTask.Distinct().ToList()
                };

                response.Status = meetsMinScore ? TaskState.Complete : TaskState.Failed;
                response.Progress = 100;
                response.Result = result;
                response.Iteration = iteration;
                
                // If failed, include detailed error information with issues
                if (!meetsMinScore)
                {
                    var issueDetails = lastValidation.Issues?
                        .OrderByDescending(i => i.Severity == "critical" ? 4 : i.Severity == "high" ? 3 : i.Severity == "warning" ? 2 : 1)
                        .Select(i => $"[{i.Severity?.ToUpper()}] {i.Message}" + (!string.IsNullOrEmpty(i.Suggestion) ? $" -> {i.Suggestion}" : ""))
                        .ToList() ?? new List<string>();
                    
                    response.Error = new TaskError
                    {
                        Type = "validation_failed",
                        Message = lastValidation.Summary ?? "Code did not pass validation",
                        CanRetry = true,
                        PartialResult = result,
                        Details = new Dictionary<string, object>
                        {
                            ["score"] = lastValidation.Score,
                            ["issues"] = issueDetails,
                            ["totalIterations"] = iteration,
                            ["modelsUsed"] = modelsUsedDuringTask.Distinct().ToList()
                        }
                    };
                }
                
                // ☁️ Include cloud usage if Claude was used
                if (cloudUsage.ApiCalls > 0)
                {
                    response.CloudUsage = cloudUsage;
                }

                // Store successful Q&A and record prompt feedback in Lightning (with graceful degradation)
                if (meetsMinScore)
                {
                    // 🛡️ GRACEFUL DEGRADATION: Don't fail if storage fails
                    try
                    {
                        await StoreSuccessfulResultAsync(request, lastGeneratedCode, cancellationToken);
                        await _memoryAgent.RecordPromptFeedbackAsync(
                            "coding_agent_system",
                            wasSuccessful: true,
                            rating: lastValidation.Score,
                            cancellationToken);
                        
                        // 🎉 STORE SUCCESSFUL TASK for cross-workspace learning
                        await _memoryAgent.StoreSuccessfulTaskAsync(new TaskSuccessRecord
                        {
                            TaskDescription = request.Task,
                            Language = request.Language ?? "python",
                            Context = request.Context,
                            ApproachUsed = $"Generated code in {iteration} iteration(s) with {lastGeneratedCode.ModelUsed}",
                            PatternsUsed = taskPlan?.RequiredClasses ?? new List<string>(),
                            FilesGenerated = accumulatedFiles.Keys.ToList(),
                            Keywords = ExtractKeywords(request.Task),
                            IterationsNeeded = iteration,
                            FinalScore = lastValidation.Score,
                            ModelUsed = lastGeneratedCode.ModelUsed ?? "",
                            SemanticStructure = taskPlan?.SemanticBreakdown ?? ""
                        }, cancellationToken);
                        _logger.LogInformation("🎉 Stored successful task for future learning");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Failed to store result in Lightning memory (non-critical)");
                    }
                }
                else
                {
                    // 🛡️ Record failure for learning (non-critical)
                    try
                    {
                        await _memoryAgent.RecordPromptFeedbackAsync(
                            "coding_agent_system",
                            wasSuccessful: false,
                            rating: lastValidation.Score,
                            cancellationToken);
                        
                        // 🧠 TASK LEARNING: Record detailed failure for future avoidance
                        await RecordDetailedFailureAsync(
                            request, 
                            lastValidation, 
                            iteration, 
                            modelsUsedDuringTask,
                            approachesTried,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Failed to record feedback in Lightning (non-critical)");
                    }
                }
            }
            else
            {
                response.Status = TaskState.Failed;
                response.Error = new TaskError
                {
                    Type = "generation_failed",
                    Message = lastGeneratedCode?.Error ?? "Failed to generate code",
                    CanRetry = true
                };
            }
        }
        catch (OperationCanceledException)
        {
            response.Status = TaskState.Cancelled;
            response.Message = "Task was cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration failed for task: {Task}", request.Task);
            response.Status = TaskState.Failed;
            response.Error = new TaskError
            {
                Type = "exception",
                Message = ex.Message,
                CanRetry = true
            };
        }

        return response;
    }

    private void UpdateProgress(string? jobId, TaskState status, int progress, string phase, int iteration)
    {
        if (jobId != null && _jobManager != null)
        {
            _jobManager.UpdateJobStatus(jobId, status, progress, phase, iteration);
        }
    }

    private PhaseInfo StartPhase(string name, int? iteration = null)
    {
        return new PhaseInfo
        {
            Name = name,
            Iteration = iteration,
            StartedAt = DateTime.UtcNow,
            Status = "running"
        };
    }

    private void EndPhase(string? jobId, PhaseInfo phase)
    {
        phase.CompletedAt = DateTime.UtcNow;
        phase.DurationMs = (long)(phase.CompletedAt.Value - phase.StartedAt).TotalMilliseconds;
        phase.Status = "complete";
        
        if (jobId != null && _jobManager != null)
        {
            _jobManager.AddPhaseToTimeline(jobId, phase);
        }
    }

    private async Task StoreSuccessfulResultAsync(
        OrchestrateTaskRequest request, 
        GenerateCodeResponse generatedCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var answer = generatedCode.Explanation + "\n\n" +
                string.Join("\n\n", generatedCode.FileChanges.Select(f => 
                    $"// File: {f.Path}\n{f.Content}"));

            await _memoryAgent.StoreQaAsync(
                request.Task,
                answer,
                generatedCode.FileChanges.Select(f => f.Path).ToList(),
                request.Context,
                cancellationToken);

            _logger.LogInformation("Stored successful result in Lightning memory");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store result in Lightning memory");
        }
    }
    
    /// <summary>
    /// 📝 Write generated files to workspace (when autoWriteFiles is enabled)
    /// Handles path translation for Docker container paths
    /// </summary>
    private async Task<List<string>> WriteFilesToWorkspaceAsync(
        List<GeneratedFile> files,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var writtenFiles = new List<string>();
        
        // 🗂️ Translate workspace path from host (Windows) to container path
        var containerWorkspacePath = _pathTranslation.TranslateToContainerPath(workspacePath);
        _logger.LogDebug("Path translation: {HostPath} -> {ContainerPath}", workspacePath, containerWorkspacePath);
        
        foreach (var file in files)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Build full path using translated container workspace path
                var fullPath = Path.IsPathRooted(file.Path) 
                    ? _pathTranslation.TranslateToContainerPath(file.Path)
                    : Path.Combine(containerWorkspacePath, file.Path);
                
                // Ensure directory exists
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogDebug("Created directory: {Directory}", directory);
                }
                
                // Check if file exists and create backup if needed
                if (File.Exists(fullPath) && file.ChangeType == FileChangeType.Created)
                {
                    var backupPath = fullPath + ".backup";
                    File.Copy(fullPath, backupPath, overwrite: true);
                    _logger.LogInformation("Created backup: {BackupPath}", backupPath);
                }
                
                // Write the file
                await File.WriteAllTextAsync(fullPath, file.Content, cancellationToken);
                writtenFiles.Add(file.Path);
                
                _logger.LogInformation("📝 Wrote file: {FilePath} ({ChangeType})", file.Path, file.ChangeType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write file: {FilePath}", file.Path);
            }
        }
        
        return writtenFiles;
    }
    
    /// <summary>
    /// 🧠 TASK LEARNING: Query lessons from similar failed tasks
    /// </summary>
    private async Task<TaskLessonsResult> QueryLessonsForTaskAsync(
        OrchestrateTaskRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract keywords from task description
            var keywords = ExtractTaskKeywords(request.Task);
            
            var lessons = await _memoryAgent.QueryTaskLessonsAsync(
                request.Task,
                keywords,
                request.Language ?? "unknown",
                cancellationToken);
            
            if (lessons.FoundLessons > 0)
            {
                _logger.LogInformation(
                    "🧠 Found {Count} lessons from similar failed tasks. Avoidance advice added to prompt.",
                    lessons.FoundLessons);
            }
            
            return lessons;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to query task lessons (continuing without)");
            return new TaskLessonsResult();
        }
    }
    
    /// <summary>
    /// 🧠 TASK LEARNING: Record detailed failure for future avoidance
    /// </summary>
    private async Task RecordDetailedFailureAsync(
        OrchestrateTaskRequest request,
        ValidateCodeResponse validation,
        int iteration,
        List<string> modelsUsed,
        List<string> approachesTried,
        CancellationToken cancellationToken)
    {
        try
        {
            // Categorize the error pattern
            var errorPattern = CategorizeErrorPattern(validation);
            
            // Build lessons learned from this failure
            var lessonsLearned = BuildLessonsLearned(validation, iteration);
            
            var failure = new TaskFailureRecord
            {
                TaskDescription = request.Task.Length > 200 
                    ? request.Task.Substring(0, 200) + "..." 
                    : request.Task,
                TaskKeywords = ExtractTaskKeywords(request.Task),
                Language = request.Language ?? "unknown",
                FailurePhase = DetermineFailurePhase(validation),
                ErrorMessage = validation.Summary ?? "Unknown error",
                ErrorPattern = errorPattern,
                ApproachesTried = approachesTried,
                ModelsUsed = modelsUsed,
                IterationsAttempted = iteration,
                LessonsLearned = lessonsLearned,
                Context = request.Context
            };
            
            await _memoryAgent.RecordTaskFailureAsync(failure, cancellationToken);
            _logger.LogInformation("📝 Recorded task failure for future learning: {Pattern}", errorPattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to record task failure (non-critical)");
        }
    }
    
    /// <summary>
    /// Extract keywords from task description for similarity matching
    /// </summary>
    private static List<string> ExtractTaskKeywords(string task)
    {
        // Common keywords to extract
        var keywords = new List<string>();
        
        // Programming languages/frameworks
        var techKeywords = new[] { "flutter", "blazor", "react", "maui", "wpf", "winforms", 
            "csharp", "c#", "python", "typescript", "javascript", "kotlin", "swift", "dart",
            "api", "rest", "grpc", "graphql", "websocket",
            "sql", "nosql", "mongodb", "postgres", "neo4j",
            "docker", "kubernetes", "azure", "aws" };
        
        // Task type keywords
        var taskTypeKeywords = new[] { "crud", "game", "dashboard", "login", "auth", 
            "payment", "shopping", "cart", "checkout", "search", "filter", 
            "upload", "download", "email", "notification", "report" };
        
        var lowerTask = task.ToLowerInvariant();
        
        foreach (var keyword in techKeywords.Concat(taskTypeKeywords))
        {
            if (lowerTask.Contains(keyword))
                keywords.Add(keyword);
        }
        
        return keywords.Take(10).ToList();  // Limit to 10 keywords
    }
    
    /// <summary>
    /// Categorize the error pattern for grouping similar failures
    /// </summary>
    private static string CategorizeErrorPattern(ValidateCodeResponse validation)
    {
        var summary = (validation.Summary ?? "").ToLowerInvariant();
        var issues = validation.Issues?.Select(i => i.Message.ToLowerInvariant()) ?? Array.Empty<string>();
        var allText = summary + " " + string.Join(" ", issues);
        
        if (allText.Contains("docker") && allText.Contains("build"))
            return "docker_build";
        if (allText.Contains("docker") || allText.Contains("execution"))
            return "docker_run";
        if (allText.Contains("null") || allText.Contains("nullreference"))
            return "null_reference";
        if (allText.Contains("missing") && (allText.Contains("dependency") || allText.Contains("package")))
            return "missing_dependency";
        if (allText.Contains("type") && (allText.Contains("mismatch") || allText.Contains("cannot convert")))
            return "type_mismatch";
        if (allText.Contains("syntax") || allText.Contains("expected"))
            return "syntax_error";
        if (allText.Contains("not found") || allText.Contains("does not exist"))
            return "missing_resource";
        if (allText.Contains("timeout"))
            return "timeout";
        if (allText.Contains("auth") || allText.Contains("permission"))
            return "auth_error";
        
        return "general_failure";
    }
    
    /// <summary>
    /// Determine which phase failed
    /// </summary>
    private static string DetermineFailurePhase(ValidateCodeResponse validation)
    {
        var summary = (validation.Summary ?? "").ToLowerInvariant();
        
        if (summary.Contains("docker build") || summary.Contains("compile"))
            return "docker_build";
        if (summary.Contains("docker") || summary.Contains("execution"))
            return "docker_run";
        if (summary.Contains("validation"))
            return "validation";
        if (validation.Score == 0)
            return "code_generation";
        
        return "validation";
    }
    
    /// <summary>
    /// Get a signature for an error to detect stagnation (same error repeating)
    /// </summary>
    private static string GetErrorSignature(string errors)
    {
        if (string.IsNullOrWhiteSpace(errors))
            return "";
        
        // Extract the first error code/message (e.g., "CS0246", "error MSB1011")
        var lines = errors.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // First pass: Find CS errors (compiler errors - highest priority)
        foreach (var line in lines)
        {
            var csMatch = System.Text.RegularExpressions.Regex.Match(line, @"(CS\d{4})");
            if (csMatch.Success)
            {
                return csMatch.Value;
            }
        }
        
        // Second pass: Find MSB errors (build errors)
        foreach (var line in lines)
        {
            var msbMatch = System.Text.RegularExpressions.Regex.Match(line, @"(MSB\d{4})");
            if (msbMatch.Success)
            {
                return msbMatch.Value;
            }
        }
        
        // Third pass: Find NU errors (NuGet errors - NOT warnings)
        foreach (var line in lines)
        {
            // Only match NU if it's an error, not a warning
            if (line.Contains("error NU", StringComparison.OrdinalIgnoreCase))
            {
                var nuMatch = System.Text.RegularExpressions.Regex.Match(line, @"(NU\d{4})");
                if (nuMatch.Success)
                {
                    return nuMatch.Value;
                }
            }
        }
        
        // Fourth pass: Generic error patterns
        foreach (var line in lines)
        {
            
            // Match generic error patterns
            if (line.Contains("error") || line.Contains("Error"))
            {
                // Return first 100 chars of the error line as signature
                return line.Trim().Length > 100 ? line.Trim()[..100] : line.Trim();
            }
        }
        
        // Fallback: hash of first 200 chars
        var truncated = errors.Length > 200 ? errors[..200] : errors;
        return truncated.GetHashCode().ToString();
    }
    
    /// <summary>
    /// Extract a project name from task description
    /// </summary>
    private static string? ExtractProjectNameFromTask(string task)
    {
        // Try to find explicit project names like "MyApp", "Calculator", "UserService"
        var patterns = new[]
        {
            @"(?:create|build|make|generate)\s+(?:a\s+)?([A-Z][a-zA-Z0-9]+(?:App|Service|Api|Application|Manager|Handler)?)",
            @"(?:called|named)\s+([A-Z][a-zA-Z0-9]+)",
            @"([A-Z][a-zA-Z]+(?:Calculator|Service|Api|Manager|Handler|Controller))",
        };
        
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(task, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                var name = match.Groups[1].Value;
                // Sanitize: remove spaces, ensure valid C# identifier
                name = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9]", "");
                if (!string.IsNullOrEmpty(name) && char.IsLetter(name[0]))
                {
                    return name;
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Build lessons learned from this failure
    /// </summary>
    private static string BuildLessonsLearned(ValidateCodeResponse validation, int iterations)
    {
        var lessons = new List<string>();
        
        if (iterations >= 5)
            lessons.Add("Task may be too complex - consider breaking into smaller tasks");
        
        var criticalIssues = validation.Issues?
            .Where(i => i.Severity?.ToLowerInvariant() == "critical")
            .Select(i => i.Message)
            .ToList() ?? new();
        
        if (criticalIssues.Any())
            lessons.Add($"Critical issues: {string.Join("; ", criticalIssues.Take(3))}");
        
        if (validation.Score < 3)
            lessons.Add("Very low score - approach fundamentally flawed");
        
        return string.Join(". ", lessons);
    }
    
    /// <summary>
    /// Check if this is a UI-related task
    /// </summary>
    private static bool IsUITask(string task)
    {
        var uiKeywords = new[] { "gui", "ui", "interface", "window", "button", "form", "page", 
            "component", "widget", "screen", "view", "layout", "menu", "dialog", "modal",
            "frontend", "blazor", "react", "vue", "angular", "wpf", "winforms", "maui", "flutter" };
        
        var lowerTask = task.ToLowerInvariant();
        return uiKeywords.Any(k => lowerTask.Contains(k));
    }
    
    /// <summary>
    /// Extract keywords from task for storage
    /// </summary>
    private static List<string> ExtractKeywords(string task)
    {
        // Common keywords to extract
        var keywords = new List<string>();
        
        // Programming languages/frameworks
        var techKeywords = new[] { "flutter", "blazor", "react", "maui", "wpf", "winforms", 
            "csharp", "c#", "python", "typescript", "javascript", "kotlin", "swift", "dart",
            "api", "rest", "grpc", "graphql", "websocket",
            "sql", "nosql", "mongodb", "postgres", "neo4j",
            "docker", "kubernetes", "azure", "aws",
            "game", "blackjack", "todo", "chat", "calculator" };
        
        var lowerTask = task.ToLowerInvariant();
        
        foreach (var keyword in techKeywords)
        {
            if (lowerTask.Contains(keyword))
                keywords.Add(keyword);
        }
        
        return keywords.Take(10).ToList();
    }
    
    /// <summary>
    /// Extract class/method signatures from code to give the LLM context
    /// </summary>
    private static string ExtractSignatures(string content, string language)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";
        
        var signatures = new List<string>();
        var lines = content.Split('\n');
        
        switch (language?.ToLowerInvariant())
        {
            case "csharp":
            case "c#":
                // Extract namespace, class, interface, enum, method signatures
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    // Namespace
                    if (trimmed.StartsWith("namespace "))
                        signatures.Add(trimmed.TrimEnd(';', '{').Trim());
                    
                    // Class/Interface/Enum/Record declarations
                    if (trimmed.StartsWith("public class ") || trimmed.StartsWith("public interface ") ||
                        trimmed.StartsWith("public enum ") || trimmed.StartsWith("public record ") ||
                        trimmed.StartsWith("public struct ") || trimmed.StartsWith("public abstract class ") ||
                        trimmed.StartsWith("internal class ") || trimmed.StartsWith("internal interface "))
                    {
                        signatures.Add(trimmed.TrimEnd('{').Trim());
                    }
                    
                    // Public methods and properties
                    if ((trimmed.StartsWith("public ") || trimmed.StartsWith("public async ") || 
                         trimmed.StartsWith("public static ") || trimmed.StartsWith("public virtual ")) &&
                        (trimmed.Contains("(") || trimmed.Contains(" { get")))
                    {
                        // Get just the signature, not the body
                        var sig = trimmed.Split(new[] { " =>" }, StringSplitOptions.None)[0]
                                        .Split(new[] { " {" }, StringSplitOptions.None)[0]
                                        .Trim();
                        if (!string.IsNullOrWhiteSpace(sig))
                            signatures.Add("  " + sig + (sig.Contains("(") ? ";" : " { get; set; }"));
                    }
                }
                break;
                
            case "python":
                bool inClass = false;
                string currentClass = "";
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    // Class definitions
                    if (trimmed.StartsWith("class "))
                    {
                        inClass = true;
                        currentClass = trimmed.TrimEnd(':');
                        signatures.Add(currentClass);
                    }
                    // Function/method definitions
                    else if (trimmed.StartsWith("def ") || trimmed.StartsWith("async def "))
                    {
                        var indent = inClass ? "  " : "";
                        var sig = trimmed.Split(':')[0].Trim();
                        signatures.Add(indent + sig);
                    }
                    // Reset class tracking on blank lines at column 0
                    else if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !trimmed.StartsWith("def ") && !trimmed.StartsWith("class "))
                    {
                        inClass = false;
                    }
                }
                break;
                
            case "typescript":
            case "javascript":
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    
                    // Export/class/interface/function declarations
                    if (trimmed.StartsWith("export class ") || trimmed.StartsWith("export interface ") ||
                        trimmed.StartsWith("class ") || trimmed.StartsWith("interface ") ||
                        trimmed.StartsWith("export function ") || trimmed.StartsWith("export const ") ||
                        trimmed.StartsWith("export async function "))
                    {
                        var sig = trimmed.Split(new[] { " {" }, StringSplitOptions.None)[0].Trim();
                        signatures.Add(sig);
                    }
                    // Method signatures in classes
                    else if ((trimmed.StartsWith("public ") || trimmed.StartsWith("private ") || 
                              trimmed.StartsWith("async ") || trimmed.StartsWith("static ")) &&
                             trimmed.Contains("("))
                    {
                        var sig = trimmed.Split(new[] { " {" }, StringSplitOptions.None)[0].Trim();
                        signatures.Add("  " + sig);
                    }
                }
                break;
                
            default:
                // For unknown languages, extract lines that look like definitions
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("class ") || trimmed.StartsWith("def ") || 
                        trimmed.StartsWith("function ") || trimmed.StartsWith("public ") ||
                        trimmed.StartsWith("export "))
                    {
                        var sig = trimmed.Split(new[] { " {", ":" }, StringSplitOptions.None)[0].Trim();
                        signatures.Add(sig);
                    }
                }
                break;
        }
        
        // Limit to prevent massive prompts
        var result = string.Join("\n", signatures.Take(30));
        return result.Length > 2000 ? result[..2000] + "\n// ... more ..." : result;
    }
    
    #region Step-by-Step Execution Mode
    
    /// <summary>
    /// Execute task step-by-step: generate each plan step separately, validate after each
    /// This is better for complex multi-file tasks with dependencies
    /// </summary>
    private async Task<TaskStatusResponse> ExecuteStepByStepAsync(
        OrchestrateTaskRequest request,
        string? jobId,
        TaskStatusResponse response,
        TaskPlan taskPlan,
        ProjectSymbols? projectSymbols,
        SimilarTasksResult? similarTasks,
        DesignContext? designContext,
        CodeContext? context,
        TaskLessonsResult taskLessons,
        int effectiveMaxIterations,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        var accumulatedFiles = new Dictionary<string, FileChange>();
        var triedModels = new HashSet<string>();
        var modelsUsedDuringTask = new List<string>();
        var approachesTried = new List<string>();
        ValidateCodeResponse? lastValidation = null;
        GenerateCodeResponse? lastGeneratedCode = null;
        var totalIterations = 0;
        var maxRetriesPerStep = 10;  // More retries per step before asking for help
        
        // ☁️ Track cloud LLM usage (Anthropic) across steps
        var cloudUsage = new CloudUsage
        {
            Provider = "anthropic",
            Note = "Check console.anthropic.com for actual balance"
        };
        
        // 📋 Process each step in the plan
        for (var stepIndex = 0; stepIndex < taskPlan.Steps.Count; stepIndex++)
        {
            var step = taskPlan.Steps[stepIndex];
            var stepNumber = stepIndex + 1;
            var stepRetries = 0;
            var stepSuccess = false;
            
            _logger.LogInformation("[STEP] Step {StepNum}/{TotalSteps}: {Description}", 
                stepNumber, taskPlan.Steps.Count, step.Description);
            
            // Update job plan status
            if (_jobManager != null)
            {
                _jobManager.UpdateStepStatus(jobId, stepIndex, "InProgress");
            }
            
            // Track files from PREVIOUS (completed) steps - don't overwrite these!
            var filesFromPreviousSteps = new HashSet<string>(accumulatedFiles.Keys);
            
            while (!stepSuccess && stepRetries < maxRetriesPerStep && totalIterations < effectiveMaxIterations)
            {
                totalIterations++;
                stepRetries++;
                cancellationToken.ThrowIfCancellationRequested();
                
                // On RETRY: Remove files generated by THIS step (keep only previous steps' files)
                if (stepRetries > 1)
                {
                    var filesToRemove = accumulatedFiles.Keys.Where(k => !filesFromPreviousSteps.Contains(k)).ToList();
                    foreach (var fileKey in filesToRemove)
                    {
                        accumulatedFiles.Remove(fileKey);
                        _logger.LogDebug("[CLEANUP] Removed {File} from current step before retry", fileKey);
                    }
                }
                
                // Calculate progress: each step gets equal share
                var stepProgress = 15 + (stepIndex * 70 / taskPlan.Steps.Count) + 
                                   (stepRetries * 70 / (taskPlan.Steps.Count * maxRetriesPerStep));
                UpdateProgress(jobId, TaskState.Running, stepProgress, 
                    $"Step {stepNumber}/{taskPlan.Steps.Count}: {step.Description} (attempt {stepRetries})", totalIterations);
                
                var codingPhase = StartPhase("coding_agent", totalIterations);
                codingPhase.Details = new Dictionary<string, object>
                {
                    ["mode"] = "step_by_step",
                    ["step"] = stepNumber,
                    ["stepDescription"] = step.Description,
                    ["attempt"] = stepRetries
                };
                
                try
                {
                    // 🔍 SEARCH for relevant context before each step
                    // This finds code we just indexed from previous steps
                    List<string> searchContext = new();
                    if (stepNumber > 1)
                    {
                        try
                        {
                            var searchQuery = $"{step.Description} {string.Join(" ", taskPlan.RequiredClasses)}";
                            var searchResults = await _memoryAgent.SmartSearchAsync(
                                searchQuery, 
                                request.Context, 
                                5,  // Top 5 results
                                cancellationToken);
                            
                            if (searchResults?.Any() == true)
                            {
                                searchContext = searchResults.Take(5).Select(r => 
                                    $"// Found: {r.Name} in {r.FilePath}\n{r.Content}").ToList();
                                _logger.LogInformation("[SEARCH-STEP] Found {Count} relevant items for step {Step}", 
                                    searchResults.Count, stepNumber);
                            }
                        }
                        catch (Exception searchEx)
                        {
                            _logger.LogWarning(searchEx, "[SEARCH-STEP] Search failed (non-critical)");
                        }
                    }
                    
                    // Build focused prompt for THIS step only (only pass files from PREVIOUS steps)
                    var stepPrompt = BuildStepPrompt(step, stepNumber, taskPlan, 
                        accumulatedFiles.Where(kv => filesFromPreviousSteps.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value), 
                        projectSymbols, taskLessons, request.Language ?? "csharp", searchContext);
                    
                    // Build feedback if we have previous validation
                    var feedback = lastValidation?.ToFeedback();
                    if (feedback != null) feedback.TriedModels = triedModels;
                    
                    // Convert accumulated files to ExistingFile format for the coding agent
                    var existingFilesForAgent = accumulatedFiles
                        .Where(kv => filesFromPreviousSteps.Contains(kv.Key))
                        .Select(kv => new ExistingFile 
                        { 
                            Path = kv.Key, 
                            Content = kv.Value.Content 
                        })
                        .ToList();
                    
                    var generateRequest = new GenerateCodeRequest
                    {
                        Task = stepPrompt,
                        Language = request.Language,
                        Context = context,
                        WorkspacePath = request.WorkspacePath,
                        PreviousFeedback = stepRetries > 1 ? feedback : null,
                        ExistingFiles = existingFilesForAgent.Any() ? existingFilesForAgent : null
                    };
                    
                    _logger.LogInformation("[CONTEXT] Passing {FileCount} existing files to coding agent", existingFilesForAgent.Count);
                    
                    // Generate code for this step
                    lastGeneratedCode = stepRetries == 1
                        ? await _codingAgent.GenerateAsync(generateRequest, cancellationToken)
                        : await _codingAgent.FixAsync(generateRequest, cancellationToken);
                    
                    // Track model usage
                    if (!string.IsNullOrEmpty(lastGeneratedCode.ModelUsed))
                    {
                        triedModels.Add(lastGeneratedCode.ModelUsed);
                        modelsUsedDuringTask.Add(lastGeneratedCode.ModelUsed);
                        approachesTried.Add($"Step {stepNumber} attempt {stepRetries}: {lastGeneratedCode.ModelUsed}");
                    }
                    
                    // ☁️ Accumulate cloud usage if Claude was used
                    if (lastGeneratedCode.CloudUsage != null)
                    {
                        cloudUsage.Model = lastGeneratedCode.CloudUsage.Model;
                        cloudUsage.InputTokens += lastGeneratedCode.CloudUsage.InputTokens;
                        cloudUsage.OutputTokens += lastGeneratedCode.CloudUsage.OutputTokens;
                        cloudUsage.EstimatedCost += lastGeneratedCode.CloudUsage.Cost;
                        cloudUsage.TokensRemaining = lastGeneratedCode.CloudUsage.TokensRemaining;
                        cloudUsage.RequestsRemaining = lastGeneratedCode.CloudUsage.RequestsRemaining;
                        cloudUsage.ApiCalls++;
                        
                        codingPhase.Details["cloudCost"] = lastGeneratedCode.CloudUsage.Cost;
                    }
                    
                    codingPhase.Details["modelUsed"] = lastGeneratedCode.ModelUsed ?? "unknown";
                    codingPhase.Details["filesGenerated"] = lastGeneratedCode.FileChanges.Count;
                    
                    // Add files for THIS step (overwrite any from previous retry)
                    var newFilesThisStep = 0;
                    foreach (var file in lastGeneratedCode.FileChanges)
                    {
                        var fileName = Path.GetFileName(file.Path);
                        // Use normalized filename to avoid path confusion
                        var normalizedKey = NormalizeFileName(fileName, step.FileName);
                        
                        if (!accumulatedFiles.ContainsKey(normalizedKey))
                        {
                            newFilesThisStep++;
                        }
                        accumulatedFiles[normalizedKey] = file;
                    }
                    
                    _logger.LogInformation("[FILES] Step {StepNum} Attempt {Attempt}: Added {NewFiles} files, Total: {Total} (prev steps: {PrevCount})", 
                        stepNumber, stepRetries, newFilesThisStep, accumulatedFiles.Count, filesFromPreviousSteps.Count);
                    
                    // 🔧 INTERMEDIATE BUILD CHECK for C# (after step 2+)
                    // For C#, we check compilation incrementally to catch errors early
                    // Step 1 is always accepted (may have forward dependencies)
                    var isCSharp = (request.Language?.ToLowerInvariant() is "csharp" or "cs" or "c#") ||
                                   accumulatedFiles.Keys.Any(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
                    
                    if (isCSharp && stepNumber > 1 && accumulatedFiles.Count >= 2)
                    {
                        // 🧹 Clean before build check
                        CleanAccumulatedFilesForCSharp(accumulatedFiles);
                        
                        _logger.LogInformation("[BUILD-CHECK] Running intermediate build check for C# (step {StepNum}, {FileCount} files)", 
                            stepNumber, accumulatedFiles.Count);
                        
                        var buildCheckFiles = accumulatedFiles.Values.Select(f => new ExecutionFile
                        {
                            Path = f.Path,
                            Content = f.Content,
                            ChangeType = (int)f.Type,
                            Reason = f.Reason
                        }).ToList();
                        
                        var buildCheckResult = await _executionService.ExecuteAsync(
                            "csharp", buildCheckFiles, request.WorkspacePath, null, 
                            buildOnly: true,  // Just compile, don't execute
                            cancellationToken);
                        
                        if (!buildCheckResult.BuildPassed)
                        {
                            _logger.LogWarning("[BUILD-CHECK] Intermediate build FAILED: {Errors}", 
                                buildCheckResult.Errors?.Length > 200 ? buildCheckResult.Errors[..200] + "..." : buildCheckResult.Errors);
                            
                            // Create feedback with build errors for retry
                            lastValidation = new ValidateCodeResponse
                            {
                                Score = 2,
                                Passed = false,
                                BuildErrors = buildCheckResult.Errors, // 🔧 CRITICAL: Set build errors for focused prompt!
                                Issues = new List<ValidationIssue>
                                {
                                    new ValidationIssue
                                    {
                                        Severity = "critical",
                                        Message = "Build failed after generating this step",
                                        File = step.FileName ?? "unknown",
                                        Line = 1,
                                        Suggestion = $"Fix compilation errors:\n{buildCheckResult.Errors}"
                                    }
                                },
                                Summary = $"Build failed:\n{buildCheckResult.Errors}"
                            };
                            stepSuccess = false;
                            continue; // Retry this step
                        }
                        
                        _logger.LogInformation("[BUILD-CHECK] ✅ Intermediate build PASSED");
                    }
                    
                    stepSuccess = true;
                    _logger.LogInformation("[STEP-OK] Step {StepNum} code generated{BuildNote}", 
                        stepNumber, isCSharp && stepNumber > 1 ? " and compiled" : " (build deferred)");
                    
                    // 📊 INDEX IMMEDIATELY after step succeeds!
                    // This allows NEXT step to SEARCH and find what we just built
                    foreach (var file in lastGeneratedCode.FileChanges)
                    {
                        try
                        {
                            await _memoryAgent.IndexFileAsync(
                                file.Path,
                                file.Content,
                                request.Language ?? "csharp",
                                request.Context,
                                cancellationToken);
                            _logger.LogDebug("[INDEX-STEP] Indexed {Path} for next step to search", file.Path);
                        }
                        catch (Exception indexEx)
                        {
                            _logger.LogWarning(indexEx, "[INDEX-STEP] Failed (non-critical): {Path}", file.Path);
                        }
                    }
                    
                    // Update job plan status
                    if (_jobManager != null)
                    {
                        _jobManager.UpdateStepStatus(jobId, stepIndex, "Completed");
                    }
                    
                    // Track files for final build
                    foreach (var file in lastGeneratedCode.FileChanges)
                    {
                        _jobManager?.AddGeneratedFile(jobId, file.Path);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ERROR] Step {StepNum} failed with exception", stepNumber);
                    lastValidation = new ValidateCodeResponse
                    {
                        Score = 0,
                        Passed = false,
                        Issues = new List<ValidationIssue>
                        {
                            new ValidationIssue
                            {
                                Severity = "critical",
                                Message = ex.Message,
                                File = step.FileName ?? "unknown",
                                Line = 1,
                                Suggestion = "Fix the error and try again"
                            }
                        },
                        Summary = $"Step {stepNumber} error: {ex.Message}"
                    };
                }
                
                EndPhase(jobId, codingPhase);
                response.Timeline.Add(codingPhase);
            }
            
            // If step failed after all retries, pause for user help
            if (!stepSuccess)
            {
                _logger.LogWarning("[STEP-BY-STEP] Step {StepNum} FAILED after {Retries} attempts - waiting for user help", 
                    stepNumber, maxRetriesPerStep);
                if (_jobManager != null)
                {
                    _jobManager.UpdateStepStatus(jobId, stepIndex, "NeedsHelp");
                }
                
                // Build partial result with files generated so far (include content!)
                var partialFiles = accumulatedFiles.Values.Select(f => new GeneratedFile
                {
                    Path = f.Path,
                    Content = f.Content,
                    ChangeType = f.Type,
                    Reason = f.Reason
                }).ToList();
                
                // Extract specific errors from validation
                var errorDetails = lastValidation?.Issues?
                    .Where(i => i.Severity == "critical" || i.Severity == "error")
                    .Select(i => $"[{i.File}:{i.Line}] {i.Message}")
                    .ToList() ?? new List<string>();
                
                // Set NeedsHelp state - user can provide feedback to continue
                response.Status = TaskState.NeedsHelp;
                response.Progress = 15 + (stepIndex * 70 / taskPlan.Steps.Count);
                response.CurrentPhase = $"Step {stepNumber}/{taskPlan.Steps.Count} needs help: {step.Description}";
                response.Iteration = totalIterations;
                response.Error = new TaskError
                {
                    Type = "step_needs_help",
                    Message = $"Step {stepNumber}/{taskPlan.Steps.Count} failed after {maxRetriesPerStep} attempts.",
                    CanRetry = true,
                    Details = new Dictionary<string, object>
                    {
                        ["stepNumber"] = stepNumber,
                        ["stepDescription"] = step.Description,
                        ["targetFile"] = step.FileName ?? "unknown",
                        ["attemptsUsed"] = maxRetriesPerStep,
                        ["lastError"] = lastValidation?.Summary ?? "Unknown error",
                        ["specificErrors"] = errorDetails,
                        ["modelsTriedThisStep"] = approachesTried.Where(a => a.Contains($"Step {stepNumber}")).ToList(),
                        ["filesGeneratedSoFar"] = partialFiles.Select(f => f.Path).ToList(),
                        ["helpEndpoint"] = $"POST /api/orchestrator/task/{jobId}/help",
                        ["helpExample"] = new {
                            hint = "Describe what's wrong and how to fix it",
                            codeSnippet = "Optional: provide correct code",
                            focusFile = step.FileName ?? "the failing file"
                        }
                    }
                };
                
                // Include full file contents in result so user can see what was generated
                response.Result = new TaskResult
                {
                    Success = false,
                    Files = partialFiles,
                    ValidationScore = lastValidation?.Score ?? 0,
                    TotalIterations = totalIterations,
                    Summary = $"Step {stepNumber} needs help. Files generated so far: {partialFiles.Count}"
                };
                response.GeneratedFiles = partialFiles.Select(f => f.Path).ToList();
                
                return response;
            }
        }
        
        // 🐳 FINAL BUILD + FIX LOOP: Build all files, fix errors if needed
        _logger.LogInformation("[BUILD] All {StepCount} steps complete - building {FileCount} files together", 
            taskPlan.Steps.Count, accumulatedFiles.Count);
        
        var maxBuildRetries = 5;
        var buildRetries = 0;
        ExecutionResult? finalExecution = null;
        
        while (buildRetries < maxBuildRetries)
        {
            buildRetries++;
            totalIterations++;
            cancellationToken.ThrowIfCancellationRequested();
            
            UpdateProgress(jobId, TaskState.Running, 85 + (buildRetries * 2), 
                $"Final Build (attempt {buildRetries}/{maxBuildRetries})", totalIterations);
            var finalExecutionPhase = StartPhase("docker_execution", totalIterations);
            finalExecutionPhase.Details = new Dictionary<string, object>
            {
                ["attempt"] = buildRetries,
                ["fileCount"] = accumulatedFiles.Count
            };
            
            // 🧹 Final cleanup before execution
            if (request.Language?.ToLowerInvariant() is "csharp" or "cs" or "c#")
            {
                CleanAccumulatedFilesForCSharp(accumulatedFiles);
            }
            
            var finalFiles = accumulatedFiles.Values.Select(f => new ExecutionFile
            {
                Path = f.Path,
                Content = f.Content,
                ChangeType = (int)f.Type,
                Reason = f.Reason
            }).ToList();
            
            finalExecution = await _executionService.ExecuteAsync(
                request.Language ?? "python",
                finalFiles,
                request.WorkspacePath,
                lastGeneratedCode?.Execution,
                cancellationToken);
            
            finalExecutionPhase.Details["success"] = finalExecution.Success;
            finalExecutionPhase.Details["buildPassed"] = finalExecution.BuildPassed;
            finalExecutionPhase.Details["executionPassed"] = finalExecution.ExecutionPassed;
            finalExecutionPhase.Details["output"] = finalExecution.Output?.Length > 500 
                ? finalExecution.Output[..500] + "..." 
                : finalExecution.Output;
            EndPhase(jobId, finalExecutionPhase);
            response.Timeline.Add(finalExecutionPhase);
            
            if (finalExecution.BuildPassed)
            {
                _logger.LogInformation("[BUILD-OK] Final build succeeded on attempt {Attempt}", buildRetries);
                break;  // Build passed, exit retry loop
            }
            
            // Build failed - send ALL errors to Claude to fix ALL files
            _logger.LogWarning("[BUILD-FAIL] Final build failed (attempt {Attempt}): {Errors}", 
                buildRetries, finalExecution.Errors?.Length > 200 ? finalExecution.Errors[..200] + "..." : finalExecution.Errors);
            
            if (buildRetries >= maxBuildRetries)
            {
                _logger.LogError("[BUILD-FAIL] Build failed after {MaxRetries} attempts", maxBuildRetries);
                break;  // Max retries reached
            }
            
            // 🔧 FIX ALL ERRORS: Send all files + errors to Claude
            _logger.LogInformation("[FIX] Sending {FileCount} files to Claude to fix build errors...", accumulatedFiles.Count);
            
            var fixPhase = StartPhase("fix_build_errors", totalIterations);
            
            // Build a comprehensive fix prompt with ALL files and ALL errors
            var fixPrompt = new System.Text.StringBuilder();
            fixPrompt.AppendLine("=== BUILD FAILED - FIX ALL ERRORS ===");
            fixPrompt.AppendLine();
            fixPrompt.AppendLine("BUILD ERRORS:");
            fixPrompt.AppendLine(finalExecution.Errors);
            fixPrompt.AppendLine();
            fixPrompt.AppendLine("=== CURRENT FILES (fix these) ===");
            foreach (var file in accumulatedFiles)
            {
                fixPrompt.AppendLine($"// ========== {file.Key} ==========");
                fixPrompt.AppendLine(file.Value.Content);
                fixPrompt.AppendLine();
            }
            fixPrompt.AppendLine("=== INSTRUCTIONS ===");
            fixPrompt.AppendLine("1. Fix ALL the build errors shown above");
            fixPrompt.AppendLine("2. Return ALL files with corrections applied");
            fixPrompt.AppendLine("3. Make sure all class references are correct");
            fixPrompt.AppendLine("4. Make sure all using statements are present");
            fixPrompt.AppendLine("5. Make sure there's exactly ONE Main method entry point");
            
            // Convert existing files for the request
            var existingFilesForFix = accumulatedFiles.Select(kv => new ExistingFile 
            { 
                Path = kv.Key, 
                Content = kv.Value.Content 
            }).ToList();
            
            var fixRequest = new GenerateCodeRequest
            {
                Task = fixPrompt.ToString(),
                Language = request.Language,
                Context = context,
                WorkspacePath = request.WorkspacePath,
                PreviousFeedback = new ValidationFeedback
                {
                    TriedModels = triedModels,
                    Score = 0,
                    Issues = new List<ValidationIssue>
                    {
                        new ValidationIssue 
                        { 
                            Severity = "critical", 
                            Message = $"Build failed: {finalExecution.Errors ?? "Unknown error"}" 
                        }
                    },
                    // 🔧 SET BUILD ERRORS so CodingAgent uses focused fix prompt!
                    BuildErrors = finalExecution.Errors
                },
                ExistingFiles = existingFilesForFix
            };
            
            var fixedCode = await _codingAgent.FixAsync(fixRequest, cancellationToken);
            
            // Track model usage
            if (!string.IsNullOrEmpty(fixedCode.ModelUsed))
            {
                triedModels.Add(fixedCode.ModelUsed);
                modelsUsedDuringTask.Add(fixedCode.ModelUsed);
                approachesTried.Add($"Build fix attempt {buildRetries}: {fixedCode.ModelUsed}");
            }
            
            // Accumulate cloud usage
            if (fixedCode.CloudUsage != null)
            {
                cloudUsage.Model = fixedCode.CloudUsage.Model;
                cloudUsage.InputTokens += fixedCode.CloudUsage.InputTokens;
                cloudUsage.OutputTokens += fixedCode.CloudUsage.OutputTokens;
                cloudUsage.EstimatedCost += fixedCode.CloudUsage.Cost;
                cloudUsage.ApiCalls++;
            }
            
            // Update accumulated files with fixes
            foreach (var file in fixedCode.FileChanges)
            {
                var fileName = Path.GetFileName(file.Path);
                accumulatedFiles[fileName] = file;
                _logger.LogDebug("[FIX] Updated: {File}", fileName);
            }
            
            fixPhase.Details = new Dictionary<string, object>
            {
                ["filesFixed"] = fixedCode.FileChanges.Count,
                ["modelUsed"] = fixedCode.ModelUsed ?? "unknown"
            };
            EndPhase(jobId, fixPhase);
            response.Timeline.Add(fixPhase);
            
            lastGeneratedCode = fixedCode;
        }
        
        // Ensure we have a result
        if (finalExecution == null)
        {
            finalExecution = new ExecutionResult { Success = false, BuildPassed = false, Errors = "No build attempted" };
        }
        
        // 📊 INDEX files to Qdrant+Neo4j (only if build passed)
        if (finalExecution.BuildPassed)
        {
            _logger.LogInformation("[INDEX] Build passed - indexing {FileCount} files to memory", accumulatedFiles.Count);
            foreach (var file in accumulatedFiles.Values)
            {
                try
                {
                    await _memoryAgent.IndexFileAsync(
                        file.Path,
                        file.Content,
                        request.Language ?? "csharp",
                        request.Context,
                        cancellationToken);
                    _logger.LogDebug("[INDEX] Indexed: {Path}", file.Path);
                }
                catch (Exception indexEx)
                {
                    _logger.LogWarning(indexEx, "[INDEX] Failed (non-critical): {Path}", file.Path);
                }
            }
        }
        
        // 📊 FINAL VALIDATION
        UpdateProgress(jobId, TaskState.Running, 90, "Final Validation", totalIterations);
        var validationPhase = StartPhase("validation_agent", totalIterations);
        
        var validateRequest = new ValidateCodeRequest
        {
            Files = accumulatedFiles.Values.Select(f => new CodeFile
            {
                Path = f.Path,
                Content = f.Content,
                IsNew = f.Type == FileChangeType.Created
            }).ToList(),
            Context = request.Context,
            Language = request.Language,
            OriginalTask = request.Task,
            WorkspacePath = request.WorkspacePath,
            ValidationMode = request.ValidationMode
        };
        
        lastValidation = await _validationAgent.ValidateAsync(validateRequest, cancellationToken);
        
        if (finalExecution.Success)
        {
            lastValidation.Summary = $"✅ Code executed successfully!\nOutput: {finalExecution.Output}\n\n{lastValidation.Summary}";
        }
        
        validationPhase.Details = new Dictionary<string, object>
        {
            ["score"] = lastValidation.Score,
            ["passed"] = lastValidation.Passed,
            ["issueCount"] = lastValidation.Issues.Count
        };
        EndPhase(jobId, validationPhase);
        response.Timeline.Add(validationPhase);
        
        // Build final response
        var files = accumulatedFiles.Values.Select(f => new GeneratedFile
        {
            Path = f.Path,
            Content = f.Content,
            ChangeType = f.Type,
            Reason = f.Reason
        }).ToList();
        
        // 🎯 Use request.MinValidationScore (not ValidationAgent's internal threshold)
        var meetsMinScore = lastValidation.Score >= request.MinValidationScore;
        
        var finalResult = new TaskResult
        {
            Success = meetsMinScore && finalExecution.Success,
            Files = files,
            ValidationScore = lastValidation.Score,
            TotalIterations = totalIterations,
            TotalDurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
            Summary = lastValidation.Summary,
            ModelsUsed = modelsUsedDuringTask.Distinct().ToList()
        };
        
        response.Status = finalResult.Success ? TaskState.Complete : TaskState.Failed;
        response.Progress = 100;
        response.CurrentPhase = finalResult.Success ? "Complete" : "Failed";
        response.Iteration = totalIterations;
        response.Result = finalResult;
        response.GeneratedFiles = files.Select(f => f.Path).ToList();
        
        // If failed, include detailed error information with issues
        if (!finalResult.Success && lastValidation != null)
        {
            var issueDetails = lastValidation.Issues?
                .OrderByDescending(i => i.Severity == "critical" ? 4 : i.Severity == "high" ? 3 : i.Severity == "warning" ? 2 : 1)
                .Select(i => $"[{i.Severity?.ToUpper()}] {i.Message}" + (!string.IsNullOrEmpty(i.Suggestion) ? $" -> {i.Suggestion}" : ""))
                .ToList() ?? new List<string>();
            
            response.Error = new TaskError
            {
                Type = "validation_failed",
                Message = lastValidation.Summary ?? "Code did not pass validation",
                CanRetry = true,
                PartialResult = finalResult,
                Details = new Dictionary<string, object>
                {
                    ["score"] = lastValidation.Score,
                    ["issues"] = issueDetails,
                    ["totalIterations"] = totalIterations,
                    ["modelsUsed"] = modelsUsedDuringTask.Distinct().ToList(),
                    ["executionResult"] = finalExecution.Success ? "PASSED" : "FAILED",
                    ["executionOutput"] = finalExecution.Output?.Length > 1000 
                        ? finalExecution.Output[..1000] + "..." 
                        : finalExecution.Output ?? ""
                }
            };
        }
        
        // ☁️ Include cloud usage if Claude was used
        if (cloudUsage.ApiCalls > 0)
        {
            response.CloudUsage = cloudUsage;
        }
        
        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation("[COMPLETE] Step-by-step execution complete: {Steps} steps, {Iterations} total iterations, Score: {Score}/10, Duration: {Duration}s",
            taskPlan.Steps.Count, totalIterations, lastValidation.Score, elapsed.TotalSeconds);
        
        // Store success/failure for learning (non-critical)
        try
        {
            if (finalResult.Success && lastGeneratedCode != null)
            {
                await StoreSuccessfulResultAsync(request, lastGeneratedCode, cancellationToken);
            }
            else if (lastValidation != null)
            {
                await RecordDetailedFailureAsync(request, lastValidation, totalIterations, 
                    modelsUsedDuringTask, approachesTried, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to store learning data (non-critical)");
        }
        
        return response;
    }
    
    /// <summary>
    /// Normalize file name to avoid duplicates from different LLM naming choices
    /// E.g., "Services/Calculator.cs" and "Calculator.cs" both become "Calculator.cs"
    /// If step has a target file name, use that; otherwise use the base filename
    /// </summary>
    private static string NormalizeFileName(string generatedFileName, string? stepTargetFile)
    {
        // If the step specifies a target file, and the generated file matches its name, use the target
        if (!string.IsNullOrEmpty(stepTargetFile))
        {
            var stepBaseName = Path.GetFileNameWithoutExtension(stepTargetFile);
            var generatedBaseName = Path.GetFileNameWithoutExtension(generatedFileName);
            
            // Check if they're referring to the same class/file
            if (string.Equals(stepBaseName, generatedBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return stepTargetFile; // Use the canonical step target
            }
        }
        
        // Otherwise just use the filename (no path)
        return Path.GetFileName(generatedFileName);
    }
    
    /// <summary>
    /// Build a smart prompt for a step - shows full context and allows multiple related files
    /// </summary>
    private string BuildStepPrompt(
        PlanStep step,
        int stepNumber,
        TaskPlan taskPlan,
        Dictionary<string, FileChange> accumulatedFiles,
        ProjectSymbols? projectSymbols,
        TaskLessonsResult taskLessons,
        string language,
        List<string>? searchContext = null)
    {
        var prompt = new System.Text.StringBuilder();
        
        // Step instruction
        prompt.AppendLine($"## STEP {stepNumber} of {taskPlan.Steps.Count}: {step.Description}");
        
        // 🔍 Include search results from indexed previous steps
        if (searchContext?.Any() == true)
        {
            prompt.AppendLine();
            prompt.AppendLine("=== RELEVANT CODE FROM SEARCH (use these!) ===");
            foreach (var ctx in searchContext.Take(5))
            {
                prompt.AppendLine(ctx);
                prompt.AppendLine();
            }
        }
        prompt.AppendLine();
        
        // Target file(s)
        if (!string.IsNullOrEmpty(step.FileName))
        {
            prompt.AppendLine($"PRIMARY TARGET: {step.FileName}");
        }
        prompt.AppendLine();
        
        // STRICT RULES for step-by-step - ONE FILE ONLY
        prompt.AppendLine("=== CRITICAL RULES ===");
        prompt.AppendLine("1. Generate ONLY the ONE file for THIS step");
        prompt.AppendLine("2. Do NOT copy or redefine ANY existing classes (see below)");
        prompt.AppendLine("3. Just add 'using' statements to reference existing classes");
        prompt.AppendLine("4. Same namespace as existing files (use their namespace)");
        prompt.AppendLine("5. NO Program.Main unless step explicitly asks for Program");
        prompt.AppendLine();
        
        // Show FULL CONTENT of existing files (not just signatures!)
        if (accumulatedFiles.Any())
        {
            prompt.AppendLine("=== EXISTING CODE (reference this, do NOT redefine) ===");
            
            var existingTypes = new List<string>();
            foreach (var file in accumulatedFiles)
            {
                prompt.AppendLine($"\n// ========== {file.Key} ==========");
                
                // Show full content for small files, truncated for large ones
                var content = file.Value.Content;
                if (content.Length > 2000)
                {
                    // For large files, show structure + truncated
                    var signatures = ExtractSignatures(content, language);
                    prompt.AppendLine(signatures);
                    prompt.AppendLine("// ... (file truncated, use these signatures)");
                }
                else
                {
                    // Show full content for small files
                    prompt.AppendLine(content);
                }
                
                // Track existing type names
                var typeMatches = System.Text.RegularExpressions.Regex.Matches(
                    content, 
                    @"(?:public|internal|private)\s+(?:enum|class|struct|record|interface)\s+(\w+)");
                foreach (System.Text.RegularExpressions.Match m in typeMatches)
                {
                    existingTypes.Add(m.Groups[1].Value);
                }
            }
            
            if (existingTypes.Any())
            {
                prompt.AppendLine($"\n⚠️ ALREADY DEFINED (do NOT recreate): {string.Join(", ", existingTypes.Distinct())}");
            }
            prompt.AppendLine();
        }
        
        // Show what's coming next
        if (stepNumber < taskPlan.Steps.Count)
        {
            prompt.AppendLine("UPCOMING STEPS (for reference):");
            for (var i = stepNumber; i < Math.Min(stepNumber + 3, taskPlan.Steps.Count); i++)
            {
                var upcoming = taskPlan.Steps[i];
                prompt.AppendLine($"  - Step {i + 1}: {upcoming.Description}");
            }
            prompt.AppendLine();
        }
        
        // Lessons from past failures
        if (taskLessons.FoundLessons > 0)
        {
            prompt.AppendLine($"LEARN FROM PAST FAILURES:\n{taskLessons.AvoidanceAdvice}");
            prompt.AppendLine();
        }
        
        // Clear output instructions - be VERY specific about what to generate
        prompt.AppendLine("=== YOUR OUTPUT (ONE FILE ONLY) ===");
        prompt.AppendLine($"Implement: {step.Description}");
        if (!string.IsNullOrEmpty(step.FileName))
        {
            prompt.AppendLine($"GENERATE ONLY: {step.FileName}");
            prompt.AppendLine($"Do NOT generate any other files.");
        }
        prompt.AppendLine();
        prompt.AppendLine("REMEMBER:");
        prompt.AppendLine("- Generate ONLY the new class/code for this step");
        prompt.AppendLine("- Do NOT include classes from existing files");
        prompt.AppendLine("- Use 'using' to reference existing classes");
        
        return prompt.ToString();
    }
    
    #endregion
    
    #region 🧹 FILE CLEANUP FOR C# BUILDS
    
    /// <summary>
    /// 🧹 Clean accumulated files for C# build - remove junk and consolidate duplicates
    /// Fixes common LLM issues:
    /// - Multiple .csproj files (MSB1011 error)
    /// - Wrong .csproj names (e.g., CodingAgent.csproj from context pollution)
    /// - Duplicate .cs files with different paths
    /// - Junk config files (config.json, config.xml, etc.)
    /// - Files in wrong directories (Services/GeneratedService.cs)
    /// </summary>
    private void CleanAccumulatedFilesForCSharp(Dictionary<string, FileChange> accumulatedFiles)
    {
        var toRemove = new List<string>();
        var csprojFiles = new List<string>();
        var csFilesByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        // 🧹 POLLUTION DETECTION: These are MemoryAgent-specific patterns that indicate context pollution
        var pollutionPatterns = new[] { 
            "CodingAgent", "ValidationAgent", "MemoryAgent", "CodingOrchestrator",
            "DesignAgent", "AgentContracts", "ServerTests" 
        };
        
        foreach (var key in accumulatedFiles.Keys.ToList())
        {
            var fileName = Path.GetFileName(key);
            
            // 🧹 Remove files that look like context pollution from MemoryAgent
            if (pollutionPatterns.Any(p => fileName.Contains(p, StringComparison.OrdinalIgnoreCase) || 
                                           key.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                // Don't remove if this is actually a user task about agents
                if (!fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    // For .cs files, check if they have wrong namespaces
                    if (fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        var content = accumulatedFiles[key].Content ?? "";
                        if (pollutionPatterns.Any(p => content.Contains($"namespace {p}", StringComparison.OrdinalIgnoreCase) ||
                                                       content.Contains($"namespace.*{p}", StringComparison.OrdinalIgnoreCase)))
                        {
                            toRemove.Add(key);
                            _logger.LogWarning("🧹 Removing context-polluted file: {Path}", key);
                            continue;
                        }
                    }
                }
            }
            
            // Track .csproj files
            if (key.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                // 🧹 Remove .csproj files with pollution patterns (e.g., CodingAgent.csproj)
                if (pollutionPatterns.Any(p => fileName.Contains(p, StringComparison.OrdinalIgnoreCase)))
                {
                    toRemove.Add(key);
                    _logger.LogWarning("🧹 Removing polluted .csproj: {Path} (from context pollution)", key);
                    continue;
                }
                csprojFiles.Add(key);
                continue;
            }
            
            // Track .cs files by base name
            if (key.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (!csFilesByName.ContainsKey(fileName))
                    csFilesByName[fileName] = new List<string>();
                csFilesByName[fileName].Add(key);
                continue;
            }
            
            // 🧹 Remove junk files that shouldn't be in a C# project
            var ext = Path.GetExtension(key).ToLowerInvariant();
            if (ext is ".json" or ".xml" or ".txt" or ".yaml" or ".yml" or ".sh")
            {
                // Keep only specific config files
                if (!fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.Equals("appsettings.Development.json", StringComparison.OrdinalIgnoreCase))
                {
                    toRemove.Add(key);
                    _logger.LogWarning("🧹 Removing junk file from C# build: {Path}", key);
                }
            }
        }
        
        // 🧹 Remove GeneratedService.cs type files (common LLM pollution)
        var serviceJunk = accumulatedFiles.Keys
            .Where(k => k.EndsWith("GeneratedService.cs", StringComparison.OrdinalIgnoreCase) ||
                       k.Contains("Services/Generated", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var junk in serviceJunk)
        {
            if (!toRemove.Contains(junk))
            {
                toRemove.Add(junk);
                _logger.LogWarning("🧹 Removing junk service file: {Path}", junk);
            }
        }
        
        // Keep only ONE .csproj file (prefer the one with actual project name, not "Generated")
        if (csprojFiles.Count > 1)
        {
            // Sort: prefer files without "Generated" in name, then shorter paths
            var preferred = csprojFiles
                .OrderBy(f => f.Contains("Generated", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(f => f.Length)
                .First();
            
            foreach (var csproj in csprojFiles.Where(f => f != preferred))
            {
                toRemove.Add(csproj);
                _logger.LogWarning("🧹 Removing duplicate .csproj: {Path} (keeping {Preferred})", csproj, preferred);
            }
        }
        
        // For duplicate .cs files, keep the one with more content (or the shorter path)
        foreach (var kvp in csFilesByName.Where(k => k.Value.Count > 1))
        {
            var preferredCs = kvp.Value
                .OrderByDescending(f => accumulatedFiles[f].Content?.Length ?? 0)
                .ThenBy(f => f.Length)
                .First();
            
            foreach (var dup in kvp.Value.Where(f => f != preferredCs))
            {
                toRemove.Add(dup);
                _logger.LogWarning("🧹 Removing duplicate C# file: {Path} (keeping {Preferred})", dup, preferredCs);
            }
        }
        
        // Remove the junk
        foreach (var key in toRemove.Distinct())
        {
            accumulatedFiles.Remove(key);
        }
        
        if (toRemove.Count > 0)
        {
            _logger.LogInformation("🧹 Cleaned {Count} junk/duplicate/polluted files, {Remaining} files remain", 
                toRemove.Count, accumulatedFiles.Count);
        }
    }
    
    #endregion
}

