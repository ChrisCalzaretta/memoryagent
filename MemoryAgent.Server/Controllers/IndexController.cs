using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace MemoryAgent.Server.Controllers;

[ApiController]
[Route("api")]
public class IndexController : ControllerBase
{
    private readonly IIndexingService _indexingService;
    private readonly IReindexService _reindexService;
    private readonly ILogger<IndexController> _logger;

    public IndexController(
        IIndexingService indexingService,
        IReindexService reindexService,
        ILogger<IndexController> logger)
    {
        _indexingService = indexingService;
        _reindexService = reindexService;
        _logger = logger;
    }

    [HttpPost("index/file")]
    public async Task<ActionResult<IndexResult>> IndexFile(
        [FromBody] IndexFileRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest("Path is required");
        }

        var result = await _indexingService.IndexFileAsync(request.Path, request.Context, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("index/directory")]
    public async Task<ActionResult<IndexResult>> IndexDirectory(
        [FromBody] IndexDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest("Path is required");
        }

        var result = await _indexingService.IndexDirectoryAsync(
            request.Path,
            request.Recursive,
            request.Context,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("query")]
    public async Task<ActionResult<QueryResult>> Query(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest("Query is required");
        }

        var result = await _indexingService.QueryAsync(
            request.Query,
            request.Context,
            request.Limit,
            request.MinimumScore,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("reindex")]
    public async Task<ActionResult<ReindexResult>> Reindex(
        [FromBody] ReindexRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reindexService.ReindexAsync(
            request.Context,
            request.Path,
            request.RemoveStale,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        });
    }
}

