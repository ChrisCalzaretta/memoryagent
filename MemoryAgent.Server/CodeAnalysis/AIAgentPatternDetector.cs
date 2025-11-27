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
public class AIAgentPatternDetector
{
    private readonly ILogger<AIAgentPatternDetector>? _logger;

    // Microsoft documentation URLs
    private const string AzureOpenAIPromptUrl = "https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/prompt-engineering";
    private const string SemanticKernelUrl = "https://learn.microsoft.com/en-us/semantic-kernel/";
    private const string AzureAISearchUrl = "https://learn.microsoft.com/en-us/azure/search/";
    private const string AzureContentSafetyUrl = "https://learn.microsoft.com/en-us/azure/ai-services/content-safety/";
    private const string PromptWizardUrl = "https://www.microsoft.com/en-us/research/blog/promptwizard-the-future-of-prompt-optimization-through-feedback-driven-self-evolving-prompts/";
    private const string MicrosoftGuidanceUrl = "https://github.com/microsoft/guidance";

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

    #region Category 1: Prompt Engineering & Guardrails

    private List<CodePattern> DetectSystemPromptDefinition(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Large const/static strings with instruction keywords
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
        
        foreach (var field in fields)
        {
            if (field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword) || m.IsKind(SyntaxKind.StaticKeyword)))
            {
                var fieldText = field.ToString();
                var variable = field.Declaration.Variables.FirstOrDefault();
                var name = variable?.Identifier.Text ?? "";
                
                // Check for prompt/instruction-related names
                if (name.Contains("Prompt") || name.Contains("System") || name.Contains("Persona") ||
                    name.Contains("Instruction") || name.Contains("Policy") || name.Contains("Role"))
                {
                    // Check for instruction keywords in the value
                    if (fieldText.Length > 100 && ContainsInstructionKeywords(fieldText))
                    {
                        var lineNumber = GetLineNumber(root, field, sourceCode);
                        patterns.Add(CreatePattern(
                            name: "AI_SystemPromptDefinition",
                            type: PatternType.AgentLightning,
                            category: PatternCategory.AIAgents,
                            implementation: $"System prompt definition: {name}",
                            filePath: filePath,
                            lineNumber: lineNumber,
                            content: GetContextAroundNode(field, sourceCode, 5),
                            bestPractice: "Define system prompts as constants for reusability and version control. Use Microsoft Guidance or Semantic Kernel for structured prompts.",
                            azureUrl: AzureOpenAIPromptUrl,
                            context: context,
                            confidence: 0.90f,
                            metadata: new Dictionary<string, object>
                            {
                                ["prompt_name"] = name,
                                ["pattern_type"] = "System Prompt",
                                ["best_practices"] = new[] { "Version control", "Prompt templates", "A/B testing" }
                            }
                        ));
                    }
                }
            }
        }

        // Pattern: Classes/records storing prompts
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Prompt") || className.Contains("Persona") || 
                className.Contains("Policy") || className.Contains("Agent") && className.Contains("Config"))
            {
                var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                
                if (properties.Any(p => p.Identifier.Text.Contains("System") || 
                                       p.Identifier.Text.Contains("Message") ||
                                       p.Identifier.Text.Contains("Instruction")))
                {
                    var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                    patterns.Add(CreatePattern(
                        name: "AI_PromptPolicyClass",
                        type: PatternType.AgentLightning,
                        category: PatternCategory.AIAgents,
                        implementation: $"Prompt policy class: {className}",
                        filePath: filePath,
                        lineNumber: lineNumber,
                        content: GetContextAroundNode(classDecl, sourceCode, 10),
                        bestPractice: "Use dedicated classes for agent prompts and policies to enable configuration and testing.",
                        azureUrl: AzureOpenAIPromptUrl,
                        context: context,
                        confidence: 0.88f,
                        metadata: new Dictionary<string, object>
                        {
                            ["class_name"] = className,
                            ["pattern_type"] = "Prompt Class"
                        }
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPromptTemplates(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Semantic Kernel ChatPromptTemplate
        if (sourceCode.Contains("ChatPromptTemplate") || sourceCode.Contains("PromptTemplate"))
        {
            patterns.Add(CreatePattern(
                name: "AI_PromptTemplate",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Prompt template usage (Semantic Kernel or similar)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Prompt template detected",
                bestPractice: "Use prompt templates for parameterized, reusable prompts. Semantic Kernel and Microsoft Guidance provide robust templating.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["template_engine"] = "Semantic Kernel or equivalent",
                    ["benefits"] = new[] { "Reusability", "Parameterization", "Version control" }
                }
            ));
        }

        // Pattern: Handlebars-style templates (Microsoft Guidance)
        var handlebarPattern = new Regex(@"\{\{[\w_]+\}\}");
        if (handlebarPattern.IsMatch(sourceCode))
        {
            patterns.Add(CreatePattern(
                name: "AI_HandlebarsTemplate",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Handlebars-style prompt template ({{placeholder}})",
                filePath: filePath,
                lineNumber: 1,
                content: "// {{placeholder}} template syntax detected",
                bestPractice: "Handlebars templates enable structured prompts with placeholders. Consider Microsoft Guidance for advanced constraint-based generation.",
                azureUrl: MicrosoftGuidanceUrl,
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["template_syntax"] = "Handlebars",
                    ["suggestion"] = "Consider Microsoft Guidance library"
                }
            ));
        }

        // Pattern: String interpolation with multiple variables (basic templating)
        var interpolationPattern = new Regex(@"\$""[^""]*\{[^}]+\}[^""]*\{[^}]+\}");
        if (interpolationPattern.IsMatch(sourceCode))
        {
            patterns.Add(CreatePattern(
                name: "AI_StringInterpolationTemplate",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "String interpolation for prompts (basic templating)",
                filePath: filePath,
                lineNumber: 1,
                content: "// String interpolation prompt detected",
                bestPractice: "For complex prompts, consider upgrading to a template engine like Semantic Kernel or Microsoft Guidance for better maintainability.",
                azureUrl: AzureOpenAIPromptUrl,
                context: context,
                confidence: 0.75f,
                metadata: new Dictionary<string, object>
                {
                    ["template_type"] = "String interpolation",
                    ["recommendation"] = "Upgrade to structured template engine"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectGuardrailInjection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Azure Content Safety usage
        if (sourceCode.Contains("ContentSafetyClient") || sourceCode.Contains("ContentSafety"))
        {
            patterns.Add(CreatePattern(
                name: "AI_ContentSafetyGuardrail",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Azure Content Safety integration for content moderation",
                filePath: filePath,
                lineNumber: 1,
                content: "// Azure Content Safety detected",
                bestPractice: "Use Azure Content Safety to moderate harmful content before and after LLM calls.",
                azureUrl: AzureContentSafetyUrl,
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["service"] = "Azure Content Safety",
                    ["categories"] = new[] { "Hate", "Violence", "Sexual", "Self-harm" }
                }
            ));
        }

        // Pattern: Safety guidelines injection in prompts
        if (sourceCode.Contains("Safety") && (sourceCode.Contains("Guidelines") || sourceCode.Contains("Policy")))
        {
            var safetyPattern = new Regex(@"(Safety|Policy|Guidelines|Prohibited|Must not)", RegexOptions.IgnoreCase);
            if (safetyPattern.IsMatch(sourceCode))
            {
                patterns.Add(CreatePattern(
                    name: "AI_SafetyPolicyInjection",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: "Safety policy/guidelines injection into prompts",
                    filePath: filePath,
                    lineNumber: 1,
                    content: "// Safety policy injection detected",
                    bestPractice: "Inject safety guidelines into system prompts to constrain agent behavior.",
                    azureUrl: AzureOpenAIPromptUrl,
                    context: context,
                    confidence: 0.80f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Policy Injection",
                        ["purpose"] = "Behavior constraints"
                    }
                ));
            }
        }

        // Pattern: Spotlighting technique (Microsoft's prompt injection mitigation)
        var spotlightPattern = new Regex(@"```(user_input|input|data)");
        if (spotlightPattern.IsMatch(sourceCode))
        {
            patterns.Add(CreatePattern(
                name: "AI_SpotlightingGuardrail",
                type: PatternType.Security,
                category: PatternCategory.Security,
                implementation: "Spotlighting technique for prompt injection mitigation",
                filePath: filePath,
                lineNumber: 1,
                content: "// Spotlighting pattern detected (```user_input```)",
                bestPractice: "Microsoft's Spotlighting technique uses delimiters to separate user input from instructions, mitigating prompt injection attacks.",
                azureUrl: "https://www.microsoft.com/en-us/security/blog/2024/04/11/how-microsoft-discovers-and-mitigates-evolving-attacks-against-ai-guardrails/",
                context: context,
                confidence: 0.92f,
                metadata: new Dictionary<string, object>
                {
                    ["technique"] = "Spotlighting",
                    ["protection"] = "Prompt injection mitigation"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region Category 2: Memory & State

    private List<CodePattern> DetectShortTermMemoryBuffer(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Chat history lists
        var variableDeclarations = root.DescendantNodes().OfType<VariableDeclarationSyntax>();
        
        foreach (var varDecl in variableDeclarations)
        {
            var typeName = varDecl.Type.ToString();
            
            if ((typeName.Contains("List") || typeName.Contains("IEnumerable") || typeName.Contains("[]")) &&
                (typeName.Contains("ChatMessage") || typeName.Contains("Message") || 
                 typeName.Contains("ConversationTurn") || typeName.Contains("ChatHistory")))
            {
                var variable = varDecl.Variables.FirstOrDefault();
                var name = variable?.Identifier.Text ?? "";
                var lineNumber = GetLineNumber(root, varDecl, sourceCode);
                
                patterns.Add(CreatePattern(
                    name: "AI_ShortTermMemoryBuffer",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.StateManagement,
                    implementation: $"Chat history buffer: {name}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(varDecl, sourceCode, 5),
                    bestPractice: "Maintain chat history for multi-turn conversations. This is essential for agent context and continuity.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["memory_type"] = "Short-term (chat history)",
                        ["variable_name"] = name,
                        ["pattern_significance"] = "CRITICAL - Without this, it's just a single LLM call, not an agent"
                    }
                ));
                break; // One detection per file is sufficient
            }
        }

        // Pattern: ConversationBuffer-style classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("ConversationBuffer") || className.Contains("MessageBuffer") ||
                className.Contains("ChatHistory") || className.Contains("ConversationMemory"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_ConversationBufferClass",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.StateManagement,
                    implementation: $"Conversation buffer class: {className}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Dedicated conversation buffer classes enable better memory management and testing.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["class_name"] = className,
                        ["pattern_significance"] = "CRITICAL - Core agent memory"
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectLongTermMemoryVector(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Vector store usage (Qdrant, Azure AI Search, Pinecone)
        var vectorStoreKeywords = new[] { 
            "QdrantClient", "Qdrant", 
            "SearchClient", "AzureSearch", 
            "PineconeClient", "Pinecone",
            "VectorStore", "EmbeddingStore",
            "Weaviate", "Chroma", "Milvus"
        };

        foreach (var keyword in vectorStoreKeywords)
        {
            if (sourceCode.Contains(keyword))
            {
                patterns.Add(CreatePattern(
                    name: "AI_LongTermMemoryVector",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.StateManagement,
                    implementation: $"Vector store integration: {keyword}",
                    filePath: filePath,
                    lineNumber: 1,
                    content: $"// {keyword} detected",
                    bestPractice: "Vector stores enable long-term agent memory beyond conversation history. Use Azure AI Search, Qdrant, or Pinecone for semantic memory.",
                    azureUrl: AzureAISearchUrl,
                    context: context,
                    confidence: 0.95f,
                    metadata: new Dictionary<string, object>
                    {
                        ["vector_store"] = keyword,
                        ["memory_type"] = "Long-term (vector/semantic)",
                        ["pattern_significance"] = "HIGH - Enables agent knowledge beyond training data"
                    }
                ));
                break; // One detection sufficient
            }
        }

        // Pattern: Semantic Kernel memory connectors
        if (sourceCode.Contains("SemanticTextMemory") || sourceCode.Contains("ISemanticTextMemory"))
        {
            patterns.Add(CreatePattern(
                name: "AI_SemanticKernelMemory",
                type: PatternType.AgentLightning,
                category: PatternCategory.StateManagement,
                implementation: "Semantic Kernel text memory integration",
                filePath: filePath,
                lineNumber: 1,
                content: "// Semantic Kernel memory detected",
                bestPractice: "Semantic Kernel provides abstractions for vector memory stores. Enables agent recall and knowledge retrieval.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["framework"] = "Semantic Kernel",
                    ["memory_type"] = "Semantic/Vector"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectUserProfileMemory(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: UserProfile/AgentPersona classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("UserProfile") || className.Contains("AgentPersona") ||
                className.Contains("PersonaStore") || className.Contains("ProfileStore") ||
                (className.Contains("Memory") && className.Contains("Store")))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_UserProfileMemory",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.StateManagement,
                    implementation: $"User/agent profile memory: {className}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Store user preferences and agent personas for personalized interactions. Key by user ID or agent ID.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.88f,
                    metadata: new Dictionary<string, object>
                    {
                        ["class_name"] = className,
                        ["memory_type"] = "User/Agent Profile",
                        ["use_cases"] = new[] { "Personalization", "Preferences", "Agent personas" }
                    }
                ));
                break;
            }
        }

        // Pattern: Memory keys with user/agent prefixes
        var memoryKeyPattern = new Regex(@"""(user|agent|profile):[^""]+""");
        if (memoryKeyPattern.IsMatch(sourceCode))
        {
            patterns.Add(CreatePattern(
                name: "AI_KeyedProfileMemory",
                type: PatternType.AgentLightning,
                category: PatternCategory.StateManagement,
                implementation: "Keyed profile memory (user:*, agent:* patterns)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Keyed memory pattern detected",
                bestPractice: "Use consistent key patterns (user:{id}, agent:{id}) for profile storage and retrieval.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.82f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Keyed memory",
                    ["key_format"] = "user:*, agent:*, profile:*"
                }
            ));
        }

        return patterns;
    }

    #endregion

    #region Category 3: Tools & Function Calling

    private List<CodePattern> DetectToolRegistration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Semantic Kernel [KernelFunction] attribute
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var attributes = method.AttributeLists.SelectMany(al => al.Attributes);
            
            if (attributes.Any(a => a.Name.ToString().Contains("KernelFunction")))
            {
                var methodName = method.Identifier.Text;
                var lineNumber = GetLineNumber(root, method, sourceCode);
                
                patterns.Add(CreatePattern(
                    name: "AI_KernelFunctionRegistration",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: $"Kernel function: {methodName}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 8),
                    bestPractice: "Use [KernelFunction] attribute to register functions/tools that agents can call. Add [Description] for better LLM understanding.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.98f,
                    metadata: new Dictionary<string, object>
                    {
                        ["function_name"] = methodName,
                        ["framework"] = "Semantic Kernel",
                        ["pattern_significance"] = "HIGH - Agent tool capability"
                    }
                ));
            }
        }

        // Pattern: Tool/function collections
        if (sourceCode.Contains("FunctionDef") || sourceCode.Contains("ToolManifest") ||
            sourceCode.Contains("ToolRegistry") || sourceCode.Contains("List<Tool>"))
        {
            patterns.Add(CreatePattern(
                name: "AI_ToolCollection",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "Tool/function collection for agent capabilities",
                filePath: filePath,
                lineNumber: 1,
                content: "// Tool collection detected",
                bestPractice: "Maintain a registry of available tools/functions for the agent. Enables dynamic tool discovery and execution.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Tool Collection",
                    ["significance"] = "Distinguishes agent from simple LLM call"
                }
            ));
        }

        // Pattern: ITool interface implementations
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        
        foreach (var interfaceDecl in interfaces)
        {
            var interfaceName = interfaceDecl.Identifier.Text;
            
            if (interfaceName == "ITool" || interfaceName == "IPlugin" || interfaceName == "IFunction")
            {
                var lineNumber = GetLineNumber(root, interfaceDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_ToolInterface",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: $"Tool interface definition: {interfaceName}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(interfaceDecl, sourceCode, 10),
                    bestPractice: "Define standard tool interfaces for consistent agent tool integration.",
                    azureUrl: SemanticKernelUrl,
                    context: context,
                    confidence: 0.92f,
                    metadata: new Dictionary<string, object>
                    {
                        ["interface_name"] = interfaceName
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectToolRouting(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: FunctionCall inspection and dispatch
        if ((sourceCode.Contains("FunctionCall") || sourceCode.Contains("ToolCall")) &&
            (sourceCode.Contains("Execute") || sourceCode.Contains("Invoke") || sourceCode.Contains("Dispatch")))
        {
            patterns.Add(CreatePattern(
                name: "AI_ToolRouting",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "Tool routing/dispatch based on LLM function calls",
                filePath: filePath,
                lineNumber: 1,
                content: "// Tool routing detected",
                bestPractice: "Inspect LLM outputs for function/tool calls and route to appropriate handlers. Core pattern for agentic behavior.",
                azureUrl: AzureOpenAIPromptUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Tool Routing",
                    ["significance"] = "CRITICAL - Enables agent actions beyond text generation"
                }
            ));
        }

        // Pattern: ReAct-style action dispatch in loops
        if (sourceCode.Contains("Action") && sourceCode.Contains("while"))
        {
            var actionDispatchPattern = new Regex(@"while.*\{[^}]*(Action|Tool|Function).*Execute", RegexOptions.Singleline);
            if (actionDispatchPattern.IsMatch(sourceCode))
            {
                patterns.Add(CreatePattern(
                    name: "AI_ActionDispatchLoop",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: "Action dispatch in agent loop (ReAct pattern)",
                    filePath: filePath,
                    lineNumber: 1,
                    content: "// Action dispatch loop detected",
                    bestPractice: "ReAct pattern: loop of reasoning → action → observation. Enables autonomous multi-step agent behavior.",
                    azureUrl: "https://arxiv.org/abs/2210.03629",
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "ReAct Tool Dispatch",
                        ["significance"] = "HIGH - Autonomous agent loop"
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectExternalServiceTool(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Tools with HttpClient (external APIs)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Tool") || className.Contains("Plugin") || className.Contains("Function"))
            {
                var classText = classDecl.ToString();
                
                if (classText.Contains("HttpClient") || classText.Contains("_http") || classText.Contains("_client"))
                {
                    var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                    patterns.Add(CreatePattern(
                        name: "AI_ExternalAPITool",
                        type: PatternType.AgentLightning,
                        category: PatternCategory.ToolIntegration,
                        implementation: $"External API tool: {className}",
                        filePath: filePath,
                        lineNumber: lineNumber,
                        content: GetContextAroundNode(classDecl, sourceCode, 10),
                        bestPractice: "Tools that call external APIs extend agent capabilities beyond the model. Ensure proper error handling and timeouts.",
                        azureUrl: SemanticKernelUrl,
                        context: context,
                        confidence: 0.90f,
                        metadata: new Dictionary<string, object>
                        {
                            ["tool_class"] = className,
                            ["integration_type"] = "External API",
                            ["significance"] = "Agent has external capabilities"
                        }
                    ));
                }
            }
        }

        // Pattern: Database access tools
        if ((sourceCode.Contains("SqlConnection") || sourceCode.Contains("DbContext")) &&
            (sourceCode.Contains("Tool") || sourceCode.Contains("Function")))
        {
            patterns.Add(CreatePattern(
                name: "AI_DatabaseTool",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "Database access tool for agent",
                filePath: filePath,
                lineNumber: 1,
                content: "// Database tool detected",
                bestPractice: "Database tools enable agents to query and manipulate data. Implement proper authorization and SQL injection protection.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["integration_type"] = "Database",
                    ["security_note"] = "Requires SQL injection protection"
                }
            ));
        }

        // Pattern: File system tools
        if ((sourceCode.Contains("File.Read") || sourceCode.Contains("Directory.") || sourceCode.Contains("FileSystem")) &&
            (sourceCode.Contains("Tool") || sourceCode.Contains("Function")))
        {
            patterns.Add(CreatePattern(
                name: "AI_FileSystemTool",
                type: PatternType.AgentLightning,
                category: PatternCategory.ToolIntegration,
                implementation: "File system access tool",
                filePath: filePath,
                lineNumber: 1,
                content: "// File system tool detected",
                bestPractice: "File system tools enable agents to read/write files. Implement path validation and access controls.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.85f,
                metadata: new Dictionary<string, object>
                {
                    ["integration_type"] = "File System",
                    ["security_note"] = "Requires path validation"
                }
            ));
        }

        return patterns;
    }

    #endregion

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

    #region Category 5: RAG & Knowledge Integration

    private List<CodePattern> DetectEmbeddingGeneration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Azure OpenAI embedding calls
        if (sourceCode.Contains("GetEmbeddings") || sourceCode.Contains("GenerateEmbedding") ||
            sourceCode.Contains("text-embedding"))
        {
            patterns.Add(CreatePattern(
                name: "AI_EmbeddingGeneration",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Embedding generation for vector storage",
                filePath: filePath,
                lineNumber: 1,
                content: "// Embedding generation detected",
                bestPractice: "Generate embeddings for semantic search and RAG. Use Azure OpenAI text-embedding-ada-002 or text-embedding-3-large.",
                azureUrl: "https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/embeddings",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Embedding Generation",
                    ["use_case"] = "Vector search, RAG",
                    ["recommended_models"] = new[] { "text-embedding-ada-002", "text-embedding-3-large" }
                }
            ));
        }

        // Pattern: Semantic Kernel text embedding
        if (sourceCode.Contains("ITextEmbeddingGeneration") || sourceCode.Contains("TextEmbedding"))
        {
            patterns.Add(CreatePattern(
                name: "AI_SemanticKernelEmbedding",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Semantic Kernel text embedding generation",
                filePath: filePath,
                lineNumber: 1,
                content: "// Semantic Kernel embedding detected",
                bestPractice: "Semantic Kernel abstracts embedding generation across providers. Enables easy switching between Azure OpenAI, OpenAI, etc.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.98f,
                metadata: new Dictionary<string, object>
                {
                    ["framework"] = "Semantic Kernel"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectVectorSearchRAG(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Retrieve → Augment → Generate pipeline
        var hasEmbedding = sourceCode.Contains("embedding") || sourceCode.Contains("Embedding");
        var hasSearch = sourceCode.Contains("Search") || sourceCode.Contains("search") || 
                       sourceCode.Contains("Similarity") || sourceCode.Contains("Recall");
        var hasContextInjection = sourceCode.Contains("context") && sourceCode.Contains("prompt");
        
        if (hasEmbedding && hasSearch && hasContextInjection)
        {
            patterns.Add(CreatePattern(
                name: "AI_RAGPipeline",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "RAG pipeline: Retrieve → Augment → Generate",
                filePath: filePath,
                lineNumber: 1,
                content: "// RAG pipeline detected",
                bestPractice: "RAG (Retrieval-Augmented Generation) extends agent knowledge beyond training data. Essential pattern for production agents.",
                azureUrl: AzureAISearchUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "RAG Pipeline",
                    ["significance"] = "HIGH - Enables agent knowledge beyond training cutoff",
                    ["steps"] = new[] { "Retrieve", "Augment", "Generate" }
                }
            ));
        }

        // Pattern: Semantic Kernel memory search
        if (sourceCode.Contains("SearchAsync") && 
            (sourceCode.Contains("memory") || sourceCode.Contains("Memory")))
        {
            patterns.Add(CreatePattern(
                name: "AI_SemanticMemorySearch",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Semantic Kernel memory search for RAG",
                filePath: filePath,
                lineNumber: 1,
                content: "// Semantic memory search detected",
                bestPractice: "Semantic Kernel memory connectors enable RAG with minimal code. Supports Azure AI Search, Qdrant, Pinecone, and more.",
                azureUrl: SemanticKernelUrl,
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["framework"] = "Semantic Kernel",
                    ["pattern"] = "Semantic Search"
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectRAGOrchestrator(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Conditional RAG (decision whether to retrieve)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            
            if ((methodBody.Contains("RequiresKnowledge") || methodBody.Contains("ShouldRetrieve") ||
                 methodBody.Contains("NeedsContext")) &&
                methodBody.Contains("if"))
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_ConditionalRAG",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.AIAgents,
                    implementation: "Conditional RAG orchestration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 12),
                    bestPractice: "Conditional RAG optimizes costs by only retrieving when necessary. Use LLM or heuristics to decide if external knowledge is needed.",
                    azureUrl: AzureAISearchUrl,
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Conditional RAG",
                        ["benefit"] = "Cost optimization"
                    }
                ));
                break;
            }
        }

        // Pattern: Hybrid search (vector + keyword)
        if ((sourceCode.Contains("vector") && sourceCode.Contains("keyword")) ||
            (sourceCode.Contains("hybrid") && sourceCode.Contains("search")))
        {
            patterns.Add(CreatePattern(
                name: "AI_HybridSearch",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Hybrid search (vector + keyword)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Hybrid search detected",
                bestPractice: "Hybrid search combines semantic (vector) and lexical (keyword) search for better retrieval quality. Azure AI Search supports this natively.",
                azureUrl: AzureAISearchUrl,
                context: context,
                confidence: 0.90f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Hybrid Search",
                    ["types"] = new[] { "Vector (semantic)", "Keyword (lexical)" }
                }
            ));
        }

        // Pattern: Reranking
        if (sourceCode.Contains("Rerank") || sourceCode.Contains("rerank"))
        {
            patterns.Add(CreatePattern(
                name: "AI_Reranking",
                type: PatternType.AgentLightning,
                category: PatternCategory.AIAgents,
                implementation: "Result reranking for improved retrieval",
                filePath: filePath,
                lineNumber: 1,
                content: "// Reranking detected",
                bestPractice: "Reranking refines initial search results using cross-encoders or LLMs for better relevance. Common in production RAG systems.",
                azureUrl: AzureAISearchUrl,
                context: context,
                confidence: 0.88f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Reranking"
                }
            ));
        }

        return patterns;
    }

    #endregion

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

        // Pattern: Tenant ID in collection/index names
        var tenantPattern = new Regex(@"""tenant[_-]?\{[^}]+\}""");
        if (tenantPattern.IsMatch(sourceCode) || sourceCode.Contains("tenant_id"))
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

    #region Category 9: Advanced Multi-Agent Patterns (NEW - Critical Gap Filled)

    private List<CodePattern> DetectGroupChatPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: GroupChat (AutoGen-style)
        if (sourceCode.Contains("GroupChat") || sourceCode.Contains("ConversableAgent"))
        {
            patterns.Add(CreatePattern(
                name: "AI_GroupChatOrchestration",
                type: PatternType.AgentLightning,
                category: PatternCategory.MultiAgentOrchestration,
                implementation: "Group chat multi-agent orchestration (AutoGen pattern)",
                filePath: filePath,
                lineNumber: 1,
                content: "// Group chat orchestration detected",
                bestPractice: "Group chat enables multiple agents to communicate in a shared environment. AutoGen's pattern allows agents to self-organize and collaborate.",
                azureUrl: "https://microsoft.github.io/autogen/docs/tutorial/conversation-patterns",
                context: context,
                confidence: 0.95f,
                metadata: new Dictionary<string, object>
                {
                    ["pattern"] = "Group Chat",
                    ["framework"] = "AutoGen",
                    ["significance"] = "ADVANCED - Multi-agent collaboration",
                    ["benefits"] = new[] { "Self-organization", "Emergent behavior", "Flexible collaboration" }
                }
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectSequentialOrchestration(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Sequential agent execution
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? "";
            
            // Look for sequential agent calls
            var agentCallPattern = new Regex(@"await\s+\w*[Aa]gent\w*\..*Async");
            var matches = agentCallPattern.Matches(methodBody);
            
            if (matches.Count >= 2)
            {
                var lineNumber = GetLineNumber(root, method, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_SequentialOrchestration",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.MultiAgentOrchestration,
                    implementation: "Sequential multi-agent orchestration",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(method, sourceCode, 10),
                    bestPractice: "Sequential orchestration chains agent outputs. Agent A's result feeds into Agent B, creating a pipeline.",
                    azureUrl: "https://learn.microsoft.com/en-us/training/modules/agent-orchestration-patterns/",
                    context: context,
                    confidence: 0.85f,
                    metadata: new Dictionary<string, object>
                    {
                        ["pattern"] = "Sequential Orchestration",
                        ["agent_calls"] = matches.Count,
                        ["use_case"] = "Agent pipelines and workflows"
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectControlPlanePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Control plane as tool (modular tool routing)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if ((className.Contains("ControlPlane") || className.Contains("ToolRouter") || 
                 className.Contains("ToolDispatcher")) &&
                classDecl.ToString().Contains("Tool"))
            {
                var lineNumber = GetLineNumber(root, classDecl, sourceCode);
                patterns.Add(CreatePattern(
                    name: "AI_ControlPlaneAsATool",
                    type: PatternType.AgentLightning,
                    category: PatternCategory.ToolIntegration,
                    implementation: $"Control plane as tool pattern: {className}",
                    filePath: filePath,
                    lineNumber: lineNumber,
                    content: GetContextAroundNode(classDecl, sourceCode, 10),
                    bestPractice: "Control plane pattern encapsulates modular tool routing logic behind a single tool interface. Improves scalability, safety, and extensibility.",
                    azureUrl: "https://arxiv.org/abs/2505.06817",
                    context: context,
                    confidence: 0.88f,
                    metadata: new Dictionary<string, object>
                    {
                        ["class_name"] = className,
                        ["pattern"] = "Control Plane",
                        ["benefits"] = new[] { "Scalability", "Safety", "Extensibility", "Modular tool routing" }
                    }
                ));
                break;
            }
        }

        return patterns;
    }

    #endregion

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

    #region Helper Methods

    private bool ContainsInstructionKeywords(string text)
    {
        var keywords = new[] { 
            "You are", "Act as", "Your role", "You must", "Never", 
            "Always", "Follow these", "Instructions:", "Guidelines:",
            "helpful assistant", "expert in"
        };
        
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private int GetLineNumber(SyntaxNode root, SyntaxNode node, string sourceCode)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return lineSpan.StartLinePosition.Line + 1;
    }

    private string GetContextAroundNode(SyntaxNode node, string sourceCode, int contextLines)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        var endLine = Math.Min(sourceCode.Split('\n').Length - 1, lineSpan.EndLinePosition.Line + contextLines);

        var lines = sourceCode.Split('\n');
        var relevantLines = lines.Skip(startLine).Take(endLine - startLine + 1);

        return string.Join('\n', relevantLines);
    }

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
        bool isPositive = true,
        Dictionary<string, object>? metadata = null)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            FilePath = filePath,
            LineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            DetectedAt = DateTime.UtcNow,
            Context = context ?? "",
            Confidence = confidence,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    #endregion
}

