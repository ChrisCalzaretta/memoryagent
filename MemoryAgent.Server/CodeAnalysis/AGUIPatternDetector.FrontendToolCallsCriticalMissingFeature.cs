using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Frontend Tool Calls (Critical Missing Feature)
/// </summary>
public partial class AGUIPatternDetector
{
    #region Frontend Tool Calls (Critical Missing Feature)

    private List<CodePattern> DetectFrontendToolCalls(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Frontend tools executed on CLIENT-SIDE
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();

        // Look for frontend tool registration or client-side tool execution
        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            if ((methodText.Contains("FrontendTool") || 
                 methodText.Contains("ClientTool") ||
                 methodText.Contains("ClientSideTool") ||
                 (methodText.Contains("Tool") && methodText.Contains("client"))) &&
                !methodText.Contains("backend") &&
                !methodText.Contains("server"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_FrontendToolCalls",
                    type: PatternType.AGUI,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Frontend Tool Calls - Client-side execution",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 8),
                    bestPractice: "Frontend tools execute in browser/client, accessing client-specific data (GPS, camera, localStorage). Use for client-side sensors and APIs.",
                    azureUrl: "https://learn.microsoft.com/cs-cz/agent-framework/integrations/ag-ui/frontend-tools",
                    context: context,
                    confidence: 0.87f,
                    metadata: new Dictionary<string, object>
                    {
                        ["execution_location"] = "Client-side",
                        ["capabilities"] = new[] { "GPS", "Camera", "Mic", "localStorage", "Browser APIs" },
                        ["use_cases"] = new[] { "Client sensors", "User-specific context", "Browser features" }
                    }
                ));
                break;
            }
        }

        // Look for tool registry patterns (frontend tool mapping)
        foreach (var prop in properties)
        {
            var propText = prop.ToString();
            
            if ((propText.Contains("toolRegistry") || 
                 propText.Contains("ToolRegistry") ||
                 propText.Contains("frontendTools")) &&
                propText.Contains("Dictionary"))
            {
                var lineNumber = GetLineNumber(root, prop, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_FrontendToolRegistry",
                    type: PatternType.AGUI,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Frontend tool registry mapping",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(prop, sourceCode, 5),
                    bestPractice: "Maintain registry mapping tool names to client functions for frontend tool execution.",
                    azureUrl: "https://learn.microsoft.com/cs-cz/agent-framework/integrations/ag-ui/frontend-tools",
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Tool Registry",
                        ["location"] = "Frontend"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
