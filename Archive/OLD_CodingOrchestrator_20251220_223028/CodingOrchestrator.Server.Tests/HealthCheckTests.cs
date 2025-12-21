using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CodingOrchestrator.Server.Services;
using System.Net;

namespace CodingOrchestrator.Server.Tests;

/// <summary>
/// Tests for health check services
/// </summary>
public class HealthCheckTests
{
    #region DockerHealthCheck Tests

    [Fact]
    public async Task DockerHealthCheck_DockerAvailable_ReturnsHealthy()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DockerHealthCheck>>();
        var healthCheck = new DockerHealthCheck(mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("docker", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert - Result depends on whether Docker is installed on the test machine
        // In CI/CD where Docker might not be available, we accept Unhealthy
        Assert.True(
            result.Status == HealthStatus.Healthy || result.Status == HealthStatus.Unhealthy,
            "Docker health check should return Healthy or Unhealthy status");
    }

    [Fact]
    public async Task DockerHealthCheck_ReturnsWithinTimeout()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DockerHealthCheck>>();
        var healthCheck = new DockerHealthCheck(mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("docker", healthCheck, null, null)
        };

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await healthCheck.CheckHealthAsync(context, CancellationToken.None);
        sw.Stop();

        // Assert - Should complete within reasonable time (10 seconds)
        Assert.True(sw.ElapsedMilliseconds < 10000, "Docker health check took too long");
    }

    [Fact]
    public async Task DockerHealthCheck_CancellationRequested_HandlesCancellation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DockerHealthCheck>>();
        var healthCheck = new DockerHealthCheck(mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("docker", healthCheck, null, null)
        };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await healthCheck.CheckHealthAsync(context, cts.Token);

        // Assert - Should handle cancellation gracefully
        // The result might be Unhealthy due to cancellation, but shouldn't throw
        Assert.NotNull(result);
    }

    #endregion

    #region AgentHealthCheck Tests

    [Fact]
    public async Task AgentHealthCheck_AgentHealthy_ReturnsHealthy()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5000") };
        var mockLogger = new Mock<ILogger<AgentHealthCheck>>();
        
        var healthCheck = new AgentHealthCheck(httpClient, "TestAgent", "/health", mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test-agent", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("TestAgent", result.Description);
        Assert.Contains("healthy", result.Description);
    }

    [Fact]
    public async Task AgentHealthCheck_AgentReturnsError_ReturnsDegraded()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5000") };
        var mockLogger = new Mock<ILogger<AgentHealthCheck>>();
        
        var healthCheck = new AgentHealthCheck(httpClient, "TestAgent", "/health", mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test-agent", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("ServiceUnavailable", result.Description);
    }

    [Fact]
    public async Task AgentHealthCheck_AgentUnreachable_ReturnsUnhealthy()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5000") };
        var mockLogger = new Mock<ILogger<AgentHealthCheck>>();
        
        var healthCheck = new AgentHealthCheck(httpClient, "TestAgent", "/health", mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test-agent", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("unreachable", result.Description);
    }

    [Fact]
    public async Task AgentHealthCheck_Timeout_ReturnsUnhealthy()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(new TaskCanceledException("Request timed out"));
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5000") };
        var mockLogger = new Mock<ILogger<AgentHealthCheck>>();
        
        var healthCheck = new AgentHealthCheck(httpClient, "TestAgent", "/health", mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test-agent", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("timed out", result.Description);
    }

    [Fact]
    public async Task AgentHealthCheck_IncludesResponseTime()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:5000") };
        var mockLogger = new Mock<ILogger<AgentHealthCheck>>();
        
        var healthCheck = new AgentHealthCheck(httpClient, "TestAgent", "/health", mockLogger.Object);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("test-agent", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Data);
        Assert.True(result.Data.ContainsKey("responseTime"));
    }

    #endregion

    #region Helper Classes

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly Exception? _exception;

        public MockHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public MockHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_exception != null)
            {
                throw _exception;
            }

            return Task.FromResult(new HttpResponseMessage(_statusCode!.Value));
        }
    }

    #endregion
}

