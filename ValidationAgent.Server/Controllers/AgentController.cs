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
    private readonly IValidationEnsembleService _ensembleService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IValidationService validationService,
        IValidationEnsembleService ensembleService,
        ILogger<AgentController> logger)
    {
        _validationService = validationService;
        _ensembleService = ensembleService;
        _logger = logger;
    }

    /// <summary>
    /// Validate code quality (with optional ensemble support)
    /// </summary>
    [HttpPost("validate")]
    public async Task<ActionResult<ValidateCodeResponse>> Validate(
        [FromBody] ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received validation request for {FileCount} files (ensemble={Strategy})", 
            request.Files.Count, request.EnsembleStrategy ?? "single");

        try
        {
            // Use ensemble if strategy is specified and not "single"
            var useEnsemble = !string.IsNullOrEmpty(request.EnsembleStrategy) && 
                             request.EnsembleStrategy.ToLowerInvariant() != "single";
            
            var response = useEnsemble
                ? await _ensembleService.ValidateWithEnsembleAsync(request, cancellationToken)
                : await _validationService.ValidateAsync(request, cancellationToken);
            
            _logger.LogInformation(
                "Validation complete. Score: {Score}/10, Passed: {Passed}, Confidence: {Confidence:P0}, Models: {ModelCount}", 
                response.Score, response.Passed, response.Confidence, response.ModelsUsed.Count);
            
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
    /// Deep code review (always uses ensemble for maximum quality)
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
            // Review is a more thorough validation with specialized ensemble
            request.Rules = new List<string> { "best_practices", "security", "patterns", "performance" };
            request.EnsembleStrategy = "specialized"; // Always use specialized ensemble for reviews
            
            var response = await _ensembleService.ValidateWithEnsembleAsync(request, cancellationToken);
            
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













