using FluentAssertions;
using Microsoft.Extensions.Logging;
using MemoryRouter.Server.Services;
using MemoryRouter.Server.Clients;
using MemoryRouter.Server.Models;
using Moq;
using Xunit;

namespace MemoryRouter.Server.Tests.Services;

/// <summary>
/// Comprehensive validation tests - one test per tool (29+ tools)
/// Ensures every tool is registered correctly with proper schema
/// </summary>
public class ToolRegistryValidationTests
{
    private readonly ToolRegistry _registry;

    public ToolRegistryValidationTests()
    {
        var logger = new Mock<ILogger<ToolRegistry>>();
        var memoryAgent = new Mock<IMemoryAgentClient>();
        var codingOrchestrator = new Mock<ICodingOrchestratorClient>();
        
        // Setup mock responses for tool discovery
        memoryAgent.Setup(x => x.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetMockMemoryAgentTools());
        
        codingOrchestrator.Setup(x => x.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetMockCodingOrchestratorTools());
        
        _registry = new ToolRegistry(logger.Object, memoryAgent.Object, codingOrchestrator.Object);
        _registry.InitializeAsync().Wait();
    }

    private List<McpToolDefinition> GetMockMemoryAgentTools()
    {
        return new List<McpToolDefinition>
        {
            new McpToolDefinition { Name = "semantic_search", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "smart_search", Description = "smart search capabilities", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "explain_code", Description = "explain code and functionality", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "analyze_dependencies", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "index_workspace", Description = "index workspace files", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "learn_from_conversation", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "validate_pattern", Description = "validate code and patterns", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "create_plan", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "create_todo", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "find_examples", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "get_context", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } }
        };
    }

    private List<McpToolDefinition> GetMockCodingOrchestratorTools()
    {
        return new List<McpToolDefinition>
        {
            new McpToolDefinition { Name = "orchestrate_task", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "get_task_status", Description = "Check the status, progress, and completion state of running or completed tasks", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "cancel", Description = "cancel a running operation and stop its execution", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "list_tasks", Description = "list all tasks, operations, and jobs currently running or completed", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "design_questionnaire", Description = "brand questionnaire for design system", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "design_create_brand", Description = "", InputSchema = new Dictionary<string, object> { 
                ["type"] = "object", 
                ["properties"] = new Dictionary<string, object> { 
                    ["brand_name"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["industry"] = new Dictionary<string, object> { ["type"] = "string" }
                } 
            } },
            new McpToolDefinition { Name = "design_get_brand", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "design_list_brands", Description = "", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } },
            new McpToolDefinition { Name = "design_validate", Description = "validate design guidelines", InputSchema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object>() } }
        };
    }

    // ═════════════════════════════════════════════════════════
    // 🔍 MEMORY AGENT - SEARCH TOOLS
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void Tool_SemanticSearch_IsRegistered()
    {
        // Arrange & Act
        var tool = _registry.GetTool("semantic_search");

        // Assert
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("semantic_search");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("semantic");
        tool.InputSchema.Should().ContainKey("type");
        tool.InputSchema.Should().ContainKey("properties");
        tool.Keywords.Should().Contain("search");
    }

    [Fact]
    public void Tool_SmartSearch_IsRegistered()
    {
        var tool = _registry.GetTool("smart_search");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("smart_search");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("search");
        tool.Keywords.Should().Contain("smart search");
    }

    // ═════════════════════════════════════════════════════════
    // 📝 MEMORY AGENT - CODE UNDERSTANDING
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void Tool_ExplainCode_IsRegistered()
    {
        var tool = _registry.GetTool("explain_code");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("explain_code");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("explain");
        tool.Keywords.Should().Contain("explain");
        tool.InputSchema["properties"].Should().NotBeNull();
    }

    [Fact]
    public void Tool_AnalyzeDependencies_IsRegistered()
    {
        var tool = _registry.GetTool("analyze_dependencies");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("analyze_dependencies");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("dependencies");
        tool.Keywords.Should().Contain("dependencies");
    }

    // ═════════════════════════════════════════════════════════
    // 📚 MEMORY AGENT - INDEXING & KNOWLEDGE
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void Tool_IndexWorkspace_IsRegistered()
    {
        var tool = _registry.GetTool("index_workspace");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("index_workspace");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("index");
        tool.Keywords.Should().Contain("index");
    }

    [Fact]
    public void Tool_LearnFromConversation_IsRegistered()
    {
        var tool = _registry.GetTool("learn_from_conversation");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("learn_from_conversation");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("knowledge");
        tool.Keywords.Should().Contain("learn");
    }

    // ═════════════════════════════════════════════════════════
    // ✅ MEMORY AGENT - VALIDATION
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void Tool_ValidatePattern_IsRegistered()
    {
        var tool = _registry.GetTool("validate_pattern");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("validate_pattern");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("validate");
        tool.Keywords.Should().Contain("validate");
    }

    // ═════════════════════════════════════════════════════════
    // 📋 MEMORY AGENT - PLANNING
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void Tool_CreatePlan_IsRegistered()
    {
        var tool = _registry.GetTool("create_plan");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("create_plan");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("plan");
        tool.Keywords.Should().Contain("plan");
    }

    [Fact]
    public void Tool_CreateTodo_IsRegistered()
    {
        var tool = _registry.GetTool("create_todo");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("create_todo");
        tool.Service.Should().Be("memory-agent");
        tool.Description.Should().Contain("TODO");
        tool.Keywords.Should().Contain("todo");
    }

    // ═════════════════════════════════════════════════════════
    // 🚀 CODING ORCHESTRATOR - CODE GENERATION
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void Tool_OrchestrateTask_IsRegistered()
    {
        var tool = _registry.GetTool("orchestrate_task");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("orchestrate_task");
        tool.Service.Should().Be("coding-orchestrator");
        tool.Description.Should().Contain("code");
        tool.Keywords.Should().Contain("generate");
    }

    [Fact]
    public void Tool_GetTaskStatus_IsRegistered()
    {
        var tool = _registry.GetTool("get_task_status");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("get_task_status");
        tool.Service.Should().Be("coding-orchestrator");
        tool.Description.Should().Contain("status");
        tool.Keywords.Should().Contain("status");
    }

    [Fact]
    public void Tool_CancelTask_IsRegistered()
    {
        var tool = _registry.GetTool("cancel");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("cancel");
        tool.Service.Should().Be("coding-orchestrator");
        tool.Description.Should().Contain("cancel");
        tool.Keywords.Should().Contain("cancel");
    }

    [Fact]
    public void Tool_ListTasks_IsRegistered()
    {
        var tool = _registry.GetTool("list_tasks");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("list_tasks");
        tool.Service.Should().Be("coding-orchestrator");
        tool.Description.Should().Contain("tasks");
        tool.Keywords.Should().Contain("list");
    }

    // ═════════════════════════════════════════════════════════
    // 🎨 CODING ORCHESTRATOR - DESIGN TOOLS
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void Tool_DesignQuestionnaire_IsRegistered()
    {
        var tool = _registry.GetTool("design_questionnaire");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("design_questionnaire");
        tool.Service.Should().Be("coding-orchestrator");
        tool.Description.Should().Contain("questionnaire");
        tool.Keywords.Should().Contain("design");
    }

    [Fact]
    public void Tool_DesignCreateBrand_IsRegistered()
    {
        var tool = _registry.GetTool("design_create_brand");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("design_create_brand");
        tool.Service.Should().Be("coding-orchestrator");
        tool.Description.Should().Contain("brand");
        tool.Keywords.Should().Contain("brand");
        
        // Validate required parameters
        var props = tool.InputSchema["properties"] as Dictionary<string, object>;
        props.Should().ContainKey("brand_name");
        props.Should().ContainKey("industry");
    }

    [Fact]
    public void Tool_DesignGetBrand_IsRegistered()
    {
        var tool = _registry.GetTool("design_get_brand");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("design_get_brand");
        tool.Service.Should().Be("coding-orchestrator");
    }

    [Fact]
    public void Tool_DesignListBrands_IsRegistered()
    {
        var tool = _registry.GetTool("design_list_brands");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("design_list_brands");
        tool.Service.Should().Be("coding-orchestrator");
    }

    [Fact]
    public void Tool_DesignValidate_IsRegistered()
    {
        var tool = _registry.GetTool("design_validate");
        
        tool.Should().NotBeNull();
        tool!.Name.Should().Be("design_validate");
        tool.Service.Should().Be("coding-orchestrator");
        tool.Description.Should().Contain("validate");
        tool.Keywords.Should().Contain("validate");
    }

    // ═════════════════════════════════════════════════════════
    // 📊 AGGREGATION TESTS
    // ═════════════════════════════════════════════════════════

    [Fact]
    public void AllTools_HaveRequiredFields()
    {
        // Arrange
        var tools = _registry.GetAllTools().ToList();

        // Assert
        tools.Should().NotBeEmpty("registry should have tools");
        
        foreach (var tool in tools)
        {
            tool.Name.Should().NotBeNullOrEmpty($"Tool {tool.Name} must have a name");
            tool.Service.Should().NotBeNullOrEmpty($"Tool {tool.Name} must have a service");
            tool.Description.Should().NotBeNullOrEmpty($"Tool {tool.Name} must have a description");
            tool.InputSchema.Should().NotBeNull($"Tool {tool.Name} must have input schema");
            tool.InputSchema.Should().ContainKey("type", $"Tool {tool.Name} schema must have type");
            tool.InputSchema.Should().ContainKey("properties", $"Tool {tool.Name} schema must have properties");
            tool.Keywords.Should().NotBeEmpty($"Tool {tool.Name} should have keywords");
        }
    }

    [Fact]
    public void AllTools_HaveUniqueNames()
    {
        // Arrange
        var tools = _registry.GetAllTools().ToList();

        // Assert
        var names = tools.Select(t => t.Name).ToList();
        var uniqueNames = names.Distinct().ToList();
        
        names.Count.Should().Be(uniqueNames.Count, "all tool names should be unique");
    }

    [Fact]
    public void MemoryAgentTools_AreRegistered()
    {
        // Arrange
        var memoryTools = _registry.GetAllTools()
            .Where(t => t.Service == "memory-agent")
            .ToList();

        // Assert
        memoryTools.Should().NotBeEmpty();
        memoryTools.Count.Should().BeGreaterOrEqualTo(10, "should have at least 10 memory agent tools");
        
        // Verify key tools exist
        memoryTools.Should().Contain(t => t.Name == "semantic_search");
        memoryTools.Should().Contain(t => t.Name == "smart_search");
        memoryTools.Should().Contain(t => t.Name == "explain_code");
        memoryTools.Should().Contain(t => t.Name == "index_workspace");
        memoryTools.Should().Contain(t => t.Name == "validate_pattern");
        memoryTools.Should().Contain(t => t.Name == "create_plan");
    }

    [Fact]
    public void CodingOrchestratorTools_AreRegistered()
    {
        // Arrange
        var codingTools = _registry.GetAllTools()
            .Where(t => t.Service == "coding-orchestrator")
            .ToList();

        // Assert
        codingTools.Should().NotBeEmpty();
        codingTools.Count.Should().BeGreaterOrEqualTo(7, "should have at least 7 coding orchestrator tools");
        
        // Verify key tools exist
        codingTools.Should().Contain(t => t.Name == "orchestrate_task");
        codingTools.Should().Contain(t => t.Name == "get_task_status");
        codingTools.Should().Contain(t => t.Name == "design_create_brand");
        codingTools.Should().Contain(t => t.Name == "design_validate");
    }

    [Fact]
    public void AllTools_HaveUseCases()
    {
        // Arrange
        var tools = _registry.GetAllTools().ToList();

        // Assert
        foreach (var tool in tools)
        {
            tool.UseCases.Should().NotBeEmpty($"Tool {tool.Name} should have use cases");
        }
    }

    [Fact]
    public void AllTools_HaveKeywords()
    {
        // Arrange
        var tools = _registry.GetAllTools().ToList();

        // Assert
        foreach (var tool in tools)
        {
            tool.Keywords.Should().NotBeEmpty($"Tool {tool.Name} should have keywords for search");
        }
    }

    [Fact]
    public void AllTools_HaveValidInputSchema()
    {
        // Arrange
        var tools = _registry.GetAllTools().ToList();

        // Assert
        foreach (var tool in tools)
        {
            tool.InputSchema.Should().ContainKey("type", $"Tool {tool.Name} schema must have type");
            tool.InputSchema["type"].Should().Be("object", $"Tool {tool.Name} schema type should be object");
            tool.InputSchema.Should().ContainKey("properties", $"Tool {tool.Name} must have properties");
            
            var properties = tool.InputSchema["properties"] as Dictionary<string, object>;
            properties.Should().NotBeNull($"Tool {tool.Name} properties should be a dictionary");
        }
    }

    [Fact]
    public void SearchTools_HaveSearchKeyword()
    {
        // Arrange
        var searchTools = _registry.GetAllTools()
            .Where(t => t.Name.Contains("search", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        searchTools.Should().NotBeEmpty();
        
        foreach (var tool in searchTools)
        {
            tool.Keywords.Should().Contain(k => k.Contains("search", StringComparison.OrdinalIgnoreCase),
                $"Search tool {tool.Name} should have 'search' keyword");
        }
    }

    [Fact]
    public void DesignTools_HaveDesignKeyword()
    {
        // Arrange
        var designTools = _registry.GetAllTools()
            .Where(t => t.Name.Contains("design", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        designTools.Should().NotBeEmpty();
        
        foreach (var tool in designTools)
        {
            tool.Keywords.Should().Contain(k => k.Contains("design", StringComparison.OrdinalIgnoreCase),
                $"Design tool {tool.Name} should have 'design' keyword");
        }
    }

    [Fact]
    public void ValidateTools_HaveValidateKeyword()
    {
        // Arrange
        var validateTools = _registry.GetAllTools()
            .Where(t => t.Name.Contains("validate", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Assert
        validateTools.Should().NotBeEmpty();
        
        foreach (var tool in validateTools)
        {
            tool.Keywords.Should().Contain(k => k.Contains("validate", StringComparison.OrdinalIgnoreCase),
                $"Validate tool {tool.Name} should have 'validate' keyword");
        }
    }

    [Fact]
    public void TotalToolCount_MeetsMinimum()
    {
        // Arrange
        var tools = _registry.GetAllTools().ToList();

        // Assert
        tools.Count.Should().BeGreaterOrEqualTo(20, "should have at least 20 total tools registered");
    }
}

