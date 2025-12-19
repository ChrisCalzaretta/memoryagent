using FluentAssertions;
using Xunit;

namespace MemoryRouter.Server.Tests.Integration;

/// <summary>
/// Integration tests for individual tool calls
/// Tests direct tool execution through the router
/// </summary>
[Trait("Category", "Integration")]
public class ToolCallIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task SemanticSearch_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var request = "Find authentication code";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Steps.Should().NotBeEmpty();
        result.Steps[0].Success.Should().BeTrue();
        result.Steps[0].Result.Should().NotBeNull();
    }

    [Fact]
    public async Task SmartSearch_WithQuery_ReturnsOptimizedResults()
    {
        // Arrange
        var request = "Show me all user management code";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Steps.Should().NotBeEmpty();
        
        var searchStep = result.Steps.FirstOrDefault(s => 
            s.ToolName.Contains("search", StringComparison.OrdinalIgnoreCase));
        searchStep.Should().NotBeNull();
        searchStep!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task OrchestrateTask_WithSimpleTask_GeneratesCode()
    {
        // Arrange
        var request = "Create a simple calculator function";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Steps.Should().NotBeEmpty();
        
        var generateStep = result.Steps.FirstOrDefault(s => 
            s.ToolName.Contains("orchestrate", StringComparison.OrdinalIgnoreCase));
        generateStep.Should().NotBeNull();
        generateStep!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetTaskStatus_ChecksStatus()
    {
        // Arrange
        var request = "What's the status of my running tasks?";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePlan_GeneratesPlan()
    {
        // Arrange
        var request = "Create an execution plan for a user registration system";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Steps.Should().NotBeEmpty();
        
        var planStep = result.Steps.FirstOrDefault(s => 
            s.ToolName.Contains("plan", StringComparison.OrdinalIgnoreCase));
        planStep.Should().NotBeNull();
    }

    [Fact]
    public async Task ToolRegistry_HasAllRequiredTools()
    {
        // Act
        var tools = ToolRegistry.GetAllTools().ToList();

        // Assert
        tools.Should().NotBeEmpty();
        tools.Should().Contain(t => t.Name.Contains("search", StringComparison.OrdinalIgnoreCase));
        tools.Should().Contain(t => t.Name.Contains("orchestrate", StringComparison.OrdinalIgnoreCase));
        tools.Count.Should().BeGreaterOrEqualTo(10, "should have at least 10 tools registered");
    }

    [Fact]
    public async Task MemoryAgent_IsAccessible()
    {
        // Act
        var tools = await MemoryAgentClient.GetToolsAsync();

        // Assert
        tools.Should().NotBeNull();
        tools.Should().NotBeEmpty("MemoryAgent should expose tools");
    }

    [Fact]
    public async Task CodingOrchestrator_IsAccessible()
    {
        // Act
        var tools = await CodingOrchestratorClient.GetToolsAsync();

        // Assert
        tools.Should().NotBeNull();
        tools.Should().NotBeEmpty("CodingOrchestrator should expose tools");
    }
}
