using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Feature 5: Tool-based Generative UI
/// </summary>
public partial class AGUIPatternDetector
{
    #region AG-UI Feature 5: Tool-based Generative UI

    private List<CodePattern> DetectToolBasedGenerativeUI(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Custom UI component rendering based on tool calls
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Look for UI rendering patterns based on tool results
            if ((methodText.Contains("RenderUI") || 
                 methodText.Contains("GenerateUI") ||
                 methodText.Contains("UIComponent")) &&
                (methodText.Contains("ToolResult") || 
                 methodText.Contains("tool") ||
                 methodText.Contains("function")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_ToolBasedGenerativeUI",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "AG-UI Feature 5: Tool-based Generative UI",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Custom UI components rendered based on tool call results. Dynamic, context-aware user interfaces.",
                    azureUrl: AGUIGenUIUrl,
                    context: context,
                    confidence: 0.82f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "5 - Tool-based Generative UI",
                        ["capabilities"] = new[] { "Dynamic UI", "Tool-driven rendering", "Context-aware components" },
                        ["examples"] = new[] { "Charts from data", "Maps from locations", "Forms from schemas" }
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
