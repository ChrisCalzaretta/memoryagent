using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Feature 7: Predictive State Updates
/// </summary>
public partial class AGUIPatternDetector
{
    #region AG-UI Feature 7: Predictive State Updates

    private List<CodePattern> DetectPredictiveStateUpdates(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Streaming tool arguments as optimistic state updates
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Look for optimistic update patterns
            if ((methodText.Contains("optimistic") || 
                 methodText.Contains("Optimistic") ||
                 methodText.Contains("predictive") ||
                 methodText.Contains("Predictive")) &&
                (methodText.Contains("update") || methodText.Contains("state")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_PredictiveStateUpdates",
                    type: PatternType.AGUI,
                    category: PatternCategory.Performance,
                    implementation: "AG-UI Feature 7: Predictive State Updates",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Stream tool arguments as optimistic state updates for instant UI responsiveness before server confirmation.",
                    azureUrl: AGUIStateUrl,
                    context: context,
                    confidence: 0.84f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "7 - Predictive State Updates",
                        ["benefits"] = new[] { "Instant UI feedback", "Perceived performance", "Better UX" },
                        ["pattern"] = "Optimistic UI"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
