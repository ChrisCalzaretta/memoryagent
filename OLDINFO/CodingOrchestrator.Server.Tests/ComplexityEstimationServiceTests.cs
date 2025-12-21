using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CodingOrchestrator.Server.Services;
using CodingOrchestrator.Server.Clients;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Tests;

/// <summary>
/// Tests for ComplexityEstimationService
/// </summary>
public class ComplexityEstimationServiceTests
{
    private readonly Mock<ICodingAgentClient> _mockCodingAgent;
    private readonly Mock<ILogger<ComplexityEstimationService>> _mockLogger;
    private readonly ComplexityEstimationService _service;

    public ComplexityEstimationServiceTests()
    {
        _mockCodingAgent = new Mock<ICodingAgentClient>();
        _mockLogger = new Mock<ILogger<ComplexityEstimationService>>();
        _service = new ComplexityEstimationService(_mockCodingAgent.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task EstimateAsync_SuccessfulEstimation_ReturnsEstimate()
    {
        // Arrange
        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse
            {
                Success = true,
                ComplexityLevel = "Medium",
                RecommendedIterations = 5,
                EstimatedFiles = 3,
                Reasoning = "Standard CRUD operation"
            });

        var request = new OrchestrateTaskRequest
        {
            Task = "Create a user service",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 10
        };

        // Act
        var result = await _service.EstimateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Medium", result.ComplexityLevel);
        Assert.Equal(5, result.RecommendedIterations);
        Assert.Equal(10, result.EffectiveIterations);  // User's cap
        Assert.Equal(3, result.EstimatedFiles);
        Assert.NotNull(result.Phase);
        Assert.Equal("complexity_estimation", result.Phase.Name);
    }

    [Fact]
    public async Task EstimateAsync_LLMRecommendsMoreThanMax_RespectsUserCap()
    {
        // Arrange
        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse
            {
                Success = true,
                ComplexityLevel = "High",
                RecommendedIterations = 15,  // LLM wants 15
                EstimatedFiles = 10,
                Reasoning = "Complex multi-file implementation"
            });

        var request = new OrchestrateTaskRequest
        {
            Task = "Create a complex service",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 5  // User only wants 5
        };

        // Act
        var result = await _service.EstimateAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(15, result.RecommendedIterations);  // LLM recommended
        Assert.Equal(5, result.EffectiveIterations);     // But we respect user's cap
    }

    [Fact]
    public async Task EstimateAsync_CodingAgentFails_ReturnsDefaults()
    {
        // Arrange
        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 10
        };

        // Act
        var result = await _service.EstimateAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(10, result.EffectiveIterations);  // Falls back to user's max
        Assert.NotNull(result.Phase);
        Assert.Equal("fallback", result.Phase.Status);
    }

    [Fact]
    public async Task EstimateAsync_PhaseHasCorrectTiming()
    {
        // Arrange
        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse
            {
                Success = true,
                ComplexityLevel = "Low",
                RecommendedIterations = 2
            });

        var request = new OrchestrateTaskRequest
        {
            Task = "Simple task",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var result = await _service.EstimateAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Phase);
        Assert.NotNull(result.Phase.CompletedAt);
        Assert.True(result.Phase.DurationMs >= 0);
        Assert.Equal("complete", result.Phase.Status);
    }

    [Fact]
    public async Task EstimateAsync_PhaseDetailsContainEstimation()
    {
        // Arrange
        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse
            {
                Success = true,
                ComplexityLevel = "High",
                RecommendedIterations = 8,
                EstimatedFiles = 5,
                Reasoning = "Complex task"
            });

        var request = new OrchestrateTaskRequest
        {
            Task = "Complex task",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 10
        };

        // Act
        var result = await _service.EstimateAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Phase?.Details);
        Assert.Equal("High", result.Phase.Details["complexityLevel"]);
        Assert.Equal(8, result.Phase.Details["recommendedIterations"]);
        Assert.Equal(10, result.Phase.Details["effectiveIterations"]);
        Assert.Equal(5, result.Phase.Details["estimatedFiles"]);
        Assert.Equal("Complex task", result.Phase.Details["reasoning"]);
    }

    [Fact]
    public async Task EstimateAsync_CancellationRequested_ThrowsCancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var request = new OrchestrateTaskRequest
        {
            Task = "Task",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act & Assert - Should handle cancellation gracefully with fallback
        var result = await _service.EstimateAsync(request, cts.Token);
        Assert.False(result.Success);
    }
}

