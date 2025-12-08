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
        ILogger<TaskOrchestrator> logger)
    {
        _codingAgent = codingAgent;
        _validationAgent = validationAgent;
        _memoryAgent = memoryAgent;
        _executionService = executionService;
        _pathTranslation = pathTranslation;
        _logger = logger;
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
            
            // 📁 ACCUMULATE files across iterations (not replace!)
            var accumulatedFiles = new Dictionary<string, FileChange>();
            
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
                _logger.LogInformation("📋 Generated plan with {Steps} steps", taskPlan.Steps.Count);
                
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
                if (feedback != null)
                {
                    feedback.TriedModels = triedModels;
                }

                // 📁 Tell the LLM what files already exist
                var existingFilesList = accumulatedFiles.Any()
                    ? $"\n\n📁 FILES ALREADY GENERATED ({accumulatedFiles.Count}):\n" + 
                      string.Join("\n", accumulatedFiles.Keys.Select(f => $"- {f}")) +
                      "\n\n⚠️ Generate the MISSING files. You may also update existing files if needed."
                    : "";

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
                
                // 🔍 Include project symbols context (only relevant ones)
                var symbolsSection = "";
                if (projectSymbols != null && (projectSymbols.Classes.Any() || projectSymbols.Functions.Any()))
                {
                    symbolsSection = "\n\n🔍 AVAILABLE SYMBOLS IN PROJECT:\n";
                    foreach (var cls in projectSymbols.Classes.Take(10))
                    {
                        symbolsSection += $"  • {cls.Name}: {cls.ImportStatement}\n";
                        if (cls.Methods.Any())
                            symbolsSection += $"    Methods: {string.Join(", ", cls.Methods.Take(5))}\n";
                    }
                }
                
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
                
                var generateRequest = new GenerateCodeRequest
                {
                    Task = request.Task + existingFilesList + lessonsSection + planSection + symbolsSection + similarTasksSection + designSection,
                    Language = request.Language,
                    Context = context,
                    WorkspacePath = request.WorkspacePath,
                    PreviousFeedback = feedback
                };

                lastGeneratedCode = iteration == 1 
                    ? await _codingAgent.GenerateAsync(generateRequest, cancellationToken)
                    : await _codingAgent.FixAsync(generateRequest, cancellationToken);
                
                // Track which model was used
                if (!string.IsNullOrEmpty(lastGeneratedCode.ModelUsed))
                {
                    triedModels.Add(lastGeneratedCode.ModelUsed);
                    modelsUsedDuringTask.Add(lastGeneratedCode.ModelUsed);  // For failure learning
                    _logger.LogDebug("Added {Model} to tried models. Total tried: {Count}",
                        lastGeneratedCode.ModelUsed, triedModels.Count);
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
                    continue;
                }

                // 📁 ACCUMULATE files - merge new files with existing ones
                foreach (var file in lastGeneratedCode.FileChanges)
                {
                    accumulatedFiles[file.Path] = file;
                    _logger.LogDebug("📁 Accumulated file: {Path} (total: {Count})", file.Path, accumulatedFiles.Count);
                    
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
                    
                    // Create feedback with REAL execution errors (not LLM guesses!)
                    lastValidation = new ValidateCodeResponse
                    {
                        Score = 0,
                        Passed = false,
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
                    WorkspacePath = request.WorkspacePath
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
                if (request.AutoWriteFiles && lastValidation.Passed && !string.IsNullOrEmpty(request.WorkspacePath))
                {
                    filesWritten = await WriteFilesToWorkspaceAsync(files, request.WorkspacePath, cancellationToken);
                    _logger.LogInformation("📝 Auto-wrote {Count} files to workspace", filesWritten.Count);
                }
                
                var autoWriteStatus = request.AutoWriteFiles && lastValidation.Passed
                    ? $" Files written to: {string.Join(", ", filesWritten)}"
                    : " Files returned for manual review.";
                
                var result = new TaskResult
                {
                    Success = lastValidation.Passed,
                    Files = files,
                    ValidationScore = lastValidation.Score,
                    TotalIterations = iteration,
                    TotalDurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Summary = lastValidation.Passed 
                        ? $"Successfully generated code with score {lastValidation.Score}/10 in {iteration} iteration(s).{autoWriteStatus}"
                        : $"Code generated but validation score {lastValidation.Score}/10 did not meet minimum {request.MinValidationScore}"
                };

                response.Status = lastValidation.Passed ? TaskState.Complete : TaskState.Failed;
                response.Progress = 100;
                response.Result = result;
                response.Iteration = iteration;

                // Store successful Q&A and record prompt feedback in Lightning (with graceful degradation)
                if (lastValidation.Passed)
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
}

