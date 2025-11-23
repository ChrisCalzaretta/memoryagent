using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects patterns specific to Microsoft Agent Framework, Semantic Kernel, and AutoGen
/// Based on: https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview
/// </summary>
public class AgentFrameworkPatternDetector
{
    private readonly ILogger<AgentFrameworkPatternDetector>? _logger;

    // Azure URLs for AI Agent Framework best practices
    private const string AgentFrameworkUrl = "https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview";
    private const string SemanticKernelUrl = "https://learn.microsoft.com/en-us/semantic-kernel/overview/";
    private const string AutoGenUrl = "https://microsoft.github.io/autogen/";
    private const string McpServerUrl = "https://modelcontextprotocol.io/introduction";
    private const string AgentLightningUrl = "https://www.microsoft.com/en-us/research/project/agent-lightning/";
    private const string AgentLightningGitHub = "https://github.com/microsoft/agent-lightning";

    public AgentFrameworkPatternDetector(ILogger<AgentFrameworkPatternDetector>? logger = null)
    {
        _logger = logger;
    }

    public async Task<List<CodePattern>> DetectPatternsAsync(
        string filePath,
        string? context,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = tree.GetRoot(cancellationToken);

            // Microsoft Agent Framework Patterns (Core)
            patterns.AddRange(DetectAgentFrameworkAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentFrameworkWorkflows(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentThreadUsage(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMcpServerIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentMiddleware(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCheckpointing(root, filePath, context, sourceCode));
            
            // Microsoft Agent Framework Patterns (Advanced)
            patterns.AddRange(DetectContextProviders(root, filePath, context, sourceCode));
            patterns.AddRange(DetectToolRegistration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentComposition(root, filePath, context, sourceCode));
            patterns.AddRange(DetectStreamingResponses(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentErrorHandling(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentTelemetry(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRequestResponsePatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentLifecycle(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCustomAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentDecorators(root, filePath, context, sourceCode));

            // Semantic Kernel Patterns (Core)
            patterns.AddRange(DetectSemanticKernelPlugins(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticKernelPlanners(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticKernelMemory(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticKernelFilters(root, filePath, context, sourceCode));
            
            // Semantic Kernel Patterns (Advanced)
            patterns.AddRange(DetectPromptTemplates(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticFunctions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectNativeFunctions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMemoryConnectors(root, filePath, context, sourceCode));
            patterns.AddRange(DetectEmbeddingGeneration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectChatHistoryManagement(root, filePath, context, sourceCode));

            // AutoGen Patterns (Core)
            patterns.AddRange(DetectAutoGenConversableAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAutoGenGroupChat(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAutoGenUserProxy(root, filePath, context, sourceCode));
            
            // AutoGen Patterns (Advanced)
            patterns.AddRange(DetectReplyFunctions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTerminationConditions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSpeakerSelection(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCodeExecution(root, filePath, context, sourceCode));

            // Multi-Agent Orchestration Patterns (Core)
            patterns.AddRange(DetectSequentialOrchestration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConcurrentOrchestration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHandoffPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMagenticPattern(root, filePath, context, sourceCode));
            
            // Multi-Agent Orchestration Patterns (Advanced)
            patterns.AddRange(DetectSupervisorPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHierarchicalAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSwarmIntelligence(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConsensusPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectDebatePattern(root, filePath, context, sourceCode));

            // Agent Lightning Patterns (Core RL-based Optimization)
            patterns.AddRange(DetectAgentLightningServer(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentLightningClient(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRLTrainingPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRewardSignals(root, filePath, context, sourceCode));
            patterns.AddRange(DetectErrorMonitoring(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTraceCollection(root, filePath, context, sourceCode));
            
            // Agent Lightning Patterns (Advanced RL Techniques)
            patterns.AddRange(DetectCurriculumLearning(root, filePath, context, sourceCode));
            patterns.AddRange(DetectOffPolicyRL(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHierarchicalRL(root, filePath, context, sourceCode));
            patterns.AddRange(DetectOnlineSFT(root, filePath, context, sourceCode));
            patterns.AddRange(DetectUserFeedbackIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectToolSuccessSignals(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLongHorizonCredit(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLLamaFactoryIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectDSPyIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMultiTaskLearning(root, filePath, context, sourceCode));

            // Best Practice Anti-Patterns
            patterns.AddRange(DetectAntiPatterns(root, filePath, context, sourceCode));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting Agent Framework patterns in {FilePath}", filePath);
        }

        return patterns;
    }

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

    #region Semantic Kernel Patterns

    private List<CodePattern> DetectSemanticKernelPlugins(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect SK plugin classes with [KernelFunction] attributes
            var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var hasKernelFunction = method.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(attr => attr.Name.ToString().Contains("KernelFunction"));

                if (hasKernelFunction)
                {
                    var lineNumber = GetLineNumber(root, method, sourceCode);
                    patterns.Add(CreatePattern(
                        name: $"SemanticKernel_Plugin_{classDecl.Identifier.Text}_{method.Identifier.Text}",
                        type: PatternType.SemanticKernel,
                        category: PatternCategory.ToolIntegration,
                        implementation: "Semantic Kernel Plugin with [KernelFunction]",
                        filePath: filePath,
                        lineNumber: lineNumber,
                        content: GetContextAroundNode(method, sourceCode, 7),
                        bestPractice: "Using [KernelFunction] attribute to create AI-callable functions (tools) in Semantic Kernel",
                        azureUrl: SemanticKernelUrl,
                        context: context,
                        confidence: 0.95f,
                        metadata: new Dictionary<string, object>
                        {
                            ["framework"] = "Semantic Kernel",
                            ["pattern_category"] = "Plugin Development",
                            ["plugin_name"] = classDecl.Identifier.Text,
                            ["function_name"] = method.Identifier.Text
                        }
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticKernelPlanners(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect planner usage (NOTE: Planners are legacy in SK, migrated to Agent Framework workflows)
            if (invocationText.Contains("Planner") || invocationText.Contains("CreatePlan"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_Planner_Legacy",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Semantic Kernel Planner (Legacy)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: Semantic Kernel Planners are legacy. Migrate to Agent Framework Workflows for better control and reliability.",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-semantic-kernel",
                    context: context,
                    confidence: 0.85f,
                    isPositivePattern: false, // Anti-pattern: should migrate
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Legacy Pattern",
                        ["recommendation"] = "Migrate to Agent Framework Workflows"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticKernelMemory(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect SK memory usage
            if (invocationText.Contains("ISemanticTextMemory") || invocationText.Contains("MemoryStore"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_Memory",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.StateManagement,
                    implementation: "Semantic Kernel Memory Store",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using Semantic Kernel memory for storing and retrieving embeddings and semantic information",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Memory Management"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticKernelFilters(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect filter implementations
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IFunctionFilter") ||
                                                   t.ToString().Contains("IPromptFilter")) == true)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_Filter_{classDecl.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.Interceptors,
                    implementation: "Semantic Kernel Filter",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Using SK filters for telemetry, logging, safety checks, and function interception",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Filters/Middleware"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPromptTemplates(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var variables = root.DescendantNodes().OfType<VariableDeclaratorSyntax>();

        foreach (var variable in variables)
        {
            var initializer = variable.Initializer?.Value.ToString() ?? "";

            if ((initializer.Contains("{{") && initializer.Contains("}}")) || 
                variable.Identifier.Text.Contains("PromptTemplate", StringComparison.OrdinalIgnoreCase))
            {
                var lineNumber = GetLineNumber(root, variable, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_PromptTemplate_{variable.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.AIAgents,
                    implementation: "Semantic Kernel Prompt Template",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(variable, sourceCode, 5),
                    bestPractice: "Using prompt templates with {{ }} placeholders for dynamic prompt generation with variable substitution",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Prompt Engineering"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticFunctions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("CreateSemanticFunction") || invocationText.Contains("RegisterSemanticFunction"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_SemanticFunction",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.AIAgents,
                    implementation: "Semantic Function (Prompt-based)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Creating semantic functions (AI-powered functions defined by prompts) for natural language tasks",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "AI Functions"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectNativeFunctions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var hasSKFunction = method.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString().Contains("SKFunction"));

            if (hasSKFunction)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_NativeFunction_{method.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Native Function (Code-based)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Creating native functions (C# functions callable by AI) using [SKFunction] attribute for deterministic operations",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Native Functions",
                        ["function_name"] = method.Identifier.Text
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMemoryConnectors(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("AzureAISearchMemoryStore") || 
                invocationText.Contains("QdrantMemoryStore") ||
                invocationText.Contains("PostgresMemoryStore") ||
                invocationText.Contains("RedisMemoryStore") ||
                invocationText.Contains("MemoryStore"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                var connector = invocationText.Contains("AzureAISearch") ? "Azure AI Search" :
                               invocationText.Contains("Qdrant") ? "Qdrant" :
                               invocationText.Contains("Postgres") ? "PostgreSQL" :
                               invocationText.Contains("Redis") ? "Redis" : "Generic";

                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_MemoryConnector_{connector}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.StateManagement,
                    implementation: $"Semantic Kernel Memory - {connector}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: $"Using {connector} memory connector for persistent storage and retrieval of embeddings and semantic information",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Memory Connectors",
                        ["connector"] = connector
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectEmbeddingGeneration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("GenerateEmbeddingAsync") || 
                invocationText.Contains("GetEmbeddingsAsync") ||
                invocationText.Contains("ITextEmbeddingGeneration"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_EmbeddingGeneration",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.AIAgents,
                    implementation: "Embedding Generation",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Generating text embeddings for semantic search, similarity matching, and memory storage",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Embeddings"
                    }
                ));
                break; // Only report once per file
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectChatHistoryManagement(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var variables = root.DescendantNodes().OfType<VariableDeclaratorSyntax>();

        foreach (var variable in variables)
        {
            var type = variable.Parent?.Parent as VariableDeclarationSyntax;

            if (type?.Type.ToString().Contains("ChatHistory") == true)
            {
                var lineNumber = GetLineNumber(root, variable, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_ChatHistory_{variable.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.StateManagement,
                    implementation: "Chat History Management",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(variable, sourceCode, 5),
                    bestPractice: "Managing chat history for multi-turn conversations with context preservation and message history tracking",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Conversation Management"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AutoGen Patterns

    private List<CodePattern> DetectAutoGenConversableAgents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect AutoGen ConversableAgent
            if (invocationText.Contains("ConversableAgent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_ConversableAgent",
                    type: PatternType.AutoGen,
                    category: PatternCategory.AIAgents,
                    implementation: "AutoGen ConversableAgent",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen is superseded by Agent Framework. Consider migrating for better enterprise features.",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false, // Legacy pattern
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Agent Pattern",
                        ["recommendation"] = "Migrate to Agent Framework ChatCompletionAgent"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAutoGenGroupChat(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect AutoGen GroupChat
            if (invocationText.Contains("GroupChat") && !invocationText.Contains("AgentFramework"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_GroupChat",
                    type: PatternType.AutoGen,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "AutoGen GroupChat",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen GroupChat → Agent Framework Workflow with multi-agent orchestration patterns",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Multi-Agent Pattern",
                        ["recommendation"] = "Migrate to Agent Framework Workflows"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAutoGenUserProxy(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect AutoGen UserProxyAgent
            if (invocationText.Contains("UserProxyAgent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_UserProxyAgent",
                    type: PatternType.AutoGen,
                    category: PatternCategory.HumanInLoop,
                    implementation: "AutoGen UserProxyAgent",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: UserProxyAgent → Agent Framework request/response patterns for human-in-the-loop",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Human-in-Loop Pattern"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectReplyFunctions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("ReplyFunction") || method.Identifier.Text.Contains("GenerateReply"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AutoGen_ReplyFunction_{method.Identifier.Text}",
                    type: PatternType.AutoGen,
                    category: PatternCategory.AIAgents,
                    implementation: "AutoGen Reply Function",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "MIGRATION RECOMMENDED: Custom reply functions in AutoGen → Agent Framework agent methods with type-safe responses",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.85f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Pattern"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectTerminationConditions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("is_termination_msg") || methodBody.Contains("TerminationCondition"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AutoGen_TerminationCondition_{method.Identifier.Text}",
                    type: PatternType.AutoGen,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "AutoGen Termination Condition",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen termination conditions → Agent Framework workflow completion logic",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.85f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Orchestration"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSpeakerSelection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("SpeakerSelection") || method.Identifier.Text.Contains("SelectSpeaker"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AutoGen_SpeakerSelection_{method.Identifier.Text}",
                    type: PatternType.AutoGen,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "AutoGen Speaker Selection Strategy",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen speaker selection → Agent Framework workflow routing logic with type-safe agent selection",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Multi-Agent Pattern"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCodeExecution(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("execute_code") || invocationText.Contains("CodeExecution"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_CodeExecution",
                    type: PatternType.AutoGen,
                    category: PatternCategory.AIAgents,
                    implementation: "AutoGen Code Execution",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen code execution → Agent Framework with sandboxed execution tools and MCP servers for safe code execution",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Code Execution",
                        ["security_concern"] = "Ensure sandboxed execution"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Multi-Agent Orchestration Patterns

    private List<CodePattern> DetectSequentialOrchestration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for sequential agent invocations
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            
            // Detect sequential await pattern for multiple agents
            var awaitCount = Regex.Matches(methodBody, @"await.*Agent.*\.InvokeAsync").Count;
            
            if (awaitCount >= 2)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Sequential_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Sequential Agent Orchestration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Sequential multi-agent pattern: agents process tasks one after another in a defined order",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Sequential",
                        ["agent_count"] = awaitCount
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectConcurrentOrchestration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect Task.WhenAll for concurrent agent execution
            if (invocationText.Contains("Task.WhenAll") && invocationText.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MultiAgent_Concurrent",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Concurrent Agent Orchestration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Concurrent multi-agent pattern: multiple agents execute in parallel for faster processing",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Concurrent/Parallel"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHandoffPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for handoff keywords in method/variable names
        var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifiers)
        {
            if (identifier.Identifier.Text.Contains("Handoff", StringComparison.OrdinalIgnoreCase) ||
                identifier.Identifier.Text.Contains("Transfer", StringComparison.OrdinalIgnoreCase))
            {
                var lineNumber = GetLineNumber(root, identifier, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MultiAgent_Handoff",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Agent Handoff Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(identifier, sourceCode, 5),
                    bestPractice: "Handoff pattern: one agent transfers control to another specialized agent based on task requirements",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Handoff/Transfer"
                    }
                ));
                break; // Only report once per file
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMagenticPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Magentic pattern typically involves dynamic agent selection based on task/query
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            
            // Look for conditional agent selection logic
            if ((methodBody.Contains("switch") || methodBody.Contains("if")) &&
                methodBody.Contains("Agent") && 
                (methodBody.Contains("Select") || methodBody.Contains("Route")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Magentic_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Magentic Routing Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Magentic pattern: dynamically route tasks to the most appropriate agent based on task characteristics",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.7f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Magentic/Dynamic Routing"
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSupervisorPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            if (classDecl.Identifier.Text.Contains("Supervisor") && 
                classDecl.ToString().Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Supervisor_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Supervisor Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Supervisor pattern: a manager agent orchestrates and delegates work to worker agents, monitoring progress and handling failures",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Supervisor/Manager",
                        ["use_case"] = "Task delegation, progress monitoring"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHierarchicalAgents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            var fields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>();
            var hasParentAgent = fields.Any(f => f.Declaration.Type.ToString().Contains("Agent") && 
                                                (f.Declaration.Variables.Any(v => v.Identifier.Text.Contains("parent") || 
                                                                                 v.Identifier.Text.Contains("manager"))));

            if (hasParentAgent)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Hierarchical_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Hierarchical Agent Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Hierarchical agent pattern: organizing agents in parent-child relationships for complex task decomposition and delegation",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Hierarchical",
                        ["structure"] = "Parent-Child Agent Tree"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSwarmIntelligence(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("Swarm") || methodBody.Contains("Collective")) && 
                methodBody.Contains("Agent") && 
                Regex.IsMatch(methodBody, @"Task\.WhenAll|Parallel\.ForEach"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Swarm_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Swarm Intelligence Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Swarm intelligence: many simple agents collaborate to solve complex problems through emergent behavior and collective decision-making",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Swarm/Collective Intelligence",
                        ["characteristic"] = "Emergent behavior from simple agents"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectConsensusPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("Consensus") || methodBody.Contains("Voting") || methodBody.Contains("Majority")) && 
                methodBody.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Consensus_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Consensus Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Consensus pattern: multiple agents independently process a task and vote/agree on the final result for improved accuracy and reliability",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Consensus/Voting",
                        ["benefit"] = "Improved accuracy through agreement"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectDebatePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("Debate") || methodBody.Contains("Argument") || methodBody.Contains("Challenge")) && 
                methodBody.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Debate_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Debate Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Debate pattern: agents take opposing viewpoints and debate to explore different perspectives, leading to more robust solutions",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Debate/Adversarial",
                        ["benefit"] = "Exploring multiple perspectives"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Agent Lightning Patterns (Core RL-Based Optimization)

    private List<CodePattern> DetectAgentLightningServer(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect Lightning Server implementation
            if (classDecl.Identifier.Text.Contains("LightningServer") ||
                classDecl.Identifier.Text.Contains("AgentOptimization"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_Server_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Agent Lightning Server",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Using Agent Lightning Server to bridge agent frameworks with RL training (verl). Enables seamless optimization for ANY agent with ANY framework.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "RL-Based Agent Optimization",
                        ["capability"] = "Task pulling, trace collection, reward reporting",
                        ["reference"] = AgentLightningGitHub
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentLightningClient(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect OpenAI-compatible API calls to Lightning Client
            if (invocationText.Contains("LightningClient") ||
                (invocationText.Contains("OpenAI") && invocationText.Contains("training")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_Client",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Agent Lightning Client (OpenAI-compatible API)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Agent Lightning Client provides OpenAI-compatible LLM API inside training infrastructure, enabling zero-code-change integration with existing agents.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Training Infrastructure Integration",
                        ["compatibility"] = "OpenAI Agent SDK, LangChain, AutoGen"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRLTrainingPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect GRPO (Group Relative Policy Optimization) or verl usage
            if (invocationText.Contains("GRPO") || invocationText.Contains("verl") ||
                invocationText.Contains("PolicyOptimization") || invocationText.Contains("RLTraining"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_RLTraining",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "RL Training (GRPO/verl)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using reinforcement learning (GRPO algorithm via verl) to optimize agent models based on task success signals and interaction data.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Model Optimization",
                        ["algorithm"] = "GRPO (Group Relative Policy Optimization)",
                        ["training_framework"] = "verl",
                        ["optimization_type"] = "Reinforcement Learning"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRewardSignals(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for reward signal definitions
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("Reward") || 
                method.Identifier.Text.Contains("TaskSuccess") ||
                method.ReturnType.ToString().Contains("Reward"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_RewardSignal_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Reward Signal Definition",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Defining custom reward signals to reflect task success/failure. Agent Lightning uses these to guide RL optimization towards desired agent behaviors.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Reward Engineering",
                        ["use_case"] = "Task success signals, feedback signals, credit assignment"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectErrorMonitoring(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect agent-side error monitoring (critical for stable optimization)
            if (invocationText.Contains("MonitorExecution") || 
                invocationText.Contains("TrackFailure") ||
                invocationText.Contains("ReportError") ||
                (invocationText.Contains("agent") && invocationText.Contains("error")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_ErrorMonitoring",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Agent-Side Error Monitoring",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Agent Lightning's error monitoring tracks execution status, detects failure modes, and reports error types. Critical for stable optimization when agents fail or get stuck.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Fault Tolerance",
                        ["capability"] = "Failure detection, error reporting, graceful degradation"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectTraceCollection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for transition tuple pattern: (state, action, reward, next_state)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            // Detect sidecar-based trace collection
            if ((methodBody.Contains("state") && methodBody.Contains("action") && methodBody.Contains("reward")) ||
                methodBody.Contains("transition") || methodBody.Contains("trajectory") ||
                methodBody.Contains("CollectTrace"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_TraceCollection_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Sidecar-Based Trace Collection",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Agent Lightning's sidecar design non-intrusively monitors agent runs and collects transition tuples (state, action, reward, next_state) for RL training without modifying agent code.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Data Collection",
                        ["design_pattern"] = "Sidecar (Non-Intrusive Monitoring)",
                        ["data_format"] = "Transition tuples for RL"
                    }
                ));
                break; // Only report once per file
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCurriculumLearning(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("Curriculum") || 
                (methodBody.Contains("difficulty") && methodBody.Contains("progressive")) ||
                methodBody.Contains("TaskProgression"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_CurriculumLearning_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Curriculum Learning",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Curriculum learning: progressively increase task difficulty during training, starting simple and building to complex tasks. Accelerates learning and improves final performance.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Advanced RL Training",
                        ["technique"] = "Progressive Difficulty Scaling"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectOffPolicyRL(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("OffPolicy") || invocationText.Contains("ExperienceReplay") ||
                invocationText.Contains("ReplayBuffer"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_OffPolicyRL",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Off-Policy RL with Experience Replay",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Off-policy RL reuses past experiences for training, improving sample efficiency and enabling parallel data collection from multiple agents.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Sample Efficiency",
                        ["benefit"] = "Reuse past experiences, parallel collection"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHierarchicalRL(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            if ((classDecl.Identifier.Text.Contains("HierarchicalPolicy") || 
                 classDecl.Identifier.Text.Contains("HighLevelPolicy") ||
                 classDecl.Identifier.Text.Contains("LowLevelPolicy")) &&
                classDecl.ToString().Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_HierarchicalRL_{classDecl.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Hierarchical RL",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Hierarchical RL: decompose complex tasks into high-level goals and low-level actions, enabling faster learning on long-horizon tasks.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Temporal Abstraction",
                        ["use_case"] = "Long-horizon, multi-step tasks"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectOnlineSFT(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("OnlineSFT") || invocationText.Contains("SupervisedFineTuning") ||
                (invocationText.Contains("Online") && invocationText.Contains("FineTune")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_OnlineSFT",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Online Supervised Fine-Tuning",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Online SFT: continuously collect and filter high-quality agent interactions for supervised fine-tuning, complementing RL training.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Hybrid Training",
                        ["technique"] = "RL + Supervised Learning"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectUserFeedbackIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("UserFeedback") || method.Identifier.Text.Contains("HumanReward") ||
                method.ReturnType.ToString().Contains("Feedback"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_UserFeedback_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.HumanInLoop,
                    implementation: "User Feedback Integration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Integrating user feedback (thumbs up/down, ratings, corrections) as reward signals for RLHF (Reinforcement Learning from Human Feedback).",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "RLHF",
                        ["signal_type"] = "Human preferences"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectToolSuccessSignals(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("ToolSuccess") || methodBody.Contains("FunctionSuccess")) &&
                (methodBody.Contains("reward") || methodBody.Contains("signal")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_ToolSuccessSignals_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Tool Success Signals",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Using tool/function execution success as reward signals to teach agents when they correctly use tools and APIs.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Reward Engineering",
                        ["signal_source"] = "Tool execution results"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectLongHorizonCredit(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("CreditAssignment") || methodBody.Contains("LongHorizon") ||
                (methodBody.Contains("discount") && methodBody.Contains("gamma")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_LongHorizonCredit_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Long-Horizon Credit Assignment",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Properly assigning credit to actions in multi-step tasks with delayed rewards, critical for training agents on complex workflows.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Credit Assignment",
                        ["challenge"] = "Delayed rewards in long tasks"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectLLamaFactoryIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("LLamaFactory") || invocationText.Contains("LLaMA-Factory"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_LLamaFactory",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "LLaMA-Factory Integration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Integrating Agent Lightning with LLaMA-Factory for efficient fine-tuning and training of open-source LLMs on agent tasks.",
                    azureUrl: AgentLightningGitHub,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Training Infrastructure",
                        ["integration"] = "LLaMA-Factory"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectDSPyIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("DSPy") || invocationText.Contains("dspy"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_DSPy",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "DSPy Integration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Integrating DSPy (Declarative Self-improving Python) with Agent Lightning for prompt optimization and program synthesis.",
                    azureUrl: AgentLightningGitHub,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Prompt Optimization",
                        ["integration"] = "DSPy"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMultiTaskLearning(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("MultiTask") || 
                (methodBody.Contains("tasks") && methodBody.Contains("shared") && methodBody.Contains("representation")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_MultiTaskLearning_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Multi-Task Learning",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Training agents on multiple related tasks simultaneously to learn shared representations, improving generalization and sample efficiency.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Transfer Learning",
                        ["benefit"] = "Shared knowledge across tasks"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Anti-Patterns

    private List<CodePattern> DetectAntiPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Anti-Pattern 1: Using agents for structured tasks (should use functions)
        var comments = root.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            .Select(t => t.ToString().ToLowerInvariant());

        var hasAgentUsage = root.ToString().Contains("Agent");
        var hasStructuredTask = comments.Any(c => c.Contains("well-defined") || c.Contains("predefined") || c.Contains("fixed steps"));

        if (hasAgentUsage && hasStructuredTask)
        {
            patterns.Add(CreatePattern(
                name: "AntiPattern_AgentForStructuredTask",
                type: PatternType.AgentFramework,
                category: PatternCategory.AntiPatterns,
                implementation: "Using AI agent for well-defined task",
                filePath: filePath,
                lineNumber: 1,
                content: "AI agent used for structured/predefined task",
                bestPractice: "ANTI-PATTERN: Don't use AI agents for well-defined tasks with fixed steps. Use regular functions instead. Agents add latency, cost, and uncertainty.",
                azureUrl: AgentFrameworkUrl,
                context: context,
                confidence: 0.6f,
                isPositivePattern: false,
                metadata: new Dictionary<string, object>
                {
                    ["anti_pattern"] = "Agent for Structured Task",
                    ["recommendation"] = "Use functions for well-defined tasks, agents for dynamic/exploratory tasks"
                }
            ));
        }

        // Anti-Pattern 2: Single agent with too many tools (>20)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            var toolRegistrations = Regex.Matches(methodBody, @"AddTool|RegisterFunction|AddPlugin").Count;

            if (toolRegistrations > 20)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AntiPattern_TooManyTools",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AntiPatterns,
                    implementation: $"Single agent with {toolRegistrations} tools",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 5),
                    bestPractice: "ANTI-PATTERN: Single agent with >20 tools becomes unmanageable. Use multi-agent workflow with specialized agents instead.",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.8f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["anti_pattern"] = "Too Many Tools",
                        ["tool_count"] = toolRegistrations,
                        ["recommendation"] = "Split into multiple specialized agents in a workflow"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Helper Methods

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        int lineNumber,
        string content,
        string bestPractice,
        string azureUrl,
        string? context,
        float confidence,
        Dictionary<string, object>? metadata = null,
        bool isPositivePattern = true)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "csharp",
            FilePath = filePath,
            LineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default",
            Confidence = confidence,
            IsPositivePattern = isPositivePattern,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    private int GetLineNumber(SyntaxNode root, SyntaxNode node, string sourceCode)
    {
        var span = node.GetLocation().SourceSpan;
        var lineSpan = root.SyntaxTree.GetLineSpan(span);
        return lineSpan.StartLinePosition.Line + 1;
    }

    private string GetContextAroundNode(SyntaxNode node, string sourceCode, int contextLines)
    {
        var lines = sourceCode.Split('\n');
        var span = node.GetLocation().SourceSpan;
        var lineSpan = node.SyntaxTree.GetLineSpan(span);
        
        var startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        var endLine = Math.Min(lines.Length - 1, lineSpan.EndLinePosition.Line + contextLines);
        
        return string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));
    }

    #endregion
}

