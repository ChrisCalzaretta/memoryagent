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
                "CREATE CONSTRAINT pattern_name IF NOT EXISTS FOR (p:Pattern) REQUIRE p.name IS UNIQUE",
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
                "CREATE INDEX file_context IF NOT EXISTS FOR (f:File) ON (f.context)"
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

            _logger.LogInformation("Neo4j database initialized successfully");
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
                            _ => throw new ArgumentException($"Unknown type: {memory.Type}")
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
                var cursor = await tx.RunAsync(@"
                    MATCH (changed:Class {name: $className})<-[:INHERITS|USES*]-(impacted)
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
                var cursor = await tx.RunAsync($@"
                    MATCH path = (class:Class {{name: $className}})-[:USES*1..{maxDepth}]->(dep)
                    RETURN DISTINCT dep.name AS name
                    ORDER BY length(path)
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
        await using var session = _driver.AsyncSession();

        try
        {
            var contextFilter = string.IsNullOrWhiteSpace(context) ? "" : "WHERE c1.context = $context AND c2.context = $context";

            var result = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync($@"
                    MATCH path = (c1:Class)-[:USES*2..10]->(c2:Class)-[:USES*]->(c1)
                    {contextFilter}
                    WHERE c1 <> c2
                    RETURN [node in nodes(path) | node.name] AS cycle
                    LIMIT 50",
                    new { context = context ?? "" });

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
                var cursor = await tx.RunAsync(@"
                    MATCH (c:Class)-[:FOLLOWS_PATTERN]->(p:Pattern {name: $patternName})
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
            @namespace = memory.Metadata.GetValueOrDefault("namespace", ""),
            context = memory.Context,
            filePath = memory.FilePath,
            isAbstract = memory.Metadata.GetValueOrDefault("is_abstract", false),
            isStatic = memory.Metadata.GetValueOrDefault("is_static", false),
            isSealed = memory.Metadata.GetValueOrDefault("is_sealed", false),
            accessModifier = memory.Metadata.GetValueOrDefault("access_modifier", ""),
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
                context: $context
            })";

        var parameters = new
        {
            name = memory.Name,
            signature = memory.Content,
            returnType = memory.Metadata.GetValueOrDefault("return_type", ""),
            isAsync = memory.Metadata.GetValueOrDefault("is_async", false),
            isStatic = memory.Metadata.GetValueOrDefault("is_static", false),
            accessModifier = memory.Metadata.GetValueOrDefault("access_modifier", ""),
            lineNumber = memory.LineNumber,
            className = memory.Metadata.GetValueOrDefault("class_name", ""),
            filePath = memory.FilePath,
            context = memory.Context
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
            type = memory.Metadata.GetValueOrDefault("type", ""),
            hasGetter = memory.Metadata.GetValueOrDefault("has_getter", false),
            hasSetter = memory.Metadata.GetValueOrDefault("has_setter", false),
            accessModifier = memory.Metadata.GetValueOrDefault("access_modifier", ""),
            lineNumber = memory.LineNumber,
            className = memory.Metadata.GetValueOrDefault("class_name", ""),
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
            @namespace = memory.Metadata.GetValueOrDefault("namespace", ""),
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
            size = memory.Metadata.GetValueOrDefault("size", 0),
            language = memory.Metadata.GetValueOrDefault("language", ""),
            lastModified = memory.Metadata.GetValueOrDefault("last_modified", DateTime.UtcNow.ToString("O")),
            lineCount = memory.Metadata.GetValueOrDefault("line_count", 0)
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreatePatternNodeQuery(CodeMemory memory)
    {
        var cypher = @"
            MERGE (p:Pattern {name: $name})
            SET p.description = $description,
                p.category = $category,
                p.context = $context,
                p.confidence = $confidence,
                p.usage_count = $usageCount,
                p.detected_at = datetime($detectedAt),
                p.last_seen = datetime($lastSeen)";

        var parameters = new
        {
            name = memory.Name,
            description = memory.Content,
            category = memory.Metadata.GetValueOrDefault("category", ""),
            context = memory.Context,
            confidence = memory.Metadata.GetValueOrDefault("confidence", 0.0),
            usageCount = memory.Metadata.GetValueOrDefault("usage_count", 0),
            detectedAt = DateTime.UtcNow.ToString("O"),
            lastSeen = DateTime.UtcNow.ToString("O")
        };

        return (cypher, parameters);
    }

    private static (string cypher, object parameters) CreateRelationshipQuery(CodeRelationship relationship)
    {
        var relationshipType = relationship.Type.ToString().ToUpperInvariant();

        // Create or match "to" node as a reference (might be external type, namespace, etc.)
        // This ensures relationships work even when target isn't fully indexed
        var cypher = $@"
            MATCH (from {{name: $fromName}})
            MERGE (to:Reference {{name: $toName}})
            MERGE (from)-[r:{relationshipType}]->(to)";

        // Add properties if any
        if (relationship.Properties.Any())
        {
            cypher += "\nSET " + string.Join(", ", relationship.Properties.Select((p, i) => $"r.{p.Key} = $prop{i}"));
        }

        var parameters = new Dictionary<string, object>
        {
            ["fromName"] = relationship.FromName,
            ["toName"] = relationship.ToName
        };

        // Add property values
        int propIndex = 0;
        foreach (var prop in relationship.Properties)
        {
            parameters[$"prop{propIndex++}"] = prop.Value;
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
        await using var session = _driver.AsyncSession();
        
        return await session.ExecuteReadAsync(async tx =>
        {
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(context))
            {
                conditions.Add("t.context = $context");
                parameters["context"] = context;
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
        await using var session = _driver.AsyncSession();
        
        return await session.ExecuteReadAsync(async tx =>
        {
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(context))
            {
                conditions.Add("p.context = $context");
                parameters["context"] = context;
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
}

