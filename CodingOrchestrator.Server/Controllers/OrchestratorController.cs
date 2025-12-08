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
}




