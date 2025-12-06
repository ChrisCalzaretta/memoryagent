using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingOrchestrator.Server.Clients;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Orchestrates the multi-agent coding workflow
/// </summary>
public class TaskOrchestrator : ITaskOrchestrator
{
    private readonly ICodingAgentClient _codingAgent;
    private readonly IValidationAgentClient _validationAgent;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<TaskOrchestrator> _logger;
    private IJobManager? _jobManager;

    public TaskOrchestrator(
        ICodingAgentClient codingAgent,
        IValidationAgentClient validationAgent,
        IMemoryAgentClient memoryAgent,
        ILogger<TaskOrchestrator> logger)
    {
        _codingAgent = codingAgent;
        _validationAgent = validationAgent;
        _memoryAgent = memoryAgent;
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
            // Phase 1: Get context from Lightning (if available)
            UpdateProgress(jobId, TaskState.Running, 10, "Gathering context", 0);
            var contextPhase = StartPhase("context_gathering");
            
            var context = await _memoryAgent.GetContextAsync(request.Task, request.Context, cancellationToken);
            
            EndPhase(jobId, contextPhase);
            response.Timeline.Add(contextPhase);

            // Phase 2-N: Coding and Validation loop
            GenerateCodeResponse? lastGeneratedCode = null;
            ValidateCodeResponse? lastValidation = null;
            var iteration = 0;
            
            // Track which models have been tried (for smart rotation)
            var triedModels = new HashSet<string>();

            while (iteration < request.MaxIterations)
            {
                iteration++;
                cancellationToken.ThrowIfCancellationRequested();

                // Coding Agent phase
                var codingProgress = 10 + (iteration * 80 / request.MaxIterations);
                UpdateProgress(jobId, TaskState.Running, codingProgress, $"Coding Agent (iteration {iteration})", iteration);
                
                var codingPhase = StartPhase("coding_agent", iteration);

                // Build feedback with tried models so CodingAgent can rotate
                var feedback = lastValidation?.ToFeedback();
                if (feedback != null)
                {
                    feedback.TriedModels = triedModels;
                }

                var generateRequest = new GenerateCodeRequest
                {
                    Task = request.Task,
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

                // Validation Agent phase
                var validationProgress = codingProgress + 10;
                UpdateProgress(jobId, TaskState.Running, validationProgress, $"Validation Agent (iteration {iteration})", iteration);
                
                var validationPhase = StartPhase("validation_agent", iteration);

                var validateRequest = new ValidateCodeRequest
                {
                    Files = lastGeneratedCode.FileChanges.Select(f => new CodeFile
                    {
                        Path = f.Path,
                        Content = f.Content,
                        IsNew = f.Type == FileChangeType.Created
                    }).ToList(),
                    Context = request.Context,
                    OriginalTask = request.Task,
                    WorkspacePath = request.WorkspacePath
                };

                lastValidation = await _validationAgent.ValidateAsync(validateRequest, cancellationToken);
                
                validationPhase.Details = new Dictionary<string, object>
                {
                    ["score"] = lastValidation.Score,
                    ["passed"] = lastValidation.Passed,
                    ["issueCount"] = lastValidation.Issues.Count
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

            // Build result
            if (lastGeneratedCode?.Success == true && lastValidation != null)
            {
                var result = new TaskResult
                {
                    Success = lastValidation.Passed,
                    Files = lastGeneratedCode.FileChanges.Select(f => new GeneratedFile
                    {
                        Path = f.Path,
                        Content = f.Content,
                        ChangeType = f.Type,
                        Reason = f.Reason
                    }).ToList(),
                    ValidationScore = lastValidation.Score,
                    TotalIterations = iteration,
                    TotalDurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds,
                    Summary = lastValidation.Passed 
                        ? $"Successfully generated code with score {lastValidation.Score}/10 in {iteration} iteration(s)"
                        : $"Code generated but validation score {lastValidation.Score}/10 did not meet minimum {request.MinValidationScore}"
                };

                response.Status = lastValidation.Passed ? TaskState.Complete : TaskState.Failed;
                response.Progress = 100;
                response.Result = result;
                response.Iteration = iteration;

                // Store successful Q&A and record prompt feedback in Lightning
                if (lastValidation.Passed)
                {
                    await StoreSuccessfulResultAsync(request, lastGeneratedCode, cancellationToken);
                    
                    // Record prompt feedback for learning
                    await _memoryAgent.RecordPromptFeedbackAsync(
                        "coding_agent_system",
                        wasSuccessful: true,
                        rating: lastValidation.Score,
                        cancellationToken);
                }
                else
                {
                    // Record failure for learning
                    await _memoryAgent.RecordPromptFeedbackAsync(
                        "coding_agent_system",
                        wasSuccessful: false,
                        rating: lastValidation.Score,
                        cancellationToken);
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
}

