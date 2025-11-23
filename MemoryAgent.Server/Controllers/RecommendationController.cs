using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MemoryAgent.Server.Controllers;

/// <summary>
/// API endpoints for generating pattern recommendations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;
    private readonly ILogger<RecommendationController> _logger;

    public RecommendationController(
        IRecommendationService recommendationService,
        ILogger<RecommendationController> logger)
    {
        _recommendationService = recommendationService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a project and generate recommendations
    /// </summary>
    /// <param name="request">Recommendation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis results with prioritized recommendations</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(RecommendationResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<RecommendationResponse>> AnalyzeProject(
        [FromBody] RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Context))
        {
            return BadRequest("Context is required");
        }

        try
        {
            var result = await _recommendationService.AnalyzeAndRecommendAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing project for recommendations: {Context}", request.Context);
            return BadRequest($"Error analyzing project: {ex.Message}");
        }
    }
}

