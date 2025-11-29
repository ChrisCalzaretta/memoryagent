using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Feature 4: Agentic Generative UI
/// </summary>
public partial class AGUIPatternDetector
{
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
}
