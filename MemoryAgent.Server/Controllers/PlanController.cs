using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;
using TaskStatusModel = MemoryAgent.Server.Models.TaskStatus;

namespace MemoryAgent.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlanController : ControllerBase
{
    private readonly IPlanService _planService;
    private readonly ILogger<PlanController> _logger;

    public PlanController(
        IPlanService planService,
        ILogger<PlanController> logger)
    {
        _planService = planService;
        _logger = logger;
    }

    [HttpPost("add")]
    public async Task<ActionResult<DevelopmentPlan>> AddPlan(
        [FromBody] AddPlanRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await _planService.AddPlanAsync(request, cancellationToken);
        return Ok(plan);
    }

    [HttpPut("update")]
    public async Task<ActionResult<DevelopmentPlan>> UpdatePlan(
        [FromBody] UpdatePlanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _planService.UpdatePlanAsync(request, cancellationToken);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{planId}/complete")]
    public async Task<ActionResult<DevelopmentPlan>> CompletePlan(
        string planId,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _planService.CompletePlanAsync(planId, cancellationToken);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{planId}")]
    public async Task<ActionResult<DevelopmentPlan>> GetPlan(
        string planId,
        CancellationToken cancellationToken)
    {
        var plan = await _planService.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
        {
            return NotFound($"Plan not found: {planId}");
        }
        return Ok(plan);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<DevelopmentPlan>>> SearchPlans(
        [FromQuery] string? context = null,
        [FromQuery] PlanStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var plans = await _planService.GetPlansAsync(context, status, cancellationToken);
        return Ok(plans);
    }

    [HttpGet("list")]
    public async Task<ActionResult<List<DevelopmentPlan>>> ListPlans(
        [FromQuery] string? context = null,
        CancellationToken cancellationToken = default)
    {
        var plans = await _planService.GetPlansAsync(context, null, cancellationToken);
        return Ok(plans);
    }

    [HttpDelete("{planId}")]
    public async Task<ActionResult> DeletePlan(string planId, CancellationToken cancellationToken)
    {
        var result = await _planService.DeletePlanAsync(planId, cancellationToken);
        if (!result)
        {
            return NotFound($"Plan not found: {planId}");
        }
        return Ok(new { message = "Plan deleted successfully" });
    }

    [HttpPut("{planId}/task/{taskId}/status")]
    public async Task<ActionResult<DevelopmentPlan>> UpdateTaskStatus(
        string planId,
        string taskId,
        [FromBody] TaskStatusModel status,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = await _planService.UpdateTaskStatusAsync(planId, taskId, status, cancellationToken);
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{planId}/status")]
    public async Task<ActionResult<object>> GetPlanStatus(
        string planId,
        CancellationToken cancellationToken)
    {
        var plan = await _planService.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
        {
            return NotFound($"Plan not found: {planId}");
        }

        var totalTasks = plan.Tasks.Count;
        var completedTasks = plan.Tasks.Count(t => t.Status == TaskStatusModel.Completed);
        var inProgressTasks = plan.Tasks.Count(t => t.Status == TaskStatusModel.InProgress);
        var pendingTasks = plan.Tasks.Count(t => t.Status == TaskStatusModel.Pending);
        var blockedTasks = plan.Tasks.Count(t => t.Status == TaskStatusModel.Blocked);
        var progress = totalTasks > 0 ? (double)completedTasks / totalTasks * 100 : 0;

        return Ok(new
        {
            planId = plan.Id,
            name = plan.Name,
            status = plan.Status.ToString(),
            progress = Math.Round(progress, 2),
            totalTasks,
            completedTasks,
            inProgressTasks,
            pendingTasks,
            blockedTasks,
            createdAt = plan.CreatedAt,
            completedAt = plan.CompletedAt,
            tasks = plan.Tasks.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                status = t.Status.ToString(),
                orderIndex = t.OrderIndex,
                completedAt = t.CompletedAt
            }).OrderBy(t => t.orderIndex)
        });
    }
}

