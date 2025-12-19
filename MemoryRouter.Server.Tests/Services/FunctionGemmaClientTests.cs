using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MemoryRouter.Server.Models;
using MemoryRouter.Server.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace MemoryRouter.Server.Tests.Services;

public class FunctionGemmaClientTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandler;
    private readonly Mock<ILogger<FunctionGemmaClient>> _logger;
    private readonly FunctionGemmaClient _client;

    public FunctionGemmaClientTests()
    {
        _httpMessageHandler = new Mock<HttpMessageHandler>();
        _logger = new Mock<ILogger<FunctionGemmaClient>>();

        var httpClient = new HttpClient(_httpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11435")
        };

        _client = new FunctionGemmaClient(httpClient, _logger.Object);
    }

    [Fact]
    public async Task PlanWorkflowAsync_ValidRequest_ReturnsWorkflowPlan()
    {
        // Arrange
        var tools = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Name = "semantic_search",
                Service = "memory-agent",
                Description = "Search for code",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["query"] = new Dictionary<string, object> { ["type"] = "string" }
                    }
                }
            },
            new ToolDefinition
            {
                Name = "orchestrate_task",
                Service = "coding-orchestrator",
                Description = "Generate code",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["task"] = new Dictionary<string, object> { ["type"] = "string" }
                    }
                }
            }
        };

        var gemmaResponse = new
        {
            response = JsonSerializer.Serialize(new
            {
                name = "semantic_search",
                parameters = new { query = "user authentication" }
            })
        };

        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(gemmaResponse), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.PlanWorkflowAsync(
            "Create a user authentication service",
            tools
        );

        // Assert
        result.Should().NotBeNull();
        result.Reasoning.Should().Contain("Execute");
        result.FunctionCalls.Should().HaveCount(1);
        result.FunctionCalls[0].Name.Should().Be("semantic_search");
        result.FunctionCalls[0].Order.Should().Be(1);
        result.FunctionCalls[0].Arguments.Should().ContainKey("query");
    }

    [Fact]
    public async Task PlanWorkflowAsync_WithContext_IncludesContextInPrompt()
    {
        // Arrange
        var tools = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Name = "semantic_search",
                Service = "memory-agent",
                Description = "Search",
                InputSchema = new Dictionary<string, object>()
            }
        };

        var context = new Dictionary<string, object>
        {
            ["workspacePath"] = "/my/project",
            ["language"] = "csharp"
        };

        HttpRequestMessage? capturedRequest = null;

        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    response = @"{""name"":""semantic_search"",""parameters"":{""query"":""test""}}"
                }), Encoding.UTF8, "application/json")
            });

        // Act
        await _client.PlanWorkflowAsync("Test request", tools, context);

        // Assert
        capturedRequest.Should().NotBeNull();
        var requestBody = await capturedRequest!.Content!.ReadAsStringAsync();
        requestBody.Should().Contain("/my/project");
        requestBody.Should().Contain("csharp");
    }

    [Fact]
    public async Task PlanWorkflowAsync_InvalidJson_ThrowsException()
    {
        // Arrange
        var tools = new List<ToolDefinition>();

        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    response = "This is not valid JSON {{{{"
                }), Encoding.UTF8, "application/json")
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _client.PlanWorkflowAsync("Test", tools)
        );
    }

    [Fact]
    public async Task PlanWorkflowAsync_EmptyPlan_ReturnsValidPlan()
    {
        // Arrange
        var tools = new List<ToolDefinition>();

        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    response = @"{""name"":""test_tool"",""parameters"":{}}"
                }), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.PlanWorkflowAsync("Test", tools);
        
        // Assert
        result.Should().NotBeNull();
        result.FunctionCalls.Should().HaveCount(1);
    }

    [Fact]
    public async Task PlanWorkflowAsync_HandlesMarkdownCodeBlocks()
    {
        // Arrange
        var tools = new List<ToolDefinition>();

        var gemmaResponse = new
        {
            response = @"```json
{
  ""name"": ""test_tool"",
  ""parameters"": {}
}
```"
        };

        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(gemmaResponse), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.PlanWorkflowAsync("Test", tools);

        // Assert
        result.Should().NotBeNull();
        result.FunctionCalls.Should().HaveCount(1);
        result.FunctionCalls[0].Name.Should().Be("test_tool");
    }

    [Fact]
    public async Task PlanWorkflowAsync_AutoAssignsOrderIfMissing()
    {
        // Arrange
        var tools = new List<ToolDefinition>();

        var gemmaResponse = new
        {
            response = JsonSerializer.Serialize(new
            {
                name = "tool1",
                parameters = new { }
            })
        };

        _httpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(gemmaResponse), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _client.PlanWorkflowAsync("Test", tools);

        // Assert
        result.FunctionCalls.Should().HaveCount(1);
        result.FunctionCalls[0].Order.Should().Be(1);
        result.FunctionCalls[0].Name.Should().Be("tool1");
    }
}

