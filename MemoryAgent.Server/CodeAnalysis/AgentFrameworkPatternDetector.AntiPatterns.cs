using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Anti-Patterns
/// </summary>
public partial class AgentFrameworkPatternDetector
{
    #region Anti-Patterns

    private List<CodePattern> DetectAntiPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Anti-Pattern 1: Using agents for structured tasks (should use functions)
        var comments = root.DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) || t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            .Select(t => t.ToString().ToLowerInvariant());

        var hasAgentUsage = root.ToString().Contains("Agent");
        var hasStructuredTask = comments.Any(c => c.Contains("well-defined") || c.Contains("predefined") || c.Contains("fixed steps"));

        if (hasAgentUsage && hasStructuredTask)
        {
            patterns.Add(CreatePattern(
                name: "AntiPattern_AgentForStructuredTask",
                type: PatternType.AgentFramework,
                category: PatternCategory.AntiPatterns,
                implementation: "Using AI agent for well-defined task",
                filePath: filePath,
                lineNumber: 1,
                content: "AI agent used for structured/predefined task",
                bestPractice: "ANTI-PATTERN: Don't use AI agents for well-defined tasks with fixed steps. Use regular functions instead. Agents add latency, cost, and uncertainty.",
                azureUrl: AgentFrameworkUrl,
                context: context,
                confidence: 0.6f,
                isPositivePattern: false,
                metadata: new Dictionary<string, object>
                {
                    ["anti_pattern"] = "Agent for Structured Task",
                    ["recommendation"] = "Use functions for well-defined tasks, agents for dynamic/exploratory tasks"
                }
            ));
        }

        // Anti-Pattern 2: Single agent with too many tools (>20)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            var toolRegistrations = Regex.Matches(methodBody, @"AddTool|RegisterFunction|AddPlugin").Count;

            if (toolRegistrations > 20)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AntiPattern_TooManyTools",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AntiPatterns,
                    implementation: $"Single agent with {toolRegistrations} tools",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 5),
                    bestPractice: "ANTI-PATTERN: Single agent with >20 tools becomes unmanageable. Use multi-agent workflow with specialized agents instead.",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.8f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["anti_pattern"] = "Too Many Tools",
                        ["tool_count"] = toolRegistrations,
                        ["recommendation"] = "Split into multiple specialized agents in a workflow"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
