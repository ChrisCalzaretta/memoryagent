using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Background service that pre-pulls common Docker images on startup
/// This eliminates cold-start delays when executing code
/// </summary>
public class DockerWarmupService : BackgroundService
{
    private readonly ILogger<DockerWarmupService> _logger;
    private readonly DockerExecutionConfig _config;

    public DockerWarmupService(
        ILogger<DockerWarmupService> logger,
        IOptions<DockerExecutionConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.EnableWarmup)
        {
            _logger.LogInformation("🐳 Docker warmup disabled");
            return;
        }

        _logger.LogInformation("🐳 Starting Docker image warmup for {Count} images...", 
            _config.WarmupImages.Count);

        var tasks = _config.WarmupImages.Select(image => WarmupImageAsync(image, stoppingToken));
        
        try
        {
            await Task.WhenAll(tasks);
            _logger.LogInformation("🐳 Docker warmup complete - all images ready");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "🐳 Docker warmup completed with some failures");
        }
    }

    private async Task WarmupImageAsync(string image, CancellationToken cancellationToken)
    {
        try
        {
            // Check if image already exists
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"image inspect {image}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);

            if (checkProcess.ExitCode == 0)
            {
                _logger.LogDebug("✅ Image already cached: {Image}", image);
                return;
            }

            // Pull the image
            _logger.LogInformation("📥 Pulling Docker image: {Image}", image);

            var pullProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = $"pull {image}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            pullProcess.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(_config.ImagePullTimeoutMinutes));

            await pullProcess.WaitForExitAsync(cts.Token);

            if (pullProcess.ExitCode == 0)
            {
                _logger.LogInformation("✅ Successfully pulled: {Image}", image);
            }
            else
            {
                var error = await pullProcess.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogWarning("⚠️ Failed to pull {Image}: {Error}", image, error);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("⏱️ Timeout pulling image: {Image}", image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "❌ Error pulling image: {Image}", image);
        }
    }
}

