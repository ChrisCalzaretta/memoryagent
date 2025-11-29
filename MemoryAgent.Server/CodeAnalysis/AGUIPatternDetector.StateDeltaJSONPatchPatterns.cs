using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// State Delta / JSON Patch Patterns
/// </summary>
public partial class AGUIPatternDetector
{
    #region State Delta / JSON Patch Patterns

    private List<CodePattern> DetectStateDelta(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: JSON Patch usage for state deltas
        if (sourceCode.Contains("JsonPatch") || sourceCode.Contains("json-patch") ||
            sourceCode.Contains("PatchDocument"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_StateDelta_JsonPatch",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "JSON Patch for state deltas",
                filePath: filePath,
                lineNumber: 1,
                content: "// JSON Patch state delta detected",
                bestPractice: "Use JSON Patch format for incremental state updates. More efficient than full state snapshots for large states.",
                azureUrl: "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "State Delta",
                    ["format"] = "JSON Patch",
                    ["benefits"] = new[] { "Efficient updates", "Reduced bandwidth", "Event sourcing" }
                }
            ));
        }

        // Pattern: Event-sourced state management
        if (sourceCode.Contains("event-sourced") || sourceCode.Contains("EventSourced") ||
            (sourceCode.Contains("event") && sourceCode.Contains("diff")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_EventSourced_State",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Event-sourced state with diffs",
                filePath: filePath,
                lineNumber: 1,
                content: "// Event-sourced state management detected",
                bestPractice: "AG-UI supports event-sourced state diffs for collaborative workflows and state reconstruction.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Event Sourcing",
                    ["benefits"] = new[] { "State history", "Replay capability", "Collaborative editing" }
                }
            ));
        }

        // Pattern: Conflict resolution
        if (sourceCode.Contains("conflict") && (sourceCode.Contains("resolution") || sourceCode.Contains("merge")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ConflictResolution",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "State conflict resolution",
                filePath: filePath,
                lineNumber: 1,
                content: "// Conflict resolution detected",
                bestPractice: "AG-UI shared state includes conflict resolution for concurrent client/server updates.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Conflict Resolution",
                    ["scenarios"] = new[] { "Multi-client", "Race conditions", "Network delays" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
