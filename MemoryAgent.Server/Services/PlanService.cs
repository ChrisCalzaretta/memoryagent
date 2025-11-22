using MemoryAgent.Server.Models;
using TaskStatusModel = MemoryAgent.Server.Models.TaskStatus;

namespace MemoryAgent.Server.Services;

public class PlanService : IPlanService
{
    private readonly IGraphService _graphService;
    private readonly ITaskValidationService _validationService;
    private readonly ILogger<PlanService> _logger;

    public PlanService(
        IGraphService graphService, 
        ITaskValidationService validationService,
        ILogger<PlanService> logger)
    {
        _graphService = graphService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<DevelopmentPlan> AddPlanAsync(AddPlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = new DevelopmentPlan
        {
            Context = request.Context,
            Name = request.Name,
            Description = request.Description,
            Status = PlanStatus.Active,
            Tasks = request.Tasks.Select((t, index) => new PlanTask
            {
                Title = t.Title,
                Description = t.Description,
                OrderIndex = t.OrderIndex,
                Dependencies = t.Dependencies,
                ValidationRules = t.ValidationRules,
                Status = TaskStatusModel.Pending
            }).ToList()
        };

        await _graphService.StorePlanAsync(plan, cancellationToken);
        
        _logger.LogInformation("Added Plan: {Name} with {TaskCount} tasks in context {Context}", 
            plan.Name, plan.Tasks.Count, plan.Context);
        
        return plan;
    }

    public async Task<DevelopmentPlan> UpdatePlanAsync(UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await _graphService.GetPlanAsync(request.PlanId, cancellationToken);
        if (plan == null)
        {
            throw new InvalidOperationException($"Plan not found: {request.PlanId}");
        }

        if (request.Name != null) plan.Name = request.Name;
        if (request.Description != null) plan.Description = request.Description;
        if (request.Status.HasValue) plan.Status = request.Status.Value;

        if (request.Tasks != null)
        {
            foreach (var taskUpdate in request.Tasks)
            {
                var task = plan.Tasks.FirstOrDefault(t => t.Id == taskUpdate.TaskId);
                if (task != null)
                {
                    if (taskUpdate.Title != null) task.Title = taskUpdate.Title;
                    if (taskUpdate.Description != null) task.Description = taskUpdate.Description;
                    if (taskUpdate.Status.HasValue)
                    {
                        task.Status = taskUpdate.Status.Value;
                        if (taskUpdate.Status.Value == TaskStatusModel.Completed)
                        {
                            task.CompletedAt = DateTime.UtcNow;
                        }
                    }
                }
            }
        }

        // Auto-complete plan if all tasks are completed
        if (plan.Tasks.All(t => t.Status == TaskStatusModel.Completed))
        {
            plan.Status = PlanStatus.Completed;
            plan.CompletedAt = DateTime.UtcNow;
        }

        await _graphService.UpdatePlanAsync(plan, cancellationToken);
        
        _logger.LogInformation("Updated Plan: {PlanId}", request.PlanId);
        return plan;
    }

    public async Task<DevelopmentPlan> CompletePlanAsync(string planId, CancellationToken cancellationToken = default)
    {
        var plan = await _graphService.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
        {
            throw new InvalidOperationException($"Plan not found: {planId}");
        }

        plan.Status = PlanStatus.Completed;
        plan.CompletedAt = DateTime.UtcNow;

        // Mark all incomplete tasks as cancelled
        foreach (var task in plan.Tasks.Where(t => t.Status != TaskStatusModel.Completed))
        {
            task.Status = TaskStatusModel.Cancelled;
        }

        await _graphService.UpdatePlanAsync(plan, cancellationToken);
        
        _logger.LogInformation("Completed Plan: {PlanId}", planId);
        return plan;
    }

    public async Task<DevelopmentPlan?> GetPlanAsync(string planId, CancellationToken cancellationToken = default)
    {
        return await _graphService.GetPlanAsync(planId, cancellationToken);
    }

    public async Task<List<DevelopmentPlan>> GetPlansAsync(string? context = null, PlanStatus? status = null, CancellationToken cancellationToken = default)
    {
        return await _graphService.GetPlansAsync(context, status, cancellationToken);
    }

    public async Task<bool> DeletePlanAsync(string planId, CancellationToken cancellationToken = default)
    {
        var result = await _graphService.DeletePlanAsync(planId, cancellationToken);
        
        if (result)
        {
            _logger.LogInformation("Deleted Plan: {PlanId}", planId);
        }
        
        return result;
    }

    public async Task<DevelopmentPlan> UpdateTaskStatusAsync(string planId, string taskId, TaskStatusModel status, CancellationToken cancellationToken = default)
    {
        // Get the plan and task
        var plan = await _graphService.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
        {
            throw new InvalidOperationException($"Plan not found: {planId}");
        }

        var task = plan.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null)
        {
            throw new InvalidOperationException($"Task not found: {taskId}");
        }

        // If trying to mark as Completed, validate rules first
        if (status == TaskStatusModel.Completed && task.ValidationRules.Any())
        {
            _logger.LogInformation("🔍 Validating task '{Title}' before completion...", task.Title);
            
            var validationResult = await _validationService.ValidateTaskAsync(task, plan.Context, cancellationToken);
            
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("❌ Task validation failed: {Failures}", string.Join(", ", validationResult.Failures.Select(f => f.Message)));
                
                // Check if we can auto-fix
                var canAutoFix = validationResult.Failures.Any(f => f.CanAutoFix);
                if (canAutoFix)
                {
                    _logger.LogInformation("🔧 Attempting auto-fix for validation failures...");
                    var wasFixed = await _validationService.AutoFixValidationFailuresAsync(task, validationResult, plan.Context, cancellationToken);
                    
                    if (wasFixed)
                    {
                        _logger.LogInformation("✅ Auto-fix successful! Please re-validate.");
                        
                        // Re-validate after auto-fix
                        validationResult = await _validationService.ValidateTaskAsync(task, plan.Context, cancellationToken);
                        
                        if (!validationResult.IsValid)
                        {
                            throw new InvalidOperationException($"Task validation failed after auto-fix: {string.Join(", ", validationResult.Failures.Select(f => f.Message))}");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Task validation failed and auto-fix unsuccessful: {string.Join(", ", validationResult.Failures.Select(f => f.Message))}");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Task validation failed: {string.Join(", ", validationResult.Failures.Select(f => f.Message))}");
                }
            }
            
            _logger.LogInformation("✅ Task validation passed!");
        }

        // Proceed with status update
        var updateRequest = new UpdatePlanRequest
        {
            PlanId = planId,
            Tasks = new List<UpdatePlanTaskRequest>
            {
                new UpdatePlanTaskRequest { TaskId = taskId, Status = status }
            }
        };

        return await UpdatePlanAsync(updateRequest, cancellationToken);
    }
}

