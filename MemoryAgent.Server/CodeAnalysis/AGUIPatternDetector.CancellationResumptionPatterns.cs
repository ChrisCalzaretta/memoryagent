using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Cancellation & Resumption Patterns
/// </summary>
public partial class AGUIPatternDetector
{
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
}
