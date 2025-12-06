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
    [Fact]
    public async Task OrchestrateTask_HelloWorld_GeneratesValidCode()
    {
        // Arrange
        var mockCodingAgent = new Mock<ICodingAgentClient>();
        var mockValidationAgent = new Mock<IValidationAgentClient>();
        var mockMemoryAgent = new Mock<IMemoryAgentClient>();
        var mockLogger = new Mock<ILogger<TaskOrchestrator>>();

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
    }

    [Fact]
    public async Task OrchestrateTask_ValidationFails_IteratesAndFixes()
    {
        // Arrange
        var mockCodingAgent = new Mock<ICodingAgentClient>();
        var mockValidationAgent = new Mock<IValidationAgentClient>();
        var mockMemoryAgent = new Mock<IMemoryAgentClient>();
        var mockLogger = new Mock<ILogger<TaskOrchestrator>>();

        var callCount = 0;

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
}



