using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - Streaming Response Handlers
/// </summary>
public partial class AGUIPatternDetector
{
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
}
