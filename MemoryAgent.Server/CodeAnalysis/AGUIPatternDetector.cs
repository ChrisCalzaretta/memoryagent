using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects AG-UI (Agent UI) Protocol Integration patterns
/// Based on: https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/
/// 
/// AG-UI enables web-based AI agent applications with:
/// - Real-time streaming via Server-Sent Events (SSE)
/// - Standardized communication protocol
/// - Thread/conversation management
/// - Human-in-the-loop approvals
/// - State synchronization
/// - Custom UI component rendering
/// </summary>
public class AGUIPatternDetector
{
    private readonly ILogger<AGUIPatternDetector>? _logger;

    // Azure documentation URLs
    private const string AGUIOverviewUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/";
    private const string AGUIGettingStartedUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/getting-started";
    private const string AGUIBackendToolsUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/backend-tools";
    private const string AGUIHumanLoopUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/human-in-loop";
    private const string AGUIStateUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/shared-state";
    private const string AGUIGenUIUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/generative-ui";
    private const string CopilotKitUrl = "https://docs.copilotkit.ai/reference/components/CopilotChat";

    public AGUIPatternDetector(ILogger<AGUIPatternDetector>? logger = null)
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

            // AG-UI Core Integration Patterns
            patterns.AddRange(DetectMapAGUIEndpoint(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSSEStreaming(root, filePath, context, sourceCode));
            patterns.AddRange(DetectThreadManagement(root, filePath, context, sourceCode));
            
            // AG-UI Feature 1: Agentic Chat (Basic streaming chat)
            patterns.AddRange(DetectAgenticChat(root, filePath, context, sourceCode));
            
            // AG-UI Feature 2: Backend Tool Rendering
            patterns.AddRange(DetectBackendToolRendering(root, filePath, context, sourceCode));
            
            // AG-UI Feature 3: Human in the Loop (Approval workflows)
            patterns.AddRange(DetectHumanInLoop(root, filePath, context, sourceCode));
            
            // AG-UI Feature 4: Agentic Generative UI (Async tools with progress)
            patterns.AddRange(DetectAgenticGenerativeUI(root, filePath, context, sourceCode));
            
            // AG-UI Feature 5: Tool-based Generative UI (Custom UI components)
            patterns.AddRange(DetectToolBasedGenerativeUI(root, filePath, context, sourceCode));
            
            // AG-UI Feature 6: Shared State (Bidirectional sync)
            patterns.AddRange(DetectSharedState(root, filePath, context, sourceCode));
            
            // AG-UI Feature 7: Predictive State Updates (Optimistic updates)
            patterns.AddRange(DetectPredictiveStateUpdates(root, filePath, context, sourceCode));
            
            // Protocol-level patterns
            patterns.AddRange(DetectProtocolEvents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMiddlewarePatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCopilotKitIntegration(root, filePath, context, sourceCode));
            
            // Enhanced patterns from deep research
            patterns.AddRange(DetectFrontendToolCalls(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMultimodality(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCompleteEventTypes(root, filePath, context, sourceCode));
            patterns.AddRange(DetectStateDelta(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCancellationPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectWebSocketTransport(root, filePath, context, sourceCode));
            
            // 100% Coverage - Final Patterns
            patterns.AddRange(DetectCopilotKitHooks(root, filePath, context, sourceCode));
            patterns.AddRange(DetectErrorHandling(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTelemetryLogging(root, filePath, context, sourceCode));
            patterns.AddRange(DetectJsonSchemaValidation(root, filePath, context, sourceCode));
            patterns.AddRange(DetectThreadPersistence(root, filePath, context, sourceCode));
            patterns.AddRange(DetectStreamingHandlers(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAuthentication(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRateLimiting(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSessionManagement(root, filePath, context, sourceCode));
            
            // Anti-patterns and legacy detection
            patterns.AddRange(DetectAGUIAntiPatterns(root, filePath, context, sourceCode));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting AG-UI patterns in {FilePath}", filePath);
        }

        return patterns;
    }

    #region AG-UI Core Integration

    private List<CodePattern> DetectMapAGUIEndpoint(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: app.MapAGUI("/", agent) or endpoints.MapAGUI
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("MapAGUI"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_MapEndpoint",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "MapAGUI ASP.NET Core endpoint",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "AG-UI endpoint mapping for remote agent hosting. Enables web/mobile clients to interact with AI agents via HTTP.",
                    azureUrl: AGUIGettingStartedUrl,
                    context: context,
                    confidence: 0.98f,
                    metadata: new Dictionary<string, object>
                    {
                        ["feature"] = "AG-UI Integration",
                        ["deployment_model"] = "Remote Service via HTTP",
                        ["supports_streaming"] = true,
                        ["supports_multiple_clients"] = true
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSSEStreaming(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Server-Sent Events implementation
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Look for SSE-related patterns
            if ((invocationText.Contains("Server-Sent Events") || 
                 invocationText.Contains("text/event-stream") ||
                 invocationText.Contains("RunStreamingAsync") ||
                 invocationText.Contains("StreamAsync")) &&
                (invocationText.Contains("agent") || invocationText.Contains("Agent")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_SSEStreaming",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "Server-Sent Events (SSE) streaming",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "AG-UI uses SSE for real-time streaming of agent responses. Provides immediate feedback to users.",
                    azureUrl: AGUIOverviewUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["feature"] = "Real-time Streaming",
                        ["protocol"] = "Server-Sent Events",
                        ["benefits"] = "Immediate user feedback, progressive responses"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectThreadManagement(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Thread ID / Conversation ID management
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        var assignments = root.DescendantNodes().OfType<AssignmentExpressionSyntax>();

        foreach (var prop in properties)
        {
            var propText = prop.ToString();
            if (propText.Contains("threadId") || propText.Contains("ThreadId") || 
                propText.Contains("conversationId") || propText.Contains("ConversationId"))
            {
                var lineNumber = GetLineNumber(root, prop, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_ThreadManagement",
                    type: PatternType.AGUI,
                    category: PatternCategory.StateManagement,
                    implementation: "AG-UI Thread/Conversation management",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(prop, sourceCode, 3),
                    bestPractice: "AG-UI maintains conversation context across requests using protocol-managed thread IDs.",
                    azureUrl: AGUIOverviewUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["feature"] = "Thread Management",
                        ["maintains_context"] = true
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AG-UI Feature 1: Agentic Chat

    private List<CodePattern> DetectAgenticChat(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Basic streaming chat with automatic tool calling
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
            
            // Look for chat patterns with streaming and tool calling
            if ((methodBody.Contains("ChatMessage") || methodBody.Contains("chat")) &&
                (methodBody.Contains("StreamAsync") || methodBody.Contains("RunStreamingAsync")) &&
                (methodBody.Contains("tool") || methodBody.Contains("function")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_AgenticChat",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "AG-UI Feature 1: Agentic Chat",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Implements AG-UI Agentic Chat: streaming chat interface with automatic tool calling for enhanced user interactions.",
                    azureUrl: AGUIGettingStartedUrl,
                    context: context,
                    confidence: 0.87f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "1 - Agentic Chat",
                        ["capabilities"] = new[] { "Streaming", "Auto tool calling", "Real-time responses" }
                    }
                ));
                break; // One per method is enough
            }
        }

        return patterns;
    }

    #endregion

    #region AG-UI Feature 2: Backend Tool Rendering

    private List<CodePattern> DetectBackendToolRendering(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AIFunctionFactory.Create or similar patterns for backend tools
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if ((invocationText.Contains("AIFunctionFactory") || 
                 invocationText.Contains("AddTool") ||
                 invocationText.Contains("RegisterTool")) &&
                !invocationText.Contains("frontend") && 
                !invocationText.Contains("client"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_BackendToolRendering",
                    type: PatternType.AGUI,
                    category: PatternCategory.ToolIntegration,
                    implementation: "AG-UI Feature 2: Backend Tool Rendering",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 7),
                    bestPractice: "Backend tools executed on server with results streamed to client. Separates business logic from UI.",
                    azureUrl: AGUIBackendToolsUrl,
                    context: context,
                    confidence: 0.88f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "2 - Backend Tool Rendering",
                        ["execution_location"] = "Server-side",
                        ["benefits"] = new[] { "Security", "Performance", "Centralized logic" }
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AG-UI Feature 3: Human in the Loop

    private List<CodePattern> DetectHumanInLoop(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: ApprovalRequiredAIFunction or approval middleware
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var cls in classes)
        {
            var classText = cls.ToString();
            
            if (classText.Contains("ApprovalRequired") || 
                classText.Contains("RequiresApproval") ||
                (classText.Contains("approval") && classText.Contains("middleware")))
            {
                var lineNumber = GetLineNumber(root, cls, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_HumanInLoop",
                    type: PatternType.AGUI,
                    category: PatternCategory.HumanInLoop,
                    implementation: "AG-UI Feature 3: Human-in-the-Loop",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(cls, sourceCode, 15),
                    bestPractice: "Approval workflows where users confirm agent actions before execution. Critical for sensitive operations.",
                    azureUrl: AGUIHumanLoopUrl,
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "3 - Human-in-the-Loop",
                        ["use_cases"] = new[] { "Sensitive operations", "Financial transactions", "Data deletion", "External API calls" },
                        ["security_benefit"] = "Prevents unauthorized actions"
                    }
                ));
                break;
            }
        }

        // Also check for approval request/response handling
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();
            
            if (invocationText.Contains("RequestApproval") || 
                invocationText.Contains("WaitForApproval") ||
                invocationText.Contains("HandleApproval"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_ApprovalHandling",
                    type: PatternType.AGUI,
                    category: PatternCategory.HumanInLoop,
                    implementation: "Approval request/response handling",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Explicit approval handling ensures user consent for agent actions.",
                    azureUrl: AGUIHumanLoopUrl,
                    context: context,
                    confidence: 0.88f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "3 - Human-in-the-Loop",
                        ["pattern"] = "Approval Request/Response"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AG-UI Feature 4: Agentic Generative UI

    private List<CodePattern> DetectAgenticGenerativeUI(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Async tools with progress updates for long-running operations
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Look for async tool patterns with progress reporting
            if (method.Modifiers.Any(m => m.ValueText == "async") &&
                (methodText.Contains("IProgress") || 
                 methodText.Contains("ReportProgress") ||
                 methodText.Contains("ProgressUpdate")) &&
                (methodText.Contains("Tool") || methodText.Contains("Function")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_AgenticGenerativeUI",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "AG-UI Feature 4: Agentic Generative UI",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 12),
                    bestPractice: "Async tools with progress updates for long-running operations. Provides real-time feedback during complex tasks.",
                    azureUrl: AGUIGenUIUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "4 - Agentic Generative UI",
                        ["capabilities"] = new[] { "Progress tracking", "Long-running tasks", "Real-time updates" },
                        ["use_cases"] = new[] { "Data processing", "API calls", "File operations" }
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AG-UI Feature 5: Tool-based Generative UI

    private List<CodePattern> DetectToolBasedGenerativeUI(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Custom UI component rendering based on tool calls
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Look for UI rendering patterns based on tool results
            if ((methodText.Contains("RenderUI") || 
                 methodText.Contains("GenerateUI") ||
                 methodText.Contains("UIComponent")) &&
                (methodText.Contains("ToolResult") || 
                 methodText.Contains("tool") ||
                 methodText.Contains("function")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_ToolBasedGenerativeUI",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "AG-UI Feature 5: Tool-based Generative UI",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Custom UI components rendered based on tool call results. Dynamic, context-aware user interfaces.",
                    azureUrl: AGUIGenUIUrl,
                    context: context,
                    confidence: 0.82f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "5 - Tool-based Generative UI",
                        ["capabilities"] = new[] { "Dynamic UI", "Tool-driven rendering", "Context-aware components" },
                        ["examples"] = new[] { "Charts from data", "Maps from locations", "Forms from schemas" }
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AG-UI Feature 6: Shared State

    private List<CodePattern> DetectSharedState(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Bidirectional state synchronization
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();

        foreach (var cls in classes)
        {
            var classText = cls.ToString();
            
            // Look for state management classes with sync capabilities
            if ((classText.Contains("State") && classText.Contains("Sync")) ||
                classText.Contains("SharedState") ||
                (classText.Contains("AgentState") && classText.Contains("client")))
            {
                var lineNumber = GetLineNumber(root, cls, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_SharedState",
                    type: PatternType.AGUI,
                    category: PatternCategory.StateManagement,
                    implementation: "AG-UI Feature 6: Shared State",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(cls, sourceCode, 15),
                    bestPractice: "Bidirectional state synchronization between client and server for interactive agent experiences.",
                    azureUrl: AGUIStateUrl,
                    context: context,
                    confidence: 0.89f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "6 - Shared State",
                        ["sync_direction"] = "Bidirectional",
                        ["benefits"] = new[] { "Reactive UI", "State consistency", "Multi-client sync" },
                        ["use_cases"] = new[] { "Collaborative editing", "Real-time dashboards", "Multi-step workflows" }
                    }
                ));
                break;
            }
        }

        // Also detect ChatResponseFormat.ForJsonSchema<T>() for state snapshots
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();
            
            if (invocationText.Contains("ForJsonSchema") || 
                invocationText.Contains("StateSnapshot"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_StateSnapshot",
                    type: PatternType.AGUI,
                    category: PatternCategory.StateManagement,
                    implementation: "Structured state output via JSON schema",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Structured output becomes state events for client synchronization.",
                    azureUrl: AGUIStateUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "6 - Shared State",
                        ["pattern"] = "State Snapshot"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AG-UI Feature 7: Predictive State Updates

    private List<CodePattern> DetectPredictiveStateUpdates(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Streaming tool arguments as optimistic state updates
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Look for optimistic update patterns
            if ((methodText.Contains("optimistic") || 
                 methodText.Contains("Optimistic") ||
                 methodText.Contains("predictive") ||
                 methodText.Contains("Predictive")) &&
                (methodText.Contains("update") || methodText.Contains("state")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_PredictiveStateUpdates",
                    type: PatternType.AGUI,
                    category: PatternCategory.Performance,
                    implementation: "AG-UI Feature 7: Predictive State Updates",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Stream tool arguments as optimistic state updates for instant UI responsiveness before server confirmation.",
                    azureUrl: AGUIStateUrl,
                    context: context,
                    confidence: 0.84f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "7 - Predictive State Updates",
                        ["benefits"] = new[] { "Instant UI feedback", "Perceived performance", "Better UX" },
                        ["pattern"] = "Optimistic UI"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Protocol Events and Middleware

    private List<CodePattern> DetectProtocolEvents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AG-UI protocol event handling
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var enumDecl in enums)
        {
            var enumText = enumDecl.ToString();
            
            if (enumText.Contains("EventType") && 
                (enumText.Contains("TEXT_MESSAGE") || 
                 enumText.Contains("TOOL_CALL") ||
                 enumText.Contains("APPROVAL_REQUEST")))
            {
                var lineNumber = GetLineNumber(root, enumDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_ProtocolEvents",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "AG-UI Protocol event types",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(enumDecl, sourceCode, 10),
                    bestPractice: "Standardized AG-UI protocol events for reliable agent-client communication.",
                    azureUrl: AGUIOverviewUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["protocol"] = "AG-UI",
                        ["event_types"] = new[] { "TEXT_MESSAGE_CONTENT", "TOOL_CALL_START", "TOOL_CALL_END", "APPROVAL_REQUEST" }
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMiddlewarePatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AG-UI middleware for approvals, state, etc.
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var cls in classes)
        {
            var baseList = cls.BaseList?.ToString() ?? "";
            
            if (baseList.Contains("IAgentMiddleware") || 
                baseList.Contains("AgentMiddleware") ||
                (cls.Identifier.Text.Contains("Middleware") && cls.ToString().Contains("agent")))
            {
                var lineNumber = GetLineNumber(root, cls, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_Middleware",
                    type: PatternType.AGUI,
                    category: PatternCategory.Interceptors,
                    implementation: "AG-UI agent middleware pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(cls, sourceCode, 15),
                    bestPractice: "Middleware pattern for approvals, state management, and custom logic in AG-UI pipeline.",
                    azureUrl: AGUIOverviewUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Middleware/Interceptor",
                        ["use_cases"] = new[] { "Approval workflows", "State sync", "Logging", "Error handling" }
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    #endregion

    #region Frontend Tool Calls (Critical Missing Feature)

    private List<CodePattern> DetectFrontendToolCalls(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Frontend tools executed on CLIENT-SIDE
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();

        // Look for frontend tool registration or client-side tool execution
        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            if ((methodText.Contains("FrontendTool") || 
                 methodText.Contains("ClientTool") ||
                 methodText.Contains("ClientSideTool") ||
                 (methodText.Contains("Tool") && methodText.Contains("client"))) &&
                !methodText.Contains("backend") &&
                !methodText.Contains("server"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_FrontendToolCalls",
                    type: PatternType.AGUI,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Frontend Tool Calls - Client-side execution",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 8),
                    bestPractice: "Frontend tools execute in browser/client, accessing client-specific data (GPS, camera, localStorage). Use for client-side sensors and APIs.",
                    azureUrl: "https://learn.microsoft.com/cs-cz/agent-framework/integrations/ag-ui/frontend-tools",
                    context: context,
                    confidence: 0.87f,
                    metadata: new Dictionary<string, object>
                    {
                        ["execution_location"] = "Client-side",
                        ["capabilities"] = new[] { "GPS", "Camera", "Mic", "localStorage", "Browser APIs" },
                        ["use_cases"] = new[] { "Client sensors", "User-specific context", "Browser features" }
                    }
                ));
                break;
            }
        }

        // Look for tool registry patterns (frontend tool mapping)
        foreach (var prop in properties)
        {
            var propText = prop.ToString();
            
            if ((propText.Contains("toolRegistry") || 
                 propText.Contains("ToolRegistry") ||
                 propText.Contains("frontendTools")) &&
                propText.Contains("Dictionary"))
            {
                var lineNumber = GetLineNumber(root, prop, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_FrontendToolRegistry",
                    type: PatternType.AGUI,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Frontend tool registry mapping",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(prop, sourceCode, 5),
                    bestPractice: "Maintain registry mapping tool names to client functions for frontend tool execution.",
                    azureUrl: "https://learn.microsoft.com/cs-cz/agent-framework/integrations/ag-ui/frontend-tools",
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Tool Registry",
                        ["location"] = "Frontend"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Multimodality Support (Files, Images, Audio)

    private List<CodePattern> DetectMultimodality(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: File/attachment handling
        if (sourceCode.Contains("attachment") || sourceCode.Contains("Attachment") ||
            sourceCode.Contains("file upload") || sourceCode.Contains("FileUpload"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Multimodal_Files",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "Multimodal file/attachment support",
                filePath: filePath,
                lineNumber: 1,
                content: "// File/attachment handling detected",
                bestPractice: "AG-UI supports typed attachments for rich, multi-modal agent interactions.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.75f,
                metadata: new Dictionary<string, object>
                {
                    ["multimodal_type"] = "Files",
                    ["capabilities"] = new[] { "Document uploads", "File attachments", "Binary data" }
                }
            ));
        }

        // Pattern: Image processing
        if (sourceCode.Contains("image") || sourceCode.Contains("Image") ||
            sourceCode.Contains("ImageData") || sourceCode.Contains("picture"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Multimodal_Images",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "Multimodal image processing",
                filePath: filePath,
                lineNumber: 1,
                content: "// Image processing detected",
                bestPractice: "AG-UI supports image inputs for visual agent interactions and multimodal AI.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.7f,
                metadata: new Dictionary<string, object>
                {
                    ["multimodal_type"] = "Images",
                    ["capabilities"] = new[] { "Image upload", "Visual analysis", "OCR" }
                }
            ));
        }

        // Pattern: Audio/voice processing
        if (sourceCode.Contains("audio") || sourceCode.Contains("Audio") ||
            sourceCode.Contains("voice") || sourceCode.Contains("transcript") ||
            sourceCode.Contains("Transcript"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Multimodal_Audio",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "Multimodal audio/voice support",
                filePath: filePath,
                lineNumber: 1,
                content: "// Audio/voice processing detected",
                bestPractice: "AG-UI supports audio inputs and real-time transcripts for voice-enabled agents.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.72f,
                metadata: new Dictionary<string, object>
                {
                    ["multimodal_type"] = "Audio",
                    ["capabilities"] = new[] { "Voice input", "Audio transcripts", "Speech-to-text" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region Complete Event Types (16 AG-UI Events)

    private List<CodePattern> DetectCompleteEventTypes(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Check for comprehensive AG-UI event type enum or constants
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();

        foreach (var enumDecl in enums)
        {
            var enumText = enumDecl.ToString();
            
            // Check for comprehensive event type coverage
            int eventCount = 0;
            var detectedEvents = new List<string>();
            
            if (enumText.Contains("TEXT_MESSAGE_START")) { eventCount++; detectedEvents.Add("TEXT_MESSAGE_START"); }
            if (enumText.Contains("TEXT_MESSAGE_DELTA")) { eventCount++; detectedEvents.Add("TEXT_MESSAGE_DELTA"); }
            if (enumText.Contains("TEXT_MESSAGE_END")) { eventCount++; detectedEvents.Add("TEXT_MESSAGE_END"); }
            if (enumText.Contains("TOOL_CALL_START")) { eventCount++; detectedEvents.Add("TOOL_CALL_START"); }
            if (enumText.Contains("TOOL_CALL_DELTA")) { eventCount++; detectedEvents.Add("TOOL_CALL_DELTA"); }
            if (enumText.Contains("TOOL_CALL_END")) { eventCount++; detectedEvents.Add("TOOL_CALL_END"); }
            if (enumText.Contains("STATE_SNAPSHOT")) { eventCount++; detectedEvents.Add("STATE_SNAPSHOT"); }
            if (enumText.Contains("STATE_DELTA")) { eventCount++; detectedEvents.Add("STATE_DELTA"); }
            if (enumText.Contains("APPROVAL_REQUEST")) { eventCount++; detectedEvents.Add("APPROVAL_REQUEST"); }
            if (enumText.Contains("APPROVAL_RESPONSE")) { eventCount++; detectedEvents.Add("APPROVAL_RESPONSE"); }
            if (enumText.Contains("RUN_STARTED")) { eventCount++; detectedEvents.Add("RUN_STARTED"); }
            if (enumText.Contains("RUN_COMPLETED")) { eventCount++; detectedEvents.Add("RUN_COMPLETED"); }
            if (enumText.Contains("ERROR")) { eventCount++; detectedEvents.Add("ERROR"); }
            if (enumText.Contains("CANCEL")) { eventCount++; detectedEvents.Add("CANCEL"); }
            if (enumText.Contains("RESUME")) { eventCount++; detectedEvents.Add("RESUME"); }
            
            if (eventCount >= 8) // At least half of the 16 events
            {
                var lineNumber = GetLineNumber(root, enumDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_CompleteEventTypes",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: $"AG-UI event types ({eventCount}/16 detected)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(enumDecl, sourceCode, 15),
                    bestPractice: $"Comprehensive AG-UI event type system. Detected {eventCount} of 16 standardized events: {string.Join(", ", detectedEvents.Take(5))}...",
                    azureUrl: "https://docs.ag-ui.com/concepts/architecture",
                    context: context,
                    confidence: eventCount >= 12 ? 0.95f : 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["event_count"] = eventCount,
                        ["detected_events"] = detectedEvents,
                        ["coverage"] = $"{eventCount * 100 / 16}%"
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    #endregion

    #region State Delta / JSON Patch Patterns

    private List<CodePattern> DetectStateDelta(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: JSON Patch usage for state deltas
        if (sourceCode.Contains("JsonPatch") || sourceCode.Contains("json-patch") ||
            sourceCode.Contains("PatchDocument"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_StateDelta_JsonPatch",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "JSON Patch for state deltas",
                filePath: filePath,
                lineNumber: 1,
                content: "// JSON Patch state delta detected",
                bestPractice: "Use JSON Patch format for incremental state updates. More efficient than full state snapshots for large states.",
                azureUrl: "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "State Delta",
                    ["format"] = "JSON Patch",
                    ["benefits"] = new[] { "Efficient updates", "Reduced bandwidth", "Event sourcing" }
                }
            ));
        }

        // Pattern: Event-sourced state management
        if (sourceCode.Contains("event-sourced") || sourceCode.Contains("EventSourced") ||
            (sourceCode.Contains("event") && sourceCode.Contains("diff")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_EventSourced_State",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Event-sourced state with diffs",
                filePath: filePath,
                lineNumber: 1,
                content: "// Event-sourced state management detected",
                bestPractice: "AG-UI supports event-sourced state diffs for collaborative workflows and state reconstruction.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Event Sourcing",
                    ["benefits"] = new[] { "State history", "Replay capability", "Collaborative editing" }
                }
            ));
        }

        // Pattern: Conflict resolution
        if (sourceCode.Contains("conflict") && (sourceCode.Contains("resolution") || sourceCode.Contains("merge")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ConflictResolution",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "State conflict resolution",
                filePath: filePath,
                lineNumber: 1,
                content: "// Conflict resolution detected",
                bestPractice: "AG-UI shared state includes conflict resolution for concurrent client/server updates.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Conflict Resolution",
                    ["scenarios"] = new[] { "Multi-client", "Race conditions", "Network delays" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region Cancellation & Resumption Patterns

    private List<CodePattern> DetectCancellationPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Cancellation support
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var parameters = method.ParameterList.Parameters;
            var methodText = method.ToString();
            
            // Check for CancellationToken in AG-UI context
            if (parameters.Any(p => p.Type?.ToString().Contains("CancellationToken") == true) &&
                (methodText.Contains("agent") || methodText.Contains("Agent") || 
                 methodText.Contains("run") || methodText.Contains("stream")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_Cancellation",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "Cancellation token support",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 6),
                    bestPractice: "AG-UI supports cancellation to stop agent execution mid-flow. Use CancellationToken throughout pipeline.",
                    azureUrl: "https://docs.ag-ui.com/",
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["capability"] = "Cancellation",
                        ["use_cases"] = new[] { "User abort", "Timeout", "Resource cleanup" }
                    }
                ));
                break;
            }
        }

        // Pattern: Pause/Resume workflow
        if (sourceCode.Contains("Pause") && sourceCode.Contains("Resume"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_PauseResume",
                type: PatternType.AGUI,
                category: PatternCategory.HumanInLoop,
                implementation: "Pause/Resume workflow control",
                filePath: filePath,
                lineNumber: 1,
                content: "// Pause/Resume detected",
                bestPractice: "AG-UI interrupts allow pausing for human intervention and resuming without losing state.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.87f,
                metadata: new Dictionary<string, object>
                {
                    ["capabilities"] = new[] { "Pause", "Resume", "State preservation" },
                    ["use_cases"] = new[] { "Human approval", "Escalation", "Error review" }
                }
            ));
        }

        // Pattern: Retry logic
        if ((sourceCode.Contains("retry") || sourceCode.Contains("Retry")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("tool")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Retry",
                type: PatternType.AGUI,
                category: PatternCategory.Reliability,
                implementation: "Retry capability",
                filePath: filePath,
                lineNumber: 1,
                content: "// Retry logic detected",
                bestPractice: "AG-UI supports retrying failed operations without losing conversation context.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.78f,
                metadata: new Dictionary<string, object>
                {
                    ["capability"] = "Retry",
                    ["benefits"] = new[] { "Error recovery", "Resilience", "UX improvement" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region WebSocket Transport

    private List<CodePattern> DetectWebSocketTransport(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: WebSocket usage for AG-UI
        if (sourceCode.Contains("WebSocket") || sourceCode.Contains("websocket") ||
            sourceCode.Contains("ws://") || sourceCode.Contains("wss://"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_WebSocketTransport",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "WebSocket bidirectional transport",
                filePath: filePath,
                lineNumber: 1,
                content: "// WebSocket transport detected",
                bestPractice: "AG-UI supports WebSocket transport for bidirectional real-time communication (alternative to SSE).",
                azureUrl: "https://docs.ag-ui.com/concepts/architecture",
                context: context,
                confidence: 0.82f,
                metadata: new Dictionary<string, object>
                {
                    ["transport"] = "WebSocket",
                    ["direction"] = "Bidirectional",
                    ["vs_sse"] = "More complex, full duplex"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region CopilotKit Integration

    private List<CodePattern> DetectCopilotKitIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Check for CopilotKit usage (common client library for AG-UI)
        if (sourceCode.Contains("CopilotKit") || 
            sourceCode.Contains("useCopilotChat") ||
            sourceCode.Contains("CopilotChat"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "CopilotKit AG-UI client integration",
                filePath: filePath,
                lineNumber: 1,
                content: "// File uses CopilotKit for AG-UI client",
                bestPractice: "CopilotKit provides rich UI components for AG-UI protocol, supporting all 7 features with polished UX.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["client_library"] = "CopilotKit",
                    ["supports"] = new[] { 
                        "Streaming chat", 
                        "Tool calling", 
                        "Human-in-loop", 
                        "Generative UI", 
                        "Shared state" 
                    }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - CopilotKit React Hooks

    private List<CodePattern> DetectCopilotKitHooks(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: useCopilotChat hook
        if (sourceCode.Contains("useCopilotChat"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_useCopilotChat",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "useCopilotChat React hook for AG-UI chat",
                filePath: filePath,
                lineNumber: 1,
                content: "// useCopilotChat hook detected",
                bestPractice: "useCopilotChat provides streaming chat with automatic tool calling via AG-UI protocol.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["hook"] = "useCopilotChat",
                    ["framework"] = "React",
                    ["capabilities"] = new[] { "Streaming chat", "Tool calling", "State management" }
                }
            ));
        }

        // Pattern: useCopilotAction hook
        if (sourceCode.Contains("useCopilotAction"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_useCopilotAction",
                type: PatternType.AGUI,
                category: PatternCategory.ToolIntegration,
                implementation: "useCopilotAction for defining frontend tools",
                filePath: filePath,
                lineNumber: 1,
                content: "// useCopilotAction hook detected",
                bestPractice: "useCopilotAction defines frontend-executable actions that AG-UI agents can call.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.97f,
                metadata: new Dictionary<string, object>
                {
                    ["hook"] = "useCopilotAction",
                    ["tool_location"] = "Frontend"
                }
            ));
        }

        // Pattern: useCopilotReadable hook
        if (sourceCode.Contains("useCopilotReadable"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_useCopilotReadable",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "useCopilotReadable for sharing app state with agent",
                filePath: filePath,
                lineNumber: 1,
                content: "// useCopilotReadable hook detected",
                bestPractice: "useCopilotReadable shares application state with AG-UI agents for context-aware responses.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.96f,
                metadata: new Dictionary<string, object>
                {
                    ["hook"] = "useCopilotReadable",
                    ["purpose"] = "State sharing"
                }
            ));
        }

        // Pattern: CopilotKit component
        if (sourceCode.Contains("<CopilotKit") || sourceCode.Contains("CopilotProvider"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_Provider",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "CopilotKit provider component",
                filePath: filePath,
                lineNumber: 1,
                content: "// CopilotKit provider detected",
                bestPractice: "CopilotKit provider wraps app to enable AG-UI integration with React components.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["component"] = "CopilotKit",
                    ["type"] = "Provider"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - Error Handling

    private List<CodePattern> DetectErrorHandling(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AG-UI specific error handling
        if ((sourceCode.Contains("AGUIException") || sourceCode.Contains("AGUIError")) ||
            (sourceCode.Contains("catch") && sourceCode.Contains("agent") && sourceCode.Contains("error")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ErrorHandling",
                type: PatternType.AGUI,
                category: PatternCategory.Reliability,
                implementation: "AG-UI error handling",
                filePath: filePath,
                lineNumber: 1,
                content: "// AG-UI error handling detected",
                bestPractice: "Implement comprehensive error handling for AG-UI operations with proper logging and user feedback.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.75f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Error Handling",
                    ["best_practices"] = new[] { "Log errors", "User feedback", "Graceful degradation" }
                }
            ));
        }

        // Pattern: Retry with exponential backoff
        if ((sourceCode.Contains("retry") || sourceCode.Contains("Retry")) &&
            (sourceCode.Contains("exponential") || sourceCode.Contains("backoff") || sourceCode.Contains("Polly")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ExponentialBackoff",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Exponential backoff for AG-UI retries",
                filePath: filePath,
                lineNumber: 1,
                content: "// Exponential backoff detected",
                bestPractice: "Use exponential backoff for AG-UI connection retries to handle transient failures gracefully.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/retry-service-specific",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Exponential Backoff",
                    ["use_case"] = "Transient failure handling"
                }
            ));
        }

        // Pattern: Circuit breaker
        if (sourceCode.Contains("CircuitBreaker") || sourceCode.Contains("circuit-breaker"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CircuitBreaker",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Circuit breaker for AG-UI resilience",
                filePath: filePath,
                lineNumber: 1,
                content: "// Circuit breaker detected",
                bestPractice: "Circuit breaker prevents cascading failures in AG-UI by failing fast when service is unhealthy.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Circuit Breaker",
                    ["benefits"] = new[] { "Fail fast", "Prevent cascading failures", "Auto-recovery" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - Telemetry & Logging

    private List<CodePattern> DetectTelemetryLogging(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: OpenTelemetry
        if (sourceCode.Contains("OpenTelemetry") || sourceCode.Contains("ActivitySource") ||
            sourceCode.Contains("Tracer") || sourceCode.Contains("Meter"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_OpenTelemetry",
                type: PatternType.AGUI,
                category: PatternCategory.Operational,
                implementation: "OpenTelemetry instrumentation for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// OpenTelemetry detected",
                bestPractice: "Use OpenTelemetry to trace AG-UI agent runs, tool calls, and streaming operations for observability.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["telemetry"] = "OpenTelemetry",
                    ["capabilities"] = new[] { "Tracing", "Metrics", "Logging" }
                }
            ));
        }

        // Pattern: Structured logging
        if ((sourceCode.Contains("ILogger") || sourceCode.Contains("Serilog")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("AG-UI") || sourceCode.Contains("tool")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_StructuredLogging",
                type: PatternType.AGUI,
                category: PatternCategory.Operational,
                implementation: "Structured logging for AG-UI events",
                filePath: filePath,
                lineNumber: 1,
                content: "// Structured logging detected",
                bestPractice: "Use structured logging to capture AG-UI events with proper context (thread IDs, tool names, user IDs).",
                azureUrl: "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging",
                context: context,
                confidence: 0.82f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Structured Logging",
                    ["context_fields"] = new[] { "ThreadID", "ToolName", "UserID", "EventType" }
                }
            ));
        }

        // Pattern: Application Insights
        if (sourceCode.Contains("TelemetryClient") || sourceCode.Contains("ApplicationInsights"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ApplicationInsights",
                type: PatternType.AGUI,
                category: PatternCategory.Operational,
                implementation: "Azure Application Insights for AG-UI monitoring",
                filePath: filePath,
                lineNumber: 1,
                content: "// Application Insights detected",
                bestPractice: "Use Application Insights to monitor AG-UI performance, errors, and usage patterns.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview",
                context: context,
                confidence: 0.93f,
                metadata: new Dictionary<string, object>
                {
                    ["service"] = "Application Insights",
                    ["metrics"] = new[] { "Response time", "Error rate", "Active connections" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - JSON Schema Validation

    private List<CodePattern> DetectJsonSchemaValidation(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: JSON Schema for state validation
        if ((sourceCode.Contains("JsonSchema") || sourceCode.Contains("jsonschema")) &&
            (sourceCode.Contains("state") || sourceCode.Contains("State")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_JsonSchemaValidation",
                type: PatternType.AGUI,
                category: PatternCategory.Reliability,
                implementation: "JSON Schema validation for AG-UI state",
                filePath: filePath,
                lineNumber: 1,
                content: "// JSON Schema validation detected",
                bestPractice: "Define JSON Schemas for AG-UI shared state to ensure type safety and validate state transitions.",
                azureUrl: "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management",
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "JSON Schema",
                    ["purpose"] = "State validation"
                }
            ));
        }

        // Pattern: ChatResponseFormat.ForJsonSchema
        if (sourceCode.Contains("ChatResponseFormat") && sourceCode.Contains("ForJsonSchema"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_TypedStateSchema",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Typed state schema with ChatResponseFormat",
                filePath: filePath,
                lineNumber: 1,
                content: "// Typed state schema detected",
                bestPractice: "Use ChatResponseFormat.ForJsonSchema<T>() to enforce structured state updates in AG-UI.",
                azureUrl: "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["api"] = "ChatResponseFormat.ForJsonSchema",
                    ["benefit"] = "Type-safe state"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - Thread ID Persistence

    private List<CodePattern> DetectThreadPersistence(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Thread ID storage/persistence
        if ((sourceCode.Contains("threadId") || sourceCode.Contains("ThreadId") || sourceCode.Contains("thread_id")) &&
            (sourceCode.Contains("Save") || sourceCode.Contains("Store") || sourceCode.Contains("Persist") || 
             sourceCode.Contains("Database") || sourceCode.Contains("Cache")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ThreadPersistence",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Thread ID persistence for conversation continuity",
                filePath: filePath,
                lineNumber: 1,
                content: "// Thread ID persistence detected",
                bestPractice: "Persist AG-UI thread IDs to maintain conversation context across sessions and reconnections.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Thread Persistence",
                    ["storage"] = new[] { "Database", "Cache", "Session" },
                    ["benefit"] = "Conversation continuity"
                }
            ));
        }

        // Pattern: Thread management service
        if ((sourceCode.Contains("ThreadManager") || sourceCode.Contains("ThreadService") || 
             sourceCode.Contains("ConversationManager")) &&
            (sourceCode.Contains("AG-UI") || sourceCode.Contains("agent")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ThreadManagementService",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Thread management service for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// Thread management service detected",
                bestPractice: "Centralize thread management for AG-UI to handle creation, storage, and cleanup of conversation threads.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Thread Management",
                    ["responsibilities"] = new[] { "Create", "Store", "Retrieve", "Cleanup" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - Streaming Response Handlers

    private List<CodePattern> DetectStreamingHandlers(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: IAsyncEnumerable for streaming
        if (sourceCode.Contains("IAsyncEnumerable") && 
            (sourceCode.Contains("AGUIEvent") || sourceCode.Contains("StreamingResponse") || sourceCode.Contains("agent")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_AsyncEnumerableStreaming",
                type: PatternType.AGUI,
                category: PatternCategory.Performance,
                implementation: "IAsyncEnumerable for AG-UI streaming",
                filePath: filePath,
                lineNumber: 1,
                content: "// IAsyncEnumerable streaming detected",
                bestPractice: "Use IAsyncEnumerable<T> for efficient memory usage when streaming AG-UI events to clients.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Async Enumerable",
                    ["benefits"] = new[] { "Memory efficient", "Backpressure", "Cancellation support" }
                }
            ));
        }

        // Pattern: Event handler for streaming updates
        if ((sourceCode.Contains("OnEventReceived") || sourceCode.Contains("HandleStreamingUpdate")) &&
            (sourceCode.Contains("AG-UI") || sourceCode.Contains("agent") || sourceCode.Contains("SSE")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_StreamingEventHandler",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "Event handler for AG-UI streaming updates",
                filePath: filePath,
                lineNumber: 1,
                content: "// Streaming event handler detected",
                bestPractice: "Implement event handlers to process AG-UI streaming updates (text deltas, tool progress, state changes).",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.80f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Event Handler",
                    ["events"] = new[] { "Text delta", "Tool progress", "State update" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - Authentication & Authorization

    private List<CodePattern> DetectAuthentication(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: JWT authentication for AG-UI
        if ((sourceCode.Contains("JWT") || sourceCode.Contains("JwtBearer")) &&
            (sourceCode.Contains("AG-UI") || sourceCode.Contains("agent") || sourceCode.Contains("api")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_JWTAuthentication",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "JWT authentication for AG-UI endpoints",
                filePath: filePath,
                lineNumber: 1,
                content: "// JWT authentication detected",
                bestPractice: "Secure AG-UI endpoints with JWT authentication to verify user identity before agent interactions.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/security/authentication/",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["auth_type"] = "JWT",
                    ["endpoints"] = "AG-UI MapAGUI"
                }
            ));
        }

        // Pattern: Authorization policies
        if ((sourceCode.Contains("AuthorizeAttribute") || sourceCode.Contains("[Authorize") || sourceCode.Contains("Policy")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("tool")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Authorization",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Authorization policies for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// Authorization detected",
                bestPractice: "Implement authorization policies to control which users can access specific AG-UI agents or tools.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/security/authorization/",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Authorization",
                    ["scope"] = new[] { "Agent access", "Tool execution", "State modification" }
                }
            ));
        }

        // Pattern: API Key authentication
        if ((sourceCode.Contains("ApiKey") || sourceCode.Contains("API_KEY")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("AG-UI")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ApiKeyAuth",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "API Key authentication for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// API Key authentication detected",
                bestPractice: "Use API keys for service-to-service AG-UI authentication, stored securely in Azure Key Vault.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/key-vault/",
                context: context,
                confidence: 0.83f,
                metadata: new Dictionary<string, object>
                {
                    ["auth_type"] = "API Key",
                    ["storage"] = "Azure Key Vault recommended"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - Rate Limiting

    private List<CodePattern> DetectRateLimiting(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Rate limiting middleware
        if ((sourceCode.Contains("RateLimit") || sourceCode.Contains("Throttle") || sourceCode.Contains("RateLimiter")) &&
            (sourceCode.Contains("AG-UI") || sourceCode.Contains("agent") || sourceCode.Contains("api")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_RateLimiting",
                type: PatternType.AGUI,
                category: PatternCategory.Performance,
                implementation: "Rate limiting for AG-UI endpoints",
                filePath: filePath,
                lineNumber: 1,
                content: "// Rate limiting detected",
                bestPractice: "Implement rate limiting on AG-UI endpoints to prevent abuse and ensure fair resource allocation.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit",
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Rate Limiting",
                    ["algorithms"] = new[] { "Fixed window", "Sliding window", "Token bucket" },
                    ["scope"] = new[] { "Per user", "Per IP", "Per endpoint" }
                }
            ));
        }

        // Pattern: Concurrent connection limits
        if ((sourceCode.Contains("MaxConcurrentConnections") || sourceCode.Contains("ConcurrencyLimiter")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("SSE") || sourceCode.Contains("WebSocket")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ConcurrencyLimit",
                type: PatternType.AGUI,
                category: PatternCategory.Performance,
                implementation: "Concurrent connection limits for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// Concurrency limiting detected",
                bestPractice: "Limit concurrent AG-UI connections per user to prevent resource exhaustion and ensure scalability.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit",
                context: context,
                confidence: 0.87f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Concurrency Limit",
                    ["benefits"] = new[] { "Resource protection", "Fair usage", "Scalability" }
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region 100% Coverage - Session Management

    private List<CodePattern> DetectSessionManagement(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Session state management
        if ((sourceCode.Contains("Session") || sourceCode.Contains("ISession")) &&
            (sourceCode.Contains("threadId") || sourceCode.Contains("agent") || sourceCode.Contains("conversation")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_SessionManagement",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Session management for AG-UI conversations",
                filePath: filePath,
                lineNumber: 1,
                content: "// Session management detected",
                bestPractice: "Use session management to associate AG-UI thread IDs with user sessions for conversation continuity.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state",
                context: context,
                confidence: 0.82f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Session Management",
                    ["stored_data"] = new[] { "Thread ID", "User context", "Conversation history" }
                }
            ));
        }

        // Pattern: Distributed session with Redis
        if ((sourceCode.Contains("Redis") || sourceCode.Contains("DistributedCache")) &&
            (sourceCode.Contains("session") || sourceCode.Contains("thread")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_DistributedSession",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Distributed session storage with Redis",
                filePath: filePath,
                lineNumber: 1,
                content: "// Distributed session detected",
                bestPractice: "Use Redis for distributed session storage to enable AG-UI scalability across multiple server instances.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/",
                context: context,
                confidence: 0.93f,
                metadata: new Dictionary<string, object>
                {
                    ["storage"] = "Redis",
                    ["benefits"] = new[] { "Scalability", "High availability", "Fast access" }
                }
            ));
        }

        // Pattern: Session timeout handling
        if ((sourceCode.Contains("SessionTimeout") || sourceCode.Contains("IdleTimeout")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("conversation")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_SessionTimeout",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Session timeout for inactive AG-UI conversations",
                filePath: filePath,
                lineNumber: 1,
                content: "// Session timeout detected",
                bestPractice: "Implement session timeouts for AG-UI conversations to clean up inactive threads and free resources.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state",
                context: context,
                confidence: 0.80f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Session Timeout",
                    ["cleanup"] = "Inactive thread removal"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region Anti-Patterns

    private List<CodePattern> DetectAGUIAntiPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Anti-pattern: Direct agent.Run() instead of AG-UI for web apps
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();
            
            // Detect direct Run() in web context without AG-UI
            if ((invocationText.Contains("agent.Run(") || invocationText.Contains("agent.RunAsync(")) &&
                !sourceCode.Contains("MapAGUI") &&
                (sourceCode.Contains("Controller") || sourceCode.Contains("WebApplication")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_AntiPattern_DirectRun",
                    type: PatternType.AGUI,
                    category: PatternCategory.AntiPatterns,
                    implementation: "Direct agent.Run() in web context",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 7),
                    bestPractice: "For web apps, use MapAGUI() instead of direct Run(). AG-UI provides streaming, multi-client support, and standardized protocol.",
                    azureUrl: AGUIOverviewUrl,
                    context: context,
                    confidence: 0.75f,
                    isPositive: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["anti_pattern"] = true,
                        ["issue"] = "Missing AG-UI integration in web context",
                        ["recommendation"] = "Migrate to MapAGUI for better streaming and client support",
                        ["migration_url"] = AGUIGettingStartedUrl
                    }
                ));
            }
        }

        // Anti-pattern: Custom SSE implementation instead of AG-UI protocol
        if (sourceCode.Contains("text/event-stream") && 
            !sourceCode.Contains("MapAGUI") &&
            !sourceCode.Contains("AG-UI"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_AntiPattern_CustomSSE",
                type: PatternType.AGUI,
                category: PatternCategory.AntiPatterns,
                implementation: "Custom SSE without AG-UI protocol",
                filePath: filePath,
                lineNumber: 1,
                content: "// Custom SSE implementation detected",
                bestPractice: "Use AG-UI protocol instead of custom SSE. AG-UI provides standardized events, thread management, and client libraries.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.7f,
                isPositive: false,
                metadata: new Dictionary<string, object>
                {
                    ["anti_pattern"] = true,
                    ["issue"] = "Custom SSE without protocol standardization",
                    ["recommendation"] = "Adopt AG-UI protocol for better interoperability"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region Helper Methods

    private int GetLineNumber(SyntaxNode root, SyntaxNode node, string sourceCode)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return lineSpan.StartLinePosition.Line + 1;
    }

    private string GetContextAroundNode(SyntaxNode node, string sourceCode, int contextLines)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        var endLine = Math.Min(sourceCode.Split('\n').Length - 1, lineSpan.EndLinePosition.Line + contextLines);

        var lines = sourceCode.Split('\n');
        var relevantLines = lines.Skip(startLine).Take(endLine - startLine + 1);

        return string.Join("\n", relevantLines);
    }

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
        bool isPositive = true)
    {
        return new CodePattern
        {
            Id = $"{name}_{filePath}_{lineNumber}",
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "C#",
            FilePath = filePath,
            LineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Confidence = confidence,
            IsPositivePattern = isPositive,
            Context = context ?? "",
            Metadata = metadata ?? new Dictionary<string, object>(),
            DetectedAt = DateTime.UtcNow
        };
    }

    #endregion
}

