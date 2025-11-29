using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 2: Memory & State
/// </summary>
public partial class AIAgentPatternDetector
{
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
}
