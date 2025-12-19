using FluentAssertions;
using Xunit;

namespace MemoryRouter.Server.Tests.Integration;

/// <summary>
/// End-to-end integration tests with real services
/// Requires: MemoryAgent, CodingOrchestrator, Ollama with FunctionGemma
/// </summary>
[Trait("Category", "Integration")]
public class EndToEndTests : IntegrationTestBase
{
    [Fact]
    public async Task Execute_SearchTask_ReturnsResults()
    {
        // Arrange
        var request = "Find all authentication code in the project";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("workflow should complete successfully");
        result.Steps.Should().NotBeEmpty("should have executed at least one step");
        result.FinalResult.Should().NotBeNullOrEmpty("should have a final result");
        
        // Should have used a search tool
        result.Steps.Should().Contain(s => 
            s.ToolName.Contains("search", StringComparison.OrdinalIgnoreCase),
            "should have used a search tool for finding code");
    }

    [Fact]
    public async Task Execute_CodeGenerationTask_CreatesCode()
    {
        // Arrange
        var request = "Create a simple Hello World function in C#";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("workflow should complete successfully");
        result.Steps.Should().NotBeEmpty("should have executed steps");
        
        // Should have used code generation
        result.Steps.Should().Contain(s => 
            s.ToolName.Contains("orchestrate", StringComparison.OrdinalIgnoreCase) ||
            s.ToolName.Contains("task", StringComparison.OrdinalIgnoreCase),
            "should have used code generation tool");
    }

    [Fact]
    public async Task Execute_StatusCheck_ReturnsStatus()
    {
        // Arrange
        var request = "Check the status of my tasks";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("workflow should complete successfully");
        result.Steps.Should().NotBeEmpty("should have executed steps");
        
        // Should have used status check
        result.Steps.Should().Contain(s => 
            s.ToolName.Contains("status", StringComparison.OrdinalIgnoreCase),
            "should have used status checking tool");
    }

    [Fact]
    public async Task Execute_PlanCreation_CreatesPlan()
    {
        // Arrange
        var request = "Create a plan for building a REST API";

        // Act
        var result = await RouterService.ExecuteRequestAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("workflow should complete successfully");
        result.Steps.Should().NotBeEmpty("should have executed steps");
        
        // Should have created a plan
        result.Steps.Should().Contain(s => 
            s.ToolName.Contains("plan", StringComparison.OrdinalIgnoreCase),
            "should have used planning tool");
    }
}
