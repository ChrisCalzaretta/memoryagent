using MemoryAgent.Server.Models;
using Neo4j.Driver;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for learning from user interactions and improving memory recall.
/// Implements Agent Lightning patterns for self-improving AI assistance.
/// </summary>
public class LearningService : ILearningService
{
    private readonly IDriver _driver;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorService _vectorService;
    private readonly ILogger<LearningService> _logger;
    
    // Domain detection keywords
    private static readonly Dictionary<string, List<string>> DomainKeywords = new()
    {
        ["Authentication"] = new() { "auth", "login", "logout", "jwt", "token", "oauth", "identity", "password", "credential", "session", "cookie" },
        ["Authorization"] = new() { "role", "policy", "permission", "claim", "authorize", "access", "grant", "deny" },
        ["Billing"] = new() { "payment", "invoice", "subscription", "charge", "billing", "price", "cost", "stripe", "paypal" },
        ["Orders"] = new() { "order", "cart", "checkout", "purchase", "shipping", "delivery" },
        ["Users"] = new() { "user", "profile", "account", "registration", "signup", "member" },
        ["Products"] = new() { "product", "catalog", "inventory", "stock", "item", "sku" },
        ["Notifications"] = new() { "notification", "email", "sms", "push", "alert", "message", "send" },
        ["Logging"] = new() { "log", "trace", "debug", "error", "warning", "telemetry", "metrics" },
        ["Caching"] = new() { "cache", "redis", "memory", "distributed", "invalidate" },
        ["DataAccess"] = new() { "repository", "dbcontext", "entity", "migration", "query", "sql", "database" },
        ["API"] = new() { "controller", "endpoint", "rest", "graphql", "swagger", "openapi" },
        ["Background"] = new() { "background", "job", "worker", "queue", "hangfire", "scheduled", "hosted" },
        ["Configuration"] = new() { "config", "settings", "options", "appsettings", "environment" },
        ["Testing"] = new() { "test", "mock", "fixture", "assert", "xunit", "nunit", "fact", "theory" }
    };

    public LearningService(
        IConfiguration configuration,
        IEmbeddingService embeddingService,
        IVectorService vectorService,
        ILogger<LearningService> logger)
    {
        var neo4jUrl = configuration["Neo4j:Url"] ?? "bolt://localhost:7687";
        var neo4jUser = configuration["Neo4j:User"] ?? "neo4j";
        var neo4jPassword = configuration["Neo4j:Password"] ?? "memoryagent";

        _driver = GraphDatabase.Driver(neo4jUrl, AuthTokens.Basic(neo4jUser, neo4jPassword));
        _embeddingService = embeddingService;
        _vectorService = vectorService;
        _logger = logger;
    }

    #region Session Tracking

    public async Task<Session> StartSessionAsync(string context, CancellationToken cancellationToken = default)
    {
        // Normalize context to lowercase for consistent storage
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        
        var session = new Session
        {
            Id = Guid.NewGuid().ToString(),
            Context = normalizedContext,
            StartedAt = DateTime.UtcNow
        };

        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                CREATE (s:Session {
                    id: $id,
                    context: $context,
                    startedAt: datetime($startedAt),
                    summary: '',
                    isActive: true
                })";

            await tx.RunAsync(cypher, new
            {
                id = session.Id,
                context = session.Context,
                startedAt = session.StartedAt.ToString("O")
            });
        });

        _logger.LogInformation("🆕 Started new session {SessionId} for context {Context}", session.Id, normalizedContext);
        return session;
    }

    public async Task EndSessionAsync(string sessionId, string? summary = null, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MATCH (s:Session {id: $sessionId})
                SET s.endedAt = datetime($endedAt),
                    s.isActive = false,
                    s.summary = $summary";

            await tx.RunAsync(cypher, new
            {
                sessionId,
                endedAt = DateTime.UtcNow.ToString("O"),
                summary = summary ?? ""
            });
        });

        _logger.LogInformation("✅ Ended session {SessionId}", sessionId);
    }

    public async Task<Session?> GetActiveSessionAsync(string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (s:Session {context: $context, isActive: true})
                RETURN s
                ORDER BY s.startedAt DESC
                LIMIT 1";

            var cursor = await tx.RunAsync(cypher, new { context = normalizedContext });
            
            if (await cursor.FetchAsync())
            {
                var node = cursor.Current["s"].As<INode>();
                return MapSessionFromNode(node);
            }
            
            return null;
        });
    }

    public async Task RecordFileDiscussedAsync(string sessionId, string filePath, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            // Create or update DISCUSSED_IN relationship
            var cypher = @"
                MATCH (s:Session {id: $sessionId})
                MERGE (f:File {path: $filePath})
                MERGE (f)-[r:DISCUSSED_IN]->(s)
                ON CREATE SET r.count = 1, r.firstDiscussedAt = datetime()
                ON MATCH SET r.count = r.count + 1, r.lastDiscussedAt = datetime()
                
                WITH s, f
                SET s.filesDiscussed = CASE 
                    WHEN s.filesDiscussed IS NULL THEN [$filePath]
                    WHEN NOT $filePath IN s.filesDiscussed THEN s.filesDiscussed + $filePath
                    ELSE s.filesDiscussed
                END";

            await tx.RunAsync(cypher, new { sessionId, filePath });
        });

        _logger.LogDebug("📝 Recorded file discussed: {FilePath} in session {SessionId}", filePath, sessionId);
    }

    public async Task RecordFileEditedAsync(string sessionId, string filePath, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            // Create MODIFIED_DURING relationship
            var cypher = @"
                MATCH (s:Session {id: $sessionId})
                MERGE (f:File {path: $filePath})
                MERGE (f)-[r:MODIFIED_DURING]->(s)
                ON CREATE SET r.count = 1, r.firstEditedAt = datetime()
                ON MATCH SET r.count = r.count + 1, r.lastEditedAt = datetime()
                
                WITH s, f
                SET s.filesEdited = CASE 
                    WHEN s.filesEdited IS NULL THEN [$filePath]
                    WHEN NOT $filePath IN s.filesEdited THEN s.filesEdited + $filePath
                    ELSE s.filesEdited
                END";

            await tx.RunAsync(cypher, new { sessionId, filePath });
        });

        _logger.LogDebug("✏️ Recorded file edited: {FilePath} in session {SessionId}", filePath, sessionId);
    }

    public async Task RecordQuestionAskedAsync(string sessionId, string question, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MATCH (s:Session {id: $sessionId})
                SET s.questionsAsked = CASE 
                    WHEN s.questionsAsked IS NULL THEN [$question]
                    ELSE s.questionsAsked + $question
                END";

            await tx.RunAsync(cypher, new { sessionId, question });
        });

        _logger.LogDebug("❓ Recorded question in session {SessionId}: {Question}", sessionId, question.Length > 50 ? question[..50] + "..." : question);
    }

    public async Task<List<Session>> GetRecentSessionsAsync(string context, int limit = 10, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var sessions = new List<Session>();
            
            var cypher = @"
                MATCH (s:Session {context: $context})
                RETURN s
                ORDER BY s.startedAt DESC
                LIMIT $limit";

            var cursor = await tx.RunAsync(cypher, new { context = normalizedContext, limit });
            
            await foreach (var record in cursor)
            {
                var node = record["s"].As<INode>();
                sessions.Add(MapSessionFromNode(node));
            }
            
            return sessions;
        });
    }

    #endregion

    #region Q&A Learning

    public async Task StoreQuestionMappingAsync(
        string question, 
        string answer, 
        List<string> relevantFiles, 
        string context,
        CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        
        // Generate embedding for the question
        var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question, cancellationToken);
        var qaId = Guid.NewGuid().ToString();
        
        // Store in Qdrant lightning collection for vector similarity search
        await _vectorService.StoreLightningQAAsync(
            qaId,
            question,
            questionEmbedding,
            answer,
            relevantFiles,
            normalizedContext,
            cancellationToken);
        
        // Also store in Neo4j for graph relationships
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                CREATE (q:QuestionMapping {
                    id: $id,
                    question: $question,
                    answer: $answer,
                    relevantFiles: $relevantFiles,
                    context: $context,
                    timesAsked: 1,
                    firstAskedAt: datetime($askedAt),
                    lastAskedAt: datetime($askedAt)
                })";

            await tx.RunAsync(cypher, new
            {
                id = qaId,
                question,
                answer,
                relevantFiles,
                context = normalizedContext,
                askedAt = DateTime.UtcNow.ToString("O")
            });

            // Create relationships to relevant files
            foreach (var filePath in relevantFiles)
            {
                var relCypher = @"
                    MATCH (q:QuestionMapping {id: $id})
                    MERGE (f:File {path: $filePath})
                    MERGE (f)-[:ANSWERED_WITH]->(q)";
                
                await tx.RunAsync(relCypher, new { id = qaId, filePath });
            }
        });

        _logger.LogInformation("⚡ Stored Q&A in lightning collection ({Context}): {Question}", 
            normalizedContext, question.Length > 50 ? question[..50] + "..." : question);
    }

    public async Task<List<QuestionMapping>> FindSimilarQuestionsAsync(
        string question, 
        string context, 
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        var mappings = new List<QuestionMapping>();
        
        try
        {
            // Generate embedding for the question
            var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question, cancellationToken);
            
            // Search Qdrant lightning collection using vector similarity
            var qdrantResults = await _vectorService.SearchSimilarQuestionsAsync(
                questionEmbedding,
                normalizedContext,
                limit,
                0.6f, // Lower threshold for Q&A similarity
                cancellationToken);
            
            // Convert Qdrant results to QuestionMapping
            foreach (var result in qdrantResults)
            {
                mappings.Add(new QuestionMapping
                {
                    Id = result.Id,
                    Question = result.Question,
                    Answer = result.Answer,
                    RelevantFiles = result.RelevantFiles,
                    Context = normalizedContext,
                    LastAskedAt = result.StoredAt,
                    FirstAskedAt = result.StoredAt
                });
            }
            
            _logger.LogDebug("⚡ Found {Count} similar questions in lightning collection", mappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search lightning collection, falling back to Neo4j");
            
            // Fallback to Neo4j text search
            await using var neo4jSession = _driver.AsyncSession();
            
            mappings = await neo4jSession.ExecuteReadAsync(async tx =>
            {
                var results = new List<QuestionMapping>();
                
                var cypher = @"
                    MATCH (q:QuestionMapping)
                    WHERE q.context = $context
                      AND toLower(q.question) CONTAINS toLower($searchTerm)
                    RETURN q
                    ORDER BY q.timesAsked DESC
                    LIMIT $limit";

                var searchTerm = ExtractSearchTerm(question);
                var cursor = await tx.RunAsync(cypher, new { context = normalizedContext, searchTerm, limit });
                
                await foreach (var record in cursor)
                {
                    var node = record["q"].As<INode>();
                    results.Add(MapQuestionMappingFromNode(node));
                }
                
                return results;
            });
        }
        
        return mappings;
    }

    public async Task RecordAnswerFeedbackAsync(string questionId, bool wasHelpful, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MATCH (q:QuestionMapping {id: $questionId})
                SET q.wasHelpful = $wasHelpful,
                    q.feedbackAt = datetime()";

            await tx.RunAsync(cypher, new { questionId, wasHelpful });
        });
    }

    #endregion

    #region Importance Scoring

    public async Task RecordAccessAsync(string filePath, string? elementName, CodeMemoryType type, string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MERGE (i:ImportanceMetric {filePath: $filePath, context: $context})
                ON CREATE SET 
                    i.id = randomUUID(),
                    i.elementName = $elementName,
                    i.elementType = $elementType,
                    i.accessCount = 1,
                    i.editCount = 0,
                    i.discussionCount = 0,
                    i.searchResultCount = 0,
                    i.selectedCount = 0,
                    i.lastAccessedAt = datetime(),
                    i.importanceScore = 0.5,
                    i.recencyScore = 1.0,
                    i.frequencyScore = 0.1
                ON MATCH SET 
                    i.accessCount = i.accessCount + 1,
                    i.lastAccessedAt = datetime(),
                    i.recencyScore = 1.0,
                    i.frequencyScore = CASE 
                        WHEN i.frequencyScore < 0.9 THEN i.frequencyScore + 0.05
                        ELSE 0.95
                    END";

            await tx.RunAsync(cypher, new 
            { 
                filePath, 
                context = normalizedContext, 
                elementName = elementName ?? "", 
                elementType = type.ToString() 
            });
        });
    }

    public async Task RecordEditAsync(string filePath, string? elementName, string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MERGE (i:ImportanceMetric {filePath: $filePath, context: $context})
                ON CREATE SET 
                    i.id = randomUUID(),
                    i.elementName = $elementName,
                    i.accessCount = 0,
                    i.editCount = 1,
                    i.discussionCount = 0,
                    i.searchResultCount = 0,
                    i.selectedCount = 0,
                    i.lastEditedAt = datetime(),
                    i.importanceScore = 0.6,
                    i.recencyScore = 1.0,
                    i.frequencyScore = 0.2
                ON MATCH SET 
                    i.editCount = i.editCount + 1,
                    i.lastEditedAt = datetime(),
                    i.recencyScore = 1.0,
                    i.frequencyScore = CASE 
                        WHEN i.frequencyScore < 0.9 THEN i.frequencyScore + 0.1
                        ELSE 0.95
                    END,
                    i.importanceScore = CASE 
                        WHEN i.importanceScore < 0.9 THEN i.importanceScore + 0.05
                        ELSE 0.95
                    END";

            await tx.RunAsync(cypher, new { filePath, context = normalizedContext, elementName = elementName ?? "" });
        });
    }

    public async Task RecordSearchResultAsync(string filePath, string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MERGE (i:ImportanceMetric {filePath: $filePath, context: $context})
                ON CREATE SET 
                    i.id = randomUUID(),
                    i.accessCount = 0,
                    i.editCount = 0,
                    i.discussionCount = 0,
                    i.searchResultCount = 1,
                    i.selectedCount = 0,
                    i.importanceScore = 0.5,
                    i.recencyScore = 0.5,
                    i.frequencyScore = 0.1
                ON MATCH SET 
                    i.searchResultCount = i.searchResultCount + 1";

            await tx.RunAsync(cypher, new { filePath, context = normalizedContext });
        });
    }

    public async Task RecordSelectionAsync(string filePath, string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MERGE (i:ImportanceMetric {filePath: $filePath, context: $context})
                ON CREATE SET 
                    i.id = randomUUID(),
                    i.accessCount = 1,
                    i.editCount = 0,
                    i.discussionCount = 0,
                    i.searchResultCount = 1,
                    i.selectedCount = 1,
                    i.importanceScore = 0.6,
                    i.recencyScore = 1.0,
                    i.frequencyScore = 0.2
                ON MATCH SET 
                    i.selectedCount = i.selectedCount + 1,
                    i.accessCount = i.accessCount + 1,
                    i.lastAccessedAt = datetime(),
                    i.recencyScore = 1.0,
                    i.importanceScore = CASE 
                        WHEN i.importanceScore < 0.95 THEN i.importanceScore + 0.02
                        ELSE 0.95
                    END";

            await tx.RunAsync(cypher, new { filePath, context = normalizedContext });
        });
    }

    public async Task<ImportanceMetric?> GetImportanceAsync(string filePath, string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (i:ImportanceMetric {filePath: $filePath, context: $context})
                RETURN i";

            var cursor = await tx.RunAsync(cypher, new { filePath, context = normalizedContext });
            
            if (await cursor.FetchAsync())
            {
                var node = cursor.Current["i"].As<INode>();
                return MapImportanceMetricFromNode(node);
            }
            
            return null;
        });
    }

    public async Task<List<ImportanceMetric>> GetMostImportantFilesAsync(string context, int limit = 20, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var metrics = new List<ImportanceMetric>();
            
            var cypher = @"
                MATCH (i:ImportanceMetric {context: $context})
                RETURN i
                ORDER BY i.importanceScore DESC, i.accessCount DESC
                LIMIT $limit";

            var cursor = await tx.RunAsync(cypher, new { context = normalizedContext, limit });
            
            await foreach (var record in cursor)
            {
                var node = record["i"].As<INode>();
                metrics.Add(MapImportanceMetricFromNode(node));
            }
            
            return metrics;
        });
    }

    public async Task RecalculateImportanceScoresAsync(string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            // Decay recency scores based on time since last access
            var cypher = @"
                MATCH (i:ImportanceMetric {context: $context})
                WITH i, 
                     duration.between(i.lastAccessedAt, datetime()).days AS daysSinceAccess
                SET i.recencyScore = CASE
                    WHEN daysSinceAccess IS NULL THEN 0.5
                    WHEN daysSinceAccess < 1 THEN 1.0
                    WHEN daysSinceAccess < 7 THEN 0.8
                    WHEN daysSinceAccess < 30 THEN 0.5
                    WHEN daysSinceAccess < 90 THEN 0.3
                    ELSE 0.1
                END,
                i.importanceScore = (
                    (i.accessCount * 0.1) + 
                    (i.editCount * 0.3) + 
                    (i.selectedCount * 0.2) + 
                    (i.discussionCount * 0.2) +
                    (i.recencyScore * 0.2)
                ) / (i.accessCount + i.editCount + i.selectedCount + i.discussionCount + 1)";

            await tx.RunAsync(cypher, new { context = normalizedContext });
        });

        _logger.LogInformation("📊 Recalculated importance scores for context: {Context}", normalizedContext);
    }

    #endregion

    #region Co-Edit Tracking

    public async Task RecordCoEditAsync(List<string> filePaths, string sessionId, string context, CancellationToken cancellationToken = default)
    {
        if (filePaths.Count < 2)
            return;

        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        // Create CO_EDITED_WITH relationships for all pairs
        var pairs = GetFilePairs(filePaths);
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            foreach (var (file1, file2) in pairs)
            {
                var cypher = @"
                    MERGE (f1:File {path: $file1})
                    MERGE (f2:File {path: $file2})
                    MERGE (f1)-[r:CO_EDITED_WITH]->(f2)
                    ON CREATE SET 
                        r.count = 1, 
                        r.firstCoEditAt = datetime(),
                        r.lastCoEditAt = datetime(),
                        r.sessions = [$sessionId],
                        r.context = $context
                    ON MATCH SET 
                        r.count = r.count + 1,
                        r.lastCoEditAt = datetime(),
                        r.sessions = CASE 
                            WHEN NOT $sessionId IN r.sessions THEN r.sessions + $sessionId
                            ELSE r.sessions
                        END";

                await tx.RunAsync(cypher, new { file1, file2, sessionId, context = normalizedContext });
            }
        });

        _logger.LogDebug("🔗 Recorded co-edit for {Count} files in session {SessionId}", filePaths.Count, sessionId);
    }

    public async Task<List<CoEditMetric>> GetCoEditedFilesAsync(string filePath, string context, int limit = 10, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var metrics = new List<CoEditMetric>();
            
            var cypher = @"
                MATCH (f1:File {path: $filePath})-[r:CO_EDITED_WITH]-(f2:File)
                WHERE r.context = $context
                RETURN f2.path AS otherFile, r.count AS count, 
                       r.firstCoEditAt AS firstCoEdit, r.lastCoEditAt AS lastCoEdit,
                       r.sessions AS sessions
                ORDER BY r.count DESC
                LIMIT $limit";

            var cursor = await tx.RunAsync(cypher, new { filePath, context = normalizedContext, limit });
            
            await foreach (var record in cursor)
            {
                metrics.Add(new CoEditMetric
                {
                    FilePath1 = filePath,
                    FilePath2 = record["otherFile"].As<string>(),
                    Context = normalizedContext,
                    CoEditCount = record["count"].As<int>(),
                    SessionIds = record["sessions"].As<List<string>>() ?? new List<string>(),
                    CoEditStrength = CalculateCoEditStrength(record["count"].As<int>())
                });
            }
            
            return metrics;
        });
    }

    public async Task<List<List<string>>> GetFileClusterssAsync(string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var clusters = new List<List<string>>();
            
            // Find strongly connected file groups
            var cypher = @"
                MATCH (f1:File)-[r:CO_EDITED_WITH]->(f2:File)
                WHERE r.context = $context AND r.count >= 3
                WITH f1, collect(DISTINCT f2.path) + [f1.path] AS cluster
                RETURN DISTINCT cluster
                ORDER BY size(cluster) DESC
                LIMIT 20";

            var cursor = await tx.RunAsync(cypher, new { context = normalizedContext });
            
            await foreach (var record in cursor)
            {
                var cluster = record["cluster"].As<List<string>>();
                if (cluster.Count > 1)
                {
                    clusters.Add(cluster.Distinct().ToList());
                }
            }
            
            return clusters;
        });
    }

    #endregion

    #region Reward Signals

    public async Task RecordRewardSignalAsync(RewardSignal signal, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        await neo4jSession.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                CREATE (r:RewardSignal {
                    query: $query,
                    resultPath: $resultPath,
                    type: $type,
                    reward: $reward,
                    recordedAt: datetime($recordedAt),
                    sessionId: $sessionId
                })";

            await tx.RunAsync(cypher, new
            {
                query = signal.Query,
                resultPath = signal.ResultPath,
                type = signal.Type.ToString(),
                reward = signal.Reward,
                recordedAt = signal.RecordedAt.ToString("O"),
                sessionId = signal.SessionId ?? ""
            });
        });
    }

    public async Task<float> GetAccumulatedRewardAsync(string query, string filePath, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (r:RewardSignal)
                WHERE r.resultPath = $filePath
                  AND toLower(r.query) CONTAINS toLower($queryTerm)
                RETURN sum(r.reward) AS totalReward";

            var queryTerm = ExtractSearchTerm(query);
            var cursor = await tx.RunAsync(cypher, new { filePath, queryTerm });
            
            if (await cursor.FetchAsync())
            {
                return cursor.Current["totalReward"].As<float>();
            }
            
            return 0f;
        });
    }

    #endregion

    #region Domain Tagging

    public Task<List<DomainTag>> DetectDomainsAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        var domains = new List<DomainTag>();
        var lowerContent = content.ToLowerInvariant();
        var lowerPath = filePath.ToLowerInvariant();

        foreach (var (domain, keywords) in DomainKeywords)
        {
            var matchCount = keywords.Count(keyword => 
                lowerContent.Contains(keyword) || lowerPath.Contains(keyword));
            
            if (matchCount > 0)
            {
                var confidence = Math.Min(1.0f, matchCount * 0.2f);
                domains.Add(new DomainTag
                {
                    Name = domain,
                    Keywords = keywords.Where(k => lowerContent.Contains(k) || lowerPath.Contains(k)).ToList(),
                    Confidence = confidence
                });
            }
        }

        return Task.FromResult(domains.OrderByDescending(d => d.Confidence).ToList());
    }

    public async Task<List<string>> GetFilesByDomainAsync(string domain, string context, CancellationToken cancellationToken = default)
    {
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var files = new List<string>();
            
            var cypher = @"
                MATCH (f:File)-[:BELONGS_TO_DOMAIN]->(d:Domain {name: $domain})
                WHERE f.context = $context
                RETURN f.path AS filePath";

            var cursor = await tx.RunAsync(cypher, new { domain, context });
            
            await foreach (var record in cursor)
            {
                files.Add(record["filePath"].As<string>());
            }
            
            return files;
        });
    }

    public async Task<List<string>> GetDomainsAsync(string context, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant() ?? "default";
        await using var neo4jSession = _driver.AsyncSession();
        
        return await neo4jSession.ExecuteReadAsync(async tx =>
        {
            var domains = new List<string>();
            
            var cypher = @"
                MATCH (d:Domain)
                WHERE exists((d)<-[:BELONGS_TO_DOMAIN]-(:File {context: $context}))
                RETURN DISTINCT d.name AS domain";

            var cursor = await tx.RunAsync(cypher, new { context = normalizedContext });
            
            await foreach (var record in cursor)
            {
                domains.Add(record["domain"].As<string>());
            }
            
            return domains;
        });
    }

    #endregion

    #region Smart Search Integration

    public async Task<List<CodeExample>> EnhanceSearchResultsAsync(
        List<CodeExample> results, 
        string query, 
        string context,
        CancellationToken cancellationToken = default)
    {
        if (!results.Any())
            return results;

        // Get importance scores for all result files
        var importanceMap = new Dictionary<string, float>();
        
        foreach (var result in results)
        {
            var importance = await GetImportanceAsync(result.FilePath, context, cancellationToken);
            importanceMap[result.FilePath] = importance?.ImportanceScore ?? 0.5f;
            
            // Record that this file was returned in search results
            await RecordSearchResultAsync(result.FilePath, context, cancellationToken);
        }

        // Get accumulated rewards
        var rewardMap = new Dictionary<string, float>();
        foreach (var result in results)
        {
            var reward = await GetAccumulatedRewardAsync(query, result.FilePath, cancellationToken);
            rewardMap[result.FilePath] = reward;
        }

        // Re-rank results based on learned signals
        var enhancedResults = results
            .Select(r => new
            {
                Result = r,
                EnhancedScore = r.Score * (1 + importanceMap.GetValueOrDefault(r.FilePath, 0.5f))
                               * (1 + rewardMap.GetValueOrDefault(r.FilePath, 0f) * 0.1f)
            })
            .OrderByDescending(r => r.EnhancedScore)
            .Select(r =>
            {
                r.Result.Score = (float)r.EnhancedScore;
                r.Result.Metadata["importance_score"] = importanceMap.GetValueOrDefault(r.Result.FilePath, 0.5f);
                r.Result.Metadata["reward_score"] = rewardMap.GetValueOrDefault(r.Result.FilePath, 0f);
                return r.Result;
            })
            .ToList();

        return enhancedResults;
    }

    #endregion

    #region Helper Methods

    private static string ExtractSearchTerm(string question)
    {
        // Extract key terms from question, removing common words
        var stopWords = new HashSet<string> { "how", "what", "where", "when", "why", "is", "are", "the", "a", "an", "do", "does", "can", "to", "in", "for", "of", "and", "or" };
        
        var words = question.ToLowerInvariant()
            .Split(new[] { ' ', '?', '.', ',', '!', ':', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !stopWords.Contains(w) && w.Length > 2)
            .Take(5);
        
        return string.Join(" ", words);
    }

    private static List<(string, string)> GetFilePairs(List<string> files)
    {
        var pairs = new List<(string, string)>();
        for (int i = 0; i < files.Count; i++)
        {
            for (int j = i + 1; j < files.Count; j++)
            {
                // Normalize order so (a,b) and (b,a) are the same
                var pair = string.Compare(files[i], files[j], StringComparison.Ordinal) < 0
                    ? (files[i], files[j])
                    : (files[j], files[i]);
                pairs.Add(pair);
            }
        }
        return pairs;
    }

    private static float CalculateCoEditStrength(int count)
    {
        // Logarithmic scaling: 1 edit = 0.1, 5 edits = 0.5, 10+ edits = 0.8+
        return Math.Min(0.95f, (float)(Math.Log(count + 1) * 0.3));
    }

    private Session MapSessionFromNode(INode node)
    {
        var props = node.Properties;
        return new Session
        {
            Id = props["id"].As<string>(),
            Context = props["context"].As<string>(),
            StartedAt = props.ContainsKey("startedAt") ? ConvertNeo4jDateTime(props["startedAt"]) : DateTime.UtcNow,
            EndedAt = props.ContainsKey("endedAt") ? ConvertNeo4jDateTime(props["endedAt"]) : null,
            Summary = props.ContainsKey("summary") ? props["summary"].As<string>() : "",
            FilesDiscussed = props.ContainsKey("filesDiscussed") ? props["filesDiscussed"].As<List<string>>() ?? new() : new(),
            FilesEdited = props.ContainsKey("filesEdited") ? props["filesEdited"].As<List<string>>() ?? new() : new(),
            QuestionsAsked = props.ContainsKey("questionsAsked") ? props["questionsAsked"].As<List<string>>() ?? new() : new()
        };
    }

    private QuestionMapping MapQuestionMappingFromNode(INode node)
    {
        var props = node.Properties;
        return new QuestionMapping
        {
            Id = props["id"].As<string>(),
            Question = props["question"].As<string>(),
            Answer = props.ContainsKey("answer") ? props["answer"].As<string>() : "",
            RelevantFiles = props.ContainsKey("relevantFiles") ? props["relevantFiles"].As<List<string>>() ?? new() : new(),
            Context = props.ContainsKey("context") ? props["context"].As<string>() : "",
            TimesAsked = props.ContainsKey("timesAsked") ? props["timesAsked"].As<int>() : 1,
            FirstAskedAt = props.ContainsKey("firstAskedAt") ? ConvertNeo4jDateTime(props["firstAskedAt"]) : DateTime.UtcNow,
            LastAskedAt = props.ContainsKey("lastAskedAt") ? ConvertNeo4jDateTime(props["lastAskedAt"]) : DateTime.UtcNow,
            WasHelpful = props.ContainsKey("wasHelpful") ? props["wasHelpful"].As<bool?>() : null
        };
    }

    private ImportanceMetric MapImportanceMetricFromNode(INode node)
    {
        var props = node.Properties;
        return new ImportanceMetric
        {
            Id = props.ContainsKey("id") ? props["id"].As<string>() : "",
            FilePath = props["filePath"].As<string>(),
            ElementName = props.ContainsKey("elementName") ? props["elementName"].As<string>() : "",
            Context = props.ContainsKey("context") ? props["context"].As<string>() : "",
            AccessCount = props.ContainsKey("accessCount") ? props["accessCount"].As<int>() : 0,
            EditCount = props.ContainsKey("editCount") ? props["editCount"].As<int>() : 0,
            DiscussionCount = props.ContainsKey("discussionCount") ? props["discussionCount"].As<int>() : 0,
            SearchResultCount = props.ContainsKey("searchResultCount") ? props["searchResultCount"].As<int>() : 0,
            SelectedCount = props.ContainsKey("selectedCount") ? props["selectedCount"].As<int>() : 0,
            LastAccessedAt = props.ContainsKey("lastAccessedAt") ? ConvertNeo4jDateTime(props["lastAccessedAt"]) : null,
            LastEditedAt = props.ContainsKey("lastEditedAt") ? ConvertNeo4jDateTime(props["lastEditedAt"]) : null,
            ImportanceScore = props.ContainsKey("importanceScore") ? props["importanceScore"].As<float>() : 0.5f,
            RecencyScore = props.ContainsKey("recencyScore") ? props["recencyScore"].As<float>() : 0.5f,
            FrequencyScore = props.ContainsKey("frequencyScore") ? props["frequencyScore"].As<float>() : 0.1f
        };
    }

    private DateTime ConvertNeo4jDateTime(object value)
    {
        if (value == null) return DateTime.UtcNow;
        
        if (value is ZonedDateTime zonedDateTime)
        {
            return zonedDateTime.ToDateTimeOffset().UtcDateTime;
        }
        else if (value is LocalDateTime localDateTime)
        {
            return localDateTime.ToDateTime();
        }
        else if (value is DateTime dt)
        {
            return dt;
        }
        else if (value is string str && DateTime.TryParse(str, out var parsed))
        {
            return parsed;
        }
        
        return DateTime.UtcNow;
    }

    #endregion
}

