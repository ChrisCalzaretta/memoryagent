using System.Text.Json;
using MemoryAgent.PatternManagement.Models;
using Neo4j.Driver;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace MemoryAgent.PatternManagement.Services;

/// <summary>
/// Manages global patterns in Neo4j (relationships) and Qdrant (embeddings)
/// </summary>
public class PatternService : IPatternService
{
    private readonly IDriver _neo4j;
    private readonly QdrantClient _qdrant;
    private readonly IEmbeddingService _embedding;
    private readonly IPatternSyncService _sync;
    private readonly ILogger<PatternService> _logger;
    
    private const string MASTER_COLLECTION = "global_patterns_master";
    private const int VECTOR_SIZE = 1024; // mxbai-embed-large
    
    public PatternService(
        IDriver neo4j,
        QdrantClient qdrant,
        IEmbeddingService embedding,
        IPatternSyncService sync,
        ILogger<PatternService> logger)
    {
        _neo4j = neo4j;
        _qdrant = qdrant;
        _embedding = embedding;
        _sync = sync;
        _logger = logger;
    }
    
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Ensure Qdrant collection exists
        var collections = await _qdrant.ListCollectionsAsync(ct);
        if (!collections.Any(c => c == MASTER_COLLECTION))
        {
            await _qdrant.CreateCollectionAsync(
                MASTER_COLLECTION,
                new VectorParams { Size = VECTOR_SIZE, Distance = Distance.Cosine },
                cancellationToken: ct);
            
            _logger.LogInformation("Created master collection: {Collection}", MASTER_COLLECTION);
        }
        
        // Ensure Neo4j constraints
        await using var session = _neo4j.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
                CREATE CONSTRAINT IF NOT EXISTS FOR (p:GlobalPattern) REQUIRE p.id IS UNIQUE
            ");
            await tx.RunAsync(@"
                CREATE CONSTRAINT IF NOT EXISTS FOR (p:GlobalPattern) REQUIRE p.name IS UNIQUE
            ");
            await tx.RunAsync(@"
                CREATE INDEX IF NOT EXISTS FOR (p:GlobalPattern) ON (p.category)
            ");
        });
        
        _logger.LogInformation("PatternService initialized");
    }
    
    public async Task<GlobalPattern> CreatePatternAsync(GlobalPattern pattern, CancellationToken ct = default)
    {
        pattern.Id = Guid.NewGuid().ToString();
        pattern.CreatedAt = DateTime.UtcNow;
        pattern.UpdatedAt = DateTime.UtcNow;
        pattern.Version = 1;
        
        // 1. Save to Neo4j
        await SaveToNeo4jAsync(pattern, ct);
        
        // 2. Generate embedding and save to Qdrant master
        await SaveToQdrantMasterAsync(pattern, ct);
        
        // 3. Sync to all workspaces
        await _sync.SyncPatternToAllWorkspacesAsync(pattern, ct);
        
        _logger.LogInformation("Created pattern: {Name} ({Id})", pattern.Name, pattern.Id);
        
        return pattern;
    }
    
    public async Task<GlobalPattern> UpdatePatternAsync(GlobalPattern pattern, CancellationToken ct = default)
    {
        pattern.UpdatedAt = DateTime.UtcNow;
        pattern.Version++;
        
        // 1. Update in Neo4j
        await SaveToNeo4jAsync(pattern, ct);
        
        // 2. Update in Qdrant master
        await SaveToQdrantMasterAsync(pattern, ct);
        
        // 3. Sync to all workspaces
        await _sync.SyncPatternToAllWorkspacesAsync(pattern, ct);
        
        _logger.LogInformation("Updated pattern: {Name} (v{Version})", pattern.Name, pattern.Version);
        
        return pattern;
    }
    
    public async Task<bool> DeletePatternAsync(string patternId, CancellationToken ct = default)
    {
        // 1. Delete from Neo4j
        await using var session = _neo4j.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
                MATCH (p:GlobalPattern {id: $id})
                DETACH DELETE p
            ", new { id = patternId });
        });
        
        // 2. Delete from Qdrant master
        await _qdrant.DeleteAsync(
            MASTER_COLLECTION, 
            new PointIdList { Ids = { new PointId { Uuid = patternId } } },
            cancellationToken: ct);
        
        // 3. Remove from all workspaces
        await _sync.RemovePatternFromAllWorkspacesAsync(patternId, ct);
        
        _logger.LogInformation("Deleted pattern: {Id}", patternId);
        
        return true;
    }
    
    public async Task<GlobalPattern?> GetPatternAsync(string patternId, CancellationToken ct = default)
    {
        await using var session = _neo4j.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:GlobalPattern {id: $id})
                OPTIONAL MATCH (p)-[:REQUIRES]->(req:GlobalPattern)
                OPTIONAL MATCH (p)-[:CONFLICTS_WITH]->(conf:GlobalPattern)
                OPTIONAL MATCH (p)-[:SUPERSEDES]->(sup:GlobalPattern)
                RETURN p, collect(DISTINCT req.id) as requires, 
                       collect(DISTINCT conf.id) as conflicts,
                       sup.id as supersedes
            ", new { id = patternId });
            
            if (await cursor.FetchAsync())
            {
                return MapFromNeo4j(cursor.Current);
            }
            return null;
        });
        
        return result;
    }
    
    public async Task<GlobalPattern?> GetPatternByNameAsync(string name, CancellationToken ct = default)
    {
        await using var session = _neo4j.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:GlobalPattern {name: $name})
                OPTIONAL MATCH (p)-[:REQUIRES]->(req:GlobalPattern)
                OPTIONAL MATCH (p)-[:CONFLICTS_WITH]->(conf:GlobalPattern)
                OPTIONAL MATCH (p)-[:SUPERSEDES]->(sup:GlobalPattern)
                RETURN p, collect(DISTINCT req.id) as requires, 
                       collect(DISTINCT conf.id) as conflicts,
                       sup.id as supersedes
            ", new { name });
            
            if (await cursor.FetchAsync())
            {
                return MapFromNeo4j(cursor.Current);
            }
            return null;
        });
        
        return result;
    }
    
    public async Task<List<GlobalPattern>> ListPatternsAsync(
        string? category = null, 
        PatternSeverity? severity = null,
        bool includeDeprecated = false,
        CancellationToken ct = default)
    {
        var patterns = new List<GlobalPattern>();
        
        await using var session = _neo4j.AsyncSession();
        await session.ExecuteReadAsync(async tx =>
        {
            var query = @"
                MATCH (p:GlobalPattern)
                WHERE ($category IS NULL OR p.category = $category)
                AND ($severity IS NULL OR p.severity = $severity)
                AND ($includeDeprecated = true OR p.isDeprecated = false)
                OPTIONAL MATCH (p)-[:REQUIRES]->(req:GlobalPattern)
                OPTIONAL MATCH (p)-[:CONFLICTS_WITH]->(conf:GlobalPattern)
                OPTIONAL MATCH (p)-[:SUPERSEDES]->(sup:GlobalPattern)
                RETURN p, collect(DISTINCT req.id) as requires, 
                       collect(DISTINCT conf.id) as conflicts,
                       sup.id as supersedes
                ORDER BY p.category, p.name
            ";
            
            var cursor = await tx.RunAsync(query, new 
            { 
                category, 
                severity = severity?.ToString(),
                includeDeprecated 
            });
            
            while (await cursor.FetchAsync())
            {
                var pattern = MapFromNeo4j(cursor.Current);
                if (pattern != null) patterns.Add(pattern);
            }
        });
        
        return patterns;
    }
    
    public async Task<List<GlobalPattern>> SearchPatternsAsync(string query, int limit = 10, CancellationToken ct = default)
    {
        // Generate embedding for query
        var queryEmbedding = await _embedding.EmbedAsync(query, ct);
        
        // Search in Qdrant
        var results = await _qdrant.SearchAsync(
            MASTER_COLLECTION,
            queryEmbedding,
            limit: (ulong)limit,
            cancellationToken: ct);
        
        // Get full patterns from Neo4j
        var patterns = new List<GlobalPattern>();
        foreach (var result in results)
        {
            var pattern = await GetPatternAsync(result.Id.Uuid, ct);
            if (pattern != null) patterns.Add(pattern);
        }
        
        return patterns;
    }
    
    public async Task<List<GlobalPattern>> GetRequiredPatternsAsync(string patternId, CancellationToken ct = default)
    {
        var patterns = new List<GlobalPattern>();
        
        await using var session = _neo4j.AsyncSession();
        await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:GlobalPattern {id: $id})-[:REQUIRES*]->(req:GlobalPattern)
                RETURN DISTINCT req
            ", new { id = patternId });
            
            while (await cursor.FetchAsync())
            {
                var pattern = MapPatternNode(cursor.Current["req"].As<INode>());
                patterns.Add(pattern);
            }
        });
        
        return patterns;
    }
    
    public async Task<List<GlobalPattern>> GetConflictingPatternsAsync(string patternId, CancellationToken ct = default)
    {
        var patterns = new List<GlobalPattern>();
        
        await using var session = _neo4j.AsyncSession();
        await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:GlobalPattern {id: $id})-[:CONFLICTS_WITH]->(conf:GlobalPattern)
                RETURN conf
            ", new { id = patternId });
            
            while (await cursor.FetchAsync())
            {
                var pattern = MapPatternNode(cursor.Current["conf"].As<INode>());
                patterns.Add(pattern);
            }
        });
        
        return patterns;
    }
    
    public async Task<GlobalPattern?> GetSupersedesPatternAsync(string patternId, CancellationToken ct = default)
    {
        await using var session = _neo4j.AsyncSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (old:GlobalPattern {id: $id})<-[:SUPERSEDES]-(new:GlobalPattern)
                RETURN new
            ", new { id = patternId });
            
            if (await cursor.FetchAsync())
            {
                return MapPatternNode(cursor.Current["new"].As<INode>());
            }
            return null;
        });
    }
    
    public async Task<int> ImportPatternsAsync(string json, CancellationToken ct = default)
    {
        var patterns = JsonSerializer.Deserialize<List<GlobalPattern>>(json, 
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (patterns == null) return 0;
        
        var count = 0;
        foreach (var pattern in patterns)
        {
            var existing = await GetPatternByNameAsync(pattern.Name, ct);
            if (existing != null)
            {
                pattern.Id = existing.Id;
                pattern.Version = existing.Version;
                await UpdatePatternAsync(pattern, ct);
            }
            else
            {
                await CreatePatternAsync(pattern, ct);
            }
            count++;
        }
        
        return count;
    }
    
    public async Task<string> ExportPatternsAsync(CancellationToken ct = default)
    {
        var patterns = await ListPatternsAsync(includeDeprecated: true, ct: ct);
        return JsonSerializer.Serialize(patterns, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
    
    // ============ Private Helpers ============
    
    private async Task SaveToNeo4jAsync(GlobalPattern pattern, CancellationToken ct)
    {
        await using var session = _neo4j.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            // Upsert pattern node
            await tx.RunAsync(@"
                MERGE (p:GlobalPattern {id: $id})
                SET p.name = $name,
                    p.version = $version,
                    p.category = $category,
                    p.severity = $severity,
                    p.description = $description,
                    p.antiPattern = $antiPattern,
                    p.correctPattern = $correctPattern,
                    p.codeExample = $codeExample,
                    p.validationRules = $validationRules,
                    p.detectionRegex = $detectionRegex,
                    p.tags = $tags,
                    p.isDeprecated = $isDeprecated,
                    p.deprecatedReason = $deprecatedReason,
                    p.createdAt = $createdAt,
                    p.updatedAt = $updatedAt,
                    p.author = $author
            ", new
            {
                id = pattern.Id,
                name = pattern.Name,
                version = pattern.Version,
                category = pattern.Category,
                severity = pattern.Severity.ToString(),
                description = pattern.Description,
                antiPattern = pattern.AntiPattern,
                correctPattern = pattern.CorrectPattern,
                codeExample = pattern.CodeExample,
                validationRules = pattern.ValidationRules,
                detectionRegex = pattern.DetectionRegex,
                tags = pattern.Tags,
                isDeprecated = pattern.IsDeprecated,
                deprecatedReason = pattern.DeprecatedReason,
                createdAt = pattern.CreatedAt.ToString("O"),
                updatedAt = pattern.UpdatedAt.ToString("O"),
                author = pattern.Author
            });
            
            // Clear old relationships
            await tx.RunAsync(@"
                MATCH (p:GlobalPattern {id: $id})-[r:REQUIRES|CONFLICTS_WITH|SUPERSEDES]->()
                DELETE r
            ", new { id = pattern.Id });
            
            // Create REQUIRES relationships
            foreach (var reqId in pattern.RequiresPatterns)
            {
                await tx.RunAsync(@"
                    MATCH (p:GlobalPattern {id: $id}), (req:GlobalPattern {id: $reqId})
                    MERGE (p)-[:REQUIRES]->(req)
                ", new { id = pattern.Id, reqId });
            }
            
            // Create CONFLICTS_WITH relationships
            foreach (var confId in pattern.ConflictsWithPatterns)
            {
                await tx.RunAsync(@"
                    MATCH (p:GlobalPattern {id: $id}), (conf:GlobalPattern {id: $confId})
                    MERGE (p)-[:CONFLICTS_WITH]->(conf)
                ", new { id = pattern.Id, confId });
            }
            
            // Create SUPERSEDES relationship
            if (!string.IsNullOrEmpty(pattern.SupersedesPattern))
            {
                await tx.RunAsync(@"
                    MATCH (p:GlobalPattern {id: $id}), (old:GlobalPattern {id: $oldId})
                    MERGE (p)-[:SUPERSEDES]->(old)
                    SET old.isDeprecated = true,
                        old.deprecatedReason = 'Superseded by ' + p.name
                ", new { id = pattern.Id, oldId = pattern.SupersedesPattern });
            }
        });
    }
    
    private async Task SaveToQdrantMasterAsync(GlobalPattern pattern, CancellationToken ct)
    {
        // Generate embedding from pattern content
        var textToEmbed = $"{pattern.Name} {pattern.Category} {pattern.Description} {pattern.AntiPattern} {pattern.CorrectPattern}";
        var embedding = await _embedding.EmbedAsync(textToEmbed, ct);
        
        // Upsert to Qdrant
        await _qdrant.UpsertAsync(
            MASTER_COLLECTION,
            new[]
            {
                new PointStruct
                {
                    Id = new PointId { Uuid = pattern.Id },
                    Vectors = embedding,
                    Payload =
                    {
                        ["name"] = pattern.Name,
                        ["category"] = pattern.Category,
                        ["severity"] = pattern.Severity.ToString(),
                        ["version"] = pattern.Version,
                        ["isDeprecated"] = pattern.IsDeprecated
                    }
                }
            },
            cancellationToken: ct);
    }
    
    private GlobalPattern? MapFromNeo4j(IRecord record)
    {
        var node = record["p"].As<INode>();
        if (node == null) return null;
        
        var pattern = MapPatternNode(node);
        
        // Map relationships
        pattern.RequiresPatterns = record["requires"].As<List<string>>() ?? new();
        pattern.ConflictsWithPatterns = record["conflicts"].As<List<string>>() ?? new();
        pattern.SupersedesPattern = record["supersedes"].As<string?>();
        
        return pattern;
    }
    
    private GlobalPattern MapPatternNode(INode node)
    {
        return new GlobalPattern
        {
            Id = node["id"].As<string>(),
            Name = node["name"].As<string>(),
            Version = node["version"].As<int>(),
            Category = node["category"].As<string>(),
            Severity = Enum.Parse<PatternSeverity>(node["severity"].As<string>() ?? "Recommended"),
            Description = node["description"].As<string>(),
            AntiPattern = node["antiPattern"].As<string?>(),
            CorrectPattern = node["correctPattern"].As<string?>(),
            CodeExample = node["codeExample"].As<string?>(),
            ValidationRules = node["validationRules"].As<List<string>>() ?? new(),
            DetectionRegex = node["detectionRegex"].As<string?>(),
            Tags = node["tags"].As<List<string>>() ?? new(),
            IsDeprecated = node["isDeprecated"].As<bool>(),
            DeprecatedReason = node["deprecatedReason"].As<string?>(),
            CreatedAt = DateTime.Parse(node["createdAt"].As<string>() ?? DateTime.UtcNow.ToString("O")),
            UpdatedAt = DateTime.Parse(node["updatedAt"].As<string>() ?? DateTime.UtcNow.ToString("O")),
            Author = node["author"].As<string?>()
        };
    }
}

