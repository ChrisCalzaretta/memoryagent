using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// CopilotKit Integration
/// </summary>
public partial class AGUIPatternDetector
{
    #region CopilotKit Integration

    private List<CodePattern> DetectCopilotKitIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Check for CopilotKit usage (common client library for AG-UI)
        if (sourceCode.Contains("CopilotKit") || 
            sourceCode.Contains("useCopilotChat") ||
            sourceCode.Contains("CopilotChat"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "CopilotKit AG-UI client integration",
                filePath: filePath,
                lineNumber: 1,
                content: "// File uses CopilotKit for AG-UI client",
                bestPractice: "CopilotKit provides rich UI components for AG-UI protocol, supporting all 7 features with polished UX.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["client_library"] = "CopilotKit",
                    ["supports"] = new[] { 
                        "Streaming chat", 
                        "Tool calling", 
                        "Human-in-loop", 
                        "Generative UI", 
                        "Shared state" 
                    }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
