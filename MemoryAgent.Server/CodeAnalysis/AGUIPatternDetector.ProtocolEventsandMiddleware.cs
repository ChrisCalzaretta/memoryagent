using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Protocol Events and Middleware
/// </summary>
public partial class AGUIPatternDetector
{
    #region Protocol Events and Middleware

    private List<CodePattern> DetectProtocolEvents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AG-UI protocol event handling
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var enumDecl in enums)
        {
            var enumText = enumDecl.ToString();
            
            if (enumText.Contains("EventType") && 
                (enumText.Contains("TEXT_MESSAGE") || 
                 enumText.Contains("TOOL_CALL") ||
                 enumText.Contains("APPROVAL_REQUEST")))
            {
                var lineNumber = GetLineNumber(root, enumDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_ProtocolEvents",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: "AG-UI Protocol event types",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(enumDecl, sourceCode, 10),
                    bestPractice: "Standardized AG-UI protocol events for reliable agent-client communication.",
                    azureUrl: AGUIOverviewUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["protocol"] = "AG-UI",
                        ["event_types"] = new[] { "TEXT_MESSAGE_CONTENT", "TOOL_CALL_START", "TOOL_CALL_END", "APPROVAL_REQUEST" }
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMiddlewarePatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AG-UI middleware for approvals, state, etc.
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var cls in classes)
        {
            var baseList = cls.BaseList?.ToString() ?? "";
            
            if (baseList.Contains("IAgentMiddleware") || 
                baseList.Contains("AgentMiddleware") ||
                (cls.Identifier.Text.Contains("Middleware") && cls.ToString().Contains("agent")))
            {
                var lineNumber = GetLineNumber(root, cls, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_Middleware",
                    type: PatternType.AGUI,
                    category: PatternCategory.Interceptors,
                    implementation: "AG-UI agent middleware pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(cls, sourceCode, 15),
                    bestPractice: "Middleware pattern for approvals, state management, and custom logic in AG-UI pipeline.",
                    azureUrl: AGUIOverviewUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Middleware/Interceptor",
                        ["use_cases"] = new[] { "Approval workflows", "State sync", "Logging", "Error handling" }
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    
    #endregion
}
