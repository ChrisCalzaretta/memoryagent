using MemoryAgent.Server.Models;
using TaskStatusModel = MemoryAgent.Server.Models.TaskStatus;

namespace MemoryAgent.Server.Services;

public interface IPlanService
{
    Task<DevelopmentPlan> AddPlanAsync(AddPlanRequest request, CancellationToken cancellationToken = default);
    Task<DevelopmentPlan> UpdatePlanAsync(UpdatePlanRequest request, CancellationToken cancellationToken = default);
    Task<DevelopmentPlan> CompletePlanAsync(string planId, CancellationToken cancellationToken = default);
    Task<DevelopmentPlan?> GetPlanAsync(string planId, CancellationToken cancellationToken = default);
    Task<List<DevelopmentPlan>> GetPlansAsync(string? context = null, PlanStatus? status = null, CancellationToken cancellationToken = default);
    Task<bool> DeletePlanAsync(string planId, CancellationToken cancellationToken = default);
    Task<DevelopmentPlan> UpdateTaskStatusAsync(string planId, string taskId, TaskStatusModel status, CancellationToken cancellationToken = default);
}

