using DesignAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DesignAgent.Server.Controllers;

[ApiController]
[Route("api/design/tokens")]
public class TokensController : ControllerBase
{
    private readonly IBrandService _brandService;
    private readonly ITokenGeneratorService _tokenGenerator;
    private readonly IComponentSpecService _componentSpecService;
    private readonly ILogger<TokensController> _logger;

    public TokensController(
        IBrandService brandService,
        ITokenGeneratorService tokenGenerator,
        IComponentSpecService componentSpecService,
        ILogger<TokensController> logger)
    {
        _brandService = brandService;
        _tokenGenerator = tokenGenerator;
        _componentSpecService = componentSpecService;
        _logger = logger;
    }

    /// <summary>
    /// Get design tokens for a brand
    /// </summary>
    [HttpGet("{context}")]
    public async Task<IActionResult> GetTokens(string context, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        
        return Ok(brand.Tokens);
    }

    /// <summary>
    /// Export tokens as CSS variables
    /// </summary>
    [HttpGet("{context}/export/css")]
    public async Task<IActionResult> ExportCss(string context, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        
        var css = _tokenGenerator.ExportToCss(brand);
        return Content(css, "text/css");
    }

    /// <summary>
    /// Export tokens as Tailwind config
    /// </summary>
    [HttpGet("{context}/export/tailwind")]
    public async Task<IActionResult> ExportTailwind(string context, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        
        var config = _tokenGenerator.ExportToTailwindConfig(brand);
        return Content(config, "application/javascript");
    }

    /// <summary>
    /// Export tokens as JSON
    /// </summary>
    [HttpGet("{context}/export/json")]
    public async Task<IActionResult> ExportJson(string context, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        
        var json = _tokenGenerator.ExportToJson(brand);
        return Content(json, "application/json");
    }

    /// <summary>
    /// Export tokens as SCSS
    /// </summary>
    [HttpGet("{context}/export/scss")]
    public async Task<IActionResult> ExportScss(string context, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        
        var scss = _tokenGenerator.ExportToScss(brand);
        return Content(scss, "text/x-scss");
    }

    /// <summary>
    /// Get component specification
    /// </summary>
    [HttpGet("{context}/component/{componentName}")]
    public async Task<IActionResult> GetComponentSpec(string context, string componentName, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        
        if (!brand.Components.TryGetValue(componentName, out var spec))
        {
            return NotFound(new { error = $"Component '{componentName}' not found in brand" });
        }
        
        return Ok(spec);
    }

    /// <summary>
    /// Get component guidance (markdown)
    /// </summary>
    [HttpGet("{context}/component/{componentName}/guidance")]
    public async Task<IActionResult> GetComponentGuidance(string context, string componentName, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{context}' not found" });
        }
        
        var guidance = _componentSpecService.GenerateComponentGuidance(componentName, brand);
        return Content(guidance, "text/markdown");
    }

    /// <summary>
    /// Get styling guidance for a query
    /// </summary>
    [HttpPost("guidance")]
    public async Task<IActionResult> GetGuidance([FromBody] GuidanceRequest request, CancellationToken cancellationToken)
    {
        var brand = await _brandService.GetBrandAsync(request.Context, cancellationToken);
        if (brand == null)
        {
            return NotFound(new { error = $"Brand '{request.Context}' not found" });
        }
        
        // Generate contextual guidance based on query
        var guidance = new
        {
            query = request.Query,
            brand = brand.Name,
            tokens = brand.Tokens,
            relevantComponents = FindRelevantComponents(request.Query, brand),
            voice = brand.Voice,
            accessibility = brand.Accessibility
        };
        
        return Ok(guidance);
    }

    private List<string> FindRelevantComponents(string query, Models.Brand.BrandDefinition brand)
    {
        var queryLower = query.ToLower();
        var relevant = new List<string>();
        
        foreach (var component in brand.Components.Keys)
        {
            if (queryLower.Contains(component.ToLower()))
            {
                relevant.Add(component);
            }
        }
        
        // Add common components if query mentions them
        if (queryLower.Contains("button")) relevant.Add("Button");
        if (queryLower.Contains("card")) relevant.Add("Card");
        if (queryLower.Contains("input") || queryLower.Contains("form")) relevant.Add("Input");
        if (queryLower.Contains("table") || queryLower.Contains("data")) relevant.Add("DataTable");
        if (queryLower.Contains("modal") || queryLower.Contains("dialog")) relevant.Add("Modal");
        
        return relevant.Distinct().ToList();
    }
}

public class GuidanceRequest
{
    public string Context { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}

