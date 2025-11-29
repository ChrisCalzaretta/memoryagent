using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 4: Planning, Autonomy & Loops
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 4: Planning, Autonomy & Loops

    private List<CodePattern> DetectTaskPlanner(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Plan/Step classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className == "Plan" || className.Contains("Planner") || 
                className == "Step" || className.Contains("TaskList") ||
                className.Contains("Subtask"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_TaskPlanner",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AIAgents,
                    implementation: $"Task planner: {className}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Task planning decomposes goals into steps. LLM generates plans that the agent executes sequentially.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.90f,
                    metadata: new Dictionary<string, object>
                    {
                        ["class_name"] = className,
                        ["pattern"] = "Task Planning",
                        ["significance"] = "HIGH - Enables multi-step agent reasoning"
                    }
                ));
                break;
            }
        }

        // Pattern: Semantic Kernel Planner (even if deprecated, still used)
        if (sourceCode.Contains("FunctionCallingStepwisePlanner") || 
            sourceCode.Contains("HandlebarsPlanner") ||
            sourceCode.Contains("CreatePlanAsync"))
        {
            patterns.Add(CreatePattern(
                name: "AI_SemanticKernelPlanner",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Semantic Kernel Planner usage",
                filePath: filePath,
                lineNumber: 1,
                content: "// Semantic Kernel Planner detected",
                bestPractice: "Semantic Kernel Planners (now deprecated) enabled automatic planning. Consider migrating to Agent Framework workflows.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["framework"] = "Semantic Kernel",
                    ["status"] = "Deprecated (consider migration)",
                    ["alternative"] = "Agent Framework Workflows"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectActionLoop(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: While loops with LLM calls (ReAct pattern indicator)
        var whileStatements = root.DescendantNodes().OfType<WhileStatementSyntax>();
        
        foreach (var whileStmt in whileStatements)
        {
            var loopBody = whileStmt.Statement.ToString();
            
            // Check for LLM-like calls inside loop
            if ((loopBody.Contains("Completion") || loopBody.Contains("Chat") || 
                 loopBody.Contains("llm") || loopBody.Contains("LLM")) &&
                (loopBody.Contains("await") || loopBody.Contains("Async")))
            {
                var lineNumber = GetLineNumber(root, whileStmt, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_ActionLoop",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AIAgents,
                    implementation: "Agent action loop (potential ReAct pattern)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(whileStmt, sourceCode, 12),
                    bestPractice: "ReAct loops (Reason → Act → Observe) enable autonomous agents. Loop until goal achieved or max iterations reached.",
                    azureUrl: "https://arxiv.org/abs/2210.03629",
                    context: context,
                    confidence: 0.82f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Agent Loop",
                        ["significance"] = "CRITICAL - Distinguishes autonomous agent from single call",
                        ["pattern_type"] = "ReAct or similar"
                    }
                ));
                break; // One detection sufficient
            }
        }

        // Pattern: Do-while loops with refinement
        var doStatements = root.DescendantNodes().OfType<DoStatementSyntax>();
        
        foreach (var doStmt in doStatements)
        {
            var loopBody = doStmt.Statement.ToString();
            
            if (loopBody.Contains("refinement") || loopBody.Contains("improve") || 
                loopBody.Contains("feedback"))
            {
                var lineNumber = GetLineNumber(root, doStmt, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_IterativeRefinementLoop",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AIAgents,
                    implementation: "Iterative refinement loop",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(doStmt, sourceCode, 10),
                    bestPractice: "Iterative refinement loops improve agent outputs through multiple LLM iterations.",
                    azureUrl: AzureOpenAIPromptUrl,
                    context: context,
                    confidence: 0.78f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Iterative Refinement"
                    }
                ));
                break;
            }
        }

        // Pattern: maxIterations parameter (common in agent loops)
        if (sourceCode.Contains("maxIterations") || sourceCode.Contains("max_iterations") ||
            sourceCode.Contains("MaxSteps"))
        {
            patterns.Add(CreatePattern(
                name: "AI_MaxIterationsPattern",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Max iterations control (agent loop termination)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Max iterations detected",
                bestPractice: "Always set max iterations to prevent infinite agent loops. Typical values: 5-20 depending on task complexity.",
                azureUrl: AzureOpenAIPromptUrl,
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Loop Termination",
                    ["best_practice"] = "Prevent runaway costs"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectMultiAgentOrchestrator(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Multiple agent instances
        var agentRoles = new[] { "Planner", "Executor", "Critic", "Reviewer", "Manager", "Orchestrator" };
        var detectedRoles = agentRoles.Where(role => sourceCode.Contains(role + "Agent")).ToList();
        
        if (detectedRoles.Count >= 2)
        {
            patterns.Add(CreatePattern(
                name: "AI_MultiAgentSystem",
                type: PatternType.AgentLightning,
                category: PatternCategory.MultiAgentOrchestration,
                implementation: $"Multi-agent system: {string.Join(", ", detectedRoles)}",
                filePath: filePath,
                lineNumber: 1,
                content: $"// Multi-agent roles detected: {string.Join(", ", detectedRoles)}",
                bestPractice: "Multi-agent systems assign specialized roles (Planner, Executor, Critic) for complex tasks. AutoGen and Agent Framework support this pattern.",
                azureUrl: "https://microsoft.github.io/autogen/",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["agent_roles"] = detectedRoles,
                    ["pattern"] = "Multi-Agent Orchestration",
                    ["significance"] = "ADVANCED - Sophisticated agent system"
                }
            ));
        }

        // Pattern: AgentOrchestrator/AgentManager classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("AgentOrchestrator") || className.Contains("AgentManager") ||
                className.Contains("MultiAgentSystem"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_AgentOrchestrator",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: $"Agent orchestrator: {className}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Orchestrators coordinate multiple agents, routing tasks based on specialization.",
                    azureUrl: "https://microsoft.github.io/autogen/",
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["class_name"] = className,
                        ["pattern"] = "Orchestration"
                    }
                ));
                break;
            }
        }

        // Pattern: ConversableAgent (AutoGen)
        if (sourceCode.Contains("ConversableAgent"))
        {
            patterns.Add(CreatePattern(
                name: "AI_AutoGenConversableAgent",
                type: PatternType.AgentLightning,
                category: PatternCategory.MultiAgentOrchestration,
                implementation: "AutoGen ConversableAgent usage",
                filePath: filePath,
                lineNumber: 1,
                content: "// AutoGen ConversableAgent detected",
                bestPractice: "AutoGen's ConversableAgent enables multi-agent conversations. Agents can autonomously collaborate to solve tasks.",
                azureUrl: "https://microsoft.github.io/autogen/",
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["framework"] = "AutoGen",
                    ["pattern"] = "Multi-Agent Conversation"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectSelfReflection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Critique/Review methods
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodName = method.Identifier.Text;
            
            if (methodName.Contains("Critique") || methodName.Contains("Review") ||
                methodName.Contains("Reflect") || methodName.Contains("Improve") ||
                methodName.Contains("SelfEvaluate"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_SelfReflection",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AIAgents,
                    implementation: $"Self-reflection: {methodName}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Self-reflection enables agents to critique their own outputs and iterate for improvement. Common in advanced reasoning systems.",
                    azureUrl: AzureOpenAIPromptUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["method_name"] = methodName,
                        ["pattern"] = "Self-Reflection",
                        ["use_case"] = "Quality improvement through iteration"
                    }
                ));
                break;
            }
        }

        // Pattern: Reflection prompts
        var reflectionKeywords = new[] { "review your", "critique your", "what's wrong", "what could be improved" };
        
        if (reflectionKeywords.Any(k => sourceCode.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            patterns.Add(CreatePattern(
                name: "AI_ReflectionPrompt",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Reflection prompt pattern",
                filePath: filePath,
                lineNumber: 1,
                content: "// Reflection prompt detected",
                bestPractice: "Reflection prompts ask the LLM to critique its own output. Effective for iterative improvement.",
                azureUrl: AzureOpenAIPromptUrl,
                context: context,
                confidence: 0.78f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Reflection Prompt"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
