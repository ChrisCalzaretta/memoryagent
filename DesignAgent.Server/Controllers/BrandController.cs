using DesignAgent.Server.Models.Brand;
using DesignAgent.Server.Models.Questionnaire;
using DesignAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DesignAgent.Server.Controllers;

[ApiController]
[Route("api/design/brand")]
public class BrandController : ControllerBase
{
    private readonly IBrandService _brandService;
    private readonly IQuestionnaireService _questionnaireService;
    private readonly ITokenGeneratorService _tokenGenerator;
    private readonly ILogger<BrandController> _logger;

    public BrandController(
        IBrandService brandService,
        IQuestionnaireService questionnaireService,
        ITokenGeneratorService tokenGenerator,
        ILogger<BrandController> logger)
    {
        _brandService = brandService;
        _questionnaireService = questionnaireService;
        _tokenGenerator = tokenGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Get the brand builder questionnaire
    /// </summary>
    [HttpGet("questionnaire")]
    public ActionResult<BrandQuestionnaire> GetQuestionnaire()
    {
        return Ok(_questionnaireService.GetQuestionnaire());
    }

    /// <summary>
    /// Get the questionnaire as markdown (for display in Cursor)
    /// </summary>
    [HttpGet("questionnaire/markdown")]
    public ActionResult<string> GetQuestionnaireMarkdown()
    {
        return Ok(_questionnaireService.GetQuestionnaireMarkdown());
    }

    /// <summary>
    /// Create a new brand from questionnaire answers
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<BrandDefinition>> CreateBrand(
        [FromBody] QuestionnaireAnswers answers,
        CancellationToken cancellationToken)
    {
        try
        {
            var input = _questionnaireService.ParseAnswers(answers);
            var brand = await _brandService.CreateBrandAsync(input, cancellationToken);
            
            _logger.LogInformation("Created brand: {BrandName} ({Context})", brand.Name, brand.Context);
            
            return CreatedAtAction(nameof(GetBrand), new { context = brand.Context }, brand);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create brand");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a brand directly from parsed input (for API use)
    /// </summary>
    [HttpPost("create/direct")]
    public async Task<ActionResult<BrandDefinition>> CreateBrandDirect(
        [FromBody] ParsedBrandInput input,
        CancellationToken cancellationToken)
    {
        try
        {
            var brand = await _brandService.CreateBrandAsync(input, cancellationToken);
            return CreatedAtAction(nameof(GetBrand), new { context = brand.Context }, brand);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create brand");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a brand by context
    /// </summary>
    [HttpGet("{context}")]
    public async Task<ActionResult<BrandDefinition>> GetBrand(string context, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        return Ok(brand);
    }

    /// <summary>
    /// List all brands
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BrandSummary>>> ListBrands(CancellationToken cancellationToken)
    {
        var brands = await _brandService.ListBrandsAsync(cancellationToken);
        return Ok(brands);
    }

    /// <summary>
    /// Update a brand
    /// </summary>
    [HttpPut("{context}")]
    public async Task<ActionResult<BrandDefinition>> UpdateBrand(
        string context,
        [FromBody] BrandDefinition updates,
        CancellationToken cancellationToken)
    {
        try
        {
            var brand = await _brandService.UpdateBrandAsync(context, updates, cancellationToken);
            return Ok(brand);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
    }

    /// <summary>
    /// Delete a brand
    /// </summary>
    [HttpDelete("{context}")]
    public async Task<IActionResult> DeleteBrand(string context, CancellationToken cancellationToken)
    {
        var deleted = await _brandService.DeleteBrandAsync(context, cancellationToken);
        if (!deleted)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        return NoContent();
    }

    /// <summary>
    /// Clone a brand
    /// </summary>
    [HttpPost("{fromContext}/clone")]
    public async Task<ActionResult<BrandDefinition>> CloneBrand(
        string fromContext,
        [FromQuery] string toContext,
        [FromBody] Dictionary<string, object>? overrides,
        CancellationToken cancellationToken)
    {
        try
        {
            var brand = await _brandService.CloneBrandAsync(fromContext, toContext, overrides, cancellationToken);
            return CreatedAtAction(nameof(GetBrand), new { context = brand.Context }, brand);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Brand '{fromContext}' not found" });
        }
    }
}

