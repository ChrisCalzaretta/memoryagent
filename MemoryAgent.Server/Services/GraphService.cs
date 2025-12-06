using System.Text.Json;
using MemoryAgent.Server.Models;
using Neo4j.Driver;
using Polly;
using Polly.Retry;
using TaskStatusModel = MemoryAgent.Server.Models.TaskStatus;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for interacting with Neo4j graph database
/// </summary>
public class GraphService : IGraphService, IDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<GraphService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public GraphService(
        IConfiguration configuration,
        ILogger<GraphService> _logger)
    {
        var neo4jUrl = configuration["Neo4j:Url"] ?? "bolt://localhost:7687";
        var neo4jUser = configuration["Neo4j:User"] ?? "neo4j";
        var neo4jPassword = configuration["Neo4j:Password"] ?? "memoryagent";

        _driver = GraphDatabase.Driver(neo4jUrl, AuthTokens.Basic(neo4jUser, neo4jPassword));
        this._logger = _logger;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} after {Delay}s due to: {Exception}",
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                });
    }

    /// <summary>
    /// Create a session for a specific workspace database
    /// Note: For Community Edition, this always returns the default database.
    /// Context isolation is handled via Cypher query filtering.
    /// </summary>
    private IAsyncSession CreateSession(string? context = null)
    {
        // Always use default database (Community Edition doesn't support multiple databases)
        // Context filtering is done at the query level using WHERE clauses
        return _driver.AsyncSession();
    }

    /// <summary>
    /// Create/initialize storage for a workspace context
    /// Note: Neo4j Community Edition doesn't support multiple databases.
    /// We use the default database with context-based filtering instead.
    /// </summary>
    public async Task CreateDatabaseAsync(string context, CancellationToken cancellationToken = default)
    {
        var dbName = context.ToLower();
        
        try
        {
            // Try to create database (Enterprise Edition only)
            try
            {
                await using var session = _driver.AsyncSession(o => o.WithDatabase("system"));
                
                await session.ExecuteWriteAsync(async tx =>
                {
                    await tx.RunAsync($"CREATE DATABASE `{dbName}` IF NOT EXISTS");
                });
                
                _logger.LogInformation("✅ Neo4j database created (Enterprise): {Database}", dbName);
                
                // Initialize constraints and indexes for this database
                await InitializeDatabaseForContextAsync(context, cancellationToken);
            }
            catch (Exception dbEx)
            {
                // Community Edition - use default database with context filtering
                _logger.LogInformation("ℹ️ Neo4j Community Edition detected - using context-based filtering for: {Context}", context);
                _logger.LogDebug("Database creation not supported: {Message}", dbEx.Message);
                
                // Initialize indexes in default database (still useful for performance)
                await InitializeDatabaseForContextAsync(null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Neo4j for context: {Context}", context);
            throw;
        }
    }

    /// <summary>
    /// Initialize constraints and indexes for a specific database
    /// </summary>
    private async Task InitializeDatabaseForContextAsync(string context, CancellationToken cancellationToken = default)
    {
        await using var session = CreateSession(context);

        try
        {
            // Create constraints (ensures uniqueness and creates indexes)
            var constraints = new[]
            {
                "CREATE CONSTRAINT class_name IF NOT EXISTS FOR (c:Class) REQUIRE c.name IS UNIQUE",
                "CREATE CONSTRAINT file_path IF NOT EXISTS FOR (f:File) REQUIRE f.path IS UNIQUE",
                "CREATE CONSTRAINT pattern_id IF NOT EXISTS FOR (p:Pattern) REQUIRE p.id IS UNIQUE",
                "CREATE CONSTRAINT namespace_name IF NOT EXISTS FOR (n:Namespace) REQUIRE n.name IS UNIQUE"
            };

            foreach (var constraint in constraints)
            {
                try
                {
                    await session.ExecuteWriteAsync(async tx =>
                    {
                        await tx.RunAsync(constraint);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Constraint creation note: {Message}", ex.Message);
                }
            }

            // Create indexes for performance
            var indexes = new[]
            {
                "CREATE INDEX class_namespace IF NOT EXISTS FOR (c:Class) ON (c.namespace)",
                "CREATE INDEX method_class IF NOT EXISTS FOR (m:Method) ON (m.class_name)",
                "CREATE INDEX file_path_idx IF NOT EXISTS FOR (f:File) ON (f.path)"
            };

            foreach (var index in indexes)
            {
                try
                {
                    await session.ExecuteWriteAsync(async tx =>
                    {
                        await tx.RunAsync(index);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Index creation note: {Message}", ex.Message);
                }
            }

            _logger.LogInformation("✅ Neo4j database initialized for context: {Context}", context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Neo4j database for context: {Context}", context);
            throw;
        }
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        try
        {
            // Create constraints (ensures uniqueness and creates indexes)
            var constraints = new[]
            {
                "CREATE CONSTRAINT class_name IF NOT EXISTS FOR (c:Class) REQUIRE c.name IS UNIQUE",
                "CREATE CONSTRAINT file_path IF NOT EXISTS FOR (f:File) REQUIRE f.path IS UNIQUE",
                "CREATE CONSTRAINT pattern_id IF NOT EXISTS FOR (p:Pattern) REQUIRE p.id IS UNIQUE",
                "CREATE CONSTRAINT namespace_name IF NOT EXISTS FOR (n:Namespace) REQUIRE n.name IS UNIQUE",
                "CREATE CONSTRAINT context_name IF NOT EXISTS FOR (c:Context) REQUIRE c.name IS UNIQUE"
            };

            foreach (var constraint in constraints)
            {
                try
                {
                    await session.ExecuteWriteAsync(async tx =>
                    {
                        await tx.RunAsync(constraint);
                    });
                }
                catch (Exception ex)
                {
                    // Constraint might already exist
                    _logger.LogDebug("Constraint creation note: {Message}", ex.Message);
                }
            }

            // Create indexes for performance
            var indexes = new[]
            {
                "CREATE INDEX class_namespace IF NOT EXISTS FOR (c:Class) ON (c.namespace)",
                "CREATE INDEX class_context IF NOT EXISTS FOR (c:Class) ON (c.context)",
                "CREATE INDEX method_class IF NOT EXISTS FOR (m:Method) ON (m.class_name)",
                "CREATE INDEX file_context IF NOT EXISTS FOR (f:File) ON (f.context)",
                // Learning & Session indexes
                "CREATE INDEX session_context IF NOT EXISTS FOR (s:Session) ON (s.context)",
                "CREATE INDEX session_id IF NOT EXISTS FOR (s:Session) ON (s.id)",
                "CREATE INDEX importance_file IF NOT EXISTS FOR (i:ImportanceMetric) ON (i.filePath)",
                "CREATE INDEX question_context IF NOT EXISTS FOR (q:QuestionMapping) ON (q.context)"
            };

            foreach (var index in indexes)
            {
                try
                {
                    await session.ExecuteWriteAsync(async tx =>
                    {
                        await tx.RunAsync(index);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Index creation note: {Message}", ex.Message);
                }
            }

            // Create full-text indexes for 10x faster search
            var fullTextIndexes = new[]
            {
                // Main code search index - searches across name, content, summary, purpose, signature
                @"CREATE FULLTEXT INDEX code_fulltext IF NOT EXISTS 
                  FOR (n:File|Class|Method|Pattern|Property|Interface) 
                  ON EACH [n.name, n.content, n.summary, n.purpose, n.signature]",
                
                // Session and Q&A search index
                @"CREATE FULLTEXT INDEX session_fulltext IF NOT EXISTS 
                  FOR (n:Session|QuestionMapping) 
                  ON EACH [n.summary, n.question, n.answer]",
                
                // Pattern-specific search
                @"CREATE FULLTEXT INDEX pattern_fulltext IF NOT EXISTS 
                  FOR (p:Pattern) 
                  ON EACH [p.name, p.description, p.category, p.bestPractice]"
            };

            foreach (var ftIndex in fullTextIndexes)
            {
                try
                {
                    await session.ExecuteWriteAsync(async tx =>
                    {
                        await tx.RunAsync(ftIndex);
                    });
                    _logger.LogInformation("✅ Full-text index created");
                }
                catch (Exception ex)
                {
                    // Full-text index might already exist or not be supported
                    _logger.LogDebug("Full-text index note: {Message}", ex.Message);
                }
            }

            _logger.LogInformation("Neo4j database initialized successfully with full-text indexes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Neo4j database");
            throw;
        }
    }

    public async Task StoreCodeNodeAsync(CodeMemory memory, CancellationToken cancellationToken = default)
    {
        await StoreCodeNodesAsync(new List<CodeMemory> { memory }, cancellationToken);
    }

    public async Task StoreCodeNodesAsync(List<CodeMemory> memories, CancellationToken cancellationToken = default)
    {
        if (!memories.Any())
            return;

        await using var session = _driver.AsyncSession();

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await session.ExecuteWriteAsync(async tx =>
                {
                    foreach (var memory in memories)
                    {
                        var query = memory.Type switch
                        {
                            CodeMemoryType.Class => CreateClassNodeQuery(memory),
                            CodeMemoryType.Method => CreateMethodNodeQuery(memory),
                            CodeMemoryType.Property => CreatePropertyNodeQuery(memory),
                            CodeMemoryType.Interface => CreateInterfaceNodeQuery(memory),
                            CodeMemoryType.File => CreateFileNodeQuery(memory),
                            CodeMemoryType.Pattern => CreatePatternNodeQuery(memory),
                            
                            // Map additional types to appropriate node queries
                            CodeMemoryType.Test => CreateClassNodeQuery(memory),       // Tests are class-like
                            CodeMemoryType.Enum => CreateClassNodeQuery(memory),       // Enums go with classes
                            CodeMemoryType.Record => CreateClassNodeQuery(memory),     // Records are class-like
                            CodeMemoryType.Struct => CreateClassNodeQuery(memory),     // Structs are class-like
                            CodeMemoryType.Delegate => CreateClassNodeQuery(memory),   // Delegates go with classes
                            CodeMemoryType.Event => CreateMethodNodeQuery(memory),     // Events go with methods
                            CodeMemoryType.Constant => CreateMethodNodeQuery(memory),  // Constants go with methods
                            CodeMemoryType.Repository => CreateClassNodeQuery(memory), // Architecture patterns
                            CodeMemoryType.Service => CreateClassNodeQuery(memory),
                            CodeMemoryType.Controller => CreateClassNodeQuery(memory),
                            CodeMemoryType.Middleware => CreateClassNodeQuery(memory),
                            CodeMemoryType.Filter => CreateClassNodeQuery(memory),
                            CodeMemoryType.DbContext => CreateClassNodeQuery(memory),
                            CodeMemoryType.Entity => CreateClassNodeQuery(memory),
                            CodeMemoryType.Migration => CreateClassNodeQuery(memory),
                            CodeMemoryType.Component => CreateClassNodeQuery(memory),  // Frontend components
                            CodeMemoryType.Hook => CreateMethodNodeQuery(memory),      // React hooks are function-like
                            CodeMemoryType.Endpoint => CreateMethodNodeQuery(memory),  // API endpoints are method-like
                            
                            _ => CreateClassNodeQuery(memory) // Default fallback to classes
                        };

                        await tx.RunAsync(query.cypher, query.parameters);
                    }
                });
            });

            _logger.LogInformation("Stored {Count} code nodes in Neo4j", memories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing code nodes");
            throw;
        }
    }

    public async Task CreateRelationshipAsync(CodeRelationship relationship, CancellationToken cancellationToken = default)
    {
        await CreateRelationshipsAsync(new List<CodeRelationship> { relationship }, cancellationToken);
    }

    public async Task CreateRelationshipsAsync(List<CodeRelationship> relationships, CancellationToken cancellationToken = default)
    {
        if (!relationships.Any())
            return;

        await using var session = _driver.AsyncSession();

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await session.ExecuteWriteAsync(async tx =>
                {
                    foreach (var rel in relationships)
                    {
                        var (cypher, parameters) = CreateRelationshipQuery(rel);
                        await tx.RunAsync(cypher, parameters);
                    }
                });
            });

            _logger.LogInformation("Created {Count} relationships in Neo4j", relationships.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relationships");
            throw;
        }
    }

    public async Task<List<string>> GetImpactAnalysisAsync(string className, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        try
        {
            var result = await session.ExecuteReadAsync(async tx =>
            {
                // IMPORTANT: Extract context from the className to ensure workspace isolation
                // className format: "ContextName.Namespace.ClassName" or just "Namespace.ClassName"
                // We need to find the class first to get its context, then filter impacted classes by the same context
                var cursor = await tx.RunAsync(@"
                    MATCH (changed:Class {name: $className})
                    WITH changed.context AS targetContext, changed
                    MATCH (changed)<-[:INHERITS|USES*]-(impacted)
                    WHERE impacted.context = targetContext
                    RETURN DISTINCT impacted.name AS name, impacted.file_path AS filePath
                    LIMIT 100",
                    new { className });

                var records = await cursor.ToListAsync();
                return records.Select(r => r["name"].As<string>()).ToList();
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting impact analysis for {ClassName}", className);
            throw;
        }
    }

    public async Task<List<string>> GetDependencyChainAsync(string className, int maxDepth = 5, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        try
        {
            var result = await session.ExecuteReadAsync(async tx =>
            {
                // IMPORTANT: Ensure workspace isolation by filtering dependencies to same context
                var cursor = await tx.RunAsync($@"
                    MATCH (class:Class {{name: $className}})
                    WITH class.context AS targetContext, class
                    MATCH path = (class)-[:USES*1..{maxDepth}]->(dep)
                    WHERE dep.context = targetContext
                    WITH DISTINCT dep.name AS name, length(path) AS pathLength
                    RETURN name
                    ORDER BY pathLength
                    LIMIT 100",
                    new { className });

                var records = await cursor.ToListAsync();
                return records.Select(r => r["name"].As<string>()).ToList();
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dependency chain for {ClassName}", className);
            throw;
        }
    }

    public async Task<List<List<string>>> FindCircularDependenciesAsync(string? context = null, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant();
        await using var session = _driver.AsyncSession();

        try
        {
            // IMPORTANT: Context filtering is CRITICAL for workspace isolation
            // If no context provided, we still need to ensure we don't mix workspaces
            var contextFilter = string.IsNullOrWhiteSpace(normalizedContext) 
                ? "WHERE c1.context IS NOT NULL AND toLower(c1.context) = toLower(c2.context)" // Same workspace, any workspace
                : "WHERE toLower(c1.context) = $context AND toLower(c2.context) = $context";    // Specific workspace

            var result = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync($@"
                    MATCH path = (c1:Class)-[:USES*2..10]->(c2:Class)-[:USES*]->(c1)
                    {contextFilter}
                    AND c1 <> c2
                    RETURN [node in nodes(path) | node.name] AS cycle
                    LIMIT 50",
                    new { context = normalizedContext ?? "" });

                var records = await cursor.ToListAsync();
                return records.Select(r => r["cycle"].As<List<string>>()).ToList();
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding circular dependencies");
            throw;
        }
    }

    public async Task<List<string>> GetClassesFollowingPatternAsync(string patternName, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        try
        {
            var result = await session.ExecuteReadAsync(async tx =>
            {
                // IMPORTANT: Ensure workspace isolation by matching context between class and pattern
                var cursor = await tx.RunAsync(@"
                    MATCH (p:Pattern {name: $patternName})
                    WITH p.context AS targetContext, p
                    MATCH (c:Class)-[:FOLLOWS_PATTERN]->(p)
                    WHERE c.context = targetContext
                    RETURN c.name AS name, c.file_path AS filePath",
                    new { patternName });

                var records = await cursor.ToListAsync();
                return records.Select(r => r["name"].As<string>()).ToList();
            });

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting classes following pattern {PatternName}", patternName);
            throw;
        }
    }

    public async Task DeleteByFilePathAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                // Delete all nodes associated with this file
                await tx.RunAsync(@"
                    MATCH (n)
                    WHERE n.file_path = $filePath
                    DETACH DELETE n",
                    new { filePath });
            });

            _logger.LogInformation("Deleted graph nodes for file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting graph nodes for file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _driver.VerifyConnectivityAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed for Neo4j");
            return false;
        }
    }

    public async Task<List<CodeMemory>> FullTextSearchAsync(string query, string? context = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        // IMPORTANT: Normalize context to lowercase for consistent search
        var normalizedContext = context?.ToLowerInvariant();
        
        try
        {
            return await session.ExecuteReadAsync(async tx =>
            {
                var results = new List<CodeMemory>();
                
                // Try full-text index first (10x faster), fallback to CONTAINS if index doesn't exist
                string cypher;
                var parameters = new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["limit"] = limit
                };
                
                if (!string.IsNullOrWhiteSpace(normalizedContext))
                {
                    parameters["context"] = normalizedContext;
                }
                
                // Escape special characters for Lucene full-text search
                var escapedQuery = EscapeLuceneQuery(query);
                parameters["escapedQuery"] = escapedQuery;
                
                try
                {
                    // Use full-text index for 10x faster search
                    // The index searches across name, content, summary, purpose, signature
                    var contextFilter = string.IsNullOrWhiteSpace(normalizedContext) 
                        ? "" 
                        : "WHERE toLower(node.context) = $context";
                    
                    cypher = $@"
                        CALL db.index.fulltext.queryNodes('code_fulltext', $escapedQuery) 
                        YIELD node, score
                        {contextFilter}
                        RETURN node as n, labels(node) as nodeType, score
                        ORDER BY score DESC
                        LIMIT $limit";
                }
                catch
                {
                    // Fallback to CONTAINS-based search if full-text index doesn't exist
                    _logger.LogWarning("Full-text index not available, falling back to CONTAINS search");
                    
                    var contextFilter = string.IsNullOrWhiteSpace(normalizedContext) 
                        ? "" 
                        : "AND toLower(n.context) = $context";
                    
                    cypher = $@"
                        MATCH (n)
                        WHERE (n:File OR n:Class OR n:Method OR n:Pattern)
                          AND (toLower(n.name) CONTAINS toLower($query) 
                               OR toLower(n.content) CONTAINS toLower($query)
                               OR toLower(n.filePath) CONTAINS toLower($query))
                          {contextFilter}
                        RETURN n, labels(n) as nodeType, 1.0 as score
                        LIMIT $limit";
                }
                
                var cursor = await tx.RunAsync(cypher, parameters);
                
                await foreach (var record in cursor)
                {
                    var node = record["n"].As<INode>();
                    var nodeType = record["nodeType"].As<List<string>>().FirstOrDefault() ?? "Unknown";
                    var props = node.Properties;
                    
                    // Map to CodeMemory based on node type
                    var codeMemory = new CodeMemory
                    {
                        Name = props.ContainsKey("name") ? props["name"].As<string>() : "",
                        Content = props.ContainsKey("content") ? props["content"].As<string>() : "",
                        FilePath = props.ContainsKey("filePath") ? props["filePath"].As<string>() : 
                                   props.ContainsKey("path") ? props["path"].As<string>() : "",
                        LineNumber = props.ContainsKey("lineNumber") ? props["lineNumber"].As<int>() : 0,
                        Context = props.ContainsKey("context") ? props["context"].As<string>() : context ?? "default",
                        Type = MapNodeTypeToCodeMemoryType(nodeType),
                        Metadata = new Dictionary<string, object>()
                    };
                    
                    // Copy all properties to metadata
                    foreach (var prop in props)
                    {
                        if (prop.Key != "name" && prop.Key != "content" && prop.Key != "filePath" && prop.Key != "lineNumber" && prop.Key != "context")
                        {
                            codeMemory.Metadata[prop.Key] = prop.Value;
                        }
                    }
                    
                    results.Add(codeMemory);
                }
                
                _logger.LogInformation("Neo4j full-text search for '{Query}' in context '{Context}' returned {Count} results", 
                    query, normalizedContext ?? "all", results.Count);
                
                return results;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Neo4j full-text search for query: {Query}", query);
            return new List<CodeMemory>();
        }
    }

    private static CodeMemoryType MapNodeTypeToCodeMemoryType(string nodeType)
    {
        return nodeType switch
        {
            "File" => CodeMemoryType.File,
            "Class" => CodeMemoryType.Class,
            "Method" => CodeMemoryType.Method,
            "Pattern" => CodeMemoryType.Pattern,
            _ => CodeMemoryType.File
        };
    }
    
    /// <summary>
    /// Escape special characters for Lucene full-text query syntax
    /// </summary>
    private static string EscapeLuceneQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return query;
        
        // Lucene special characters that need escaping: + - && || ! ( ) { } [ ] ^ " ~ * ? : \ /
        var specialChars = new[] { '+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/' };
        
        var escaped = query;
        foreach (var c in specialChars)
        {
            escaped = escaped.Replace(c.ToString(), "\\" + c);
        }
        
        // Handle && and || as single tokens
        escaped = escaped.Replace("\\&\\&", "\\&&");
        escaped = escaped.Replace("\\|\\|", "\\||");
        
        return escaped;
    }

    /// <summary>
    /// Gets a metadata value, converting JsonElement to primitive types for Neo4j compatibility
    /// </summary>
    private static T GetMetadataValue<T>(Dictionary<string, object> metadata, string key, T defaultValue)
    {
        if (!metadata.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        // Handle JsonElement conversion
        if (value is JsonElement je)
        {
            return ConvertJsonElement<T>(je, defaultValue);
        }

        // Try direct conversion
        try
        {
            if (value is T typedValue)
                return typedValue;

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Converts a JsonElement to the specified type
    /// </summary>
    private static T ConvertJsonElement<T>(JsonElement je, T defaultValue)
    {
        try
        {
            var targetType = typeof(T);
            
            if (targetType == typeof(string))
            {
                return (T)(object)(je.ValueKind == JsonValueKind.String ? je.GetString() ?? "" : je.ToString());
            }
            if (targetType == typeof(int))
            {
                return je.ValueKind switch
                {
                    JsonValueKind.Number => (T)(object)je.GetInt32(),
                    JsonValueKind.String when int.TryParse(je.GetString(), out var i) => (T)(object)i,
                    _ => defaultValue
                };
            }
            if (targetType == typeof(double))
            {
                return je.ValueKind switch
                {
                    JsonValueKind.Number => (T)(object)je.GetDouble(),
                    JsonValueKind.String when double.TryParse(je.GetString(), out var d) => (T)(object)d,
                    _ => defaultValue
                };
            }
            if (targetType == typeof(bool))
            {
                return je.ValueKind switch
                {
                    JsonValueKind.True => (T)(object)true,
                    JsonValueKind.False => (T)(object)false,
                    JsonValueKind.String when bool.TryParse(je.GetString(), out var b) => (T)(object)b,
                    _ => defaultValue
                };
            }
            if (targetType == typeof(DateTime))
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String when DateTime.TryParse(je.GetString(), out var dt) => (T)(object)dt,
                    _ => defaultValue
                };
            }

            return defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Converts a value to a Neo4j-compatible type
    /// </summary>
    private static object ConvertForNeo4j(object? value)
    {
        if (value == null) return "";
        
        if (value is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? "",
                JsonValueKind.Number when je.TryGetInt64(out var l) => l,
                JsonValueKind.Number when je.TryGetDouble(out var d) => d,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => "",
                _ => je.ToString()
            };
        }
        
        return value;
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }

    // Helper methods for creating Cypher queries

    private static (string cypher, object parameters) CreateClassNodeQuery(CodeMemory memory)
    {
        var cypher = @"
            MERGE (c:Class {name: $name})
            SET c.namespace = $namespace,
                c.context = $context,
                c.file_path = $filePath,
                c.is_abstract = $isAbstract,
                c.is_static = $isStatic,
                c.is_sealed = $isSealed,
                c.access_modifier = $accessModifier,
                c.line_number = $lineNumber";

        var parameters = new
        {
            name = memory.Name,
            @namespace = GetMetadataValue(memory.Metadata, "namespace", ""),
            context = memory.Context,
            filePath = memory.FilePath,
            isAbstract = GetMetadataValue(memory.Metadata, "is_abstract", false),
            isStatic = GetMetadataValue(memory.Metadata, "is_static", false),
            isSealed = GetMetadataValue(memory.Metadata, "is_sealed", false),
            accessModifier = GetMetadataValue(memory.Metadata, "access_modifier", ""),
            lineNumber = memory.LineNumber
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreateMethodNodeQuery(CodeMemory memory)
    {
        var cypher = @"
            CREATE (m:Method {
                name: $name,
                signature: $signature,
                return_type: $returnType,
                is_async: $isAsync,
                is_static: $isStatic,
                access_modifier: $accessModifier,
                line_number: $lineNumber,
                class_name: $className,
                file_path: $filePath,
                context: $context,
                cyclomatic_complexity: $cyclomaticComplexity,
                cognitive_complexity: $cognitiveComplexity,
                lines_of_code: $linesOfCode,
                code_smell_count: $codeSmellCount,
                database_calls: $databaseCalls,
                has_database_access: $hasDatabaseAccess,
                has_http_calls: $hasHttpCalls,
                has_logging: $hasLogging,
                is_public_api: $isPublicApi,
                throws_exceptions: $throwsExceptions,
                is_test: $isTest
            })";

        var parameters = new
        {
            name = memory.Name,
            signature = memory.Content,
            returnType = GetMetadataValue(memory.Metadata, "return_type", ""),
            isAsync = GetMetadataValue(memory.Metadata, "is_async", false),
            isStatic = GetMetadataValue(memory.Metadata, "is_static", false),
            accessModifier = GetMetadataValue(memory.Metadata, "access_modifier", ""),
            lineNumber = memory.LineNumber,
            className = GetMetadataValue(memory.Metadata, "class_name", ""),
            filePath = memory.FilePath,
            context = memory.Context,
            cyclomaticComplexity = GetMetadataValue(memory.Metadata, "cyclomatic_complexity", 0),
            cognitiveComplexity = GetMetadataValue(memory.Metadata, "cognitive_complexity", 0),
            linesOfCode = GetMetadataValue(memory.Metadata, "lines_of_code", 0),
            codeSmellCount = GetMetadataValue(memory.Metadata, "code_smell_count", 0),
            databaseCalls = GetMetadataValue(memory.Metadata, "database_calls", 0),
            hasDatabaseAccess = GetMetadataValue(memory.Metadata, "has_database_access", false),
            hasHttpCalls = GetMetadataValue(memory.Metadata, "has_http_calls", false),
            hasLogging = GetMetadataValue(memory.Metadata, "has_logging", false),
            isPublicApi = GetMetadataValue(memory.Metadata, "is_public_api", false),
            throwsExceptions = GetMetadataValue(memory.Metadata, "throws_exceptions", false),
            isTest = GetMetadataValue(memory.Metadata, "is_test", false)
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreatePropertyNodeQuery(CodeMemory memory)
    {
        var cypher = @"
            CREATE (p:Property {
                name: $name,
                type: $type,
                has_getter: $hasGetter,
                has_setter: $hasSetter,
                access_modifier: $accessModifier,
                line_number: $lineNumber,
                class_name: $className,
                file_path: $filePath,
                context: $context
            })";

        var parameters = new
        {
            name = memory.Name,
            type = GetMetadataValue(memory.Metadata, "type", ""),
            hasGetter = GetMetadataValue(memory.Metadata, "has_getter", false),
            hasSetter = GetMetadataValue(memory.Metadata, "has_setter", false),
            accessModifier = GetMetadataValue(memory.Metadata, "access_modifier", ""),
            lineNumber = memory.LineNumber,
            className = GetMetadataValue(memory.Metadata, "class_name", ""),
            filePath = memory.FilePath,
            context = memory.Context
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreateInterfaceNodeQuery(CodeMemory memory)
    {
        var cypher = @"
            MERGE (i:Interface {name: $name})
            SET i.namespace = $namespace,
                i.context = $context,
                i.file_path = $filePath,
                i.line_number = $lineNumber";

        var parameters = new
        {
            name = memory.Name,
            @namespace = GetMetadataValue(memory.Metadata, "namespace", ""),
            context = memory.Context,
            filePath = memory.FilePath,
            lineNumber = memory.LineNumber
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreateFileNodeQuery(CodeMemory memory)
    {
        var cypher = @"
            MERGE (f:File {path: $path})
            SET f.name = $name,
                f.context = $context,
                f.size = $size,
                f.language = $language,
                f.last_modified = datetime($lastModified),
                f.line_count = $lineCount";

        var parameters = new
        {
            path = memory.FilePath,
            name = memory.Name,
            context = memory.Context,
            size = GetMetadataValue(memory.Metadata, "size", 0),
            language = GetMetadataValue(memory.Metadata, "language", ""),
            lastModified = GetMetadataValue(memory.Metadata, "last_modified", DateTime.UtcNow.ToString("O")),
            lineCount = GetMetadataValue(memory.Metadata, "line_count", 0)
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreatePatternNodeQuery(CodeMemory memory)
    {
        // Create unique ID for pattern: filepath:line:name to allow same pattern in multiple files
        var patternId = $"{memory.FilePath}:{memory.LineNumber}:{memory.Name}";
        
        var cypher = @"
            MERGE (p:Pattern {id: $id})
            SET p.name = $name,
                p.description = $description,
                p.category = $category,
                p.context = $context,
                p.filePath = $filePath,
                p.lineNumber = $lineNumber,
                p.confidence = $confidence,
                p.usage_count = $usageCount,
                p.detected_at = datetime($detectedAt),
                p.last_seen = datetime($lastSeen)";

        var parameters = new
        {
            id = patternId,
            name = memory.Name,
            description = memory.Content,
            category = GetMetadataValue(memory.Metadata, "category", ""),
            context = memory.Context,
            filePath = memory.FilePath,
            lineNumber = memory.LineNumber,
            confidence = GetMetadataValue(memory.Metadata, "confidence", 0.0),
            usageCount = GetMetadataValue(memory.Metadata, "usage_count", 0),
            detectedAt = DateTime.UtcNow.ToString("O"),
            lastSeen = DateTime.UtcNow.ToString("O")
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreateRelationshipQuery(CodeRelationship relationship)
    {
        var relationshipType = relationship.Type.ToString().ToUpperInvariant();

        // CRITICAL: Ensure workspace isolation by filtering both nodes by context
        // Smart relationship creation: Try to match existing nodes (Class/Method/File) first,
        // only create Reference node if target doesn't exist in the graph
        var cypher = $@"
            MATCH (from {{name: $fromName, context: $context}})
            OPTIONAL MATCH (existingTo {{name: $toName, context: $context}})
            WHERE existingTo:Class OR existingTo:Method OR existingTo:File OR existingTo:Interface OR existingTo:Property
            WITH from, existingTo
            CALL {{
                WITH from, existingTo
                WITH from, existingTo
                WHERE existingTo IS NOT NULL
                MERGE (from)-[r:{relationshipType}]->(existingTo)
                RETURN r
                UNION
                WITH from, existingTo
                WITH from, existingTo
                WHERE existingTo IS NULL
                MERGE (to:Reference {{name: $toName, context: $context}})
                ON CREATE SET to.created_at = datetime()
                MERGE (from)-[r:{relationshipType}]->(to)
                RETURN r
            }}
            RETURN r";

        // Add properties if any
        if (relationship.Properties.Any())
        {
            cypher = cypher.Replace("RETURN r", 
                "SET " + string.Join(", ", relationship.Properties.Select((p, i) => $"r.{p.Key} = $prop{i}")) + "\nRETURN r");
        }

        var parameters = new Dictionary<string, object>
        {
            ["fromName"] = relationship.FromName,
            ["toName"] = relationship.ToName,
            ["context"] = relationship.Context ?? "default" // Ensure context is always set
        };

        // Add property values (converting JsonElement to primitive types)
        int propIndex = 0;
        foreach (var prop in relationship.Properties)
        {
            parameters[$"prop{propIndex++}"] = ConvertForNeo4j(prop.Value);
        }

        return (cypher, parameters);
    }

    // TODO Management Implementation
    public async Task StoreTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        await session.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                CREATE (t:Todo {
                    id: $id,
                    context: $context,
                    title: $title,
                    description: $description,
                    priority: $priority,
                    status: $status,
                    filePath: $filePath,
                    lineNumber: $lineNumber,
                    assignedTo: $assignedTo,
                    createdAt: datetime($createdAt)
                })";

            await tx.RunAsync(cypher, new
            {
                id = todo.Id,
                context = todo.Context,
                title = todo.Title,
                description = todo.Description,
                priority = todo.Priority.ToString(),
                status = todo.Status.ToString(),
                filePath = todo.FilePath,
                lineNumber = todo.LineNumber,
                assignedTo = todo.AssignedTo,
                createdAt = todo.CreatedAt.ToString("O")
            });
        });
    }

    public async Task UpdateTodoAsync(TodoItem todo, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        await session.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MATCH (t:Todo {id: $id})
                SET t.status = $status,
                    t.completedAt = datetime($completedAt)";

            await tx.RunAsync(cypher, new
            {
                id = todo.Id,
                status = todo.Status.ToString(),
                completedAt = todo.CompletedAt?.ToString("O")
            });
        });
    }

    public async Task<bool> DeleteTodoAsync(string todoId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        var result = await session.ExecuteWriteAsync(async tx =>
        {
            var cypher = "MATCH (t:Todo {id: $id}) DELETE t RETURN count(t) as deleted";
            var cursor = await tx.RunAsync(cypher, new { id = todoId });
            var record = await cursor.SingleAsync();
            return record["deleted"].As<int>() > 0;
        });

        return result;
    }

    public async Task<TodoItem?> GetTodoAsync(string todoId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = "MATCH (t:Todo {id: $id}) RETURN t";
            var cursor = await tx.RunAsync(cypher, new { id = todoId });
            
            if (await cursor.FetchAsync())
            {
                var node = cursor.Current["t"].As<INode>();
                return MapTodoFromNode(node);
            }
            
            return null;
        });
    }

    public async Task<List<TodoItem>> GetTodosAsync(string? context = null, TodoStatus? status = null, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant();
        await using var session = _driver.AsyncSession();
        
        return await session.ExecuteReadAsync(async tx =>
        {
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(normalizedContext))
            {
                conditions.Add("toLower(t.context) = $context");
                parameters["context"] = normalizedContext;
            }

            if (status.HasValue)
            {
                conditions.Add("t.status = $status");
                parameters["status"] = status.Value.ToString();
            }

            var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            var cypher = $"MATCH (t:Todo) {whereClause} RETURN t ORDER BY t.createdAt DESC";
            
            var cursor = await tx.RunAsync(cypher, parameters);
            var todos = new List<TodoItem>();

            await foreach (var record in cursor)
            {
                var node = record["t"].As<INode>();
                todos.Add(MapTodoFromNode(node));
            }

            return todos;
        });
    }

    // Plan Management Implementation
    public async Task StorePlanAsync(DevelopmentPlan plan, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        await session.ExecuteWriteAsync(async tx =>
        {
            // Create plan node
            var cypher = @"
                CREATE (p:Plan {
                    id: $id,
                    context: $context,
                    name: $name,
                    description: $description,
                    status: $status,
                    createdAt: datetime($createdAt)
                })";

            await tx.RunAsync(cypher, new
            {
                id = plan.Id,
                context = plan.Context,
                name = plan.Name,
                description = plan.Description,
                status = plan.Status.ToString(),
                createdAt = plan.CreatedAt.ToString("O")
            });

            // Create task nodes
            foreach (var task in plan.Tasks)
            {
                var taskCypher = @"
                    MATCH (p:Plan {id: $planId})
                    CREATE (t:PlanTask {
                        id: $id,
                        title: $title,
                        description: $description,
                        status: $status,
                        orderIndex: $orderIndex
                    })
                    CREATE (p)-[:HAS_TASK]->(t)";

                await tx.RunAsync(taskCypher, new
                {
                    planId = plan.Id,
                    id = task.Id,
                    title = task.Title,
                    description = task.Description,
                    status = task.Status.ToString(),
                    orderIndex = task.OrderIndex
                });

                // Create task dependencies
                foreach (var depId in task.Dependencies)
                {
                    var depCypher = @"
                        MATCH (t1:PlanTask {id: $taskId})
                        MATCH (t2:PlanTask {id: $dependsOnId})
                        CREATE (t1)-[:DEPENDS_ON]->(t2)";

                    await tx.RunAsync(depCypher, new
                    {
                        taskId = task.Id,
                        dependsOnId = depId
                    });
                }
            }
        });
    }

    public async Task UpdatePlanAsync(DevelopmentPlan plan, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        await session.ExecuteWriteAsync(async tx =>
        {
            // Update plan
            var cypher = @"
                MATCH (p:Plan {id: $id})
                SET p.name = $name,
                    p.description = $description,
                    p.status = $status,
                    p.completedAt = datetime($completedAt)";

            await tx.RunAsync(cypher, new
            {
                id = plan.Id,
                name = plan.Name,
                description = plan.Description,
                status = plan.Status.ToString(),
                completedAt = plan.CompletedAt?.ToString("O")
            });

            // Update tasks
            foreach (var task in plan.Tasks)
            {
                var taskCypher = @"
                    MATCH (t:PlanTask {id: $id})
                    SET t.title = $title,
                        t.description = $description,
                        t.status = $status,
                        t.completedAt = datetime($completedAt)";

                await tx.RunAsync(taskCypher, new
                {
                    id = task.Id,
                    title = task.Title,
                    description = task.Description,
                    status = task.Status.ToString(),
                    completedAt = task.CompletedAt?.ToString("O")
                });
            }
        });
    }

    public async Task<bool> DeletePlanAsync(string planId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        var result = await session.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:Plan {id: $id})
                OPTIONAL MATCH (p)-[:HAS_TASK]->(t:PlanTask)
                DETACH DELETE p, t
                RETURN count(p) as deleted";
            
            var cursor = await tx.RunAsync(cypher, new { id = planId });
            var record = await cursor.SingleAsync();
            return record["deleted"].As<int>() > 0;
        });

        return result;
    }

    public async Task<DevelopmentPlan?> GetPlanAsync(string planId, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        return await session.ExecuteReadAsync(async tx =>
        {
            // Get plan
            var cypher = "MATCH (p:Plan {id: $id}) RETURN p";
            var cursor = await tx.RunAsync(cypher, new { id = planId });
            
            if (!await cursor.FetchAsync())
            {
                return null;
            }

            var planNode = cursor.Current["p"].As<INode>();
            var plan = MapPlanFromNode(planNode);

            // Get tasks
            var taskCypher = @"
                MATCH (p:Plan {id: $id})-[:HAS_TASK]->(t:PlanTask)
                OPTIONAL MATCH (t)-[:DEPENDS_ON]->(dep:PlanTask)
                RETURN t, collect(dep.id) as dependencies
                ORDER BY t.orderIndex";
            
            var taskCursor = await tx.RunAsync(taskCypher, new { id = planId });
            
            await foreach (var record in taskCursor)
            {
                var taskNode = record["t"].As<INode>();
                var task = MapTaskFromNode(taskNode);
                task.Dependencies = record["dependencies"].As<List<string>>();
                plan.Tasks.Add(task);
            }

            return plan;
        });
    }

    public async Task<List<DevelopmentPlan>> GetPlansAsync(string? context = null, PlanStatus? status = null, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant();
        await using var session = _driver.AsyncSession();
        
        return await session.ExecuteReadAsync(async tx =>
        {
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(normalizedContext))
            {
                conditions.Add("toLower(p.context) = $context");
                parameters["context"] = normalizedContext;
            }

            if (status.HasValue)
            {
                conditions.Add("p.status = $status");
                parameters["status"] = status.Value.ToString();
            }

            var whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            var cypher = $"MATCH (p:Plan) {whereClause} RETURN p ORDER BY p.createdAt DESC";
            
            var cursor = await tx.RunAsync(cypher, parameters);
            var plans = new List<DevelopmentPlan>();

            await foreach (var record in cursor)
            {
                var planNode = record["p"].As<INode>();
                var plan = MapPlanFromNode(planNode);
                
                // Get tasks for this plan
                var taskCypher = @"
                    MATCH (p:Plan {id: $id})-[:HAS_TASK]->(t:PlanTask)
                    RETURN t ORDER BY t.orderIndex";
                
                var taskCursor = await tx.RunAsync(taskCypher, new { id = plan.Id });
                
                await foreach (var taskRecord in taskCursor)
                {
                    var taskNode = taskRecord["t"].As<INode>();
                    plan.Tasks.Add(MapTaskFromNode(taskNode));
                }

                plans.Add(plan);
            }

            return plans;
        });
    }

    // Helper methods for mapping
    private TodoItem MapTodoFromNode(INode node)
    {
        var props = node.Properties;
        return new TodoItem
        {
            Id = props["id"].As<string>(),
            Context = props["context"].As<string>(),
            Title = props["title"].As<string>(),
            Description = props["description"].As<string>(),
            Priority = Enum.Parse<TodoPriority>(props["priority"].As<string>()),
            Status = Enum.Parse<TodoStatus>(props["status"].As<string>()),
            FilePath = props["filePath"].As<string>(),
            LineNumber = props["lineNumber"].As<int>(),
            AssignedTo = props["assignedTo"].As<string>(),
            CreatedAt = props.ContainsKey("createdAt") ? ConvertNeo4jDateTime(props["createdAt"]) : DateTime.UtcNow,
            CompletedAt = props.ContainsKey("completedAt") ? ConvertNeo4jDateTime(props["completedAt"]) : null
        };
    }

    private DateTime ConvertNeo4jDateTime(object value)
    {
        if (value == null) return DateTime.UtcNow;
        
        // Neo4j returns ZonedDateTime or LocalDateTime objects
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

    private DevelopmentPlan MapPlanFromNode(INode node)
    {
        var props = node.Properties;
        return new DevelopmentPlan
        {
            Id = props["id"].As<string>(),
            Context = props["context"].As<string>(),
            Name = props["name"].As<string>(),
            Description = props["description"].As<string>(),
            Status = Enum.Parse<PlanStatus>(props["status"].As<string>()),
            CreatedAt = props.ContainsKey("createdAt") ? ConvertNeo4jDateTime(props["createdAt"]) : DateTime.UtcNow,
            CompletedAt = props.ContainsKey("completedAt") ? ConvertNeo4jDateTime(props["completedAt"]) : null,
            Tasks = new List<PlanTask>()
        };
    }

    private PlanTask MapTaskFromNode(INode node)
    {
        var props = node.Properties;
        return new PlanTask
        {
            Id = props["id"].As<string>(),
            Title = props["title"].As<string>(),
            Description = props["description"].As<string>(),
            Status = Enum.Parse<TaskStatusModel>(props["status"].As<string>()),
            OrderIndex = props["orderIndex"].As<int>(),
            CompletedAt = props.ContainsKey("completedAt") ? ConvertNeo4jDateTime(props["completedAt"]) : null,
            Dependencies = new List<string>()
        };
    }

    public async Task StorePatternNodeAsync(CodePattern pattern, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();
        
        await session.ExecuteWriteAsync(async tx =>
        {
            // Create Pattern node
            var cypher = @"
                MERGE (p:Pattern {id: $id})
                SET p.name = $name,
                    p.type = $type,
                    p.category = $category,
                    p.implementation = $implementation,
                    p.language = $language,
                    p.filePath = $filePath,
                    p.lineNumber = $lineNumber,
                    p.content = $content,
                    p.bestPractice = $bestPractice,
                    p.azureUrl = $azureUrl,
                    p.confidence = $confidence,
                    p.context = $context,
                    p.isPositivePattern = $isPositivePattern,
                    p.detectedAt = datetime($detectedAt)";
            
            var parameters = new
            {
                id = $"{pattern.FilePath}:{pattern.LineNumber}:{pattern.Name}",
                name = pattern.Name,
                type = pattern.Type.ToString(),
                category = pattern.Category.ToString(),
                implementation = pattern.Implementation,
                language = pattern.Language,
                filePath = pattern.FilePath,
                lineNumber = pattern.LineNumber,
                content = pattern.Content,
                bestPractice = pattern.BestPractice,
                azureUrl = pattern.AzureBestPracticeUrl,
                confidence = pattern.Confidence,
                context = pattern.Context,
                isPositivePattern = pattern.IsPositivePattern,
                detectedAt = pattern.DetectedAt.ToString("O")
            };

            await tx.RunAsync(cypher, parameters);

            // Create relationship to file
            var relCypher = @"
                MATCH (p:Pattern {id: $patternId})
                MERGE (f:File {path: $filePath})
                ON CREATE SET f.context = $context
                MERGE (f)-[:CONTAINS_PATTERN]->(p)";
            
            await tx.RunAsync(relCypher, new 
            { 
                patternId = $"{pattern.FilePath}:{pattern.LineNumber}:{pattern.Name}",
                filePath = pattern.FilePath,
                context = pattern.Context
            });
        });

        _logger.LogDebug("Stored pattern node: {PatternName} in {FilePath}", pattern.Name, pattern.FilePath);
    }

    public async Task<List<CodePattern>> GetPatternsByTypeAsync(PatternType type, string? context = null, CancellationToken cancellationToken = default)
    {
        var normalizedContext = context?.ToLowerInvariant();
        await using var session = _driver.AsyncSession();
        
        return await session.ExecuteReadAsync(async tx =>
        {
            var conditions = new List<string> { "p.type = $type" };
            var parameters = new Dictionary<string, object> { ["type"] = type.ToString() };

            if (!string.IsNullOrWhiteSpace(normalizedContext))
            {
                conditions.Add("toLower(p.context) = $context");
                parameters["context"] = normalizedContext;
            }

            var whereClause = "WHERE " + string.Join(" AND ", conditions);
            var cypher = $"MATCH (p:Pattern) {whereClause} RETURN p ORDER BY p.confidence DESC LIMIT 100";
            
            var cursor = await tx.RunAsync(cypher, parameters);
            var patterns = new List<CodePattern>();

            await foreach (var record in cursor)
            {
                var node = record["p"].As<INode>();
                var props = node.Properties;
                
                patterns.Add(new CodePattern
                {
                    Name = props["name"].As<string>(),
                    Type = Enum.Parse<PatternType>(props["type"].As<string>()),
                    Category = Enum.Parse<PatternCategory>(props["category"].As<string>()),
                    Implementation = props["implementation"].As<string>(),
                    Language = props["language"].As<string>(),
                    FilePath = props["filePath"].As<string>(),
                    LineNumber = props["lineNumber"].As<int>(),
                    Content = props["content"].As<string>(),
                    BestPractice = props["bestPractice"].As<string>(),
                    AzureBestPracticeUrl = props["azureUrl"].As<string>(),
                    Confidence = props["confidence"].As<float>(),
                    Context = props["context"].As<string>(),
                    IsPositivePattern = props["isPositivePattern"].As<bool>(),
                    DetectedAt = ConvertNeo4jDateTime(props["detectedAt"])
                });
            }

            return patterns;
        });
    }
}

