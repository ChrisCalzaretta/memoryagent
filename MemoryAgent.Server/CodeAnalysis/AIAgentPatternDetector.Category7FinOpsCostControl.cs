using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 7: FinOps / Cost Control
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 7: FinOps / Cost Control

    private List<CodePattern> DetectTokenMetering(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Usage tracking
        if (sourceCode.Contains("Usage") && 
            (sourceCode.Contains("TotalTokens") || sourceCode.Contains("PromptTokens") || sourceCode.Contains("CompletionTokens")))
        {
            patterns.Add(CreatePattern(
                name: "AI_TokenMetering",
                type: PatternType.AgentLightning,
                category: PatternCategory.Cost,
                implementation: "Token usage metering",
                filePath: filePath,
                lineNumber: 1,
                content: "// Token metering detected",
                bestPractice: "Track token usage per user, agent, and project for cost attribution and chargeback.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/manage-costs",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Token Metering",
                    ["metrics"] = new[] { "Prompt tokens", "Completion tokens", "Total tokens", "Cost" },
                    ["significance"] = "HIGH - FinOps requirement"
                }
            ));
        }

        // Pattern: Cost calculation
        if (sourceCode.Contains("cost") && sourceCode.Contains("token"))
        {
            patterns.Add(CreatePattern(
                name: "AI_CostCalculation",
                type: PatternType.AgentLightning,
                category: PatternCategory.Cost,
                implementation: "Cost calculation from token usage",
                filePath: filePath,
                lineNumber: 1,
                content: "// Cost calculation detected",
                bestPractice: "Calculate costs from token usage for billing and budget tracking. Update pricing regularly as Azure OpenAI prices change.",
                azureUrl: "https://azure.microsoft.com/en-us/pricing/details/cognitive-services/openai-service/",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Cost Calculation"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectCostBudgetGuardrail(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Budget checks
        if ((sourceCode.Contains("monthlyBudget") || sourceCode.Contains("budget")) &&
            (sourceCode.Contains("currentCost") || sourceCode.Contains("spend")) &&
            (sourceCode.Contains(">") || sourceCode.Contains("Exceeded")))
        {
            patterns.Add(CreatePattern(
                name: "AI_CostBudgetGuardrail",
                type: PatternType.AgentLightning,
                category: PatternCategory.Cost,
                implementation: "Cost budget guardrail",
                filePath: filePath,
                lineNumber: 1,
                content: "// Budget guardrail detected",
                bestPractice: "Implement budget guardrails with alerts (80%, 90%) and hard limits (100%). Auto-disable agents that exceed budget.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/cost-mgt-alerts-monitor-usage-spending",
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Budget Guardrail",
                    ["best_practices"] = new[] { "Soft alerts at 80%", "Hard limit at 100%", "Auto-disable on breach" },
                    ["significance"] = "CRITICAL - Prevents runaway costs"
                }
            ));
        }

        // Pattern: Auto-disable logic
        if (sourceCode.Contains("Disable") && (sourceCode.Contains("budget") || sourceCode.Contains("cost")))
        {
            patterns.Add(CreatePattern(
                name: "AI_AutoDisableOnBudget",
                type: PatternType.AgentLightning,
                category: PatternCategory.Cost,
                implementation: "Auto-disable on budget exceeded",
                filePath: filePath,
                lineNumber: 1,
                content: "// Auto-disable on budget detected",
                bestPractice: "Auto-disable agents when budget is exceeded to prevent cost overruns. Notify stakeholders and require manual re-enable.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/cost-management-billing/",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Auto-Disable",
                    ["benefit"] = "Prevents runaway costs"
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
