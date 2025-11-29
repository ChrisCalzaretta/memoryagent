using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Feature 1: Agentic Chat
/// </summary>
public partial class AGUIPatternDetector
{
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
}
