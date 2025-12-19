using FluentAssertions;
using Microsoft.Extensions.Logging;
using MemoryRouter.Server.Clients;
using MemoryRouter.Server.Models;
using MemoryRouter.Server.Services;
using Moq;
using Xunit;

namespace MemoryRouter.Server.Tests.Services;

public class RouterServiceTests
{
    private readonly Mock<IFunctionGemmaClient> _gemmaClient;
    private readonly Mock<IToolRegistry> _toolRegistry;
    private readonly Mock<IMemoryAgentClient> _memoryAgent;
    private readonly Mock<ICodingOrchestratorClient> _codingOrchestrator;
    private readonly Mock<IHybridExecutionClassifier> _executionClassifier;
    private readonly Mock<IBackgroundJobManager> _jobManager;
    private readonly Mock<IPerformanceTracker> _performanceTracker;
    private readonly Mock<ILogger<RouterService>> _logger;
    private readonly RouterService _service;

    public RouterServiceTests()
    {
        _gemmaClient = new Mock<IFunctionGemmaClient>();
        _toolRegistry = new Mock<IToolRegistry>();
        _memoryAgent = new Mock<IMemoryAgentClient>();
        _codingOrchestrator = new Mock<ICodingOrchestratorClient>();
        _executionClassifier = new Mock<IHybridExecutionClassifier>();
        _jobManager = new Mock<IBackgroundJobManager>();
        _performanceTracker = new Mock<IPerformanceTracker>();
        _logger = new Mock<ILogger<RouterService>>();

        // Setup default execution classifier to return synchronous execution
        _executionClassifier.Setup(x => x.DetermineExecutionModeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExecutionDecision
            {
                ShouldRunAsync = false,
                EstimatedDurationMs = 100,
                ConfidencePercent = 80,
                DecisionSource = "Test Mock",
                Reasoning = "Mock synchronous execution for tests"
            });

        _service = new RouterService(
            _gemmaClient.Object,
            _toolRegistry.Object,
            _memoryAgent.Object,
            _codingOrchestrator.Object,
            _executionClassifier.Object,
            _jobManager.Object,
            _performanceTracker.Object,
            _logger.Object
        );
    }

    [Fact]
    public async Task ExecuteRequestAsync_SimpleWorkflow_ExecutesSuccessfully()
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

        _toolRegistry
            .Setup(x => x.GetAllTools())
            .Returns(tools);

        _toolRegistry
            .Setup(x => x.GetTool("semantic_search"))
            .Returns(tools[0]);

        var plan = new WorkflowPlan
        {
            Reasoning = "Search for authentication code",
            FunctionCalls = new List<FunctionCall>
            {
                new FunctionCall
                {
                    Name = "semantic_search",
                    Arguments = new Dictionary<string, object> { ["query"] = "authentication" },
                    Reasoning = "Find auth code",
                    Order = 1
                }
            }
        };

        _gemmaClient
            .Setup(x => x.PlanWorkflowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(plan);

        _memoryAgent
            .Setup(x => x.CallToolAsync("semantic_search", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { results = "Found 5 files" });

        // Act
        var result = await _service.ExecuteRequestAsync("Find authentication code");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Success.Should().BeTrue();
        result.Steps[0].ToolName.Should().Be("semantic_search");
    }

    [Fact]
    public async Task ExecuteRequestAsync_MultiStepWorkflow_ExecutesInOrder()
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
            },
            new ToolDefinition
            {
                Name = "orchestrate_task",
                Service = "coding-orchestrator",
                Description = "Generate code",
                InputSchema = new Dictionary<string, object>()
            }
        };

        _toolRegistry
            .Setup(x => x.GetAllTools())
            .Returns(tools);

        _toolRegistry
            .Setup(x => x.GetTool("semantic_search"))
            .Returns(tools[0]);

        _toolRegistry
            .Setup(x => x.GetTool("orchestrate_task"))
            .Returns(tools[1]);

        var plan = new WorkflowPlan
        {
            Reasoning = "Search then generate",
            FunctionCalls = new List<FunctionCall>
            {
                new FunctionCall
                {
                    Name = "semantic_search",
                    Arguments = new Dictionary<string, object> { ["query"] = "user service" },
                    Reasoning = "Find existing patterns",
                    Order = 1
                },
                new FunctionCall
                {
                    Name = "orchestrate_task",
                    Arguments = new Dictionary<string, object> { ["task"] = "Create user service" },
                    Reasoning = "Generate new service",
                    Order = 2
                }
            }
        };

        _gemmaClient
            .Setup(x => x.PlanWorkflowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(plan);

        _memoryAgent
            .Setup(x => x.CallToolAsync("semantic_search", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { results = "Found patterns" });

        _codingOrchestrator
            .Setup(x => x.CallToolAsync("orchestrate_task", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new { jobId = "job_123" });

        // Act
        var result = await _service.ExecuteRequestAsync("Create a user service");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Steps.Should().HaveCount(2);
        result.Steps[0].ToolName.Should().Be("semantic_search");
        result.Steps[1].ToolName.Should().Be("orchestrate_task");
        
        // Verify execution order
        _memoryAgent.Verify(
            x => x.CallToolAsync("semantic_search", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        
        _codingOrchestrator.Verify(
            x => x.CallToolAsync("orchestrate_task", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteRequestAsync_ToolFails_ReturnsFailureResult()
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

        _toolRegistry
            .Setup(x => x.GetAllTools())
            .Returns(tools);

        _toolRegistry
            .Setup(x => x.GetTool("semantic_search"))
            .Returns(tools[0]);

        var plan = new WorkflowPlan
        {
            Reasoning = "Search",
            FunctionCalls = new List<FunctionCall>
            {
                new FunctionCall
                {
                    Name = "semantic_search",
                    Arguments = new Dictionary<string, object> { ["query"] = "test" },
                    Reasoning = "Test",
                    Order = 1
                }
            }
        };

        _gemmaClient
            .Setup(x => x.PlanWorkflowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(plan);

        _memoryAgent
            .Setup(x => x.CallToolAsync("semantic_search", It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Connection failed"));

        // Act
        var result = await _service.ExecuteRequestAsync("Test request");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Connection failed");
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteRequestAsync_PlanningFails_ReturnsFailureResult()
    {
        // Arrange
        var tools = new List<ToolDefinition>();

        _toolRegistry
            .Setup(x => x.GetAllTools())
            .Returns(tools);

        _gemmaClient
            .Setup(x => x.PlanWorkflowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .ThrowsAsync(new InvalidOperationException("FunctionGemma returned invalid JSON"));

        // Act
        var result = await _service.ExecuteRequestAsync("Test request");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("FunctionGemma returned invalid JSON");
        result.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteRequestAsync_UnknownTool_ReturnsFailureResult()
    {
        // Arrange
        var tools = new List<ToolDefinition>();

        _toolRegistry
            .Setup(x => x.GetAllTools())
            .Returns(tools);

        _toolRegistry
            .Setup(x => x.GetTool("unknown_tool"))
            .Returns((ToolDefinition?)null);

        var plan = new WorkflowPlan
        {
            Reasoning = "Test",
            FunctionCalls = new List<FunctionCall>
            {
                new FunctionCall
                {
                    Name = "unknown_tool",
                    Arguments = new Dictionary<string, object>(),
                    Reasoning = "Test",
                    Order = 1
                }
            }
        };

        _gemmaClient
            .Setup(x => x.PlanWorkflowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(plan);

        // Act
        var result = await _service.ExecuteRequestAsync("Test request");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found in registry");
    }

    [Fact]
    public async Task ExecuteRequestAsync_WithContext_PassesContextToGemma()
    {
        // Arrange
        var tools = new List<ToolDefinition>();
        var context = new Dictionary<string, object>
        {
            ["workspacePath"] = "/my/project",
            ["context"] = "my-app"
        };

        _toolRegistry
            .Setup(x => x.GetAllTools())
            .Returns(tools);

        var plan = new WorkflowPlan
        {
            Reasoning = "Test",
            FunctionCalls = new List<FunctionCall>()
        };

        Dictionary<string, object>? capturedContext = null;

        _gemmaClient
            .Setup(x => x.PlanWorkflowAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<ToolDefinition>>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<string, IEnumerable<ToolDefinition>, Dictionary<string, object>?, CancellationToken>(
                (_, _, ctx, _) => capturedContext = ctx
            )
            .ReturnsAsync(plan);

        // Act
        await _service.ExecuteRequestAsync("Test", context);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext.Should().ContainKey("workspacePath");
        capturedContext!["workspacePath"].Should().Be("/my/project");
    }
}

