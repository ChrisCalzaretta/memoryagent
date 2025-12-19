using FluentAssertions;
using Microsoft.Extensions.Logging;
using MemoryRouter.Server.Models;
using MemoryRouter.Server.Services;
using Moq;
using Xunit;

namespace MemoryRouter.Server.Tests.Services;

/// <summary>
/// Tests for McpHandler - MCP protocol integration layer
/// </summary>
public class McpHandlerTests
{
    private readonly Mock<IRouterService> _routerService;
    private readonly Mock<IToolRegistry> _toolRegistry;
    private readonly Mock<ILogger<McpHandler>> _logger;
    private readonly McpHandler _handler;

    public McpHandlerTests()
    {
        _routerService = new Mock<IRouterService>();
        _toolRegistry = new Mock<IToolRegistry>();
        _logger = new Mock<ILogger<McpHandler>>();

        _handler = new McpHandler(
            _routerService.Object,
            _toolRegistry.Object,
            _logger.Object
        );
    }

    [Fact]
    public void GetToolDefinitions_ReturnsExecuteTaskTool()
    {
        // Act
        var tools = _handler.GetToolDefinitions().ToList();

        // Assert
        tools.Should().NotBeEmpty();
        
        var executeTask = tools.FirstOrDefault(t => 
            t is Dictionary<string, object> dict && 
            dict.ContainsKey("name") && 
            dict["name"].ToString() == "execute_task"
        ) as Dictionary<string, object>;

        executeTask.Should().NotBeNull();
        executeTask!["name"].Should().Be("execute_task");
        executeTask.Should().ContainKey("description");
        executeTask.Should().ContainKey("inputSchema");
    }

    [Fact]
    public void GetToolDefinitions_ReturnsListAvailableToolsTool()
    {
        // Act
        var tools = _handler.GetToolDefinitions().ToList();

        // Assert
        var listTools = tools.FirstOrDefault(t => 
            t is Dictionary<string, object> dict && 
            dict.ContainsKey("name") && 
            dict["name"].ToString() == "list_available_tools"
        ) as Dictionary<string, object>;

        listTools.Should().NotBeNull();
        listTools!["name"].Should().Be("list_available_tools");
    }

    [Fact]
    public async Task HandleToolCallAsync_WithExecuteTask_CallsRouterService()
    {
        // Arrange
        var arguments = new Dictionary<string, object>
        {
            ["request"] = "Find authentication code"
        };

        var mockResult = new WorkflowResult
        {
            RequestId = "test-123",
            OriginalRequest = "Find authentication code",
            Plan = new WorkflowPlan
            {
                Reasoning = "Search for auth code",
                FunctionCalls = new List<FunctionCall>()
            },
            Steps = new List<StepResult>(),
            Success = true,
            FinalResult = "Found 5 files",
            TotalDurationMs = 100
        };

        _routerService
            .Setup(x => x.ExecuteRequestAsync(
                "Find authentication code",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(mockResult);

        // Act
        var result = await _handler.HandleToolCallAsync("execute_task", arguments, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("✅ Task Completed Successfully");
        result.Should().Contain("Find authentication code");
        
        _routerService.Verify(
            x => x.ExecuteRequestAsync(
                "Find authentication code",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleToolCallAsync_WithMissingRequest_ReturnsError()
    {
        // Arrange
        var arguments = new Dictionary<string, object>(); // No 'request' parameter

        // Act
        var result = await _handler.HandleToolCallAsync("execute_task", arguments, CancellationToken.None);

        // Assert
        result.Should().Contain("Error");
        result.Should().Contain("'request' parameter is required");
    }

    [Fact]
    public async Task HandleToolCallAsync_WithFailedWorkflow_ReturnsFailureMessage()
    {
        // Arrange
        var arguments = new Dictionary<string, object>
        {
            ["request"] = "Test request"
        };

        var mockResult = new WorkflowResult
        {
            RequestId = "test-123",
            OriginalRequest = "Test request",
            Plan = new WorkflowPlan
            {
                Reasoning = "Test",
                FunctionCalls = new List<FunctionCall>()
            },
            Steps = new List<StepResult>(),
            Success = false,
            Error = "Connection failed",
            TotalDurationMs = 50
        };

        _routerService
            .Setup(x => x.ExecuteRequestAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(mockResult);

        // Act
        var result = await _handler.HandleToolCallAsync("execute_task", arguments, CancellationToken.None);

        // Assert
        result.Should().Contain("❌ Task Failed");
        result.Should().Contain("Connection failed");
    }

    [Fact]
    public void HandleToolCallAsync_WithListAvailableTools_ReturnsToolList()
    {
        // Arrange
        var mockTools = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Name = "semantic_search",
                Service = "memory-agent",
                Description = "Search code",
                InputSchema = new Dictionary<string, object>(),
                UseCases = new List<string> { "Find code" },
                Keywords = new List<string> { "search" }
            },
            new ToolDefinition
            {
                Name = "orchestrate_task",
                Service = "coding-orchestrator",
                Description = "Generate code",
                InputSchema = new Dictionary<string, object>(),
                UseCases = new List<string> { "Create code" },
                Keywords = new List<string> { "generate" }
            }
        };

        _toolRegistry
            .Setup(x => x.GetAllTools())
            .Returns(mockTools);

        var arguments = new Dictionary<string, object>();

        // Act
        var result = _handler.HandleToolCallAsync("list_available_tools", arguments, CancellationToken.None).Result;

        // Assert
        result.Should().Contain("Available Tools");
        result.Should().Contain("semantic_search");
        result.Should().Contain("orchestrate_task");
        result.Should().Contain("memory-agent");
        result.Should().Contain("coding-orchestrator");
    }

    [Fact]
    public async Task HandleToolCallAsync_WithContextParameter_PassesContextToRouter()
    {
        // Arrange
        var arguments = new Dictionary<string, object>
        {
            ["request"] = "Test request",
            ["context"] = "my-project"
        };

        Dictionary<string, object>? capturedContext = null;

        _routerService
            .Setup(x => x.ExecuteRequestAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<string, Dictionary<string, object>?, CancellationToken>(
                (req, ctx, ct) => capturedContext = ctx
            )
            .ReturnsAsync(new WorkflowResult
            {
                RequestId = "test",
                OriginalRequest = "Test",
                Plan = new WorkflowPlan { FunctionCalls = new List<FunctionCall>() },
                Steps = new List<StepResult>(),
                Success = true,
                TotalDurationMs = 0
            });

        // Act
        await _handler.HandleToolCallAsync("execute_task", arguments, CancellationToken.None);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext.Should().ContainKey("context");
        capturedContext!["context"].Should().Be("my-project");
    }

    [Fact]
    public async Task HandleToolCallAsync_WithWorkspacePathParameter_PassesPathToRouter()
    {
        // Arrange
        var arguments = new Dictionary<string, object>
        {
            ["request"] = "Test request",
            ["workspacePath"] = "/my/workspace"
        };

        Dictionary<string, object>? capturedContext = null;

        _routerService
            .Setup(x => x.ExecuteRequestAsync(
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<string, Dictionary<string, object>?, CancellationToken>(
                (req, ctx, ct) => capturedContext = ctx
            )
            .ReturnsAsync(new WorkflowResult
            {
                RequestId = "test",
                OriginalRequest = "Test",
                Plan = new WorkflowPlan { FunctionCalls = new List<FunctionCall>() },
                Steps = new List<StepResult>(),
                Success = true,
                TotalDurationMs = 0
            });

        // Act
        await _handler.HandleToolCallAsync("execute_task", arguments, CancellationToken.None);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext.Should().ContainKey("workspacePath");
        capturedContext!["workspacePath"].Should().Be("/my/workspace");
    }

    [Fact]
    public async Task HandleToolCallAsync_WithUnknownTool_ReturnsErrorMessage()
    {
        // Arrange
        var arguments = new Dictionary<string, object>();

        // Act
        var result = await _handler.HandleToolCallAsync("unknown_tool", arguments, CancellationToken.None);

        // Assert
        result.Should().Contain("Unknown tool");
        result.Should().Contain("unknown_tool");
    }
}

