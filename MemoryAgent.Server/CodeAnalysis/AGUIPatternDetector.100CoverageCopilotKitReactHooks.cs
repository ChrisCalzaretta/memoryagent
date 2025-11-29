using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - CopilotKit React Hooks
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - CopilotKit React Hooks

    private List<CodePattern> DetectCopilotKitHooks(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: useCopilotChat hook
        if (sourceCode.Contains("useCopilotChat"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_useCopilotChat",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "useCopilotChat React hook for AG-UI chat",
                filePath: filePath,
                lineNumber: 1,
                content: "// useCopilotChat hook detected",
                bestPractice: "useCopilotChat provides streaming chat with automatic tool calling via AG-UI protocol.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["hook"] = "useCopilotChat",
                    ["framework"] = "React",
                    ["capabilities"] = new[] { "Streaming chat", "Tool calling", "State management" }
                }
            ));
        }

        // Pattern: useCopilotAction hook
        if (sourceCode.Contains("useCopilotAction"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_useCopilotAction",
                type: PatternType.AGUI,
                category: PatternCategory.ToolIntegration,
                implementation: "useCopilotAction for defining frontend tools",
                filePath: filePath,
                lineNumber: 1,
                content: "// useCopilotAction hook detected",
                bestPractice: "useCopilotAction defines frontend-executable actions that AG-UI agents can call.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.97f,
                metadata: new Dictionary<string, object>
                {
                    ["hook"] = "useCopilotAction",
                    ["tool_location"] = "Frontend"
                }
            ));
        }

        // Pattern: useCopilotReadable hook
        if (sourceCode.Contains("useCopilotReadable"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_useCopilotReadable",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "useCopilotReadable for sharing app state with agent",
                filePath: filePath,
                lineNumber: 1,
                content: "// useCopilotReadable hook detected",
                bestPractice: "useCopilotReadable shares application state with AG-UI agents for context-aware responses.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.96f,
                metadata: new Dictionary<string, object>
                {
                    ["hook"] = "useCopilotReadable",
                    ["purpose"] = "State sharing"
                }
            ));
        }

        // Pattern: CopilotKit component
        if (sourceCode.Contains("<CopilotKit") || sourceCode.Contains("CopilotProvider"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_CopilotKit_Provider",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "CopilotKit provider component",
                filePath: filePath,
                lineNumber: 1,
                content: "// CopilotKit provider detected",
                bestPractice: "CopilotKit provider wraps app to enable AG-UI integration with React components.",
                azureUrl: CopilotKitUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["component"] = "CopilotKit",
                    ["type"] = "Provider"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
