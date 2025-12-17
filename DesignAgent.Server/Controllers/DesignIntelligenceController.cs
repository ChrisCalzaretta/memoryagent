using Microsoft.AspNetCore.Mvc;
using DesignAgent.Server.Services.DesignIntelligence;
using DesignAgent.Server.Models.DesignIntelligence;
using System.Text.Json;

namespace DesignAgent.Server.Controllers;

/// <summary>
/// API endpoints for Design Intelligence System
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DesignIntelligenceController : ControllerBase
{
    private readonly IDesignDiscoveryService _discoveryService;
    private readonly IDesignCaptureService _captureService;
    private readonly IDesignAnalysisService _analysisService;
    private readonly IDesignLearningService _learningService;
    private readonly IA2uiGeneratorService _a2uiGenerator;
    private readonly IDesignIntelligenceStorage _storage;
    private readonly ILogger<DesignIntelligenceController> _logger;

    public DesignIntelligenceController(
        IDesignDiscoveryService discoveryService,
        IDesignCaptureService captureService,
        IDesignAnalysisService analysisService,
        IDesignLearningService learningService,
        IA2uiGeneratorService a2uiGenerator,
        IDesignIntelligenceStorage storage,
        ILogger<DesignIntelligenceController> logger)
    {
        _discoveryService = discoveryService;
        _captureService = captureService;
        _analysisService = analysisService;
        _learningService = learningService;
        _a2uiGenerator = a2uiGenerator;
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// Test endpoint - Generate search queries
    /// </summary>
    [HttpPost("test/generate-queries")]
    public async Task<IActionResult> GenerateQueries([FromQuery] int count = 5, [FromQuery] string? category = null)
    {
        try
        {
            var queries = await _discoveryService.GenerateSearchQueriesAsync(count, category);
            return Ok(new { queries, count = queries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate queries");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Run discovery cycle to find new design sources
    /// </summary>
    [HttpPost("discover")]
    public async Task<IActionResult> RunDiscovery([FromQuery] int targetCount = 10)
    {
        try
        {
            _logger.LogInformation("🚀 Starting discovery cycle for {Count} sources", targetCount);
            var discovered = await _discoveryService.RunDiscoveryCycleAsync(targetCount);
            
            return Ok(new 
            { 
                discovered,
                message = $"Discovered {discovered} new design sources"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discovery cycle failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Seed curated design sources from JSON file
    /// </summary>
    [HttpPost("seed-curated")]
    public async Task<IActionResult> SeedCurated()
    {
        try
        {
            // Read curated sources from file
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Data", "curated_sources.json");
            
            if (!System.IO.File.Exists(jsonPath))
            {
                return NotFound(new { error = "curated_sources.json not found", path = jsonPath });
            }

            var json = await System.IO.File.ReadAllTextAsync(jsonPath);
            var sources = JsonSerializer.Deserialize<List<DesignSource>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (sources == null || sources.Count == 0)
            {
                return BadRequest(new { error = "No sources found in JSON file" });
            }

            _logger.LogInformation("🌱 Seeding {Count} curated sources", sources.Count);
            var seeded = await _discoveryService.SeedCuratedSourcesAsync(sources);

            return Ok(new 
            { 
                seeded,
                total = sources.Count,
                message = $"Seeded {seeded} curated sources (skipped {sources.Count - seeded} duplicates)"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed curated sources");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get pending sources (ready to be crawled)
    /// </summary>
    [HttpGet("sources/pending")]
    public async Task<IActionResult> GetPendingSources([FromQuery] int limit = 10)
    {
        try
        {
            var sources = await _storage.GetPendingSourcesAsync(limit);
            return Ok(new { sources, count = sources.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending sources");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get leaderboard (top designs)
    /// </summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 100)
    {
        try
        {
            var designs = await _storage.GetLeaderboardAsync(limit);
            var floor = await _storage.GetLeaderboardFloorAsync();
            
            return Ok(new 
            { 
                designs,
                count = designs.Count,
                floor = floor ?? 0,
                message = $"Top {designs.Count} designs (floor: {floor:F1})"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get leaderboard");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Crawl a website (capture screenshots and DOM)
    /// </summary>
    [HttpPost("crawl")]
    public async Task<IActionResult> CrawlWebsite([FromQuery] string url)
    {
        try
        {
            // Get or create source
            var source = await _storage.GetSourceByUrlAsync(url);
            if (source == null)
            {
                source = new DesignSource
                {
                    Url = url,
                    DiscoveryMethod = "manual",
                    TrustScore = 5.0,
                    Status = "processing"
                };
                await _storage.StoreSourceAsync(source);
            }

            _logger.LogInformation("🕷️ Crawling website: {Url}", url);
            var design = await _captureService.CrawlWebsiteAsync(source);

            // Store design (will be analyzed in Phase 4)
            design.PassedQualityGate = false; // Will be set after analysis
            await _storage.StoreDesignAsync(design);

            return Ok(new
            {
                design = new
                {
                    design.Id,
                    design.Url,
                    design.CapturedAt,
                    pagesCount = design.Pages.Count,
                    pages = design.Pages.Select(p => new { p.PageType, p.Url }).ToList()
                },
                message = $"Crawled {design.Pages.Count} pages from {url}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl website: {Url}", url);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Analyze a captured design (score all pages)
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeDesign([FromQuery] string designId)
    {
        try
        {
            var design = await _storage.GetDesignAsync(designId);
            if (design == null)
            {
                return NotFound(new { error = $"Design not found: {designId}" });
            }

            _logger.LogInformation("🧠 Analyzing design: {DesignId}", designId);
            
            // Analyze all pages and calculate scores
            design = await _analysisService.AnalyzeDesignAsync(design);

            // Check quality gate
            var source = await _storage.GetSourceByUrlAsync(design.Url);
            var trustScore = source?.TrustScore ?? 5.0;
            design.PassedQualityGate = await _analysisService.PassesQualityGateAsync(design.OverallScore, trustScore);

            // Update in storage
            await _storage.StoreDesignAsync(design);

            // If passed, update leaderboard
            if (design.PassedQualityGate)
            {
                await _storage.UpdateLeaderboardRanksAsync();
                _logger.LogInformation("✅ Design added to leaderboard: {Url} (Score: {Score:F1})", design.Url, design.OverallScore);
            }

            return Ok(new
            {
                design = new
                {
                    design.Id,
                    design.Url,
                    design.AutoGeneratedName,
                    design.OverallScore,
                    design.PassedQualityGate,
                    design.LeaderboardRank,
                    pagesAnalyzed = design.Pages.Count,
                    pages = design.Pages.Select(p => new
                    {
                        p.PageType,
                        p.OverallPageScore,
                        strengthsCount = p.Strengths.Count,
                        weaknessesCount = p.Weaknesses.Count
                    }).ToList(),
                    design.DesignDNA,
                    design.DetectedSystem
                },
                message = design.PassedQualityGate
                    ? $"Design passed quality gate! Score: {design.OverallScore:F1}"
                    : $"Design did not pass quality gate. Score: {design.OverallScore:F1}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze design: {DesignId}", designId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Full pipeline: Crawl + Analyze a website
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessWebsite([FromQuery] string url)
    {
        try
        {
            _logger.LogInformation("🚀 Processing website: {Url}", url);

            // 1. Crawl
            var source = await _storage.GetSourceByUrlAsync(url);
            if (source == null)
            {
                source = new DesignSource
                {
                    Url = url,
                    DiscoveryMethod = "manual",
                    TrustScore = 5.0,
                    Status = "processing"
                };
                await _storage.StoreSourceAsync(source);
            }

            var design = await _captureService.CrawlWebsiteAsync(source);

            // 2. Analyze
            design = await _analysisService.AnalyzeDesignAsync(design);

            // 3. Quality gate
            design.PassedQualityGate = await _analysisService.PassesQualityGateAsync(design.OverallScore, source.TrustScore);

            // 4. Store
            await _storage.StoreDesignAsync(design);

            if (design.PassedQualityGate)
            {
                await _storage.UpdateLeaderboardRanksAsync();
            }

            return Ok(new
            {
                design = new
                {
                    design.Id,
                    design.Url,
                    design.AutoGeneratedName,
                    design.OverallScore,
                    design.PassedQualityGate,
                    pagesProcessed = design.Pages.Count
                },
                message = $"Website processed successfully. Score: {design.OverallScore:F1}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process website: {Url}", url);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Health check for Design Intelligence System
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            service = "Design Intelligence System",
            components = new
            {
                discovery = "ready",
                capture = "ready",
                analysis = "ready",
                storage = "ready",
                learning = "pending"
            }
        });
    }
}

