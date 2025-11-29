using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// AutoGen Patterns
/// </summary>
public partial class AgentFrameworkPatternDetector
{
    #region AutoGen Patterns

    private List<CodePattern> DetectAutoGenConversableAgents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect AutoGen ConversableAgent
            if (invocationText.Contains("ConversableAgent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_ConversableAgent",
                    type: PatternType.AutoGen,
                    category: PatternCategory.AIAgents,
                    implementation: "AutoGen ConversableAgent",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen is superseded by Agent Framework. Consider migrating for better enterprise features.",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false, // Legacy pattern
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Agent Pattern",
                        ["recommendation"] = "Migrate to Agent Framework ChatCompletionAgent"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAutoGenGroupChat(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect AutoGen GroupChat
            if (invocationText.Contains("GroupChat") && !invocationText.Contains("AgentFramework"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_GroupChat",
                    type: PatternType.AutoGen,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "AutoGen GroupChat",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen GroupChat → Agent Framework Workflow with multi-agent orchestration patterns",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Multi-Agent Pattern",
                        ["recommendation"] = "Migrate to Agent Framework Workflows"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAutoGenUserProxy(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect AutoGen UserProxyAgent
            if (invocationText.Contains("UserProxyAgent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_UserProxyAgent",
                    type: PatternType.AutoGen,
                    category: PatternCategory.HumanInLoop,
                    implementation: "AutoGen UserProxyAgent",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: UserProxyAgent → Agent Framework request/response patterns for human-in-the-loop",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Human-in-Loop Pattern"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectReplyFunctions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("ReplyFunction") || method.Identifier.Text.Contains("GenerateReply"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AutoGen_ReplyFunction_{method.Identifier.Text}",
                    type: PatternType.AutoGen,
                    category: PatternCategory.AIAgents,
                    implementation: "AutoGen Reply Function",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "MIGRATION RECOMMENDED: Custom reply functions in AutoGen → Agent Framework agent methods with type-safe responses",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.85f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Pattern"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectTerminationConditions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("is_termination_msg") || methodBody.Contains("TerminationCondition"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AutoGen_TerminationCondition_{method.Identifier.Text}",
                    type: PatternType.AutoGen,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "AutoGen Termination Condition",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen termination conditions → Agent Framework workflow completion logic",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.85f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Orchestration"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSpeakerSelection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("SpeakerSelection") || method.Identifier.Text.Contains("SelectSpeaker"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AutoGen_SpeakerSelection_{method.Identifier.Text}",
                    type: PatternType.AutoGen,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "AutoGen Speaker Selection Strategy",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen speaker selection → Agent Framework workflow routing logic with type-safe agent selection",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Multi-Agent Pattern"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCodeExecution(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("execute_code") || invocationText.Contains("CodeExecution"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AutoGen_CodeExecution",
                    type: PatternType.AutoGen,
                    category: PatternCategory.AIAgents,
                    implementation: "AutoGen Code Execution",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: AutoGen code execution → Agent Framework with sandboxed execution tools and MCP servers for safe code execution",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen",
                    context: context,
                    confidence: 0.9f,
                    isPositivePattern: false,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "AutoGen",
                        ["pattern_category"] = "Legacy Code Execution",
                        ["security_concern"] = "Ensure sandboxed execution"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
