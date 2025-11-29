using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Anti-Patterns
/// </summary>
public partial class AGUIPatternDetector
{
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
}
