using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects Microsoft.Extensions.AI patterns - the unified AI abstraction layer for .NET
/// Reference: https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai
/// </summary>
public class MicrosoftExtensionsAIPatternDetector
{
    private const string MicrosoftExtensionsAIUrl = "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai";

    #region IChatClient Patterns

    /// <summary>
    /// Detects IChatClient usage - the core chat abstraction
    /// </summary>
    public List<CodePattern> DetectChatClientPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect IChatClient interface implementations
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDecls)
        {
            var baseTypes = classDecl.BaseList?.Types
                .Select(t => t.ToString())
                .ToList() ?? new List<string>();

            if (baseTypes.Any(t => t.Contains("IChatClient")))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MEA_IChatClient_Implementation_{classDecl.Identifier.Text}",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "IChatClient implementation",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 7),
                    bestPractice: "✅ EXCELLENT: Implementing IChatClient provides a unified interface for chat AI services",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Chat Client Implementation",
                        ["class_name"] = classDecl.Identifier.Text
                    }
                ));
            }
        }

        // Detect IChatClient.GetResponseAsync usage
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("GetResponseAsync"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_IChatClient_GetResponse",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "IChatClient.GetResponseAsync",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Request complete chat response with GetResponseAsync. Use GetStreamingResponseAsync for streaming.",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.90f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Chat Request"
                    }
                ));
            }

            if (invocationText.Contains("GetStreamingResponseAsync"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_IChatClient_GetStreamingResponse",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "IChatClient.GetStreamingResponseAsync",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Streaming responses with IAsyncEnumerable<ChatResponseUpdate> for real-time UX",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Streaming Chat",
                        ["is_streaming"] = true
                    }
                ));
            }
        }

        // Detect ChatMessage usage
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            var creationType = creation.Type?.ToString() ?? "";

            if (creationType.Contains("ChatMessage"))
            {
                var lineNumber = GetLineNumber(root, creation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_ChatMessage",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "ChatMessage construction",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(creation, sourceCode, 3),
                    bestPractice: "ChatMessage represents conversation history. Include System, User, and Assistant roles.",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Message Construction"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Tool Calling Patterns

    /// <summary>
    /// Detects Microsoft.Extensions.AI tool calling patterns
    /// </summary>
    public List<CodePattern> DetectToolCallingPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // AIFunctionFactory.Create usage
            if (invocationText.Contains("AIFunctionFactory.Create"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_AIFunctionFactory",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.ToolIntegration,
                    implementation: "AIFunctionFactory.Create",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Using AIFunctionFactory to create AI-callable functions (tools)",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Tool Creation",
                        ["is_function_calling"] = true
                    }
                ));
            }

            // UseFunctionInvocation middleware
            if (invocationText.Contains("UseFunctionInvocation"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_UseFunctionInvocation",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Interceptors,
                    implementation: "ChatClientBuilder.UseFunctionInvocation()",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Automatic function invocation middleware for tool calling",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Automatic Tool Calling",
                        ["is_middleware"] = true
                    }
                ));
            }

            // FunctionInvokingChatClient
            if (invocationText.Contains("FunctionInvokingChatClient"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_FunctionInvokingChatClient",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Interceptors,
                    implementation: "FunctionInvokingChatClient wrapper",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "FunctionInvokingChatClient wraps IChatClient to add automatic function invocation",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.90f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Function Invocation Wrapper"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region IEmbeddingGenerator Patterns

    /// <summary>
    /// Detects IEmbeddingGenerator patterns
    /// </summary>
    public List<CodePattern> DetectEmbeddingGeneratorPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect IEmbeddingGenerator interface implementations
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDecls)
        {
            var baseTypes = classDecl.BaseList?.Types
                .Select(t => t.ToString())
                .ToList() ?? new List<string>();

            if (baseTypes.Any(t => t.Contains("IEmbeddingGenerator")))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MEA_IEmbeddingGenerator_Implementation_{classDecl.Identifier.Text}",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "IEmbeddingGenerator implementation",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 7),
                    bestPractice: "✅ EXCELLENT: Implementing IEmbeddingGenerator for unified embedding generation",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Embedding Generator Implementation",
                        ["class_name"] = classDecl.Identifier.Text
                    }
                ));
            }
        }

        // Detect GenerateAsync/GenerateVectorAsync usage
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("GenerateAsync") && !invocationText.Contains("GetResponseAsync"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_IEmbeddingGenerator_GenerateAsync",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "IEmbeddingGenerator.GenerateAsync",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Generate embeddings with GenerateAsync for multiple inputs or GenerateVectorAsync for single input",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Embedding Generation"
                    }
                ));
            }

            if (invocationText.Contains("GenerateVectorAsync"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_GenerateVectorAsync",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "GenerateVectorAsync (single input)",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Using GenerateVectorAsync accelerator for single embedding generation",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.90f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Single Embedding Generation"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Middleware & Pipeline Patterns

    /// <summary>
    /// Detects Microsoft.Extensions.AI middleware patterns (caching, telemetry, rate limiting)
    /// </summary>
    public List<CodePattern> DetectMiddlewarePatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // ChatClientBuilder
            if (invocationText.Contains("ChatClientBuilder") || invocationText.Contains("AsBuilder"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_ChatClientBuilder",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Interceptors,
                    implementation: "ChatClientBuilder pipeline",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 7),
                    bestPractice: "✅ EXCELLENT: Using ChatClientBuilder for composable middleware pipeline",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Pipeline Builder",
                        ["is_middleware"] = true
                    }
                ));
            }

            // UseDistributedCache middleware
            if (invocationText.Contains("UseDistributedCache"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_UseDistributedCache",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Performance,
                    implementation: "UseDistributedCache middleware",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Caching responses with distributed cache for performance",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Caching Middleware",
                        ["is_middleware"] = true
                    }
                ));
            }

            // UseOpenTelemetry middleware
            if (invocationText.Contains("UseOpenTelemetry"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_UseOpenTelemetry",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Observability,
                    implementation: "UseOpenTelemetry middleware",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: OpenTelemetry integration for telemetry and observability",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Telemetry Middleware",
                        ["is_middleware"] = true,
                        ["has_telemetry"] = true
                    }
                ));
            }

            // EmbeddingGeneratorBuilder
            if (invocationText.Contains("EmbeddingGeneratorBuilder"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_EmbeddingGeneratorBuilder",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Interceptors,
                    implementation: "EmbeddingGeneratorBuilder pipeline",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 7),
                    bestPractice: "✅ EXCELLENT: Using EmbeddingGeneratorBuilder for composable embedding pipeline",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Embedding Pipeline Builder",
                        ["is_middleware"] = true
                    }
                ));
            }
        }

        // Detect DelegatingChatClient implementations
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDecls)
        {
            var baseTypes = classDecl.BaseList?.Types
                .Select(t => t.ToString())
                .ToList() ?? new List<string>();

            if (baseTypes.Any(t => t.Contains("DelegatingChatClient")))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MEA_DelegatingChatClient_{classDecl.Identifier.Text}",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Interceptors,
                    implementation: "Custom DelegatingChatClient middleware",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "✅ EXCELLENT: Implementing custom middleware via DelegatingChatClient",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Custom Middleware",
                        ["class_name"] = classDecl.Identifier.Text,
                        ["is_custom_middleware"] = true
                    }
                ));
            }

            if (baseTypes.Any(t => t.Contains("DelegatingEmbeddingGenerator")))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MEA_DelegatingEmbeddingGenerator_{classDecl.Identifier.Text}",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Interceptors,
                    implementation: "Custom DelegatingEmbeddingGenerator middleware",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "✅ EXCELLENT: Implementing custom embedding middleware via DelegatingEmbeddingGenerator",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Custom Embedding Middleware",
                        ["class_name"] = classDecl.Identifier.Text,
                        ["is_custom_middleware"] = true
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Dependency Injection Patterns

    /// <summary>
    /// Detects Microsoft.Extensions.AI dependency injection patterns
    /// </summary>
    public List<CodePattern> DetectDependencyInjectionPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // AddChatClient service registration
            if (invocationText.Contains("AddChatClient") || invocationText.Contains("AddSingleton<IChatClient"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_AddChatClient_DI",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.DependencyInjection,
                    implementation: "IChatClient dependency injection",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Registering IChatClient in DI container for dependency injection",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Service Registration",
                        ["is_dependency_injection"] = true
                    }
                ));
            }

            // AddEmbeddingGenerator service registration
            if (invocationText.Contains("AddEmbeddingGenerator") || invocationText.Contains("AddSingleton<IEmbeddingGenerator"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_AddEmbeddingGenerator_DI",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.DependencyInjection,
                    implementation: "IEmbeddingGenerator dependency injection",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Registering IEmbeddingGenerator in DI container",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Embedding Service Registration",
                        ["is_dependency_injection"] = true
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Stateless vs. Stateful Client Patterns

    /// <summary>
    /// Detects stateless vs. stateful conversation patterns (ConversationId usage)
    /// </summary>
    public List<CodePattern> DetectConversationStatePatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect ConversationId usage for stateful clients
        var members = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
        
        foreach (var member in members)
        {
            var memberText = member.ToString();

            if (memberText.Contains("ConversationId"))
            {
                var lineNumber = GetLineNumber(root, member, sourceCode);
                
                // Check if it's being set or used
                var parent = member.Parent;
                bool isAssignment = parent is AssignmentExpressionSyntax;
                
                patterns.Add(CreatePattern(
                    name: isAssignment ? "MEA_StatefulClient_ConversationId" : "MEA_ConversationId_Usage",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.StateManagement,
                    implementation: isAssignment ? 
                        "Stateful client using ConversationId" : 
                        "Reading ConversationId for state tracking",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(member, sourceCode, 7),
                    bestPractice: "✅ EXCELLENT: Using ConversationId for stateful conversation tracking. Clear history when ConversationId is set.",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Stateful Conversation",
                        ["is_stateful"] = true,
                        ["is_assignment"] = isAssignment
                    }
                ));
            }
        }

        // Detect conversation history clearing (stateful pattern)
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if ((invocationText.Contains("chatHistory.Clear()") || invocationText.Contains("history.Clear()")) &&
                sourceCode.Contains("ConversationId"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_StatefulClient_HistoryClear",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.StateManagement,
                    implementation: "Clearing conversation history for stateful client",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "✅ EXCELLENT: Clearing local history when ConversationId is set (server maintains state)",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.90f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "State Management",
                        ["is_stateful"] = true
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region IImageGenerator Patterns

    /// <summary>
    /// Detects IImageGenerator patterns for text-to-image generation
    /// </summary>
    public List<CodePattern> DetectImageGeneratorPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect IImageGenerator interface implementations
        var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDecls)
        {
            var baseTypes = classDecl.BaseList?.Types
                .Select(t => t.ToString())
                .ToList() ?? new List<string>();

            if (baseTypes.Any(t => t.Contains("IImageGenerator")))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: $"MEA_IImageGenerator_Implementation_{classDecl.Identifier.Text}",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "IImageGenerator implementation",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 7),
                    bestPractice: "✅ EXCELLENT: Implementing IImageGenerator for text-to-image generation",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Image Generation",
                        ["class_name"] = classDecl.Identifier.Text,
                        ["is_image_generation"] = true
                    }
                ));
            }
        }

        // Detect GenerateAsync for image generation
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            if (invocationText.Contains("GenerateAsync") && 
                (sourceCode.Contains("IImageGenerator") || sourceCode.Contains("ImageGenerationRequest")))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_IImageGenerator_GenerateAsync",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: "IImageGenerator.GenerateAsync",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 5),
                    bestPractice: "Generate images from text prompts with IImageGenerator.GenerateAsync",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.90f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Image Generation",
                        ["is_async"] = true
                    }
                ));
            }
        }

        // Detect ImageGenerationOptions
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            var creationType = creation.Type?.ToString() ?? "";

            if (creationType.Contains("ImageGenerationOptions") || creationType.Contains("ImageGenerationRequest"))
            {
                var lineNumber = GetLineNumber(root, creation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_ImageGenerationOptions",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Configuration,
                    implementation: "ImageGenerationOptions configuration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(creation, sourceCode, 5),
                    bestPractice: "Configure image generation with ImageGenerationOptions (size, format, quality)",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Image Configuration"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Structured Output Patterns

    /// <summary>
    /// Detects structured output patterns (strongly-typed responses)
    /// </summary>
    public List<CodePattern> DetectStructuredOutputPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Look for generic GetResponseAsync<T> calls
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var invocationText = invocation.ToString();

            // Detect GetResponseAsync<T> or similar typed responses
            if (Regex.IsMatch(invocationText, @"GetResponseAsync<\w+>|GetStreamingResponseAsync<\w+>"))
            {
                var lineNumber = GetLineNumber(root, invocation, sourceCode);
                
                // Extract the type parameter
                var typeMatch = Regex.Match(invocationText, @"<(\w+)>");
                var responseType = typeMatch.Success ? typeMatch.Groups[1].Value : "Unknown";
                
                patterns.Add(CreatePattern(
                    name: $"MEA_StructuredOutput_{responseType}",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.AIAgents,
                    implementation: $"Structured output with type {responseType}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(invocation, sourceCode, 7),
                    bestPractice: "✅ EXCELLENT: Using strongly-typed responses for structured output",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Structured Output",
                        ["response_type"] = responseType,
                        ["is_structured"] = true
                    }
                ));
            }
        }

        // Detect JSON schema or response format specifications
        if (sourceCode.Contains("ResponseFormat") || sourceCode.Contains("JsonSchema"))
        {
            var assignments = root.DescendantNodes().OfType<AssignmentExpressionSyntax>();
            foreach (var assignment in assignments)
            {
                var assignmentText = assignment.ToString();
                
                if (assignmentText.Contains("ResponseFormat") || assignmentText.Contains("JsonSchema"))
                {
                    var lineNumber = GetLineNumber(root, assignment, sourceCode);
                    patterns.Add(CreatePattern(
                        name: "MEA_ResponseFormat_JsonSchema",
                        type: PatternType.MicrosoftExtensionsAI,
                        category: PatternCategory.Configuration,
                        implementation: "JSON schema for structured output",
                        filePath: filePath,
                        lineNumber: lineNumber,
                        content: GetContextAroundNode(assignment, sourceCode, 5),
                        bestPractice: "✅ EXCELLENT: Defining JSON schema for structured, validated AI responses",
                        azureUrl: MicrosoftExtensionsAIUrl,
                        context: context,
                        confidence: 0.90f,
                        metadata: new Dictionary<string, object>
                        {
                            ["framework"] = "Microsoft.Extensions.AI",
                            ["pattern_category"] = "Response Schema",
                            ["has_schema"] = true
                        }
                    ));
                }
            }
        }

        return patterns;
    }

    #endregion

    #region ChatOptions and Configuration Patterns

    /// <summary>
    /// Detects ChatOptions and configuration patterns
    /// </summary>
    public List<CodePattern> DetectConfigurationPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            var creationType = creation.Type?.ToString() ?? "";

            if (creationType.Contains("ChatOptions"))
            {
                var lineNumber = GetLineNumber(root, creation, sourceCode);
                
                // Check for Tools configuration
                bool hasTools = creation.ToString().Contains("Tools");
                bool hasTemperature = creation.ToString().Contains("Temperature");
                bool hasMaxTokens = creation.ToString().Contains("MaxOutputTokens") || creation.ToString().Contains("MaxTokens");
                
                patterns.Add(CreatePattern(
                    name: "MEA_ChatOptions",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Configuration,
                    implementation: "ChatOptions configuration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(creation, sourceCode, 7),
                    bestPractice: hasTools ? 
                        "✅ EXCELLENT: Using ChatOptions with Tools for function calling" :
                        "ChatOptions configures model parameters (temperature, max tokens, etc.)",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.90f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Configuration",
                        ["has_tools"] = hasTools,
                        ["has_temperature"] = hasTemperature,
                        ["has_max_tokens"] = hasMaxTokens
                    }
                ));
            }

            if (creationType.Contains("EmbeddingGenerationOptions"))
            {
                var lineNumber = GetLineNumber(root, creation, sourceCode);
                patterns.Add(CreatePattern(
                    name: "MEA_EmbeddingGenerationOptions",
                    type: PatternType.MicrosoftExtensionsAI,
                    category: PatternCategory.Configuration,
                    implementation: "EmbeddingGenerationOptions configuration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(creation, sourceCode, 5),
                    bestPractice: "Configure embedding generation with EmbeddingGenerationOptions",
                    azureUrl: MicrosoftExtensionsAIUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["framework"] = "Microsoft.Extensions.AI",
                        ["pattern_category"] = "Embedding Configuration"
                    }
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region Helper Methods

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        int lineNumber,
        string content,
        string bestPractice,
        string azureUrl,
        string? context,
        float confidence,
        Dictionary<string, object> metadata)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "C#",
            FilePath = filePath,
            LineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context,
            Confidence = confidence,
            Metadata = metadata
        };
    }

    private int GetLineNumber(SyntaxNode root, SyntaxNode node, string sourceCode)
    {
        var span = node.Span;
        var lineSpan = root.SyntaxTree.GetLineSpan(span);
        return lineSpan.StartLinePosition.Line + 1;
    }

    private string GetContextAroundNode(SyntaxNode node, string sourceCode, int contextLines)
    {
        var lines = sourceCode.Split('\n');
        var span = node.Span;
        var lineSpan = node.SyntaxTree.GetLineSpan(span);
        
        int startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        int endLine = Math.Min(lines.Length - 1, lineSpan.EndLinePosition.Line + contextLines);
        
        return string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));
    }

    #endregion

    /// <summary>
    /// Main entry point - detects all Microsoft.Extensions.AI patterns
    /// </summary>
    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context)
    {
        // Skip if not C# or doesn't contain Microsoft.Extensions.AI
        if (!sourceCode.Contains("Microsoft.Extensions.AI") && 
            !sourceCode.Contains("IChatClient") &&
            !sourceCode.Contains("IEmbeddingGenerator"))
        {
            return new List<CodePattern>();
        }

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        var patterns = new List<CodePattern>();

        patterns.AddRange(DetectChatClientPatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectToolCallingPatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectEmbeddingGeneratorPatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectMiddlewarePatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectDependencyInjectionPatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectConfigurationPatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectConversationStatePatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectImageGeneratorPatterns(root, filePath, context, sourceCode));
        patterns.AddRange(DetectStructuredOutputPatterns(root, filePath, context, sourceCode));

        return patterns;
    }
}

