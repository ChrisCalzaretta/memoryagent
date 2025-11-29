using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Microsoft Agent Framework Patterns
/// </summary>
public partial class AgentFrameworkPatternDetector
{
    #region Microsoft Agent Framework Patterns

    private List<CodePattern> DetectAgentFrameworkAgents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for Agent Framework agent creation patterns
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect: new ChatCompletionAgent(...)
            if (invocationText.Contains("ChatCompletionAgent") && invocationText.Contains("new"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_ChatCompletionAgent",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AIAgents,
                    implementation: "Microsoft.Agents.AI ChatCompletionAgent",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using Agent Framework's ChatCompletionAgent for AI-powered conversation",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "AI Agent Creation"
                    }
                ));
            }

            // Detect: AgentBuilder usage
            if (invocationText.Contains("AgentBuilder") || invocationText.Contains("CreateAgent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_AgentBuilder",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AIAgents,
                    implementation: "AgentBuilder pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using builder pattern for configuring AI agents with tools and context",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Agent Configuration"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentFrameworkWorkflows(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect workflow classes
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("Workflow")) == true ||
                classDecl.Identifier.Text.Contains("Workflow"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_Workflow_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Agent Framework Workflow",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Using Agent Framework workflows for complex multi-step AI processes with checkpointing and state management",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Workflow Orchestration",
                        ["workflow_name"] = classDecl.Identifier.Text
                    }
                ));
            }

            // Detect executor pattern
            var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                if (method.Identifier.Text.Contains("Execute") && 
                    method.ReturnType.ToString().Contains("Task"))
                {
                    var methodLineNumber = GetLineNumber(root, method, sourceCode);
                    patterns.Add(CreatePattern(
                        name: $"AgentFramework_Executor_{method.Identifier.Text}",
                        type: PatternType.AgentFramework,
                        category: PatternCategory.MultiAgentOrchestration,
                        implementation: "Workflow Executor",
                        filePath: filePath,
                        lineNumber: methodLineNumber,
                        content: GetContextAroundNode(method, sourceCode, 7),
                        bestPractice: "Workflow executors handle individual workflow steps with type-safe message passing",
                        azureUrl: AgentFrameworkUrl,
                        context: context,
                        confidence: 0.85f,
                        metadata: new Dictionary<string, object>
                        {
                            ["framework"] = "Microsoft Agent Framework",
                            ["pattern_category"] = "Workflow Execution"
                        }
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentThreadUsage(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect AgentThread usage for state management
            if (invocationText.Contains("AgentThread") || invocationText.Contains("CreateThread"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_AgentThread",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.StateManagement,
                    implementation: "AgentThread for conversation state",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using AgentThread for thread-based state management in multi-turn conversations",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "State Management",
                        ["best_practice"] = "Enterprise-grade conversation state tracking"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMcpServerIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect MCP server/client integration
            if (invocationText.Contains("McpServer") || invocationText.Contains("McpClient") ||
                invocationText.Contains("tools/call") || invocationText.Contains("tools/list"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_McpIntegration",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.ToolIntegration,
                    implementation: "MCP Server/Client Integration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Integrating Model Context Protocol (MCP) servers for tool calling and external system integration",
                    azureUrl: McpServerUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["protocol"] = "Model Context Protocol (MCP)",
                        ["pattern_category"] = "Tool Integration"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentMiddleware(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect middleware classes
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("Middleware") || 
                                                   t.ToString().Contains("IAgentFilter")) == true)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_Middleware_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.Interceptors,
                    implementation: "Agent Middleware/Filter",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Using middleware to intercept agent actions for logging, validation, safety checks, or telemetry",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Agent Middleware",
                        ["use_case"] = "Safety, logging, telemetry, validation"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCheckpointing(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect checkpoint operations
            if (invocationText.Contains("SaveCheckpoint") || invocationText.Contains("LoadCheckpoint") ||
                invocationText.Contains("Checkpoint"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_Checkpointing",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.StateManagement,
                    implementation: "Workflow Checkpointing",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using checkpointing for long-running workflows to enable recovery and resumption",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Workflow Persistence",
                        ["use_case"] = "Long-running processes, fault tolerance"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectContextProviders(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IContextProvider") || 
                                                   t.ToString().Contains("ContextProvider")) == true)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_ContextProvider_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.StateManagement,
                    implementation: "Context Provider Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Using context providers to inject dynamic information into agent prompts (current time, user data, environment state)",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Dynamic Context Injection"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectToolRegistration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("AddTool") || invocationText.Contains("RegisterTool") || 
                invocationText.Contains("WithTool"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_ToolRegistration",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Tool Registration Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Registering tools with agents enables them to call external functions, APIs, and MCP servers during task execution",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Tool Management"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentComposition(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            var fields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>();
            var agentFieldCount = fields.Count(f => f.Declaration.Type.ToString().Contains("Agent"));

            if (agentFieldCount >= 2)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_Composition_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Agent Composition Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: $"Composing {agentFieldCount} agents within a single orchestrator for complex multi-step workflows",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Agent Composition",
                        ["agent_count"] = agentFieldCount
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectStreamingResponses(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var returnType = method.ReturnType.ToString();

            if ((returnType.Contains("IAsyncEnumerable") || returnType.Contains("Stream")) && 
                method.Body?.ToString().Contains("Agent") == true)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_Streaming_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AIAgents,
                    implementation: "Streaming Response Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Implementing streaming responses for real-time agent output delivery, improving perceived latency and user experience",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Streaming",
                        ["benefit"] = "Low latency, real-time updates"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentErrorHandling(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var tryStatements = root.DescendantNodes().OfType<TryStatementSyntax>();

        foreach (var tryStatement in tryStatements)
        {
            var tryBlock = tryStatement.Block.ToString();

            if (tryBlock.Contains("Agent") && tryStatement.Catches.Any())
            {
                var lineNumber = GetLineNumber(root, tryStatement, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_ErrorHandling",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.Reliability,
                    implementation: "Agent Error Handling Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(tryStatement, sourceCode, 5),
                    bestPractice: "Implementing robust error handling for agent calls to gracefully handle model failures, timeout, rate limits, and unexpected responses",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Resilience",
                        ["catch_count"] = tryStatement.Catches.Count
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentTelemetry(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if ((invocationText.Contains("Log") || invocationText.Contains("Telemetry") || 
                 invocationText.Contains("TrackEvent")) && invocationText.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentFramework_Telemetry",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.Operational,
                    implementation: "Agent Telemetry Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Implementing telemetry for agent operations to monitor performance, track usage, detect anomalies, and optimize costs",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Observability"
                    }
                ));
                break; // Only report once per file
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRequestResponsePatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var parameters = method.ParameterList.Parameters;
            var hasRequestParam = parameters.Any(p => p.Type?.ToString().Contains("Request") == true);
            var returnsResponse = method.ReturnType.ToString().Contains("Response");

            if (hasRequestParam && returnsResponse && method.Body?.ToString().Contains("Agent") == true)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_RequestResponse_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AIAgents,
                    implementation: "Request-Response Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Using request-response pattern for agent interactions provides type-safe, structured communication between agents and callers",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Structured Communication"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentLifecycle(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if ((method.Identifier.Text.Contains("OnStart") || method.Identifier.Text.Contains("OnStop") ||
                 method.Identifier.Text.Contains("OnPause") || method.Identifier.Text.Contains("OnResume") ||
                 method.Identifier.Text.Contains("Initialize") || method.Identifier.Text.Contains("Dispose")) &&
                method.Parent is ClassDeclarationSyntax classDecl && classDecl.Identifier.Text.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_Lifecycle_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AIAgents,
                    implementation: "Agent Lifecycle Management",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Implementing lifecycle methods for proper agent initialization, resource cleanup, and state management",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Lifecycle Management",
                        ["method"] = method.Identifier.Text
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCustomAgents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("AgentBase") || 
                                                   t.ToString().Contains("IAgent")) == true)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_CustomAgent_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AIAgents,
                    implementation: "Custom Agent Implementation",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Creating custom agent implementation by inheriting from AgentBase or implementing IAgent for specialized agent behaviors",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Custom Agent Development",
                        ["agent_name"] = classDecl.Identifier.Text
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentDecorators(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            var fields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>();
            var hasWrappedAgent = fields.Any(f => f.Declaration.Type.ToString().Contains("Agent") && 
                                                 f.Declaration.Variables.Any(v => v.Identifier.Text.Contains("inner") || 
                                                                                 v.Identifier.Text.Contains("wrapped")));

            if (hasWrappedAgent && classDecl.BaseList?.Types.Any(t => t.ToString().Contains("Agent")) == true)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentFramework_Decorator_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AIAgents,
                    implementation: "Agent Decorator Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Using decorator pattern to wrap existing agents and add cross-cutting concerns (logging, caching, rate limiting, safety checks)",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft Agent Framework",
                        ["pattern_category"] = "Agent Composition",
                        ["design_pattern"] = "Decorator"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
