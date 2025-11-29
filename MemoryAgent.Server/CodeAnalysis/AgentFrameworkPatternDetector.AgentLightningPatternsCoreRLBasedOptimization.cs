using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Agent Lightning Patterns (Core RL-Based Optimization)
/// </summary>
public partial class AgentFrameworkPatternDetector
{
    #region Agent Lightning Patterns (Core RL-Based Optimization)

    private List<CodePattern> DetectAgentLightningServer(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect Lightning Server implementation
            if (classDecl.Identifier.Text.Contains("LightningServer") ||
                classDecl.Identifier.Text.Contains("AgentOptimization"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_Server_{classDecl.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Agent Lightning Server",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Using Agent Lightning Server to bridge agent frameworks with RL training (verl). Enables seamless optimization for ANY agent with ANY framework.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "RL-Based Agent Optimization",
                        ["capability"] = "Task pulling, trace collection, reward reporting",
                        ["reference"] = AgentLightningGitHub
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAgentLightningClient(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect OpenAI-compatible API calls to Lightning Client
            if (invocationText.Contains("LightningClient") ||
                (invocationText.Contains("OpenAI") && invocationText.Contains("training")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_Client",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Agent Lightning Client (OpenAI-compatible API)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Agent Lightning Client provides OpenAI-compatible LLM API inside training infrastructure, enabling zero-code-change integration with existing agents.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Training Infrastructure Integration",
                        ["compatibility"] = "OpenAI Agent SDK, LangChain, AutoGen"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRLTrainingPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect GRPO (Group Relative Policy Optimization) or verl usage
            if (invocationText.Contains("GRPO") || invocationText.Contains("verl") ||
                invocationText.Contains("PolicyOptimization") || invocationText.Contains("RLTraining"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_RLTraining",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "RL Training (GRPO/verl)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using reinforcement learning (GRPO algorithm via verl) to optimize agent models based on task success signals and interaction data.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Model Optimization",
                        ["algorithm"] = "GRPO (Group Relative Policy Optimization)",
                        ["training_framework"] = "verl",
                        ["optimization_type"] = "Reinforcement Learning"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRewardSignals(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for reward signal definitions
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("Reward") || 
                method.Identifier.Text.Contains("TaskSuccess") ||
                method.ReturnType.ToString().Contains("Reward"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_RewardSignal_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Reward Signal Definition",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Defining custom reward signals to reflect task success/failure. Agent Lightning uses these to guide RL optimization towards desired agent behaviors.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Reward Engineering",
                        ["use_case"] = "Task success signals, feedback signals, credit assignment"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectErrorMonitoring(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect agent-side error monitoring (critical for stable optimization)
            if (invocationText.Contains("MonitorExecution") || 
                invocationText.Contains("TrackFailure") ||
                invocationText.Contains("ReportError") ||
                (invocationText.Contains("agent") && invocationText.Contains("error")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_ErrorMonitoring",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Agent-Side Error Monitoring",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Agent Lightning's error monitoring tracks execution status, detects failure modes, and reports error types. Critical for stable optimization when agents fail or get stuck.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Fault Tolerance",
                        ["capability"] = "Failure detection, error reporting, graceful degradation"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectTraceCollection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for transition tuple pattern: (state, action, reward, next_state)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            // Detect sidecar-based trace collection
            if ((methodBody.Contains("state") && methodBody.Contains("action") && methodBody.Contains("reward")) ||
                methodBody.Contains("transition") || methodBody.Contains("trajectory") ||
                methodBody.Contains("CollectTrace"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_TraceCollection_{method.Identifier.Text}",
                    type: PatternType.AgentFramework,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Sidecar-Based Trace Collection",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Agent Lightning's sidecar design non-intrusively monitors agent runs and collects transition tuples (state, action, reward, next_state) for RL training without modifying agent code.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Data Collection",
                        ["design_pattern"] = "Sidecar (Non-Intrusive Monitoring)",
                        ["data_format"] = "Transition tuples for RL"
                    }
                ));
                break; // Only report once per file
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCurriculumLearning(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("Curriculum") || 
                (methodBody.Contains("difficulty") && methodBody.Contains("progressive")) ||
                methodBody.Contains("TaskProgression"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_CurriculumLearning_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Curriculum Learning",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Curriculum learning: progressively increase task difficulty during training, starting simple and building to complex tasks. Accelerates learning and improves final performance.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Advanced RL Training",
                        ["technique"] = "Progressive Difficulty Scaling"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectOffPolicyRL(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("OffPolicy") || invocationText.Contains("ExperienceReplay") ||
                invocationText.Contains("ReplayBuffer"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_OffPolicyRL",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Off-Policy RL with Experience Replay",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Off-policy RL reuses past experiences for training, improving sample efficiency and enabling parallel data collection from multiple agents.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Sample Efficiency",
                        ["benefit"] = "Reuse past experiences, parallel collection"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHierarchicalRL(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            if ((classDecl.Identifier.Text.Contains("HierarchicalPolicy") || 
                 classDecl.Identifier.Text.Contains("HighLevelPolicy") ||
                 classDecl.Identifier.Text.Contains("LowLevelPolicy")) &&
                classDecl.ToString().Contains("Agent"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_HierarchicalRL_{classDecl.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Hierarchical RL",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 15),
                    bestPractice: "Hierarchical RL: decompose complex tasks into high-level goals and low-level actions, enabling faster learning on long-horizon tasks.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Temporal Abstraction",
                        ["use_case"] = "Long-horizon, multi-step tasks"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectOnlineSFT(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("OnlineSFT") || invocationText.Contains("SupervisedFineTuning") ||
                (invocationText.Contains("Online") && invocationText.Contains("FineTune")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_OnlineSFT",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Online Supervised Fine-Tuning",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Online SFT: continuously collect and filter high-quality agent interactions for supervised fine-tuning, complementing RL training.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Hybrid Training",
                        ["technique"] = "RL + Supervised Learning"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectUserFeedbackIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("UserFeedback") || method.Identifier.Text.Contains("HumanReward") ||
                method.ReturnType.ToString().Contains("Feedback"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_UserFeedback_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.HumanInLoop,
                    implementation: "User Feedback Integration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Integrating user feedback (thumbs up/down, ratings, corrections) as reward signals for RLHF (Reinforcement Learning from Human Feedback).",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "RLHF",
                        ["signal_type"] = "Human preferences"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectToolSuccessSignals(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if ((methodBody.Contains("ToolSuccess") || methodBody.Contains("FunctionSuccess")) &&
                (methodBody.Contains("reward") || methodBody.Contains("signal")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_ToolSuccessSignals_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Tool Success Signals",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Using tool/function execution success as reward signals to teach agents when they correctly use tools and APIs.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Reward Engineering",
                        ["signal_source"] = "Tool execution results"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectLongHorizonCredit(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("CreditAssignment") || methodBody.Contains("LongHorizon") ||
                (methodBody.Contains("discount") && methodBody.Contains("gamma")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_LongHorizonCredit_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Long-Horizon Credit Assignment",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Properly assigning credit to actions in multi-step tasks with delayed rewards, critical for training agents on complex workflows.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.75f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Credit Assignment",
                        ["challenge"] = "Delayed rewards in long tasks"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectLLamaFactoryIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("LLamaFactory") || invocationText.Contains("LLaMA-Factory"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_LLamaFactory",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "LLaMA-Factory Integration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Integrating Agent Lightning with LLaMA-Factory for efficient fine-tuning and training of open-source LLMs on agent tasks.",
                    azureUrl: AgentLightningGitHub,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Training Infrastructure",
                        ["integration"] = "LLaMA-Factory"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectDSPyIntegration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("DSPy") || invocationText.Contains("dspy"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AgentLightning_DSPy",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "DSPy Integration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Integrating DSPy (Declarative Self-improving Python) with Agent Lightning for prompt optimization and program synthesis.",
                    azureUrl: AgentLightningGitHub,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Prompt Optimization",
                        ["integration"] = "DSPy"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMultiTaskLearning(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";

            if (methodBody.Contains("MultiTask") || 
                (methodBody.Contains("tasks") && methodBody.Contains("shared") && methodBody.Contains("representation")))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"AgentLightning_MultiTaskLearning_{method.Identifier.Text}",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AgentOptimization,
                    implementation: "Multi-Task Learning",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Training agents on multiple related tasks simultaneously to learn shared representations, improving generalization and sample efficiency.",
                    azureUrl: AgentLightningUrl,
                    context: context,
                    confidence: 0.8f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Agent Lightning",
                        ["pattern_category"] = "Transfer Learning",
                        ["benefit"] = "Shared knowledge across tasks"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
