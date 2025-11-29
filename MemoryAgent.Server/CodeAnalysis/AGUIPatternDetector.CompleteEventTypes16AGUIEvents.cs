using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Complete Event Types (16 AG-UI Events)
/// </summary>
public partial class AGUIPatternDetector
{
    #region Complete Event Types (16 AG-UI Events)

    private List<CodePattern> DetectCompleteEventTypes(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Check for comprehensive AG-UI event type enum or constants
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();

        foreach (var enumDecl in enums)
        {
            var enumText = enumDecl.ToString();
            
            // Check for comprehensive event type coverage
            int eventCount = 0;
            var detectedEvents = new List<string>();
            
            if (enumText.Contains("TEXT_MESSAGE_START")) { eventCount++; detectedEvents.Add("TEXT_MESSAGE_START"); }
            if (enumText.Contains("TEXT_MESSAGE_DELTA")) { eventCount++; detectedEvents.Add("TEXT_MESSAGE_DELTA"); }
            if (enumText.Contains("TEXT_MESSAGE_END")) { eventCount++; detectedEvents.Add("TEXT_MESSAGE_END"); }
            if (enumText.Contains("TOOL_CALL_START")) { eventCount++; detectedEvents.Add("TOOL_CALL_START"); }
            if (enumText.Contains("TOOL_CALL_DELTA")) { eventCount++; detectedEvents.Add("TOOL_CALL_DELTA"); }
            if (enumText.Contains("TOOL_CALL_END")) { eventCount++; detectedEvents.Add("TOOL_CALL_END"); }
            if (enumText.Contains("STATE_SNAPSHOT")) { eventCount++; detectedEvents.Add("STATE_SNAPSHOT"); }
            if (enumText.Contains("STATE_DELTA")) { eventCount++; detectedEvents.Add("STATE_DELTA"); }
            if (enumText.Contains("APPROVAL_REQUEST")) { eventCount++; detectedEvents.Add("APPROVAL_REQUEST"); }
            if (enumText.Contains("APPROVAL_RESPONSE")) { eventCount++; detectedEvents.Add("APPROVAL_RESPONSE"); }
            if (enumText.Contains("RUN_STARTED")) { eventCount++; detectedEvents.Add("RUN_STARTED"); }
            if (enumText.Contains("RUN_COMPLETED")) { eventCount++; detectedEvents.Add("RUN_COMPLETED"); }
            if (enumText.Contains("ERROR")) { eventCount++; detectedEvents.Add("ERROR"); }
            if (enumText.Contains("CANCEL")) { eventCount++; detectedEvents.Add("CANCEL"); }
            if (enumText.Contains("RESUME")) { eventCount++; detectedEvents.Add("RESUME"); }
            
            if (eventCount >= 8) // At least half of the 16 events
            {
                var lineNumber = GetLineNumber(root, enumDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AGUI_CompleteEventTypes",
                    type: PatternType.AGUI,
                    category: PatternCategory.AIAgents,
                    implementation: $"AG-UI event types ({eventCount}/16 detected)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(enumDecl, sourceCode, 15),
                    bestPractice: $"Comprehensive AG-UI event type system. Detected {eventCount} of 16 standardized events: {string.Join(", ", detectedEvents.Take(5))}...",
                    azureUrl: "https://docs.ag-ui.com/concepts/architecture",
                    context: context,
                    confidence: eventCount >= 12 ? 0.95f : 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["event_count"] = eventCount,
                        ["detected_events"] = detectedEvents,
                        ["coverage"] = $"{eventCount * 100 / 16}%"
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    
    #endregion
}
