using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Multi-Agent Orchestration Patterns
/// </summary>
public partial class AgentFrameworkPatternDetector
{
    #region Multi-Agent Orchestration Patterns

    private List<CodePattern> DetectSequentialOrchestration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for sequential agent invocations
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            
            // Detect sequential await pattern for multiple agents
            var awaitCount = Regex.Matches(methodBody, @"await.*Agent.*\.InvokeAsync").Count;
            
            if (awaitCount >= 2)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Sequential_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Sequential Agent Orchestration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Sequential multi-agent pattern: agents process tasks one after another in a defined order",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Sequential",
                        ["agent_count"] = awaitCount
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectConcurrentOrchestration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect Task.WhenAll for concurrent agent execution
            if (invocationText.Contains("Task.WhenAll") && invocationText.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MultiAgent_Concurrent",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Concurrent Agent Orchestration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Concurrent multi-agent pattern: multiple agents execute in parallel for faster processing",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Concurrent/Parallel"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHandoffPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for handoff keywords in method/variable names
        var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifiers)
        {
            if (identifier.Identifier.Text.Contains("Handoff", StringComparison.OrdinalIgnoreCase) ||
                identifier.Identifier.Text.Contains("Transfer", StringComparison.OrdinalIgnoreCase))
            {
                var lineNumber = GetLineNumber(root, identifier, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MultiAgent_Handoff",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Agent Handoff Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(identifier, sourceCode, 5),
                    bestPractice: "Handoff pattern: one agent transfers control to another specialized agent based on task requirements",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Handoff/Transfer"
                    }
                ));
                break; // Only report once per file
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMagenticPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Magentic pattern typically involves dynamic agent selection based on task/query
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            
            // Look for conditional agent selection logic
            if ((methodBody.Contains("switch") || methodBody.Contains("if")) &&
                methodBody.Contains("Agent") && 
                (methodBody.Contains("Select") || methodBody.Contains("Route")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Magentic_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Magentic Routing Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Magentic pattern: dynamically route tasks to the most appropriate agent based on task characteristics",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.7f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Magentic/Dynamic Routing"
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSupervisorPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            if (classDecl.Identifier.Text.Contains("Supervisor") && 
                classDecl.ToString().Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Supervisor_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Supervisor Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Supervisor pattern: a manager agent orchestrates and delegates work to worker agents, monitoring progress and handling failures",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Supervisor/Manager",
                        ["use_case"] = "Task delegation, progress monitoring"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHierarchicalAgents(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            var fields = classDecl.DescendantNodes().OfType<FieldDeclarationSyntax>();
            var hasParentAgent = fields.Any(f => f.Declaration.Type.ToString().Contains("Agent") && 
                                                (f.Declaration.Variables.Any(v => v.Identifier.Text.Contains("parent") || 
                                                                                 v.Identifier.Text.Contains("manager"))));

            if (hasParentAgent)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Hierarchical_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Hierarchical Agent Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Hierarchical agent pattern: organizing agents in parent-child relationships for complex task decomposition and delegation",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Hierarchical",
                        ["structure"] = "Parent-Child Agent Tree"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSwarmIntelligence(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("Swarm") || methodBody.Contains("Collective")) && 
                methodBody.Contains("Agent") && 
                Regex.IsMatch(methodBody, @"Task\.WhenAll|Parallel\.ForEach"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Swarm_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Swarm Intelligence Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Swarm intelligence: many simple agents collaborate to solve complex problems through emergent behavior and collective decision-making",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Swarm/Collective Intelligence",
                        ["characteristic"] = "Emergent behavior from simple agents"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectConsensusPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("Consensus") || methodBody.Contains("Voting") || methodBody.Contains("Majority")) && 
                methodBody.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Consensus_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Consensus Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Consensus pattern: multiple agents independently process a task and vote/agree on the final result for improved accuracy and reliability",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Consensus/Voting",
                        ["benefit"] = "Improved accuracy through agreement"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectDebatePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("Debate") || methodBody.Contains("Argument") || methodBody.Contains("Challenge")) && 
                methodBody.Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MultiAgent_Debate_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Debate Pattern",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Debate pattern: agents take opposing viewpoints and debate to explore different perspectives, leading to more robust solutions",
                    azureUrl: AgentFrameworkUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["orchestration_pattern"] = "Debate/Adversarial",
                        ["benefit"] = "Exploring multiple perspectives"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
