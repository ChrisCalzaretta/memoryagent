using Microsoft.AspNetCore.Mvc;
using AgentContracts.Requests;
using AgentContracts.Responses;
using ValidationAgent.Server.Services;

namespace ValidationAgent.Server.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly IValidationService _validationService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IValidationService validationService,
        ILogger<AgentController> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// Validate code quality
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ValidateCodeResponse>> Validate(
        [FromBody] ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received validation request for {FileCount} files", 
            request.Files.Count);

        try
        {
            var response = await _validationService.ValidateAsync(request, cancellationToken);
            
            _logger.LogInformation("Validation complete. Score: {Score}/10, Passed: {Passed}", 
                response.Score, response.Passed);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating code");
            return StatusCode(500, new ValidateCodeResponse
            {
                Passed = false,
                Score = 0,
                Summary = $"Validation error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Deep code review
    /// </summary>
    [HttpPost("review")]
    public async Task<ActionResult<ValidateCodeResponse>> Review(
        [FromBody] ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received review request for {FileCount} files", 
            request.Files.Count);

        try
        {
            // Review is a more thorough validation
            request.Rules = new List<string> { "best_practices", "security", "patterns", "performance" };
            var response = await _validationService.ValidateAsync(request, cancellationToken);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing code");
            return StatusCode(500, new ValidateCodeResponse
            {
                Passed = false,
                Score = 0,
                Summary = $"Review error: {ex.Message}"
            });
        }
    }
}













