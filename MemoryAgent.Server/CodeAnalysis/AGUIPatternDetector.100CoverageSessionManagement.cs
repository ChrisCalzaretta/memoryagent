using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// 100% Coverage - Session Management
/// </summary>
public partial class AGUIPatternDetector
{
    #region 100% Coverage - Session Management

    private List<CodePattern> DetectSessionManagement(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Session state management
        if ((sourceCode.Contains("Session") || sourceCode.Contains("ISession")) &&
            (sourceCode.Contains("threadId") || sourceCode.Contains("agent") || sourceCode.Contains("conversation")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_SessionManagement",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Session management for AG-UI conversations",
                filePath: filePath,
                lineNumber: 1,
                content: "// Session management detected",
                bestPractice: "Use session management to associate AG-UI thread IDs with user sessions for conversation continuity.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state",
                context: context,
                confidence: 0.82f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Session Management",
                    ["stored_data"] = new[] { "Thread ID", "User context", "Conversation history" }
                }
            ));
        }

        // Pattern: Distributed session with Redis
        if ((sourceCode.Contains("Redis") || sourceCode.Contains("DistributedCache")) &&
            (sourceCode.Contains("session") || sourceCode.Contains("thread")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_DistributedSession",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Distributed session storage with Redis",
                filePath: filePath,
                lineNumber: 1,
                content: "// Distributed session detected",
                bestPractice: "Use Redis for distributed session storage to enable AG-UI scalability across multiple server instances.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/",
                context: context,
                confidence: 0.93f,
                metadata: new Dictionary<string, object>
                {
                    ["storage"] = "Redis",
                    ["benefits"] = new[] { "Scalability", "High availability", "Fast access" }
                }
            ));
        }

        // Pattern: Session timeout handling
        if ((sourceCode.Contains("SessionTimeout") || sourceCode.Contains("IdleTimeout")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("conversation")))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_SessionTimeout",
                type: PatternType.AGUI,
                category: PatternCategory.StateManagement,
                implementation: "Session timeout for inactive AG-UI conversations",
                filePath: filePath,
                lineNumber: 1,
                content: "// Session timeout detected",
                bestPractice: "Implement session timeouts for AG-UI conversations to clean up inactive threads and free resources.",
                azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state",
                context: context,
                confidence: 0.80f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Session Timeout",
                    ["cleanup"] = "Inactive thread removal"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
