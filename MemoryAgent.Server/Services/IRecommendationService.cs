using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for generating pattern recommendations
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Analyze a project and generate recommendations
    /// </summary>
    Task<RecommendationResponse> AnalyzeAndRecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken = default);
}

