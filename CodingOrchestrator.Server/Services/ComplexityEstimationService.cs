using System.Diagnostics;
using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingOrchestrator.Server.Clients;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Service for estimating task complexity and determining iteration count
/// Split from TaskOrchestrator for better separation of concerns
/// </summary>
public interface IComplexityEstimationService
{
    Task<ComplexityEstimate> EstimateAsync(OrchestrateTaskRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Result of complexity estimation
/// </summary>
public class ComplexityEstimate
{
    public string ComplexityLevel { get; set; } = "Medium";
    public int RecommendedIterations { get; set; } = 5;
    public int EffectiveIterations { get; set; } = 5;
    public int EstimatedFiles { get; set; } = 1;
    public string Reasoning { get; set; } = "";
    public bool Success { get; set; } = true;
    public PhaseInfo? Phase { get; set; }
}

public class ComplexityEstimationService : IComplexityEstimationService
{
    private readonly ICodingAgentClient _codingAgent;
    private readonly ILogger<ComplexityEstimationService> _logger;
    private static readonly ActivitySource _activitySource = new("CodingOrchestrator.ComplexityEstimation");

    public ComplexityEstimationService(
        ICodingAgentClient codingAgent,
        ILogger<ComplexityEstimationService> logger)
    {
        _codingAgent = codingAgent;
        _logger = logger;
    }

    public async Task<ComplexityEstimate> EstimateAsync(
        OrchestrateTaskRequest request, 
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("EstimateComplexity");
        
        var phase = new PhaseInfo
        {
            Name = "complexity_estimation",
            StartedAt = DateTime.UtcNow,
            Status = "running"
        };

        try
        {
            var estimateRequest = new EstimateComplexityRequest
            {
                Task = request.Task,
                Language = request.Language,
                Context = request.Context
            };

            var complexity = await _codingAgent.EstimateComplexityAsync(estimateRequest, cancellationToken);

            // Use user's max iterations as hard cap - don't let LLM override it
            var effectiveMaxIterations = request.MaxIterations;
            if (complexity.Success && complexity.RecommendedIterations > request.MaxIterations)
            {
                _logger.LogWarning("🧠 LLM recommended {Recommended} iterations but user set max={Max}. Respecting user's limit.",
                    complexity.RecommendedIterations, request.MaxIterations);
            }

            phase.CompletedAt = DateTime.UtcNow;
            phase.DurationMs = (long)(phase.CompletedAt.Value - phase.StartedAt).TotalMilliseconds;
            phase.Status = "complete";
            phase.Details = new Dictionary<string, object>
            {
                ["complexityLevel"] = complexity.ComplexityLevel,
                ["recommendedIterations"] = complexity.RecommendedIterations,
                ["effectiveIterations"] = effectiveMaxIterations,
                ["estimatedFiles"] = complexity.EstimatedFiles,
                ["reasoning"] = complexity.Reasoning
            };

            activity?.SetTag("complexity.level", complexity.ComplexityLevel);
            activity?.SetTag("complexity.iterations", effectiveMaxIterations);

            return new ComplexityEstimate
            {
                ComplexityLevel = complexity.ComplexityLevel,
                RecommendedIterations = complexity.RecommendedIterations,
                EffectiveIterations = effectiveMaxIterations,
                EstimatedFiles = complexity.EstimatedFiles,
                Reasoning = complexity.Reasoning,
                Success = complexity.Success,
                Phase = phase
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to estimate complexity, using defaults");
            
            phase.CompletedAt = DateTime.UtcNow;
            phase.DurationMs = (long)(phase.CompletedAt.Value - phase.StartedAt).TotalMilliseconds;
            phase.Status = "fallback";
            phase.Details = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["fallback"] = "Using default iterations"
            };

            return new ComplexityEstimate
            {
                EffectiveIterations = request.MaxIterations,
                Success = false,
                Phase = phase
            };
        }
    }
}

