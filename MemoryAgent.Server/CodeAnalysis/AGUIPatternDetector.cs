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
public partial class AGUIPatternDetector
{
    private readonly ILogger<AGUIPatternDetector>? _logger;

    // Azure documentation URLs (protected so partial classes can access)
    protected const string AGUIOverviewUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/";
    protected const string AGUIGettingStartedUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/getting-started";
    protected const string AGUIBackendToolsUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/backend-tools";
    protected const string AGUIHumanLoopUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/human-in-loop";
    protected const string AGUIStateUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/shared-state";
    protected const string AGUIGenUIUrl = "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/generative-ui";
    protected const string CopilotKitUrl = "https://docs.copilotkit.ai/reference/components/CopilotChat";

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
}
