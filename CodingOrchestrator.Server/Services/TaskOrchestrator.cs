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
            
            // 📁 ACCUMULATE files across iterations (not replace!)
            var accumulatedFiles = new Dictionary<string, FileChange>();

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

                var generateRequest = new GenerateCodeRequest
                {
                    Task = request.Task + existingFilesList,
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
                    _logger.LogDebug("Added {Model} to tried models. Total tried: {Count}",
                        lastGeneratedCode.ModelUsed, triedModels.Count);
                }

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
                }
                
                _logger.LogInformation("📁 Iteration {Iteration}: Generated {NewFiles} files, Total accumulated: {Total}",
                    iteration, lastGeneratedCode.FileChanges.Count, accumulatedFiles.Count);

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
}

