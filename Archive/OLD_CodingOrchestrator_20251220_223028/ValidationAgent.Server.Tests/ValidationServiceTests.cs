using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ValidationAgent.Server.Services;
using ValidationAgent.Server.Clients;
using AgentContracts.Requests;
using AgentContracts.Services;

namespace ValidationAgent.Server.Tests;

public class ValidationServiceTests
{
    private readonly Mock<IValidationPromptBuilder> _mockPromptBuilder;
    private readonly Mock<IOllamaClient> _mockOllamaClient;
    private readonly Mock<IMemoryAgentClient> _mockMemoryAgent;
    private readonly Mock<ILogger<ValidationService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly ValidationService _service;

    public ValidationServiceTests()
    {
        _mockPromptBuilder = new Mock<IValidationPromptBuilder>();
        _mockOllamaClient = new Mock<IOllamaClient>();
        _mockMemoryAgent = new Mock<IMemoryAgentClient>();
        _mockLogger = new Mock<ILogger<ValidationService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        
        // Setup mock to return async prompt
        _mockPromptBuilder
            .Setup(x => x.BuildValidationPromptAsync(It.IsAny<ValidateCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test prompt");
        
        // Setup configuration
        _mockConfiguration.Setup(x => x.GetSection("Gpu:ValidationModel").Value)
            .Returns("phi4:latest");
        _mockConfiguration.Setup(x => x.GetSection("Ollama:Port").Value)
            .Returns("11434");
        
        // Setup Ollama client to return validation response
        _mockOllamaClient.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResponse
            {
                Success = true,
                Response = @"{
    ""issues"": [],
    ""summary"": ""Code looks good!""
}",
                TotalDurationMs = 500
            });
        
        _service = new ValidationService(
            _mockPromptBuilder.Object,
            _mockOllamaClient.Object,
            _mockMemoryAgent.Object,
            _mockLogger.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task ValidateAsync_GoodCode_PassesValidation()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Context = "test",
            Files = new List<CodeFile>
            {
                new CodeFile
                {
                    Path = "Services/GoodService.cs",
                    Content = @"
namespace Test.Services;

/// <summary>
/// A well-documented service
/// </summary>
public class GoodService
{
    private readonly ILogger<GoodService> _logger;

    public GoodService(ILogger<GoodService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Does something async with proper cancellation
    /// </summary>
    public async Task<string> DoSomethingAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        return ""done"";
    }
}",
                    IsNew = true
                }
            }
        };

        // Act
        var result = await _service.ValidateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Score >= 8);
        Assert.True(result.Passed);
    }

    [Fact]
    public async Task ValidateAsync_MissingNullCheck_WarnsAboutNullCheck()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Context = "test",
            Files = new List<CodeFile>
            {
                new CodeFile
                {
                    Path = "Services/BadService.cs",
                    Content = @"
public class BadService
{
    public void Process(string? input)
    {
        Console.WriteLine(input.Length);
    }
}",
                    IsNew = true
                }
            }
        };

        // Act
        var result = await _service.ValidateAsync(request, CancellationToken.None);

        // Assert
        Assert.Contains(result.Issues, i => i.Message.Contains("null", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_MissingAsyncCancellation_WarnsAboutCancellationToken()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Context = "test",
            Files = new List<CodeFile>
            {
                new CodeFile
                {
                    Path = "Services/AsyncService.cs",
                    Content = @"
public class AsyncService
{
    public async Task<string> GetDataAsync()
    {
        await Task.Delay(100);
        return ""data"";
    }
}",
                    IsNew = true
                }
            }
        };

        // Act
        var result = await _service.ValidateAsync(request, CancellationToken.None);

        // Assert
        Assert.Contains(result.Issues, i => i.Message.Contains("CancellationToken", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_SecurityVulnerability_ReportsHighSeverity()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Context = "test",
            Rules = new List<string> { "security" },
            Files = new List<CodeFile>
            {
                new CodeFile
                {
                    Path = "Services/SqlService.cs",
                    Content = @"
public class SqlService
{
    public void Execute(string userInput)
    {
        var query = ""SELECT * FROM Users WHERE Name = '"" + userInput + ""'"";
    }
}",
                    IsNew = true
                }
            }
        };

        // Act
        var result = await _service.ValidateAsync(request, CancellationToken.None);

        // Assert
        Assert.Contains(result.Issues, i => i.Severity == "critical");
        Assert.Contains(result.Issues, i => i.Message.Contains("SQL injection", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_UsesLlmValidation()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Context = "test",
            Files = new List<CodeFile>
            {
                new CodeFile
                {
                    Path = "Services/TestService.cs",
                    Content = "public class TestService { }",
                    IsNew = true
                }
            }
        };

        // Act
        await _service.ValidateAsync(request, CancellationToken.None);

        // Assert - should call Ollama for LLM validation
        _mockOllamaClient.Verify(x => x.GenerateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_LlmFailure_ContinuesWithRuleBasedOnly()
    {
        // Arrange
        _mockOllamaClient.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResponse
            {
                Response = "",
                Success = false,
                Error = "Model not available"
            });

        var request = new ValidateCodeRequest
        {
            Context = "test",
            Files = new List<CodeFile>
            {
                new CodeFile
                {
                    Path = "Services/TestService.cs",
                    Content = "public class TestService { }",
                    IsNew = true
                }
            }
        };

        // Act
        var result = await _service.ValidateAsync(request, CancellationToken.None);

        // Assert - should still return a result from rule-based validation
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
    }

    [Fact]
    public async Task ValidateAsync_LlmReturnsIssues_AddsToResults()
    {
        // Arrange
        _mockOllamaClient.Setup(x => x.GenerateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResponse
            {
                Success = true,
                Response = @"{
    ""issues"": [
        {
            ""severity"": ""warning"",
            ""file"": ""Services/TestService.cs"",
            ""line"": 5,
            ""message"": ""Consider using async/await"",
            ""suggestion"": ""Use async pattern"",
            ""rule"": ""async_patterns""
        }
    ],
    ""summary"": ""Minor improvements suggested""
}",
                TotalDurationMs = 500
            });

        var request = new ValidateCodeRequest
        {
            Context = "test",
            Files = new List<CodeFile>
            {
                new CodeFile
                {
                    Path = "Services/TestService.cs",
                    Content = "public class TestService { }",
                    IsNew = true
                }
            }
        };

        // Act
        var result = await _service.ValidateAsync(request, CancellationToken.None);

        // Assert
        Assert.Contains(result.Issues, i => i.Rule?.StartsWith("llm_") == true);
        Assert.Contains(result.Issues, i => i.Message.Contains("async/await"));
    }
}
