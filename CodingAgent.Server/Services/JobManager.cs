using System.Collections.Concurrent;
using System.Text.Json;
using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Manages background code generation jobs with persistence
/// </summary>
public interface IJobManager
{
    Task<string> StartJobAsync(string task, string? language, int maxIterations, CancellationToken ct);
    JobStatus? GetJobStatus(string jobId);
    Task CancelJobAsync(string jobId);
    List<JobStatus> ListJobs();
}

public class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<string, JobState> _jobs = new();
    private readonly ICodeGenerationService _codeGeneration;
    private readonly IValidationAgentClient _validation;
    private readonly IStubGenerator _stubGenerator;
    private readonly string _jobsPath;
    private readonly ILogger<JobManager> _logger;

    public JobManager(
        ICodeGenerationService codeGeneration,
        IValidationAgentClient validation,
        IStubGenerator stubGenerator,
        IConfiguration configuration,
        ILogger<JobManager> logger)
    {
        _codeGeneration = codeGeneration;
        _validation = validation;
        _stubGenerator = stubGenerator;
        _jobsPath = configuration["JobPersistence:Path"] ?? "/data/jobs";
        _logger = logger;

        // Ensure directory exists
        Directory.CreateDirectory(_jobsPath);
        _logger.LogInformation("💾 Job persistence at {Path}", _jobsPath);
    }

    public async Task<string> StartJobAsync(string task, string? language, int maxIterations, CancellationToken ct)
    {
        var jobId = $"job_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var jobState = new JobState
        {
            JobId = jobId,
            Task = task,
            Language = language,
            MaxIterations = maxIterations,
            Status = "running",
            Progress = 0,
            StartedAt = DateTime.UtcNow,
            CancellationTokenSource = cts
        };

        _jobs[jobId] = jobState;
        await PersistJobAsync(jobState);

        // Start background task with 10-ATTEMPT RETRY LOOP!
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("🚀 Job {JobId} started: {Task} (max {MaxIterations} attempts)", 
                    jobId, task, maxIterations);
                
                ValidationFeedback? feedback = null;
                GenerateCodeResponse? lastResult = null;
                var workspacePath = Path.Combine(_jobsPath, jobId);
                Directory.CreateDirectory(workspacePath);
                
                // 🔄 RETRY LOOP - Keep trying until we get a good score!
                for (int iteration = 1; iteration <= maxIterations; iteration++)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    
                    jobState.Progress = (iteration * 90) / maxIterations;
                    jobState.Status = $"running (attempt {iteration}/{maxIterations})";
                    await PersistJobAsync(jobState);
                    
                    _logger.LogInformation("🔄 Job {JobId} - Attempt {Iteration}/{MaxIterations}", 
                        jobId, iteration, maxIterations);
                    
                    // Generate or fix code
                    var request = new GenerateCodeRequest
                    {
                        Task = task,
                        Language = language,
                        WorkspacePath = workspacePath,
                        PreviousFeedback = feedback
                    };
                    
                    lastResult = feedback == null
                        ? await _codeGeneration.GenerateAsync(request, cts.Token)
                        : await _codeGeneration.FixAsync(request, cts.Token);
                    
                    if (!lastResult.Success)
                    {
                        _logger.LogWarning("⚠️ Job {JobId} - Generation failed on attempt {Iteration}: {Error}", 
                            jobId, iteration, lastResult.Error);
                        continue;
                    }
                    
                    _logger.LogInformation("✅ Job {JobId} - Generated {FileCount} files with {Model}", 
                        jobId, lastResult.FileChanges.Count, lastResult.ModelUsed);
                    
                // ✅ VALIDATE the generated code
                var validation = await _validation.ValidateAsync(new ValidateCodeRequest
                {
                    Files = lastResult.FileChanges.Select(f => new CodeFile
                    {
                        Path = f.Path,
                        Content = f.Content
                    }).ToList(),
                    Context = "codegen",
                    Language = language ?? "csharp",
                    WorkspacePath = workspacePath
                }, cts.Token);
                    
                    _logger.LogInformation("📊 Job {JobId} - Validation score: {Score}/10 ({IssueCount} issues)", 
                        jobId, validation.Score, validation.Issues.Count);
                    
                    // 🎯 SMART BREAK LOGIC - Your exact specs!
                    // Break at 8+ (excellent)
                    if (validation.Score >= 8)
                    {
                        _logger.LogInformation("✅ Job {JobId} - EXCELLENT score {Score}/10 on attempt {Iteration}!", 
                            jobId, validation.Score, iteration);
                        break;
                    }
                    
                    // Break at 6.5+ after attempt 3 (good enough)
                    if (validation.Score >= 6.5 && iteration >= 3)
                    {
                        _logger.LogWarning("⚠️ Job {JobId} - ACCEPTABLE score {Score}/10 on attempt {Iteration} - stopping", 
                            jobId, validation.Score, iteration);
                        break;
                    }
                    
                    // Break after 10 attempts (something is wrong)
                    if (iteration >= maxIterations)
                    {
                        _logger.LogError("🚨 Job {JobId} - CRITICAL: Score {Score}/10 after {Iterations} attempts!", 
                            jobId, validation.Score, iteration);
                        break;
                    }
                    
                    // Prepare feedback for next iteration with HISTORY tracking
                    var attemptHistory = new AttemptHistory
                    {
                        AttemptNumber = iteration,
                        Model = lastResult.ModelUsed,
                        Score = validation.Score,
                        Issues = validation.Issues,
                        BuildErrors = validation.BuildErrors,
                        Summary = validation.Summary,
                        Timestamp = DateTime.UtcNow
                    };
                    
                    feedback = validation.ToFeedback();
                    feedback.TriedModels.Add(lastResult.ModelUsed);
                    feedback.History ??= new List<AttemptHistory>();
                    feedback.History.Add(attemptHistory);
                    
                    _logger.LogInformation("⚠️ Job {JobId} - Score {Score}/10 on attempt {Iteration}, retrying with {Model}...", 
                        jobId, validation.Score, iteration, lastResult.ModelUsed);
                }

                jobState.Status = lastResult?.Success == true ? "completed" : "failed";
                jobState.Progress = 100;
                jobState.Result = lastResult;
                jobState.CompletedAt = DateTime.UtcNow;
                jobState.Error = lastResult?.Error;

                await PersistJobAsync(jobState);
                _logger.LogInformation("✅ Job {JobId} completed: {Status}", jobId, jobState.Status);
            }
            catch (OperationCanceledException)
            {
                jobState.Status = "cancelled";
                jobState.CompletedAt = DateTime.UtcNow;
                await PersistJobAsync(jobState);
                _logger.LogInformation("🛑 Job {JobId} cancelled", jobId);
            }
            catch (Exception ex)
            {
                jobState.Status = "failed";
                jobState.Error = ex.Message;
                jobState.CompletedAt = DateTime.UtcNow;
                await PersistJobAsync(jobState);
                _logger.LogError(ex, "❌ Job {JobId} failed", jobId);
            }
        }, cts.Token);

        return jobId;
    }

    public JobStatus? GetJobStatus(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            return new JobStatus
            {
                JobId = state.JobId,
                Task = state.Task,
                Status = state.Status,
                Progress = state.Progress,
                StartedAt = state.StartedAt,
                CompletedAt = state.CompletedAt,
                Error = state.Error,
                Result = state.Result
            };
        }

        // Try to load from disk
        var filePath = Path.Combine(_jobsPath, $"{jobId}.json");
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<JobStatus>(json);
            }
            catch
            {
                // Ignore
            }
        }

        return null;
    }

    public async Task CancelJobAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            state.CancellationTokenSource.Cancel();
            state.Status = "cancelled";
            state.CompletedAt = DateTime.UtcNow;
            await PersistJobAsync(state);
        }
    }

    public List<JobStatus> ListJobs()
    {
        return _jobs.Values.Select(s => new JobStatus
        {
            JobId = s.JobId,
            Task = s.Task,
            Status = s.Status,
            Progress = s.Progress,
            StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt,
            Error = s.Error,
            Result = s.Result
        }).ToList();
    }

    private async Task PersistJobAsync(JobState state)
    {
        try
        {
            var status = new JobStatus
            {
                JobId = state.JobId,
                Task = state.Task,
                Status = state.Status,
                Progress = state.Progress,
                StartedAt = state.StartedAt,
                CompletedAt = state.CompletedAt,
                Error = state.Error,
                Result = state.Result
            };

            var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            var filePath = Path.Combine(_jobsPath, $"{state.JobId}.json");
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist job {JobId}", state.JobId);
        }
    }

    private class JobState
    {
        public string JobId { get; set; } = "";
        public string Task { get; set; } = "";
        public string? Language { get; set; }
        public int MaxIterations { get; set; }
        public string Status { get; set; } = "running";
        public int Progress { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Error { get; set; }
        public GenerateCodeResponse? Result { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }
}

public class JobStatus
{
    public string JobId { get; set; } = "";
    public string Task { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public GenerateCodeResponse? Result { get; set; }
}

