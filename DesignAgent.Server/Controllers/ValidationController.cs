using DesignAgent.Server.Models;
using DesignAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DesignAgent.Server.Controllers;

[ApiController]
[Route("api/design/validate")]
public class ValidationController : ControllerBase
{
    private readonly IDesignValidationService _validationService;
    private readonly IAccessibilityService _accessibilityService;
    private readonly ILogger<ValidationController> _logger;

    public ValidationController(
        IDesignValidationService validationService,
        IAccessibilityService accessibilityService,
        ILogger<ValidationController> logger)
    {
        _validationService = validationService;
        _accessibilityService = accessibilityService;
        _logger = logger;
    }

    /// <summary>
    /// Validate code against brand guidelines
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DesignValidationResult>> Validate(
        [FromBody] ValidateRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating code against brand '{Context}'", request.Context);
        
        var result = await _validationService.ValidateAsync(request.Context, request.Code, cancellationToken);
        
        return Ok(result);
    }

    /// <summary>
    /// Validate multiple files against brand guidelines
    /// </summary>
    [HttpPost("files")]
    public async Task<ActionResult<DesignValidationResult>> ValidateFiles(
        [FromBody] ValidateFilesRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating {Count} files against brand '{Context}'", 
            request.Files.Count, request.Context);
        
        var result = await _validationService.ValidateFilesAsync(
            request.Context, 
            request.Files, 
            cancellationToken);
        
        return Ok(result);
    }

    /// <summary>
    /// Validate accessibility only
    /// </summary>
    [HttpPost("accessibility")]
    public ActionResult<AccessibilityValidationResult> ValidateAccessibility(
        [FromBody] AccessibilityRequest request)
    {
        _logger.LogInformation("Validating accessibility at level {Level}", request.WcagLevel);
        
        var result = _accessibilityService.ValidateAccessibility(request.Code, request.WcagLevel);
        
        return Ok(result);
    }

    /// <summary>
    /// Check color contrast
    /// </summary>
    [HttpPost("contrast")]
    public ActionResult<ContrastCheckResult> CheckContrast([FromBody] ContrastRequest request)
    {
        var result = _accessibilityService.CheckContrast(request.Foreground, request.Background);
        return Ok(result);
    }
}

public class ValidateRequest
{
    public string Context { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ValidateFilesRequest
{
    public string Context { get; set; } = string.Empty;
    public Dictionary<string, string> Files { get; set; } = new();
}

public class AccessibilityRequest
{
    public string Code { get; set; } = string.Empty;
    public string WcagLevel { get; set; } = "AA";
}

public class ContrastRequest
{
    public string Foreground { get; set; } = string.Empty;
    public string Background { get; set; } = string.Empty;
}

