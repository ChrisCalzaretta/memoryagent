using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Core Integration
/// </summary>
public partial class AGUIPatternDetector
{
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
}
