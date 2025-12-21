using Microsoft.AspNetCore.Mvc;
using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingOrchestrator.Server.Services;

namespace CodingOrchestrator.Server.Controllers;

[ApiController]
[Route("api/orchestrator")]
public class OrchestratorController : ControllerBase
{
    private readonly ITaskOrchestrator _orchestrator;
    private readonly IJobManager _jobManager;
    private readonly ILogger<OrchestratorController> _logger;

    public OrchestratorController(
        ITaskOrchestrator orchestrator,
        IJobManager jobManager,
        ILogger<OrchestratorController> logger)
    {
        _orchestrator = orchestrator;
        _jobManager = jobManager;
        _logger = logger;
    }

    /// <summary>
    /// Start a new coding task
    /// </summary>
    [HttpPost("task")]
    public async Task<ActionResult<TaskStatusResponse>> StartTask(
        [FromBody] OrchestrateTaskRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting task: {Task}", request.Task);

        if (request.Background)
        {
            // Start as background job
            var jobId = await _jobManager.StartJobAsync(request, cancellationToken);
            
            return Ok(new TaskStatusResponse
            {
                JobId = jobId,
                Status = TaskState.Queued,
                Progress = 0,
                CurrentPhase = "Queued",
                Message = "Task started. Use GET /api/orchestrator/task/{jobId} to check status."
            });
        }
        else
        {
            // Run synchronously (not recommended for long tasks)
            var result = await _orchestrator.ExecuteTaskAsync(request, cancellationToken);
            return Ok(result);
        }
    }

    /// <summary>
    /// Get task status
    /// </summary>
    [HttpGet("task/{jobId}")]
    public ActionResult<TaskStatusResponse> GetTaskStatus(string jobId)
    {
        var status = _jobManager.GetJobStatus(jobId);
        
        if (status == null)
        {
            return NotFound(new { error = $"Job {jobId} not found" });
        }
        
        return Ok(status);
    }

    /// <summary>
    /// Cancel a running task
    /// </summary>
    [HttpDelete("task/{jobId}")]
    public ActionResult CancelTask(string jobId)
    {
        var cancelled = _jobManager.CancelJob(jobId);
        
        if (!cancelled)
        {
            return NotFound(new { error = $"Job {jobId} not found or already completed" });
        }
        
        return Ok(new { message = $"Job {jobId} cancelled" });
    }

    /// <summary>
    /// List all active tasks
    /// </summary>
    [HttpGet("tasks")]
    public ActionResult<IEnumerable<TaskStatusResponse>> ListTasks()
    {
        var tasks = _jobManager.GetAllJobs();
        return Ok(tasks);
    }
    
    /// <summary>
    /// Provide help for a task that is stuck (NeedsHelp state)
    /// User can provide hints, corrections, or code snippets to help the LLM
    /// </summary>
    [HttpPost("task/{jobId}/help")]
    public async Task<ActionResult<TaskStatusResponse>> ProvideHelp(
        string jobId,
        [FromBody] HelpRequest request,
        CancellationToken cancellationToken)
    {
        var status = _jobManager.GetJobStatus(jobId);
        
        if (status == null)
        {
            return NotFound(new { error = $"Job {jobId} not found" });
        }
        
        if (status.Status != TaskState.NeedsHelp)
        {
            return BadRequest(new { 
                error = $"Job {jobId} is not waiting for help (current state: {status.Status})",
                hint = "Only jobs in NeedsHelp state can receive help"
            });
        }
        
        _logger.LogInformation("[HELP] User provided help for job {JobId}: {Hint}", 
            jobId, request.Hint?.Length > 100 ? request.Hint[..100] + "..." : request.Hint);
        
        // Resume the job with user's feedback
        var resumed = await _jobManager.ResumeWithHelpAsync(jobId, request, cancellationToken);
        
        if (!resumed)
        {
            return BadRequest(new { error = "Failed to resume job with help" });
        }
        
        // Return updated status
        var newStatus = _jobManager.GetJobStatus(jobId);
        return Ok(newStatus);
    }
}

/// <summary>
/// Request to provide help for a stuck task
/// </summary>
public class HelpRequest
{
    /// <summary>
    /// Hint or guidance for the LLM (e.g., "The Calculator class should use double, not int")
    /// </summary>
    public string? Hint { get; set; }
    
    /// <summary>
    /// Optional: Code snippet to use or reference
    /// </summary>
    public string? CodeSnippet { get; set; }
    
    /// <summary>
    /// Optional: Specific file to focus on fixing
    /// </summary>
    public string? FocusFile { get; set; }
    
    /// <summary>
    /// If true, skip this step and continue to next (not recommended)
    /// </summary>
    public bool SkipStep { get; set; } = false;
}





