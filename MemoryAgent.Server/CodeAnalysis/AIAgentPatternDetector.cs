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
            AzureUrl = azureUrl,
            DetectedAt = DateTime.UtcNow,
            Context = context ?? "",
            Confidence = confidence,
            IsPositive = isPositive,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    #endregion
}

