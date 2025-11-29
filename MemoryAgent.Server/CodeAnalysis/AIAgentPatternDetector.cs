using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects AI Agent Core Patterns - The fundamental patterns that distinguish
/// AI agents from simple LLM integrations.
/// 
/// Based on comprehensive research of:
/// - Microsoft Guidance library
/// - Semantic Kernel
/// - Azure AI ecosystem
/// - Industry patterns (LangChain, AutoGen, ReAct)
/// 
/// These patterns answer: "IS THIS AN AGENT?" vs "Is this just an LLM call?"
/// </summary>
public partial class AIAgentPatternDetector
{
    private readonly ILogger<AIAgentPatternDetector>? _logger;

    // Microsoft documentation URLs (protected so partial classes can access)
    protected const string AzureOpenAIPromptUrl = "https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/prompt-engineering";
    protected const string SemanticKernelUrl = "https://learn.microsoft.com/en-us/semantic-kernel/";
    protected const string AzureAISearchUrl = "https://learn.microsoft.com/en-us/azure/search/";
    protected const string AzureContentSafetyUrl = "https://learn.microsoft.com/en-us/azure/ai-services/content-safety/";
    protected const string PromptWizardUrl = "https://www.microsoft.com/en-us/research/blog/promptwizard-the-future-of-prompt-optimization-through-feedback-driven-self-evolving-prompts/";
    protected const string MicrosoftGuidanceUrl = "https://github.com/microsoft/guidance";

    public AIAgentPatternDetector(ILogger<AIAgentPatternDetector>? logger = null)
    {
        _logger = logger;
    }

    public async Task<List<CodePattern>> DetectPatternsAsync(
        string filePath,
        string? context,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = tree.GetRoot(cancellationToken);

            // CATEGORY 1: Prompt Engineering & Guardrails
            patterns.AddRange(DetectSystemPromptDefinition(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPromptTemplates(root, filePath, context, sourceCode));
            patterns.AddRange(DetectGuardrailInjection(root, filePath, context, sourceCode));

            // CATEGORY 2: Memory & State (Core Agent Behavior)
            patterns.AddRange(DetectShortTermMemoryBuffer(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLongTermMemoryVector(root, filePath, context, sourceCode));
            patterns.AddRange(DetectUserProfileMemory(root, filePath, context, sourceCode));

            // CATEGORY 3: Tools & Function Calling
            patterns.AddRange(DetectToolRegistration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectToolRouting(root, filePath, context, sourceCode));
            patterns.AddRange(DetectExternalServiceTool(root, filePath, context, sourceCode));

            // CATEGORY 4: Planning, Autonomy & Loops
            patterns.AddRange(DetectTaskPlanner(root, filePath, context, sourceCode));
            patterns.AddRange(DetectActionLoop(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMultiAgentOrchestrator(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSelfReflection(root, filePath, context, sourceCode));

            // CATEGORY 5: RAG & Knowledge Integration
            patterns.AddRange(DetectEmbeddingGeneration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectVectorSearchRAG(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRAGOrchestrator(root, filePath, context, sourceCode));

            // CATEGORY 6: Safety & Governance
            patterns.AddRange(DetectContentModeration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPIIScrubber(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTenantDataBoundary(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTokenBudgetEnforcement(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPromptLoggingWithRedaction(root, filePath, context, sourceCode));

            // CATEGORY 7: FinOps / Cost Control
            patterns.AddRange(DetectTokenMetering(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCostBudgetGuardrail(root, filePath, context, sourceCode));
            
            // CATEGORY 8: Observability & Evaluation (MISSING - NOW ADDED)
            patterns.AddRange(DetectAgentTracing(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentEvaluation(root, filePath, context, sourceCode));
            
            // CATEGORY 9: Advanced Multi-Agent Patterns (MISSING - NOW ADDED)
            patterns.AddRange(DetectGroupChatPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSequentialOrchestration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectControlPlanePattern(root, filePath, context, sourceCode));
            
            // CATEGORY 10: Agent Lifecycle (MISSING - NOW ADDED)
            patterns.AddRange(DetectAgentFactory(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSelfImprovingAgent(root, filePath, context, sourceCode));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting AI agent patterns in {FilePath}", filePath);
        }

        return patterns;
    }

}
