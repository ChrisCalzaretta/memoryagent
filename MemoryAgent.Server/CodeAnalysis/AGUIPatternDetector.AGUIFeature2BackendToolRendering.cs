using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Feature 2: Backend Tool Rendering
/// </summary>
public partial class AGUIPatternDetector
{
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
}
