using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 10: Agent Lifecycle (NEW - Critical Gap Filled)
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 10: Agent Lifecycle (NEW - Critical Gap Filled)

    private List<CodePattern> DetectAgentFactory(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Agent factory for dynamic instantiation
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("AgentFactory") || className.Contains("AgentBuilder"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_AgentFactory",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AIAgents,
                    implementation: $"Agent factory pattern: {className}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Agent factory pattern enables standardized agent creation with consistent configuration, initialization, and dependency injection.",
                    azureUrl: "https://devblogs.microsoft.com/ise/multi-agent-systems-at-scale/",
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["class_name"] = className,
                        ["pattern"] = "Factory Pattern",
                        ["benefits"] = new[] { "Standardized onboarding", "Flexible instantiation", "Consistent configuration" }
                    }
                ));
                break;
            }
        }

        // Pattern: Agent builder (fluent API)
        if (sourceCode.Contains("AgentBuilder") || 
            (sourceCode.Contains("WithModel") && sourceCode.Contains("WithTools") && sourceCode.Contains("Build")))
        {
            patterns.Add(CreatePattern(
                name: "AI_AgentBuilder",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Agent builder pattern (fluent API)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Agent builder pattern detected",
                bestPractice: "Builder pattern with fluent API enables readable, testable agent configuration. Example: new AgentBuilder().WithModel(model).WithTools(tools).Build()",
                azureUrl: "https://learn.microsoft.com/en-us/semantic-kernel/",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Builder Pattern",
                    ["benefit"] = "Readable configuration"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectSelfImprovingAgent(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Self-improving agent with retraining
        if ((sourceCode.Contains("Retrain") || sourceCode.Contains("FineTune") || sourceCode.Contains("UpdateModel")) &&
            (sourceCode.Contains("performance") || sourceCode.Contains("accuracy") || sourceCode.Contains("degradation")))
        {
            patterns.Add(CreatePattern(
                name: "AI_SelfImprovingAgent",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Self-improving agent with automatic retraining",
                filePath: filePath,
                lineNumber: 1,
                content: "// Self-improving agent detected",
                bestPractice: "Self-improving agents monitor their performance, detect accuracy degradation, and trigger retraining pipelines automatically.",
                azureUrl: "https://www.shakudo.io/blog/5-agentic-ai-design-patterns-transforming-enterprise-operations-in-2025",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Self-Improvement",
                    ["capabilities"] = new[] { "Performance monitoring", "Degradation detection", "Automatic retraining" },
                    ["significance"] = "ADVANCED - Continuous improvement"
                }
            ));
        }

        // Pattern: Performance monitoring for agents
        if ((sourceCode.Contains("MetricsCollector") || sourceCode.Contains("PerformanceMonitor")) &&
            sourceCode.Contains("agent"))
        {
            patterns.Add(CreatePattern(
                name: "AI_AgentPerformanceMonitoring",
                type: PatternType.AgentLightning,
                category: PatternCategory.Operational,
                implementation: "Agent performance monitoring",
                filePath: filePath,
                lineNumber: 1,
                content: "// Agent performance monitoring detected",
                bestPractice: "Monitor agent performance metrics (accuracy, latency, cost) to detect issues and optimize over time.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/ai-studio/how-to/evaluate-generative-ai-app",
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Performance Monitoring",
                    ["metrics"] = new[] { "Accuracy", "Latency", "Cost", "Success rate" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
