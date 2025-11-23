using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MemoryAgent.Server.Controllers;

/// <summary>
/// API endpoints for validating code against Azure best practices
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly IBestPracticeValidationService _validationService;
    private readonly ILogger<ValidationController> _logger;

    public ValidationController(
        IBestPracticeValidationService validationService,
        ILogger<ValidationController> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// Validate best practices for a project
    /// </summary>
    /// <param name="request">Validation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation results with compliance scores and recommendations</returns>
    [HttpPost("check-best-practices")]
    [ProducesResponseType(typeof(BestPracticeValidationResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<BestPracticeValidationResponse>> ValidateBestPractices(
        [FromBody] BestPracticeValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Context))
        {
            return BadRequest("Context is required");
        }

        try
        {
            var result = await _validationService.ValidateBestPracticesAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating best practices for context: {Context}", request.Context);
            return BadRequest($"Error validating best practices: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all available best practices
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of best practice names</returns>
    [HttpGet("best-practices")]
    [ProducesResponseType(typeof(List<string>), 200)]
    public async Task<ActionResult<List<string>>> GetAvailableBestPractices(
        CancellationToken cancellationToken)
    {
        try
        {
            var practices = await _validationService.GetAvailableBestPracticesAsync(cancellationToken);
            return Ok(practices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available best practices");
            return BadRequest($"Error getting best practices: {ex.Message}");
        }
    }
}

