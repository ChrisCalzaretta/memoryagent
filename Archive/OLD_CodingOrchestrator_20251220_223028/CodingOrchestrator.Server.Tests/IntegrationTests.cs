using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CodingOrchestrator.Server.Services;
using CodingOrchestrator.Server.Clients;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Tests;

public class IntegrationTests
{
    private Mock<IExecutionService> CreateMockExecutionService(bool success = true)
    {
        var mock = new Mock<IExecutionService>();
        mock.Setup(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<List<ExecutionFile>>(),
            It.IsAny<string>(),
            It.IsAny<AgentContracts.Models.ExecutionInstructions?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionResult
            {
                Success = success,
                BuildPassed = success,
                ExecutionPassed = success,
                Output = success ? "Execution successful" : "",
                Errors = success ? "" : "Execution failed"
            });
        return mock;
    }

    [Fact]
    public async Task OrchestrateTask_HelloWorld_GeneratesValidCode()
    {
        // Arrange
        var mockCodingAgent = new Mock<ICodingAgentClient>();
        var mockValidationAgent = new Mock<IValidationAgentClient>();
        var mockMemoryAgent = new Mock<IMemoryAgentClient>();
        var mockExecutionService = CreateMockExecutionService(success: true);
        var mockLogger = new Mock<ILogger<TaskOrchestrator>>();

        // Setup complexity estimation
        mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true, RecommendedIterations = 3 });

        // Setup coding agent to return Hello World code
        mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange
                    {
                        Path = "Services/HelloWorldService.cs",
                        Content = @"namespace GeneratedCode.Services;

/// <summary>
/// A simple Hello World service
/// </summary>
public class HelloWorldService
{
    public string SayHello(string? name = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ""Hello, World!"";
        return $""Hello, {name}!"";
    }
}",
                        Type = FileChangeType.Created,
                        Reason = "Created HelloWorldService"
                    }
                },
                Explanation = "Created a HelloWorldService with a SayHello method"
            });

        // Setup validation agent to pass
        mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse
            {
                Passed = true,
                Score = 9,
                Summary = "Code looks great!"
            });

        // Setup memory agent
        mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        var orchestrator = new TaskOrchestrator(
            mockCodingAgent.Object,
            mockValidationAgent.Object,
            mockMemoryAgent.Object,
            mockExecutionService.Object,
            mockLogger.Object);

        var request = new OrchestrateTaskRequest
        {
            Task = "Create a Hello World method",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 5,
            MinValidationScore = 8
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(TaskState.Complete, result.Status);
        Assert.NotNull(result.Result);
        Assert.True(result.Result.Success);
        Assert.Single(result.Result.Files);
        Assert.Contains("HelloWorld", result.Result.Files[0].Path);
        Assert.Contains("SayHello", result.Result.Files[0].Content);
        Assert.Equal(9, result.Result.ValidationScore);
        
        // Verify execution service was called
        mockExecutionService.Verify(x => x.ExecuteAsync(
            It.IsAny<string>(),
            It.IsAny<List<ExecutionFile>>(),
            It.IsAny<string>(),
            It.IsAny<AgentContracts.Models.ExecutionInstructions?>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OrchestrateTask_ValidationFails_IteratesAndFixes()
    {
        // Arrange
        var mockCodingAgent = new Mock<ICodingAgentClient>();
        var mockValidationAgent = new Mock<IValidationAgentClient>();
        var mockMemoryAgent = new Mock<IMemoryAgentClient>();
        var mockExecutionService = CreateMockExecutionService(success: true);
        var mockLogger = new Mock<ILogger<TaskOrchestrator>>();

        var callCount = 0;

        // Setup complexity estimation
        mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        // Setup coding agent - first call returns bad code, second returns fixed
        mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange
                    {
                        Path = "Services/TestService.cs",
                        Content = "public class TestService { }",
                        Type = FileChangeType.Created
                    }
                }
            });

        mockCodingAgent.Setup(x => x.FixAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange
                    {
                        Path = "Services/TestService.cs",
                        Content = @"/// <summary>Fixed service</summary>
public class TestService 
{ 
    public async Task DoWork(CancellationToken ct) => await Task.Delay(1, ct);
}",
                        Type = FileChangeType.Modified
                    }
                }
            });

        // Setup validation agent - fails first, passes second
        mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new ValidateCodeResponse
                    {
                        Passed = false,
                        Score = 5,
                        Issues = new List<ValidationIssue>
                        {
                            new ValidationIssue
                            {
                                Severity = "warning",
                                Message = "Missing documentation"
                            }
                        }
                    };
                }
                return new ValidateCodeResponse
                {
                    Passed = true,
                    Score = 9,
                    Summary = "Fixed!"
                };
            });

        mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        var orchestrator = new TaskOrchestrator(
            mockCodingAgent.Object,
            mockValidationAgent.Object,
            mockMemoryAgent.Object,
            mockExecutionService.Object,
            mockLogger.Object);

        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = 5,
            MinValidationScore = 8
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal(TaskState.Complete, result.Status);
        Assert.NotNull(result.Result);
        Assert.Equal(2, result.Result.TotalIterations);
        Assert.Equal(9, result.Result.ValidationScore);
        
        // Verify fix was called
        mockCodingAgent.Verify(x => x.FixAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OrchestrateTask_ExecutionFails_RetriesWithRealErrors()
    {
        // Arrange
        var mockCodingAgent = new Mock<ICodingAgentClient>();
        var mockValidationAgent = new Mock<IValidationAgentClient>();
        var mockMemoryAgent = new Mock<IMemoryAgentClient>();
        var mockExecutionService = new Mock<IExecutionService>();
        var mockLogger = new Mock<ILogger<TaskOrchestrator>>();

        mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "test.py", Content = "bad code", Type = FileChangeType.Created }
                }
            });

        mockCodingAgent.Setup(x => x.FixAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "test.py", Content = "print('hello')", Type = FileChangeType.Modified }
                }
            });

        var execCallCount = 0;
        mockExecutionService.Setup(x => x.ExecuteAsync(
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

        mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse { Passed = true, Score = 9 });

        mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        var orchestrator = new TaskOrchestrator(
            mockCodingAgent.Object,
            mockValidationAgent.Object,
            mockMemoryAgent.Object,
            mockExecutionService.Object,
            mockLogger.Object);

        var request = new OrchestrateTaskRequest
        {
            Task = "Create hello world",
            Context = "test",
            WorkspacePath = "/test",
            Language = "python",
            MaxIterations = 3
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Should retry with FixAsync after execution failure
        mockCodingAgent.Verify(x => x.FixAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        Assert.Equal(TaskState.Complete, result.Status);
    }

    [Fact]
    public async Task OrchestrateTask_TimelineIncludesExecutionPhase()
    {
        // Arrange
        var mockCodingAgent = new Mock<ICodingAgentClient>();
        var mockValidationAgent = new Mock<IValidationAgentClient>();
        var mockMemoryAgent = new Mock<IMemoryAgentClient>();
        var mockExecutionService = CreateMockExecutionService(success: true);
        var mockLogger = new Mock<ILogger<TaskOrchestrator>>();

        mockCodingAgent.Setup(x => x.EstimateComplexityAsync(It.IsAny<EstimateComplexityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EstimateComplexityResponse { Success = true });

        mockCodingAgent.Setup(x => x.GenerateAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateCodeResponse
            {
                Success = true,
                FileChanges = new List<FileChange>
                {
                    new FileChange { Path = "Test.cs", Content = "code", Type = FileChangeType.Created }
                }
            });

        mockValidationAgent.Setup(x => x.ValidateAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateCodeResponse { Passed = true, Score = 9 });

        mockMemoryAgent.Setup(x => x.GetContextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CodeContext());

        var orchestrator = new TaskOrchestrator(
            mockCodingAgent.Object,
            mockValidationAgent.Object,
            mockMemoryAgent.Object,
            mockExecutionService.Object,
            mockLogger.Object);

        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var result = await orchestrator.ExecuteTaskAsync(request, CancellationToken.None);

        // Assert - Timeline should include docker_execution phase
        Assert.NotNull(result.Timeline);
        Assert.Contains(result.Timeline, p => p.Name == "docker_execution");
        Assert.Contains(result.Timeline, p => p.Name == "complexity_estimation");
        Assert.Contains(result.Timeline, p => p.Name == "context_gathering");
        Assert.Contains(result.Timeline, p => p.Name == "coding_agent");
        Assert.Contains(result.Timeline, p => p.Name == "validation_agent");
    }
}



