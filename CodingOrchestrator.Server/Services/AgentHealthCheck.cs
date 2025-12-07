using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Health check for external agent services
/// </summary>
public class AgentHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly string _agentName;
    private readonly string _healthEndpoint;
    private readonly ILogger<AgentHealthCheck> _logger;

    public AgentHealthCheck(
        HttpClient httpClient,
        string agentName,
        string healthEndpoint,
        ILogger<AgentHealthCheck> logger)
    {
        _httpClient = httpClient;
        _agentName = agentName;
        _healthEndpoint = healthEndpoint;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var response = await _httpClient.GetAsync(_healthEndpoint, cancellationToken);
            sw.Stop();

            var data = new Dictionary<string, object>
            {
                ["endpoint"] = _healthEndpoint,
                ["responseTime"] = sw.ElapsedMilliseconds,
                ["statusCode"] = (int)response.StatusCode
            };

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy(
                    $"{_agentName} is healthy (responded in {sw.ElapsedMilliseconds}ms)",
                    data);
            }

            return HealthCheckResult.Degraded(
                $"{_agentName} returned {response.StatusCode}",
                data: data);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Health check failed for {Agent}", _agentName);
            return HealthCheckResult.Unhealthy(
                $"{_agentName} is unreachable: {ex.Message}",
                ex);
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy($"{_agentName} health check timed out");
        }
    }
}

/// <summary>
/// Health check for Docker availability
/// </summary>
public class DockerHealthCheck : IHealthCheck
{
    private readonly ILogger<DockerHealthCheck> _logger;

    public DockerHealthCheck(ILogger<DockerHealthCheck> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "info --format '{{.ServerVersion}}'",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode == 0)
            {
                var version = (await process.StandardOutput.ReadToEndAsync(cancellationToken)).Trim();
                return HealthCheckResult.Healthy(
                    $"Docker is available (version: {version})",
                    new Dictionary<string, object> { ["version"] = version });
            }

            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            return HealthCheckResult.Unhealthy($"Docker check failed: {error}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Docker health check failed");
            return HealthCheckResult.Unhealthy($"Docker not available: {ex.Message}", ex);
        }
    }
}

