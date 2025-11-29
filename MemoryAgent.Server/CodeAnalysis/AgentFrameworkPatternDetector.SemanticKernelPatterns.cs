using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Semantic Kernel Patterns
/// </summary>
public partial class AgentFrameworkPatternDetector
{
    #region Semantic Kernel Patterns

    private List<CodePattern> DetectSemanticKernelPlugins(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect SK plugin classes with [KernelFunction] attributes
            var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var hasKernelFunction = method.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(attr => attr.Name.ToString().Contains("KernelFunction"));

                if (hasKernelFunction)
                {
                    var lineNumber = GetLineNumber(root, method, sourceCode);
                    patterns.Add(CreatePattern(
                        name: $"SemanticKernel_Plugin_{classDecl.Identifier.Text}_{method.Identifier.Text}",
                        type: PatternType.SemanticKernel,
                        category: PatternCategory.ToolIntegration,
                        implementation: "Semantic Kernel Plugin with [KernelFunction]",
                        filePath: filePath,
                        lineNumber: lineNumber,
                        content: GetContextAroundNode(method, sourceCode, 7),
                        bestPractice: "Using [KernelFunction] attribute to create AI-callable functions (tools) in Semantic Kernel",
                        azureUrl: SemanticKernelUrl,
                        context: context,
                        confidence: 0.95f,
                        metadata: new Dictionary<string, object>
                        {
                            ["framework"] = "Semantic Kernel",
                            ["pattern_category"] = "Plugin Development",
                            ["plugin_name"] = classDecl.Identifier.Text,
                            ["function_name"] = method.Identifier.Text
                        }
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticKernelPlanners(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect planner usage (NOTE: Planners are legacy in SK, migrated to Agent Framework workflows)
            if (invocationText.Contains("Planner") || invocationText.Contains("CreatePlan"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_Planner_Legacy",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Semantic Kernel Planner (Legacy)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "MIGRATION RECOMMENDED: Semantic Kernel Planners are legacy. Migrate to Agent Framework Workflows for better control and reliability.",
                    azureUrl: "https://learn.microsoft.com/en-us/agent-framework/migrate-from-semantic-kernel",
                    context: context,
                    confidence: 0.85f,
                    isPositivePattern: false, // Anti-pattern: should migrate
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Legacy Pattern",
                        ["recommendation"] = "Migrate to Agent Framework Workflows"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticKernelMemory(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect SK memory usage
            if (invocationText.Contains("ISemanticTextMemory") || invocationText.Contains("MemoryStore"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_Memory",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.StateManagement,
                    implementation: "Semantic Kernel Memory Store",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Using Semantic Kernel memory for storing and retrieving embeddings and semantic information",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Memory Management"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticKernelFilters(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDecls)
        {
            // Detect filter implementations
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IFunctionFilter") ||
                                                   t.ToString().Contains("IPromptFilter")) == true)
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_Filter_{classDecl.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.Interceptors,
                    implementation: "Semantic Kernel Filter",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Using SK filters for telemetry, logging, safety checks, and function interception",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Filters/Middleware"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPromptTemplates(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var variables = root.DescendantNodes().OfType<VariableDeclaratorSyntax>();

        foreach (var variable in variables)
        {
            var initializer = variable.Initializer?.Value.ToString() ?? "";

            if ((initializer.Contains("{{") && initializer.Contains("}}")) || 
                variable.Identifier.Text.Contains("PromptTemplate", StringComparison.OrdinalIgnoreCase))
            {
                var lineNumber = GetLineNumber(root, variable, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_PromptTemplate_{variable.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.AIAgents,
                    implementation: "Semantic Kernel Prompt Template",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(variable, sourceCode, 5),
                    bestPractice: "Using prompt templates with {{ }} placeholders for dynamic prompt generation with variable substitution",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Prompt Engineering"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSemanticFunctions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("CreateSemanticFunction") || invocationText.Contains("RegisterSemanticFunction"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_SemanticFunction",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.AIAgents,
                    implementation: "Semantic Function (Prompt-based)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Creating semantic functions (AI-powered functions defined by prompts) for natural language tasks",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "AI Functions"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectNativeFunctions(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            var hasSKFunction = method.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString().Contains("SKFunction"));

            if (hasSKFunction)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_NativeFunction_{method.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Native Function (Code-based)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 7),
                    bestPractice: "Creating native functions (C# functions callable by AI) using [SKFunction] attribute for deterministic operations",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Native Functions",
                        ["function_name"] = method.Identifier.Text
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMemoryConnectors(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("AzureAISearchMemoryStore") || 
                invocationText.Contains("QdrantMemoryStore") ||
                invocationText.Contains("PostgresMemoryStore") ||
                invocationText.Contains("RedisMemoryStore") ||
                invocationText.Contains("MemoryStore"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                var connector = invocationText.Contains("AzureAISearch") ? "Azure AI Search" :
                               invocationText.Contains("Qdrant") ? "Qdrant" :
                               invocationText.Contains("Postgres") ? "PostgreSQL" :
                               invocationText.Contains("Redis") ? "Redis" : "Generic";

                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_MemoryConnector_{connector}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.StateManagement,
                    implementation: $"Semantic Kernel Memory - {connector}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: $"Using {connector} memory connector for persistent storage and retrieval of embeddings and semantic information",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Memory Connectors",
                        ["connector"] = connector
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectEmbeddingGeneration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("GenerateEmbeddingAsync") || 
                invocationText.Contains("GetEmbeddingsAsync") ||
                invocationText.Contains("ITextEmbeddingGeneration"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "SemanticKernel_EmbeddingGeneration",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.AIAgents,
                    implementation: "Embedding Generation",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Generating text embeddings for semantic search, similarity matching, and memory storage",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Embeddings"
                    }
                ));
                break; // Only report once per file
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectChatHistoryManagement(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();
        var variables = root.DescendantNodes().OfType<VariableDeclaratorSyntax>();

        foreach (var variable in variables)
        {
            var type = variable.Parent?.Parent as VariableDeclarationSyntax;

            if (type?.Type.ToString().Contains("ChatHistory") == true)
            {
                var lineNumber = GetLineNumber(root, variable, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"SemanticKernel_ChatHistory_{variable.Identifier.Text}",
                    type: PatternType.SemanticKernel,
                    category: PatternCategory.StateManagement,
                    implementation: "Chat History Management",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(variable, sourceCode, 5),
                    bestPractice: "Managing chat history for multi-turn conversations with context preservation and message history tracking",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.9f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Semantic Kernel",
                        ["pattern_category"] = "Conversation Management"
                    }
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
