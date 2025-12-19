using FluentAssertions;
using Microsoft.Extensions.Logging;
using MemoryRouter.Server.Services;
using MemoryRouter.Server.Clients;
using MemoryRouter.Server.Models;
using Moq;
using Xunit;

namespace MemoryRouter.Server.Tests.Services;

public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _logger;
    private readonly Mock<IMemoryAgentClient> _memoryAgent;
    private readonly Mock<ICodingOrchestratorClient> _codingOrchestrator;
    private readonly ToolRegistry _registry;

    public ToolRegistryTests()
    {
        _logger = new Mock<ILogger<ToolRegistry>>();
        _memoryAgent = new Mock<IMemoryAgentClient>();
        _codingOrchestrator = new Mock<ICodingOrchestratorClient>();
        
        // Setup mock responses for tool discovery
        _memoryAgent.Setup(x => x.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetMockMemoryAgentTools());
        
        _codingOrchestrator.Setup(x => x.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetMockCodingOrchestratorTools());
        
        _registry = new ToolRegistry(_logger.Object, _memoryAgent.Object, _codingOrchestrator.Object);
    }

    private List<McpToolDefinition> GetMockMemoryAgentTools()
    {
        return new List<McpToolDefinition>
        {
            new McpToolDefinition { Name = "semantic_search", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "smart_search", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "explain_code", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "index_workspace", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "validate_pattern", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "create_plan", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } }
        };
    }

    private List<McpToolDefinition> GetMockCodingOrchestratorTools()
    {
        return new List<McpToolDefinition>
        {
            new McpToolDefinition { Name = "orchestrate_task", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "get_task_status", Description = "Check the status, progress, and completion state of running or completed tasks", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "design_create_brand", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } },
            new McpToolDefinition { Name = "design_validate", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object" } }
        };
    }

    [Fact]
    public async Task InitializeAsync_RegistersAllTools()
    {
        // Act
        await _registry.InitializeAsync();

        // Assert
        var tools = _registry.GetAllTools().ToList();
        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Service == "memory-agent");
        tools.Should().Contain(t => t.Service == "coding-orchestrator");
    }

    [Fact]
    public async Task InitializeAsync_RegistersMemoryAgentTools()
    {
        // Act
        await _registry.InitializeAsync();

        // Assert
        var memoryTools = _registry.GetAllTools()
            .Where(t => t.Service == "memory-agent")
            .ToList();

        memoryTools.Should().Contain(t => t.Name == "semantic_search");
        memoryTools.Should().Contain(t => t.Name == "smart_search");
        memoryTools.Should().Contain(t => t.Name == "explain_code");
        memoryTools.Should().Contain(t => t.Name == "index_workspace");
        memoryTools.Should().Contain(t => t.Name == "validate_pattern");
        memoryTools.Should().Contain(t => t.Name == "create_plan");
    }

    [Fact]
    public async Task InitializeAsync_RegistersCodingOrchestratorTools()
    {
        // Act
        await _registry.InitializeAsync();

        // Assert
        var codingTools = _registry.GetAllTools()
            .Where(t => t.Service == "coding-orchestrator")
            .ToList();

        codingTools.Should().Contain(t => t.Name == "orchestrate_task");
        codingTools.Should().Contain(t => t.Name == "get_task_status");
        codingTools.Should().Contain(t => t.Name == "design_create_brand");
        codingTools.Should().Contain(t => t.Name == "design_validate");
    }

    [Fact]
    public async Task GetTool_ExistingTool_ReturnsTool()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var tool = _registry.GetTool("semantic_search");

        // Assert
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("semantic_search");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetTool_NonExistentTool_ReturnsNull()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var tool = _registry.GetTool("non_existent_tool");

        // Assert
        tool.Should().BeNull();
    }

    [Fact]
    public async Task SearchTools_ByName_ReturnsMatchingTools()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var results = _registry.SearchTools("search").ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(t => t.Name == "semantic_search");
        results.Should().Contain(t => t.Name == "smart_search");
    }

    [Fact]
    public async Task SearchTools_ByKeyword_ReturnsMatchingTools()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var results = _registry.SearchTools("code").ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(t => t.Keywords.Any(k => k.Contains("code")));
    }

    [Fact]
    public async Task SearchTools_ByDescription_ReturnsMatchingTools()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var results = _registry.SearchTools("generate").ToList();

        // Assert
        results.Should().NotBeEmpty();
        results.Should().Contain(t => t.Description.Contains("generate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchTools_CaseInsensitive_ReturnsResults()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var lowerResults = _registry.SearchTools("search").ToList();
        var upperResults = _registry.SearchTools("SEARCH").ToList();

        // Assert
        lowerResults.Should().BeEquivalentTo(upperResults);
    }

    [Fact]
    public async Task InitializeAsync_MultipleCallsIdempotent()
    {
        // Act
        await _registry.InitializeAsync();
        var firstCount = _registry.GetAllTools().Count();

        await _registry.InitializeAsync();
        var secondCount = _registry.GetAllTools().Count();

        // Assert
        firstCount.Should().Be(secondCount);
    }

    [Fact]
    public async Task GetAllTools_ReturnsToolsOrderedByServiceAndName()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var tools = _registry.GetAllTools().ToList();

        // Assert
        for (int i = 0; i < tools.Count - 1; i++)
        {
            var current = tools[i];
            var next = tools[i + 1];

            if (current.Service == next.Service)
            {
                // Same service, names should be in order
                string.Compare(current.Name, next.Name, StringComparison.Ordinal)
                    .Should().BeLessOrEqualTo(0);
            }
        }
    }

    [Fact]
    public async Task ToolDefinitions_HaveRequiredProperties()
    {
        // Arrange
        await _registry.InitializeAsync();

        // Act
        var tools = _registry.GetAllTools();

        // Assert
        foreach (var tool in tools)
        {
            tool.Name.Should().NotBeNullOrEmpty();
            tool.Service.Should().NotBeNullOrEmpty();
            tool.Description.Should().NotBeNullOrEmpty();
            tool.InputSchema.Should().NotBeNull();
            tool.InputSchema.Should().ContainKey("type");
        }
    }
}

