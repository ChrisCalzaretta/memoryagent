using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// WebSocket Transport
/// </summary>
public partial class AGUIPatternDetector
{
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
}
