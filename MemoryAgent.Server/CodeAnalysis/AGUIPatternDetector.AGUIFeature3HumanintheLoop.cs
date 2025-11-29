using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AG-UI Feature 3: Human in the Loop
/// </summary>
public partial class AGUIPatternDetector
{
    #region AG-UI Feature 3: Human in the Loop

    private List<CodePattern> DetectHumanInLoop(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: ApprovalRequiredAIFunction or approval middleware
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var cls in classes)
        {
            var classText = cls.ToString();
            
            if (classText.Contains("ApprovalRequired") || 
                classText.Contains("RequiresApproval") ||
                (classText.Contains("approval") && classText.Contains("middleware")))
            {
                var lineNumber = GetLineNumber(root, cls, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_HumanInLoop",
                    type: PatternType.AGUI,
                    category: PatternCategory.HumanInLoop,
                    implementation: "AG-UI Feature 3: Human-in-the-Loop",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(cls, sourceCode, 15),
                    bestPractice: "Approval workflows where users confirm agent actions before execution. Critical for sensitive operations.",
                    azureUrl: AGUIHumanLoopUrl,
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "3 - Human-in-the-Loop",
                        ["use_cases"] = new[] { "Sensitive operations", "Financial transactions", "Data deletion", "External API calls" },
                        ["security_benefit"] = "Prevents unauthorized actions"
                    }
                ));
                break;
            }
        }

        // Also check for approval request/response handling
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();
            
            if (invocationText.Contains("RequestApproval") || 
                invocationText.Contains("WaitForApproval") ||
                invocationText.Contains("HandleApproval"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_ApprovalHandling",
                    type: PatternType.AGUI,
                    category: PatternCategory.HumanInLoop,
                    implementation: "Approval request/response handling",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Explicit approval handling ensures user consent for agent actions.",
                    azureUrl: AGUIHumanLoopUrl,
                    context: context,
                    confidence: 0.88f,
                    metadata: new Dictionary<string, object>
                    {
                        ["agui_feature"] = "3 - Human-in-the-Loop",
                        ["pattern"] = "Approval Request/Response"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
