using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - Thread ID Persistence
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - Thread ID Persistence

    private List<CodePattern> DetectThreadPersistence(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Thread ID storage/persistence
        if ((sourceCode.Contains("threadId") || sourceCode.Contains("ThreadId") || sourceCode.Contains("thread_id")) &&
            (sourceCode.Contains("Save") || sourceCode.Contains("Store") || sourceCode.Contains("Persist") || 
             sourceCode.Contains("Database") || sourceCode.Contains("Cache")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ThreadPersistence",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Thread ID persistence for conversation continuity",
                filePath: filePath,
                lineNumber: 1,
                content: "// Thread ID persistence detected",
                bestPractice: "Persist AG-UI thread IDs to maintain conversation context across sessions and reconnections.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Thread Persistence",
                    ["storage"] = new[] { "Database", "Cache", "Session" },
                    ["benefit"] = "Conversation continuity"
                }
            ));
        }

        // Pattern: Thread management service
        if ((sourceCode.Contains("ThreadManager") || sourceCode.Contains("ThreadService") || 
             sourceCode.Contains("ConversationManager")) &&
            (sourceCode.Contains("AG-UI") || sourceCode.Contains("agent")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_ThreadManagementService",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Thread management service for AG-UI",
                filePath: filePath,
                lineNumber: 1,
                content: "// Thread management service detected",
                bestPractice: "Centralize thread management for AG-UI to handle creation, storage, and cleanup of conversation threads.",
                azureUrl: AGUIOverviewUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Thread Management",
                    ["responsibilities"] = new[] { "Create", "Store", "Retrieve", "Cleanup" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
