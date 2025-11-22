using Microsoft.AspNetCore.Mvc;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;

namespace MemoryAgent.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SmartSearchController : ControllerBase
{
    private readonly ISmartSearchService _smartSearchService;
    private readonly ILogger<SmartSearchController> _logger;

    public SmartSearchController(
        ISmartSearchService smartSearchService,
        ILogger<SmartSearchController> logger)
    {
        _smartSearchService = smartSearchService;
        _logger = logger;
    }

    /// <summary>
    /// Smart search with auto-detection of strategy (graph-first, semantic-first, or hybrid)
    /// </summary>
    /// <param name="request">Search request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with enriched relationship data</returns>
    [HttpPost]
    public async Task<ActionResult<SmartSearchResponse>> Search(
        [FromBody] SmartSearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "Query is required" });
            }

            if (request.Limit < 1 || request.Limit > 100)
            {
                return BadRequest(new { error = "Limit must be between 1 and 100" });
            }

            if (request.RelationshipDepth < 1 || request.RelationshipDepth > 3)
            {
                return BadRequest(new { error = "Relationship depth must be between 1 and 3" });
            }

            _logger.LogInformation(
                "Smart search request: Query='{Query}', Context={Context}, Limit={Limit}, Offset={Offset}",
                request.Query, request.Context ?? "all", request.Limit, request.Offset);

            var response = await _smartSearchService.SearchAsync(request, cancellationToken);

            _logger.LogInformation(
                "Smart search completed: {Results} results returned using {Strategy}",
                response.Results.Count, response.Strategy);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing smart search request");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get search suggestions based on query
    /// </summary>
    [HttpGet("suggest")]
    public ActionResult<List<string>> GetSuggestions([FromQuery] string query)
    {
        var suggestions = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(suggestions);
        }

        // Basic suggestions - can be enhanced with ML later
        var lowerQuery = query.ToLowerInvariant();

        if (lowerQuery.Contains("class"))
        {
            suggestions.Add("classes that implement IRepository");
            suggestions.Add("classes in the Controllers namespace");
            suggestions.Add("classes with DbContext dependency");
        }

        if (lowerQuery.Contains("error") || lowerQuery.Contains("exception"))
        {
            suggestions.Add("error handling patterns");
            suggestions.Add("exception handling in services");
            suggestions.Add("methods that throw exceptions");
        }

        if (lowerQuery.Contains("auth"))
        {
            suggestions.Add("authentication implementation");
            suggestions.Add("classes with Authorize attribute");
            suggestions.Add("authorization policies");
        }

        return Ok(suggestions);
    }
}

