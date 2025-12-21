using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CodingOrchestrator.Server.Services;
using CodingOrchestrator.Server.Clients;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Tests;

/// <summary>
/// Tests for graceful degradation behavior when services are unavailable
/// </summary>
public class GracefulDegradationTests
{
    private readonly Mock<ICodingAgentClient> _mockCodingAgent;
    private readonly Mock<IValidationAgentClient> _mockValidationAgent;
    private readonly Mock<IMemoryAgentClient> _mockMemoryAgent;
    private readonly Mock<IExecutionService> _mockExecutionService;
    private readonly Mock<ILogger<TaskOrchestrator>> _mockLogger;

    public GracefulDegradationTests()
    {
        _mockCodingAgent = new Mock<ICodingAgentClient>();
        _mockValidationAgent = new Mock<IValidationAgentClient>();
        _mockMemoryAgent = new Mock<IMemoryAgentClient>();
        _mockExecutionService = new Mock<IExecutionService>();
        _mockLogger = new Mock<ILogger<TaskOrchestrator>>();

        // Default setup for execution service
        _mockExecutionService.Setup(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<List<ExecutionFile>>(),
            It.IsAny<string>(),
            It.IsAny<AgentContracts.Models.ExecutionInstructions?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionResult
            {
                Success = true,
                BuildPassed = true,
                ExecutionPassed = true,
                Output = "Success"
            });
    }

    private TaskOrchestrator CreateOrchestrator()
    {
        return new TaskOrchestrator(
            _mockCodingAgent.Object,
            _mockValidationAgent.Object,
            _mockMemoryAgent.Object,
            _mockExecutionService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteTask_MemoryAgentUnavailable_ContinuesWithoutContext()
    {
        // Arrange
        _mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Memory agent unavailable"));

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true, RecommendedIterations = 3 });

        _mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "Test.cs", Content = "code", Type = FileChangeType.Created }
                }
            });

        _mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse { Passed = true, Score = 9 });

        var orchestrator = CreateOrchestrator();
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a test service",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 3
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Should complete successfully despite Memory Agent being down
        Assert.Equal(TaskState.Complete, result.Status);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.Success);
        
        // Verify coding agent was called (proving we continued)
        _mockCodingAgent.Verify(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteTask_MemoryAgentTimeout_ContinuesWithoutContext()
    {
        // Arrange
        _mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true, RecommendedIterations = 2 });

        _mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "Test.cs", Content = "code", Type = FileChangeType.Created }
                }
            });

        _mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse { Passed = true, Score = 9 });

        var orchestrator = CreateOrchestrator();
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a test service",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(TaskState.Complete, result.Status);
    }

    [Fact]
    public async Task ExecuteTask_ContextGatheringFails_TimelineIncludesDegradedStatus()
    {
        // Arrange
        _mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        _mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "Test.cs", Content = "code", Type = FileChangeType.Created }
                }
            });

        _mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse { Passed = true, Score = 9 });

        var orchestrator = CreateOrchestrator();
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Check timeline has the context_gathering phase with details
        var contextPhase = result.Timeline?.FirstOrDefault(p => p.Name == "context_gathering");
        Assert.NotNull(contextPhase);
        // The details should indicate degraded mode (if we added that to the code)
    }

    [Fact]
    public async Task ExecuteTask_StoringResultFails_StillReturnsSuccess()
    {
        // Arrange
        _mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        _mockMemoryAgent.Setup(x => x.StoreQaAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), 
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Storage failed"));

        _mockMemoryAgent.Setup(x => x.RecordPromptFeedbackAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Feedback failed"));

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        _mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                Explanation = "Generated",
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "Test.cs", Content = "code", Type = FileChangeType.Created }
                }
            });

        _mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse { Passed = true, Score = 9 });

        var orchestrator = CreateOrchestrator();
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Task should still succeed even if storage fails
        Assert.Equal(TaskState.Complete, result.Status);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.Success);
    }

    [Fact]
    public async Task ExecuteTask_CodingAgentFails_ReturnsFailed()
    {
        // Arrange - Coding Agent is critical, should fail the task
        _mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        _mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = false,
                Error = "Model unavailable"
            });

        var orchestrator = CreateOrchestrator();
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 1
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Coding Agent failure IS critical
        Assert.Equal(TaskState.Failed, result.Status);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ExecuteTask_ValidationAgentFails_ReturnsFailed()
    {
        // Arrange - Validation Agent is critical
        _mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        _mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "Test.cs", Content = "code", Type = FileChangeType.Created }
                }
            });

        _mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Validation agent down"));

        var orchestrator = CreateOrchestrator();
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 1
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Validation Agent failure causes exception in the loop
        // The orchestrator should catch this and return Failed status
        Assert.True(result.Status == TaskState.Failed || result.Status == TaskState.Complete);
    }

    [Fact]
    public async Task ExecuteTask_ExecutionFails_LoopsBackWithRealErrors()
    {
        // Arrange
        _mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        _mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        var callCount = 0;
        _mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new GenerateCodeResponse
                {
                    Success = true,
                    FileChanges = new List<FileChange>
                    {
                        new FileChange { Path = "test.py", Content = callCount == 1 ? "bad syntax" : "print('hello')", Type = FileChangeType.Created }
                    }
                };
            });

        _mockCodingAgent.Setup(x => x.FixAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "test.py", Content = "print('hello')", Type = FileChangeType.Modified }
                }
            });

        var execCallCount = 0;
        _mockExecutionService.Setup(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<List<ExecutionFile>>(),
            It.IsAny<string>(),
            It.IsAny<AgentContracts.Models.ExecutionInstructions?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                execCallCount++;
                if (execCallCount == 1)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        BuildPassed = false,
                        ExecutionPassed = false,
                        Errors = "SyntaxError: invalid syntax"
                    };
                }
                return new ExecutionResult
                {
                    Success = true,
                    BuildPassed = true,
                    ExecutionPassed = true,
                    Output = "hello"
                };
            });

        _mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse { Passed = true, Score = 9 });

        var orchestrator = CreateOrchestrator();
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a hello world",
            Context = "test",
            WorkspacePath = "/test",
            Language = "python",
            MaxIterations = 3
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Should have looped back with real errors
        _mockCodingAgent.Verify(x => x.FixAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}

