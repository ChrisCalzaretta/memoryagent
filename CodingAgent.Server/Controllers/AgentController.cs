using Microsoft.AspNetCore.Mvc;
using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingAgent.Server.Services;

namespace CodingAgent.Server.Controllers;

[ApiController]
[Route("api/agent")]
public class AgentController : ControllerBase
{
    private readonly ICodeGenerationService _codeGenerationService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        ICodeGenerationService codeGenerationService,
        ILogger<AgentController> logger)
    {
        _codeGenerationService = codeGenerationService;
        _logger = logger;
    }

    /// <summary>
    /// Generate code for a task
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<GenerateCodeResponse>> Generate(
        [FromBody] GenerateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received generate request: {Task}", request.Task);

        try
        {
            var response = await _codeGenerationService.GenerateAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating code for task: {Task}", request.Task);
            return StatusCode(500, new GenerateCodeResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Fix code based on validation feedback
    /// </summary>
    [HttpPost("fix")]
    public async Task<ActionResult<GenerateCodeResponse>> Fix(
        [FromBody] GenerateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("📥 Fix request received - PreviousFeedback is {Status}", 
            request.PreviousFeedback == null ? "NULL" : "present");
            
        if (request.PreviousFeedback == null)
        {
            _logger.LogError("❌ Returning 400 - PreviousFeedback is NULL");
            return BadRequest(new GenerateCodeResponse
            {
                Success = false,
                Error = "PreviousFeedback is required for fix requests"
            });
        }

        _logger.LogInformation("Received fix request for task: {Task}, Score: {Score}", 
            request.Task, request.PreviousFeedback.Score);

        try
        {
            var response = await _codeGenerationService.FixAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing code for task: {Task}", request.Task);
            return StatusCode(500, new GenerateCodeResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Estimate task complexity and recommended iterations
    /// </summary>
    [HttpPost("estimate")]
    public async Task<ActionResult<EstimateComplexityResponse>> EstimateComplexity(
        [FromBody] EstimateComplexityRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Estimating complexity for task: {Task}", request.Task);

        try
        {
            var response = await _codeGenerationService.EstimateComplexityAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating complexity for task: {Task}", request.Task);
            return StatusCode(500, new EstimateComplexityResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}



