using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 9: Advanced Multi-Agent Patterns (NEW - Critical Gap Filled)
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 9: Advanced Multi-Agent Patterns (NEW - Critical Gap Filled)

    private List<CodePattern> DetectGroupChatPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: GroupChat (AutoGen-style)
        if (sourceCode.Contains("GroupChat") || sourceCode.Contains("ConversableAgent"))
        {
            patterns.Add(CreatePattern(
                name: "AI_GroupChatOrchestration",
                type: PatternType.AgentLightning,
                category: PatternCategory.MultiAgentOrchestration,
                implementation: "Group chat multi-agent orchestration (AutoGen pattern)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Group chat orchestration detected",
                bestPractice: "Group chat enables multiple agents to communicate in a shared environment. AutoGen's pattern allows agents to self-organize and collaborate.",
                azureUrl: "https://microsoft.github.io/autogen/docs/tutorial/conversation-patterns",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Group Chat",
                    ["framework"] = "AutoGen",
                    ["significance"] = "ADVANCED - Multi-agent collaboration",
                    ["benefits"] = new[] { "Self-organization", "Emergent behavior", "Flexible collaboration" }
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectSequentialOrchestration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Sequential agent execution
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            
            // Look for sequential agent calls
            var agentCallPattern = new Regex(@"await\s+\w*[Aa]gent\w*\..*Async");
            var matches = agentCallPattern.Matches(methodBody);
            
            if (matches.Count >= 2)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_SequentialOrchestration",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Sequential multi-agent orchestration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Sequential orchestration chains agent outputs. Agent A's result feeds into Agent B, creating a pipeline.",
                    azureUrl: "https://learn.microsoft.com/en-us/training/modules/agent-orchestration-patterns/",
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Sequential Orchestration",
                        ["agent_calls"] = matches.Count,
                        ["use_case"] = "Agent pipelines and workflows"
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectControlPlanePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Control plane as tool (modular tool routing)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if ((className.Contains("ControlPlane") || className.Contains("ToolRouter") || 
                 className.Contains("ToolDispatcher")) &&
                classDecl.ToString().Contains("Tool"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_ControlPlaneAsATool",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: $"Control plane as tool pattern: {className}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Control plane pattern encapsulates modular tool routing logic behind a single tool interface. Improves scalability, safety, and extensibility.",
                    azureUrl: "https://arxiv.org/abs/2505.06817",
                    context: context,
                    confidence: 0.88f,
                    metadata: new Dictionary<string, object>
                    {
                        ["class_name"] = className,
                        ["pattern"] = "Control Plane",
                        ["benefits"] = new[] { "Scalability", "Safety", "Extensibility", "Modular tool routing" }
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    
    #endregion
}
