using DesignAgent.Server.Models.DesignIntelligence;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Background service for autonomous design learning
/// Runs continuously: discover → crawl → analyze → learn → leaderboard
/// </summary>
public class DesignIntelligenceBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DesignIntelligenceBackgroundService> _logger;
    private readonly DesignIntelligenceOptions _options;
    private int _totalProcessed = 0;
    private int _totalAccepted = 0;
    private int _totalRejected = 0;

    public DesignIntelligenceBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DesignIntelligenceBackgroundService> logger,
        IOptions<DesignIntelligenceOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Design Intelligence Background Service starting...");

        // Wait for startup to complete
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        // Reset any stuck "processing" sources from previous crashes
        await ResetStuckProcessingSourcesAsync(stoppingToken);

        // Seed curated sources on first run
        await SeedCuratedSourcesAsync(stoppingToken);

        _logger.LogInformation("🔄 Starting autonomous learning loop...");
        _logger.LogInformation("📊 Target: {TargetDesigns} quality designs (floor: {Floor})", 
            _options.LeaderboardSize, _options.InitialThreshold);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunLearningCycleAsync(stoppingToken);

                // Adaptive sleep based on CPU usage
                var sleepDuration = await GetAdaptiveSleepDurationAsync();
                _logger.LogDebug("💤 Sleeping for {Duration}s...", sleepDuration.TotalSeconds);
                await Task.Delay(sleepDuration, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("⏸️ Design Intelligence service stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in learning cycle");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("✅ Design Intelligence service stopped.");
        _logger.LogInformation("📊 Final stats: Processed={Processed}, Accepted={Accepted}, Rejected={Rejected}",
            _totalProcessed, _totalAccepted, _totalRejected);
    }

    /// <summary>
    /// Single learning cycle: discover → crawl → analyze → learn
    /// </summary>
    private async Task RunLearningCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IDesignIntelligenceStorage>();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IDesignDiscoveryService>();
        var captureService = scope.ServiceProvider.GetRequiredService<IDesignCaptureService>();
        var analysisService = scope.ServiceProvider.GetRequiredService<IDesignAnalysisService>();
        var learningService = scope.ServiceProvider.GetRequiredService<IDesignLearningService>();

        // 1. Check leaderboard status
        var leaderboard = await storage.GetLeaderboardAsync(_options.LeaderboardSize, cancellationToken);
        var currentFloor = await storage.GetLeaderboardFloorAsync(cancellationToken) ?? 0.0;

        _logger.LogInformation("📊 Leaderboard: {Count}/{Target} designs, Floor: {Floor:F1}",
            leaderboard.Count, _options.LeaderboardSize, currentFloor);

        // 2. If we haven't reached target, discover more sources
        if (leaderboard.Count < _options.LeaderboardSize)
        {
            _logger.LogInformation("🔍 Discovering new sources...");
            await discoveryService.RunDiscoveryCycleAsync(targetCount: 5, cancellationToken: cancellationToken);
        }

        // 3. Get pending sources
        var pendingSources = await storage.GetPendingSourcesAsync(limit: 1, cancellationToken);

        if (!pendingSources.Any())
        {
            _logger.LogInformation("✅ No pending sources. Running discovery...");
            await discoveryService.RunDiscoveryCycleAsync(targetCount: 10, cancellationToken: cancellationToken);
            pendingSources = await storage.GetPendingSourcesAsync(limit: 1, cancellationToken);
        }

        if (!pendingSources.Any())
        {
            _logger.LogWarning("⚠️ Still no pending sources after discovery");
            return;
        }

        var source = pendingSources.First();
        _logger.LogInformation("🌐 Processing: {Url} (Trust: {Trust:F1})", source.Url, source.TrustScore);

        try
        {
            // 4. Mark as processing
            await storage.UpdateSourceStatusAsync(source.Id, "processing", cancellationToken);

            // 5. Capture design (multi-page crawl)
            var design = await captureService.CrawlWebsiteAsync(source, cancellationToken);

            // 6. Analyze design
            var analyzedDesign = await analysisService.AnalyzeDesignAsync(design, cancellationToken);

            _totalProcessed++;

            // 7. Quality gate decision
            if (analyzedDesign.PassedQualityGate)
            {
                _totalAccepted++;
                _logger.LogInformation("✅ ACCEPTED: {Url} - Score: {Score:F1} (Pages: {PageCount})",
                    analyzedDesign.Url, analyzedDesign.OverallScore, analyzedDesign.Pages.Count);

                // Store design
                await storage.StoreDesignAsync(analyzedDesign, cancellationToken);

                // Extract patterns
                await learningService.ExtractPatternsAsync(analyzedDesign, cancellationToken);

                // Update leaderboard
                await storage.UpdateLeaderboardRanksAsync(cancellationToken);

                // Check if we need to evict lowest design
                var updatedLeaderboard = await storage.GetLeaderboardAsync(_options.LeaderboardSize + 1, cancellationToken);
                if (updatedLeaderboard.Count > _options.LeaderboardSize)
                {
                    var toEvict = updatedLeaderboard.Last();
                    await storage.EvictFromLeaderboardAsync(toEvict.Id, cancellationToken);
                    _logger.LogInformation("🗑️ Evicted: {Url} (Score: {Score:F1})", toEvict.Url, toEvict.OverallScore);
                }

                await storage.UpdateSourceStatusAsync(source.Id, "accepted", cancellationToken);
            }
            else
            {
                _totalRejected++;
                _logger.LogInformation("❌ REJECTED: {Url} - Score: {Score:F1} (Below floor: {Floor:F1})",
                    source.Url, analyzedDesign.OverallScore, currentFloor);

                await storage.UpdateSourceStatusAsync(source.Id, "rejected", cancellationToken);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("after 5 retries"))
        {
            _logger.LogError("❌ REJECTED: {Url} - Vision model failed after 5 retries (rejecting entire site)", source.Url);
            await storage.UpdateSourceStatusAsync(source.Id, "failed", cancellationToken);
            _totalRejected++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to process: {Url}", source.Url);
            await storage.UpdateSourceStatusAsync(source.Id, "failed", cancellationToken);
        }

        // 8. Log stats every 10 designs
        if (_totalProcessed % 10 == 0)
        {
            var acceptRate = _totalProcessed > 0 ? (_totalAccepted / (double)_totalProcessed) * 100 : 0;
            _logger.LogInformation("📈 Stats: Processed={Processed}, Accepted={Accepted} ({AcceptRate:F1}%), Rejected={Rejected}",
                _totalProcessed, _totalAccepted, acceptRate, _totalRejected);
        }
    }

    /// <summary>
    /// Reset stuck "processing" sources on startup (handles crash recovery)
    /// </summary>
    private async Task ResetStuckProcessingSourcesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IDesignIntelligenceStorage>();

        try
        {
            await storage.ResetStuckProcessingSourcesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to reset stuck processing sources");
        }
    }

    /// <summary>
    /// Seed curated sources on startup
    /// </summary>
    private async Task SeedCuratedSourcesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IDesignIntelligenceStorage>();

        var curatedFile = Path.Combine(AppContext.BaseDirectory, "Data", "curated_sources.json");

        if (!File.Exists(curatedFile))
        {
            _logger.LogWarning("⚠️ Curated sources file not found: {Path}", curatedFile);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(curatedFile, cancellationToken);
            var sources = System.Text.Json.JsonSerializer.Deserialize<List<CuratedSource>>(json);

            if (sources == null || !sources.Any())
            {
                _logger.LogWarning("⚠️ No curated sources found in file");
                return;
            }

            int seeded = 0;
            foreach (var curatedSource in sources)
            {
                var existing = await storage.GetSourceByUrlAsync(curatedSource.Url, cancellationToken);
                if (existing != null)
                {
                    continue; // Already seeded
                }

                var source = new DesignSource
                {
                    Url = curatedSource.Url,
                    Category = curatedSource.Category,
                    TrustScore = curatedSource.TrustScore,
                    DiscoveredAt = DateTime.UtcNow,
                    Status = "pending"
                };

                await storage.StoreSourceAsync(source, cancellationToken);
                seeded++;
            }

            if (seeded > 0)
            {
                _logger.LogInformation("✅ Seeded {Count} curated sources", seeded);
            }
            else
            {
                _logger.LogInformation("✅ All curated sources already seeded");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to seed curated sources");
        }
    }

    /// <summary>
    /// Get adaptive sleep duration based on CPU usage (target: <30% CPU)
    /// </summary>
    private async Task<TimeSpan> GetAdaptiveSleepDurationAsync()
    {
        // Placeholder: In production, monitor actual CPU usage
        // For now, use a conservative sleep duration
        return TimeSpan.FromMinutes(2);
    }
}

/// <summary>
/// Helper class for deserializing curated_sources.json
/// </summary>
public class CuratedSource
{
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double TrustScore { get; set; }
}

