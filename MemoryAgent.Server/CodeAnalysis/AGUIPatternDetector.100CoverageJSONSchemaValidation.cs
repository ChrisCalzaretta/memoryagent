using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - JSON Schema Validation
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - JSON Schema Validation

    private List<CodePattern> DetectJsonSchemaValidation(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: JSON Schema for state validation
        if ((sourceCode.Contains("JsonSchema") || sourceCode.Contains("jsonschema")) &&
            (sourceCode.Contains("state") || sourceCode.Contains("State")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_JsonSchemaValidation",
                type: PatternType.AGUI,
                category: PatternCategory.Reliability,
                implementation: "JSON Schema validation for AG-UI state",
                filePath: filePath,
                lineNumber: 1,
                content: "// JSON Schema validation detected",
                bestPractice: "Define JSON Schemas for AG-UI shared state to ensure type safety and validate state transitions.",
                azureUrl: "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management",
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "JSON Schema",
                    ["purpose"] = "State validation"
                }
            ));
        }

        // Pattern: ChatResponseFormat.ForJsonSchema
        if (sourceCode.Contains("ChatResponseFormat") && sourceCode.Contains("ForJsonSchema"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_TypedStateSchema",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Typed state schema with ChatResponseFormat",
                filePath: filePath,
                lineNumber: 1,
                content: "// Typed state schema detected",
                bestPractice: "Use ChatResponseFormat.ForJsonSchema<T>() to enforce structured state updates in AG-UI.",
                azureUrl: "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["api"] = "ChatResponseFormat.ForJsonSchema",
                    ["benefit"] = "Type-safe state"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
