using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using CodingAgent.Server.Services;
using AgentContracts.Models;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;

namespace CodingAgent.Server.Tests;

public class CodeGenerationServiceTests
{
    private readonly Mock<IPromptBuilder> _mockPromptBuilder;
    private readonly Mock<IOllamaClient> _mockOllamaClient;
    private readonly Mock<IModelOrchestrator> _mockModelOrchestrator;
    private readonly Mock<ILogger<CodeGenerationService>> _mockLogger;
    private readonly CodeGenerationService _service;

    public CodeGenerationServiceTests()
    {
        _mockPromptBuilder = new Mock<IPromptBuilder>();
        _mockOllamaClient = new Mock<IOllamaClient>();
        _mockModelOrchestrator = new Mock<IModelOrchestrator>();
        _mockLogger = new Mock<ILogger<CodeGenerationService>>();
        
        // Setup async prompt builder methods
        _mockPromptBuilder.Setup(x => x.BuildGeneratePromptAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test prompt");
        _mockPromptBuilder.Setup(x => x.BuildFixPromptAsync(It.IsAny<GenerateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test fix prompt");
        
        // Setup model orchestrator to return a model
        _mockModelOrchestrator.Setup(x => x.GetPrimaryModel())
            .Returns(("deepseek-v2:16b", 11434));
        _mockModelOrchestrator.Setup(x => x.SelectModelAsync(It.IsAny<ModelPurpose>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("phi4:latest", 11434));
        _mockModelOrchestrator.Setup(x => x.RecordModelPerformanceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
            
        // Setup Ollama client to return generated code
        _mockOllamaClient.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResponse
            {
                Success = true,
                Response = @"Here's a Hello World implementation:

```csharp:Services/HelloWorldService.cs
namespace GeneratedCode.Services;

/// <summary>
/// A simple Hello World service
/// </summary>
public class HelloWorldService
{
    public string SayHello(string? name = null)
    {
        return string.IsNullOrWhiteSpace(name) 
            ? ""Hello, World!"" 
            : $""Hello, {name}!"";
    }
}
```",
                TotalDurationMs = 1000,
                PromptTokens = 100,
                ResponseTokens = 200
            });
            
        _service = new CodeGenerationService(
            _mockPromptBuilder.Object,
            _mockOllamaClient.Object,
            _mockModelOrchestrator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateAsync_HelloWorld_ReturnsHelloWorldService()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Create a Hello World method",
            WorkspacePath = "/test"
        };

        // Act
        var result = await _service.GenerateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.FileChanges);
        Assert.Contains(result.FileChanges, f => f.Path.Contains("HelloWorld"));
        Assert.Contains(result.FileChanges, f => f.Content.Contains("SayHello"));
    }

    [Fact]
    public async Task GenerateAsync_UsesModelOrchestrator()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Create a service",
            WorkspacePath = "/test"
        };

        // Act
        await _service.GenerateAsync(request, CancellationToken.None);

        // Assert
        _mockModelOrchestrator.Verify(x => x.GetPrimaryModel(), Times.Once);
    }

    [Fact]
    public async Task FixAsync_UsesModelRotation()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Fix the service",
            WorkspacePath = "/test",
            PreviousFeedback = new ValidationFeedback
            {
                Score = 5,
                Issues = new List<ValidationIssue>
                {
                    new ValidationIssue { Severity = "warning", Message = "Missing null check" }
                },
                TriedModels = new HashSet<string> { "deepseek-v2:16b" }
            }
        };

        // Act
        await _service.FixAsync(request, CancellationToken.None);

        // Assert - should select a different model for fix
        _mockModelOrchestrator.Verify(x => x.SelectModelAsync(
            ModelPurpose.CodeGeneration, 
            It.Is<HashSet<string>>(h => h.Contains("deepseek-v2:16b")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_TracksModelUsed()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Create a service",
            WorkspacePath = "/test"
        };

        // Act
        var result = await _service.GenerateAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal("deepseek-v2:16b", result.ModelUsed);
    }

    [Fact]
    public async Task GenerateAsync_WithContext_UsesContext()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Create Hello World",
            WorkspacePath = "/test",
            Context = new CodeContext
            {
                SimilarSolutions = new List<PastSolution>
                {
                    new PastSolution 
                    { 
                        Question = "Previous hello world",
                        Answer = "Previous solution" 
                    }
                }
            }
        };

        // Act
        var result = await _service.GenerateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        _mockPromptBuilder.Verify(x => x.BuildGeneratePromptAsync(
            It.Is<GenerateCodeRequest>(r => r.Context != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_OllamaFailure_ReturnsError()
    {
        // Arrange
        _mockOllamaClient.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResponse
            {
                Response = "",
                Success = false,
                Error = "Model not found"
            });

        var request = new GenerateCodeRequest
        {
            Task = "Create a service",
            WorkspacePath = "/test"
        };

        // Act
        var result = await _service.GenerateAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Model not found", result.Error);
    }
}
