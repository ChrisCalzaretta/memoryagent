using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 6: Safety & Governance
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Category 6: Safety & Governance

    private List<CodePattern> DetectContentModeration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Azure Content Safety
        if (sourceCode.Contains("ContentSafetyClient") || sourceCode.Contains("AnalyzeText"))
        {
            patterns.Add(CreatePattern(
                name: "AI_ContentModeration",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Azure Content Safety integration",
                filePath: filePath,
                lineNumber: 1,
                content: "// Azure Content Safety detected",
                bestPractice: "Use Azure Content Safety to moderate harmful content (hate, violence, sexual, self-harm) before and after LLM calls.",
                azureUrl: AzureContentSafetyUrl,
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["service"] = "Azure Content Safety",
                    ["categories"] = new[] { "Hate", "Violence", "Sexual", "Self-harm" },
                    ["significance"] = "CRITICAL - Production safety requirement"
                }
            ));
        }

        // Pattern: Generic moderation calls
        if (sourceCode.Contains("Moderate") || sourceCode.Contains("moderation"))
        {
            patterns.Add(CreatePattern(
                name: "AI_GenericModeration",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Content moderation implementation",
                filePath: filePath,
                lineNumber: 1,
                content: "// Moderation detected",
                bestPractice: "Implement content moderation for user inputs and LLM outputs. Consider Azure Content Safety for comprehensive protection.",
                azureUrl: AzureContentSafetyUrl,
                context: context,
                confidence: 0.82f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Moderation"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectPIIScrubber(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Presidio (Microsoft PII library)
        if (sourceCode.Contains("Presidio") || sourceCode.Contains("RecognizePii"))
        {
            patterns.Add(CreatePattern(
                name: "AI_PIIDetection",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "PII detection and scrubbing (Presidio or Azure AI Language)",
                filePath: filePath,
                lineNumber: 1,
                content: "// PII detection detected",
                bestPractice: "Use Microsoft Presidio or Azure AI Language to detect and redact PII (emails, SSNs, phone numbers) before sending to LLMs.",
                azureUrl: "https://github.com/microsoft/presidio",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["library"] = "Presidio or Azure AI Language",
                    ["entities"] = new[] { "Email", "SSN", "Phone", "Credit Card", "Name", "Address" },
                    ["significance"] = "CRITICAL - Compliance requirement"
                }
            ));
        }

        // Pattern: Regex-based scrubbing
        var emailRegex = new Regex(@"@""\\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Z|a-z]{2,}\\b""");
        var ssnRegex = new Regex(@"@""\\b\\d{3}-\\d{2}-\\d{4}\\b""");
        
        if ((emailRegex.IsMatch(sourceCode) || ssnRegex.IsMatch(sourceCode)) &&
            (sourceCode.Contains("Regex.Replace") || sourceCode.Contains("[EMAIL]") || sourceCode.Contains("[SSN]")))
        {
            patterns.Add(CreatePattern(
                name: "AI_RegexPIIScrubber",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Regex-based PII scrubbing",
                filePath: filePath,
                lineNumber: 1,
                content: "// Regex PII scrubbing detected",
                bestPractice: "Regex-based PII scrubbing is a good start. For production, consider Microsoft Presidio for comprehensive entity recognition.",
                azureUrl: "https://github.com/microsoft/presidio",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["method"] = "Regex patterns",
                    ["recommendation"] = "Upgrade to Presidio for better accuracy"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectTenantDataBoundary(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Tenant ID in collection/index names (string interpolation or concatenation)
        var hasTenantInString = sourceCode.Contains("tenant_") || sourceCode.Contains("$\"tenant") || 
                                sourceCode.Contains("\"tenant") || sourceCode.Contains("tenantId") ||
                                sourceCode.Contains("tenant-");
        if (hasTenantInString)
        {
            patterns.Add(CreatePattern(
                name: "AI_TenantDataBoundary",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Tenant data boundary enforcement",
                filePath: filePath,
                lineNumber: 1,
                content: "// Tenant isolation detected",
                bestPractice: "Enforce tenant data boundaries in vector stores, databases, and caches. Prevents cross-tenant data leakage in multi-tenant AI systems.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/overview",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Tenant Isolation",
                    ["significance"] = "CRITICAL - Multi-tenant security"
                }
            ));
        }

        // Pattern: Row-level security
        if (sourceCode.Contains("WHERE tenant_id") || sourceCode.Contains("filter: \"tenant_id"))
        {
            patterns.Add(CreatePattern(
                name: "AI_RowLevelSecurity",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Row-level security for tenant data",
                filePath: filePath,
                lineNumber: 1,
                content: "// Row-level security detected",
                bestPractice: "Implement row-level security to filter data by tenant ID in all queries.",
                azureUrl: "https://learn.microsoft.com/en-us/sql/relational-databases/security/row-level-security",
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Row-Level Security"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectTokenBudgetEnforcement(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Token counting (tiktoken)
        if (sourceCode.Contains("tiktoken") || sourceCode.Contains("Tiktoken") ||
            sourceCode.Contains("CountTokens") || sourceCode.Contains("Encode"))
        {
            patterns.Add(CreatePattern(
                name: "AI_TokenCounting",
                type: PatternType.AgentLightning,
                category: PatternCategory.Cost,
                implementation: "Token counting implementation",
                filePath: filePath,
                lineNumber: 1,
                content: "// Token counting detected",
                bestPractice: "Count tokens to estimate costs and enforce budgets. Use tiktoken library for accurate OpenAI token counts.",
                azureUrl: "https://github.com/openai/tiktoken",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["library"] = "tiktoken",
                    ["use_cases"] = new[] { "Cost estimation", "Budget enforcement", "Context window management" }
                }
            ));
        }

        // Pattern: Budget enforcement
        if ((sourceCode.Contains("budget") || sourceCode.Contains("Budget")) &&
            (sourceCode.Contains("token") || sourceCode.Contains("Token")) &&
            (sourceCode.Contains(">") || sourceCode.Contains("Exceeded")))
        {
            patterns.Add(CreatePattern(
                name: "AI_TokenBudgetEnforcement",
                type: PatternType.AgentLightning,
                category: PatternCategory.Cost,
                implementation: "Token budget enforcement",
                filePath: filePath,
                lineNumber: 1,
                content: "// Token budget enforcement detected",
                bestPractice: "Enforce token budgets to prevent runaway costs. Set per-user, per-agent, or per-project limits.",
                azureUrl: AzureOpenAIPromptUrl,
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Budget Enforcement",
                    ["significance"] = "HIGH - FinOps requirement"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectPromptLoggingWithRedaction(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Redacted logging
        if ((sourceCode.Contains("Log") || sourceCode.Contains("_logger")) &&
            (sourceCode.Contains("Redact") || sourceCode.Contains("Sanitize") || sourceCode.Contains("Mask")))
        {
            patterns.Add(CreatePattern(
                name: "AI_RedactedLogging",
                type: PatternType.Security,
                category: PatternCategory.Operational,
                implementation: "Redacted prompt/response logging",
                filePath: filePath,
                lineNumber: 1,
                content: "// Redacted logging detected",
                bestPractice: "Always redact PII from logs. Log prompts and responses for debugging, but protect sensitive data.",
                azureUrl: "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging",
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Redacted Logging",
                    ["compliance"] = new[] { "GDPR", "HIPAA", "SOC 2" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
