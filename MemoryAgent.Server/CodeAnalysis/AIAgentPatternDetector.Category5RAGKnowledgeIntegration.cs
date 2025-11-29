using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Category 5: RAG & Knowledge Integration
/// </summary>
public partial class AIAgentPatternDetector
{
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
}
