using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Feature 6: Shared State
/// </summary>
public partial class AGUIPatternDetector
{
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
}
