using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 8: Observability & Evaluation (NEW - Critical Gap Filled)
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 8: Observability & Evaluation (NEW - Critical Gap Filled)

    private List<CodePattern> DetectAgentTracing(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: OpenTelemetry integration
        if (sourceCode.Contains("OpenTelemetry") || sourceCode.Contains("ActivitySource") ||
            sourceCode.Contains("Tracer"))
        {
            patterns.Add(CreatePattern(
                name: "AI_AgentTracing",
                type: PatternType.AgentLightning,
                category: PatternCategory.Operational,
                implementation: "OpenTelemetry agent tracing",
                filePath: filePath,
                lineNumber: 1,
                content: "// Agent tracing detected",
                bestPractice: "Implement OpenTelemetry for end-to-end agent tracing. Track LLM calls, tool executions, and decision flows for debugging and optimization.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-overview",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Observability - Tracing",
                    ["framework"] = "OpenTelemetry",
                    ["significance"] = "CRITICAL - Production observability requirement",
                    ["tracks"] = new[] { "LLM calls", "Tool executions", "Decision flows", "Latency" }
                }
            ));
        }

        // Pattern: Agent-specific logging with correlation
        if ((sourceCode.Contains("_logger") || sourceCode.Contains("ILogger")) &&
            (sourceCode.Contains("correlationId") || sourceCode.Contains("traceId") || sourceCode.Contains("spanId")))
        {
            patterns.Add(CreatePattern(
                name: "AI_CorrelatedLogging",
                type: PatternType.AgentLightning,
                category: PatternCategory.Operational,
                implementation: "Correlated agent logging",
                filePath: filePath,
                lineNumber: 1,
                content: "// Correlated logging detected",
                bestPractice: "Log agent activities with correlation IDs to trace multi-step workflows across LLM calls, tool invocations, and retries.",
                azureUrl: "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging",
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Observability - Logging",
                    ["benefit"] = "End-to-end request tracing"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentEvaluation(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Eval harness / test sets
        if ((sourceCode.Contains("EvaluationDataset") || sourceCode.Contains("TestSet") || 
             sourceCode.Contains("GroundTruth")) &&
            (sourceCode.Contains("agent") || sourceCode.Contains("Agent")))
        {
            patterns.Add(CreatePattern(
                name: "AI_AgentEvalHarness",
                type: PatternType.AgentLightning,
                category: PatternCategory.Operational,
                implementation: "Agent evaluation harness with test datasets",
                filePath: filePath,
                lineNumber: 1,
                content: "// Agent evaluation harness detected",
                bestPractice: "Use evaluation datasets to measure agent quality, accuracy, and consistency. Track metrics over time to detect regressions.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/ai-studio/how-to/evaluate-generative-ai-app",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Agent Evaluation",
                    ["metrics"] = new[] { "Accuracy", "Consistency", "Latency", "Cost per task" },
                    ["significance"] = "HIGH - Quality assurance"
                }
            ));
        }

        // Pattern: A/B testing for agents
        if ((sourceCode.Contains("ABTest") || sourceCode.Contains("Experiment")) &&
            sourceCode.Contains("variant"))
        {
            patterns.Add(CreatePattern(
                name: "AI_AgentABTesting",
                type: PatternType.AgentLightning,
                category: PatternCategory.Operational,
                implementation: "A/B testing for agent variants",
                filePath: filePath,
                lineNumber: 1,
                content: "// Agent A/B testing detected",
                bestPractice: "A/B test different agent configurations (prompts, models, parameters) to optimize for quality and cost.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/ai-studio/",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Experimentation",
                    ["use_case"] = "Optimize prompts and configurations"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
