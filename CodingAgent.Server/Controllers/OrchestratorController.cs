using Microsoft.AspNetCore.Mvc;
using CodingAgent.Server.Services;

namespace CodingAgent.Server.Controllers;

[ApiController]
[Route("api/orchestrator")]
public class OrchestratorController : ControllerBase
{
    private readonly IJobManager _jobManager;
    private readonly ILogger<OrchestratorController> _logger;

    public OrchestratorController(
        IJobManager jobManager,
        ILogger<OrchestratorController> logger)
    {
        _jobManager = jobManager;
        _logger = logger;
    }

    /// <summary>
    /// Start a new code generation job (background)
    /// </summary>
    [HttpPost("orchestrate")]
    public async Task<ActionResult<OrchestrateResponse>> Orchestrate([FromBody] OrchestrateRequest request)
    {
        _logger.LogInformation("🚀 Orchestrate request: {Task}", request.Task);

        try
        {
            var jobId = await _jobManager.StartJobAsync(
                request.Task,
                request.Language,
                request.MaxIterations,
                request.WorkspacePath,
                HttpContext.RequestAborted);

            return Ok(new OrchestrateResponse
            {
                JobId = jobId,
                Message = "Job started successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start orchestration job");
            return StatusCode(500, new OrchestrateResponse
            {
                JobId = "",
                Message = $"Error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Get job status
    /// </summary>
    [HttpGet("status/{jobId}")]
    public ActionResult<JobStatus> GetStatus(string jobId)
    {
        var status = _jobManager.GetJobStatus(jobId);
        if (status == null)
        {
            return NotFound(new { message = $"Job {jobId} not found" });
        }

        return Ok(status);
    }

    /// <summary>
    /// List all jobs
    /// </summary>
    [HttpGet("jobs")]
    public ActionResult<List<JobStatus>> ListJobs()
    {
        var jobs = _jobManager.ListJobs();
        return Ok(jobs);
    }

    /// <summary>
    /// Cancel a job
    /// </summary>
    [HttpPost("cancel/{jobId}")]
    public async Task<ActionResult> CancelJob(string jobId)
    {
        await _jobManager.CancelJobAsync(jobId);
        return Ok(new { message = $"Job {jobId} cancelled" });
    }

    /// <summary>
    /// Health check
    /// </summary>
    [HttpGet("/health")]
    public ActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "CodingAgent.Server v2.0 (NEW)",
            timestamp = DateTime.UtcNow
        });
    }
}

public class OrchestrateRequest
{
    public string Task { get; set; } = "";
    public string? Language { get; set; }
    public int MaxIterations { get; set; } = 50;
    public string? WorkspacePath { get; set; } // e.g., "E:\GitHub\testagent" → mounted as "/workspace/testagent"
}

public class OrchestrateResponse
{
    public string JobId { get; set; } = "";
    public string Message { get; set; } = "";
}


