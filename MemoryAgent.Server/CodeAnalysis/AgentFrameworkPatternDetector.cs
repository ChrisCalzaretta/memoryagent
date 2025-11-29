using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects patterns specific to Microsoft Agent Framework, Semantic Kernel, and AutoGen
/// Based on: https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview
/// SPLIT INTO 7 PARTIAL CLASS FILES (Main + 6 region files)
/// </summary>
public partial class AgentFrameworkPatternDetector
{
    private readonly ILogger<AgentFrameworkPatternDetector>? _logger;

    // Azure URLs for AI Agent Framework best practices (protected so partial classes can access)
    protected const string AgentFrameworkUrl = "https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview";
    protected const string SemanticKernelUrl = "https://learn.microsoft.com/en-us/semantic-kernel/overview/";
    protected const string AutoGenUrl = "https://microsoft.github.io/autogen/";
    protected const string McpServerUrl = "https://modelcontextprotocol.io/introduction";
    protected const string AgentLightningUrl = "https://www.microsoft.com/en-us/research/project/agent-lightning/";
    protected const string AgentLightningGitHub = "https://github.com/microsoft/agent-lightning";

    public AgentFrameworkPatternDetector(ILogger<AgentFrameworkPatternDetector>? logger = null)
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

            // Microsoft Agent Framework Patterns (Core)
            patterns.AddRange(DetectAgentFrameworkAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentFrameworkWorkflows(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentThreadUsage(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMcpServerIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentMiddleware(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCheckpointing(root, filePath, context, sourceCode));
            
            // Microsoft Agent Framework Patterns (Advanced)
            patterns.AddRange(DetectContextProviders(root, filePath, context, sourceCode));
            patterns.AddRange(DetectToolRegistration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentComposition(root, filePath, context, sourceCode));
            patterns.AddRange(DetectStreamingResponses(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentErrorHandling(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentTelemetry(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRequestResponsePatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentLifecycle(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCustomAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentDecorators(root, filePath, context, sourceCode));

            // Semantic Kernel Patterns (Core)
            patterns.AddRange(DetectSemanticKernelPlugins(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticKernelPlanners(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticKernelMemory(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticKernelFilters(root, filePath, context, sourceCode));
            
            // Semantic Kernel Patterns (Advanced)
            patterns.AddRange(DetectPromptTemplates(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSemanticFunctions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectNativeFunctions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMemoryConnectors(root, filePath, context, sourceCode));
            patterns.AddRange(DetectEmbeddingGeneration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectChatHistoryManagement(root, filePath, context, sourceCode));

            // AutoGen Patterns (Core)
            patterns.AddRange(DetectAutoGenConversableAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAutoGenGroupChat(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAutoGenUserProxy(root, filePath, context, sourceCode));
            
            // AutoGen Patterns (Advanced)
            patterns.AddRange(DetectReplyFunctions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTerminationConditions(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSpeakerSelection(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCodeExecution(root, filePath, context, sourceCode));

            // Multi-Agent Orchestration Patterns (Core)
            patterns.AddRange(DetectSequentialOrchestration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConcurrentOrchestration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHandoffPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMagenticPattern(root, filePath, context, sourceCode));
            
            // Multi-Agent Orchestration Patterns (Advanced)
            patterns.AddRange(DetectSupervisorPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHierarchicalAgents(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSwarmIntelligence(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConsensusPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectDebatePattern(root, filePath, context, sourceCode));

            // Agent Lightning Patterns (Core RL-based Optimization)
            patterns.AddRange(DetectAgentLightningServer(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAgentLightningClient(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRLTrainingPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRewardSignals(root, filePath, context, sourceCode));
            patterns.AddRange(DetectErrorMonitoring(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTraceCollection(root, filePath, context, sourceCode));
            
            // Agent Lightning Patterns (Advanced RL Techniques)
            patterns.AddRange(DetectCurriculumLearning(root, filePath, context, sourceCode));
            patterns.AddRange(DetectOffPolicyRL(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHierarchicalRL(root, filePath, context, sourceCode));
            patterns.AddRange(DetectOnlineSFT(root, filePath, context, sourceCode));
            patterns.AddRange(DetectUserFeedbackIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectToolSuccessSignals(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLongHorizonCredit(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLLamaFactoryIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectDSPyIntegration(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMultiTaskLearning(root, filePath, context, sourceCode));

            // Best Practice Anti-Patterns
            patterns.AddRange(DetectAntiPatterns(root, filePath, context, sourceCode));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting Agent Framework patterns in {FilePath}", filePath);
        }

        return patterns;
    }

}
