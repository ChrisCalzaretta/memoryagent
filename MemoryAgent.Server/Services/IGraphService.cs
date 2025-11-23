using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for interacting with Neo4j graph database
/// </summary>
public interface IGraphService
{
    /// <summary>
    /// Initialize Neo4j database with constraints and indexes
    /// </summary>
    Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a code memory as a node in the graph
    /// </summary>
    Task StoreCodeNodeAsync(CodeMemory memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store multiple code memories as nodes
    /// </summary>
    Task StoreCodeNodesAsync(List<CodeMemory> memories, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a relationship between code elements
    /// </summary>
    Task CreateRelationshipAsync(CodeRelationship relationship, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create multiple relationships in batch
    /// </summary>
    Task CreateRelationshipsAsync(List<CodeRelationship> relationships, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get impact analysis - what would be affected if this class changes
    /// </summary>
    Task<List<string>> GetImpactAnalysisAsync(string className, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dependency chain for a class
    /// </summary>
    Task<List<string>> GetDependencyChainAsync(string className, int maxDepth = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find circular dependencies
    /// </summary>
    Task<List<List<string>>> FindCircularDependenciesAsync(string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a detected code pattern as a node in the graph
    /// </summary>
    Task StorePatternNodeAsync(CodePattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get patterns by type from Neo4j
    /// </summary>
    Task<List<CodePattern>> GetPatternsByTypeAsync(PatternType type, string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get classes following a specific pattern
    /// </summary>
    Task<List<string>> GetClassesFollowingPatternAsync(string patternName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all nodes and relationships for a file
    /// </summary>
    Task DeleteByFilePathAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check for Neo4j
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);

    // TODO Management
    Task StoreTodoAsync(TodoItem todo, CancellationToken cancellationToken = default);
    Task UpdateTodoAsync(TodoItem todo, CancellationToken cancellationToken = default);
    Task<bool> DeleteTodoAsync(string todoId, CancellationToken cancellationToken = default);
    Task<TodoItem?> GetTodoAsync(string todoId, CancellationToken cancellationToken = default);
    Task<List<TodoItem>> GetTodosAsync(string? context = null, TodoStatus? status = null, CancellationToken cancellationToken = default);

    // Plan Management
    Task StorePlanAsync(DevelopmentPlan plan, CancellationToken cancellationToken = default);
    Task UpdatePlanAsync(DevelopmentPlan plan, CancellationToken cancellationToken = default);
    Task<bool> DeletePlanAsync(string planId, CancellationToken cancellationToken = default);
    Task<DevelopmentPlan?> GetPlanAsync(string planId, CancellationToken cancellationToken = default);
    Task<List<DevelopmentPlan>> GetPlansAsync(string? context = null, PlanStatus? status = null, CancellationToken cancellationToken = default);
}

