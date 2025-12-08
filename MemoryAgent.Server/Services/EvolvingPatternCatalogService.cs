using MemoryAgent.Server.Models;
using Neo4j.Driver;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for managing an evolving pattern catalog that learns and improves.
/// Stores patterns in Neo4j with full version history and learning metrics.
/// </summary>
public class EvolvingPatternCatalogService : IEvolvingPatternCatalogService, IDisposable
{
    private readonly IDriver _driver;
    private readonly ILogger<EvolvingPatternCatalogService> _logger;

    public EvolvingPatternCatalogService(
        IConfiguration configuration,
        ILogger<EvolvingPatternCatalogService> logger)
    {
        var neo4jUrl = configuration["Neo4j:Url"] ?? "bolt://localhost:7687";
        var neo4jUser = configuration["Neo4j:User"] ?? "neo4j";
        var neo4jPassword = configuration["Neo4j:Password"] ?? "memoryagent";

        _driver = GraphDatabase.Driver(neo4jUrl, AuthTokens.Basic(neo4jUser, neo4jPassword));
        _logger = logger;
    }

    #region Pattern Retrieval

    public async Task<List<EvolvingPattern>> GetActivePatternsAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true AND p.isDeprecated = false
                RETURN p
                ORDER BY p.name";

            var cursor = await tx.RunAsync(cypher);
            var patterns = new List<EvolvingPattern>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    public async Task<List<EvolvingPattern>> GetPatternsByTypeAsync(PatternType type, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:EvolvingPattern {type: $type})
                WHERE p.isActive = true AND p.isDeprecated = false
                RETURN p
                ORDER BY p.usefulnessScore DESC, p.name";

            var cursor = await tx.RunAsync(cypher, new { type = type.ToString() });
            var patterns = new List<EvolvingPattern>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    public async Task<List<EvolvingPattern>> GetPatternsByCategoryAsync(PatternCategory category, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:EvolvingPattern {category: $category})
                WHERE p.isActive = true AND p.isDeprecated = false
                RETURN p
                ORDER BY p.usefulnessScore DESC, p.name";

            var cursor = await tx.RunAsync(cypher, new { category = category.ToString() });
            var patterns = new List<EvolvingPattern>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    public async Task<EvolvingPattern?> GetPatternAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:EvolvingPattern {name: $name})
                WHERE p.isActive = true
                RETURN p";

            var cursor = await tx.RunAsync(cypher, new { name });

            if (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                return MapPatternFromNode(node);
            }

            return null;
        });
    }

    public async Task<List<EvolvingPattern>> GetPatternHistoryAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:EvolvingPattern {name: $name})
                RETURN p
                ORDER BY p.version DESC";

            var cursor = await tx.RunAsync(cypher, new { name });
            var patterns = new List<EvolvingPattern>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    public async Task<List<EvolvingPattern>> SearchPatternsAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true 
                  AND (toLower(p.name) CONTAINS toLower($query) 
                       OR toLower(p.recommendation) CONTAINS toLower($query))
                RETURN p
                ORDER BY p.usefulnessScore DESC
                LIMIT 50";

            var cursor = await tx.RunAsync(cypher, new { query });
            var patterns = new List<EvolvingPattern>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    #endregion

    #region Pattern Management

    public async Task<EvolvingPattern> CreatePatternAsync(
        string name,
        PatternType type,
        PatternCategory category,
        string recommendation,
        string referenceUrl,
        List<PatternDetectionRule>? detectionRules = null,
        List<string>? examples = null,
        CancellationToken cancellationToken = default)
    {
        var pattern = new EvolvingPattern
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Version = 1,
            Type = type,
            Category = category,
            Recommendation = recommendation,
            ReferenceUrl = referenceUrl,
            DetectionRules = detectionRules ?? new(),
            Examples = examples ?? new(),
            IsActive = true,
            CreatedBy = "system",
            Confidence = 0.5f
        };

        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                CREATE (p:EvolvingPattern {
                    id: $id,
                    name: $name,
                    version: $version,
                    type: $type,
                    category: $category,
                    recommendation: $recommendation,
                    referenceUrl: $referenceUrl,
                    detectionRulesJson: $detectionRulesJson,
                    examplesJson: $examplesJson,
                    antiPatternExamplesJson: '[]',
                    isActive: true,
                    isDeprecated: false,
                    createdAt: datetime($createdAt),
                    createdBy: $createdBy,
                    timesDetected: 0,
                    timesUseful: 0,
                    timesNotUseful: 0,
                    usefulnessScore: 0.5,
                    confidence: 0.5
                })";

            await tx.RunAsync(cypher, new
            {
                id = pattern.Id,
                name = pattern.Name,
                version = pattern.Version,
                type = pattern.Type.ToString(),
                category = pattern.Category.ToString(),
                recommendation = pattern.Recommendation,
                referenceUrl = pattern.ReferenceUrl,
                detectionRulesJson = System.Text.Json.JsonSerializer.Serialize(pattern.DetectionRules),
                examplesJson = System.Text.Json.JsonSerializer.Serialize(pattern.Examples),
                createdAt = pattern.CreatedAt.ToString("O"),
                createdBy = pattern.CreatedBy
            });
        });

        _logger.LogInformation("📋 Created pattern '{Name}' v{Version} ({Type}/{Category})", 
            name, pattern.Version, type, category);
        return pattern;
    }

    public async Task<EvolvingPattern> CreateVersionAsync(
        string name,
        string evolutionReason,
        string? newRecommendation = null,
        List<PatternDetectionRule>? newDetectionRules = null,
        List<string>? newExamples = null,
        CancellationToken cancellationToken = default)
    {
        var history = await GetPatternHistoryAsync(name, cancellationToken);
        var current = history.FirstOrDefault()
            ?? throw new InvalidOperationException($"Pattern '{name}' not found");

        var newVersion = new EvolvingPattern
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Version = current.Version + 1,
            Type = current.Type,
            Category = current.Category,
            Recommendation = newRecommendation ?? current.Recommendation,
            ReferenceUrl = current.ReferenceUrl,
            DetectionRules = newDetectionRules ?? current.DetectionRules,
            Examples = newExamples ?? current.Examples,
            AntiPatternExamples = current.AntiPatternExamples,
            IsActive = false, // New versions start inactive
            ParentVersionId = current.Id,
            EvolutionReason = evolutionReason,
            CreatedBy = "evolution",
            Confidence = current.Confidence
        };

        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                CREATE (p:EvolvingPattern {
                    id: $id,
                    name: $name,
                    version: $version,
                    type: $type,
                    category: $category,
                    recommendation: $recommendation,
                    referenceUrl: $referenceUrl,
                    detectionRulesJson: $detectionRulesJson,
                    examplesJson: $examplesJson,
                    antiPatternExamplesJson: $antiPatternExamplesJson,
                    isActive: false,
                    isDeprecated: false,
                    parentVersionId: $parentVersionId,
                    evolutionReason: $evolutionReason,
                    createdAt: datetime($createdAt),
                    createdBy: $createdBy,
                    timesDetected: 0,
                    timesUseful: 0,
                    timesNotUseful: 0,
                    usefulnessScore: 0.5,
                    confidence: $confidence
                })";

            await tx.RunAsync(cypher, new
            {
                id = newVersion.Id,
                name = newVersion.Name,
                version = newVersion.Version,
                type = newVersion.Type.ToString(),
                category = newVersion.Category.ToString(),
                recommendation = newVersion.Recommendation,
                referenceUrl = newVersion.ReferenceUrl,
                detectionRulesJson = System.Text.Json.JsonSerializer.Serialize(newVersion.DetectionRules),
                examplesJson = System.Text.Json.JsonSerializer.Serialize(newVersion.Examples),
                antiPatternExamplesJson = System.Text.Json.JsonSerializer.Serialize(newVersion.AntiPatternExamples),
                parentVersionId = newVersion.ParentVersionId,
                evolutionReason = newVersion.EvolutionReason,
                createdAt = newVersion.CreatedAt.ToString("O"),
                createdBy = newVersion.CreatedBy,
                confidence = newVersion.Confidence
            });

            // Create evolution relationship
            await tx.RunAsync(@"
                MATCH (old:EvolvingPattern {id: $parentId})
                MATCH (new:EvolvingPattern {id: $newId})
                CREATE (old)-[:EVOLVED_TO {reason: $reason, at: datetime()}]->(new)",
                new { parentId = current.Id, newId = newVersion.Id, reason = evolutionReason });
        });

        _logger.LogInformation("📋 Created pattern '{Name}' v{Version} (evolved from v{OldVersion}): {Reason}",
            name, newVersion.Version, current.Version, evolutionReason);
        return newVersion;
    }

    public async Task DeprecatePatternAsync(string name, string reason, string? supersededBy = null, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
                MATCH (p:EvolvingPattern {name: $name, isActive: true})
                SET p.isDeprecated = true,
                    p.deprecationReason = $reason,
                    p.supersededBy = $supersededBy,
                    p.deprecatedAt = datetime()",
                new { name, reason, supersededBy = supersededBy ?? "" });
        });

        _logger.LogWarning("⚠️ Deprecated pattern '{Name}': {Reason}", name, reason);
    }

    public async Task ActivateVersionAsync(string name, int version, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Deactivate all versions
            await tx.RunAsync(
                "MATCH (p:EvolvingPattern {name: $name}) SET p.isActive = false",
                new { name });

            // Activate specified version
            await tx.RunAsync(
                "MATCH (p:EvolvingPattern {name: $name, version: $version}) SET p.isActive = true",
                new { name, version });
        });

        _logger.LogInformation("✅ Activated pattern '{Name}' v{Version}", name, version);
    }

    public async Task RollbackAsync(string name, CancellationToken cancellationToken = default)
    {
        var history = await GetPatternHistoryAsync(name, cancellationToken);
        var active = history.FirstOrDefault(p => p.IsActive);

        if (active == null || string.IsNullOrEmpty(active.ParentVersionId))
        {
            throw new InvalidOperationException($"Cannot rollback pattern '{name}' - no previous version");
        }

        var previousVersion = history.FirstOrDefault(p => p.Id == active.ParentVersionId)
            ?? throw new InvalidOperationException($"Previous version not found for pattern '{name}'");

        await ActivateVersionAsync(name, previousVersion.Version, cancellationToken);
        _logger.LogWarning("⚠️ Rolled back pattern '{Name}' from v{Current} to v{Previous}",
            name, active.Version, previousVersion.Version);
    }

    #endregion

    #region Feedback & Learning

    public async Task RecordDetectionAsync(
        string patternName,
        string filePath,
        int lineNumber,
        string codeSnippet,
        float confidence,
        string? sessionId = null,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Create detection record
            var feedbackId = Guid.NewGuid().ToString();
            await tx.RunAsync(@"
                CREATE (f:PatternDetectionFeedback {
                    id: $id,
                    patternName: $patternName,
                    filePath: $filePath,
                    lineNumber: $lineNumber,
                    codeSnippet: $codeSnippet,
                    detectionConfidence: $confidence,
                    sessionId: $sessionId,
                    context: $context,
                    detectedAt: datetime()
                })",
                new { 
                    id = feedbackId, 
                    patternName, 
                    filePath, 
                    lineNumber, 
                    codeSnippet = codeSnippet.Length > 1000 ? codeSnippet.Substring(0, 1000) : codeSnippet,
                    confidence,
                    sessionId = sessionId ?? "",
                    context = context?.ToLowerInvariant() ?? ""
                });

            // Update pattern metrics
            await tx.RunAsync(@"
                MATCH (p:EvolvingPattern {name: $patternName, isActive: true})
                SET p.timesDetected = p.timesDetected + 1,
                    p.lastDetectedAt = datetime()",
                new { patternName });
        });
    }

    public async Task RecordFeedbackAsync(
        string feedbackId,
        PatternFeedbackType feedbackType,
        string? comments = null,
        string? suggestedImprovement = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Update feedback record
            await tx.RunAsync(@"
                MATCH (f:PatternDetectionFeedback {id: $feedbackId})
                SET f.feedbackType = $feedbackType,
                    f.wasCorrect = $wasCorrect,
                    f.comments = $comments,
                    f.suggestedImprovement = $suggestedImprovement,
                    f.feedbackAt = datetime()",
                new { 
                    feedbackId, 
                    feedbackType = feedbackType.ToString(),
                    wasCorrect = feedbackType == PatternFeedbackType.Correct || feedbackType == PatternFeedbackType.CorrectButNotUseful,
                    comments = comments ?? "",
                    suggestedImprovement = suggestedImprovement ?? ""
                });

            // Get pattern name and update metrics
            var cursor = await tx.RunAsync(
                "MATCH (f:PatternDetectionFeedback {id: $feedbackId}) RETURN f.patternName as patternName",
                new { feedbackId });

            if (await cursor.FetchAsync())
            {
                var patternName = cursor.Current["patternName"].As<string>();

                if (feedbackType == PatternFeedbackType.Correct)
                {
                    await tx.RunAsync(@"
                        MATCH (p:EvolvingPattern {name: $patternName, isActive: true})
                        SET p.timesUseful = p.timesUseful + 1,
                            p.usefulnessScore = toFloat(p.timesUseful + 1) / (p.timesDetected + 1),
                            p.lastEvaluatedAt = datetime()",
                        new { patternName });
                }
                else if (feedbackType == PatternFeedbackType.FalsePositive || feedbackType == PatternFeedbackType.CorrectButNotUseful)
                {
                    await tx.RunAsync(@"
                        MATCH (p:EvolvingPattern {name: $patternName, isActive: true})
                        SET p.timesNotUseful = p.timesNotUseful + 1,
                            p.usefulnessScore = toFloat(p.timesUseful) / (p.timesDetected + 1),
                            p.lastEvaluatedAt = datetime()",
                        new { patternName });
                }
            }
        });

        _logger.LogInformation("📊 Recorded {FeedbackType} feedback for detection {FeedbackId}", feedbackType, feedbackId);
    }

    public async Task RecordUsefulAsync(string patternName, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
                MATCH (p:EvolvingPattern {name: $patternName, isActive: true})
                SET p.timesUseful = p.timesUseful + 1,
                    p.usefulnessScore = toFloat(p.timesUseful + 1) / CASE WHEN p.timesDetected > 0 THEN p.timesDetected ELSE 1 END,
                    p.lastEvaluatedAt = datetime()",
                new { patternName });
        });
    }

    public async Task RecordNotUsefulAsync(string patternName, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
                MATCH (p:EvolvingPattern {name: $patternName, isActive: true})
                SET p.timesNotUseful = p.timesNotUseful + 1,
                    p.usefulnessScore = toFloat(p.timesUseful) / CASE WHEN p.timesDetected > 0 THEN p.timesDetected ELSE 1 END,
                    p.lastEvaluatedAt = datetime()",
                new { patternName });
        });
    }

    public async Task<PatternSuggestion> SuggestPatternAsync(
        PatternSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Basic suggestion logic (would ideally use LLM for better suggestions)
        var suggestion = new PatternSuggestion
        {
            SuggestedName = request.SuggestedName ?? GeneratePatternName(request.Description),
            SuggestedType = InferPatternType(request.CodeExample, request.Description),
            SuggestedCategory = InferPatternCategory(request.CodeExample, request.Description),
            GeneratedRecommendation = request.Description,
            Confidence = 0.6f
        };

        // Check if similar pattern already exists
        var existing = await SearchPatternsAsync(suggestion.SuggestedName, cancellationToken);
        if (existing.Any())
        {
            suggestion.AlreadyExists = true;
            suggestion.ExistingPatternName = existing.First().Name;
        }

        // Generate basic detection rules
        suggestion.GeneratedRules = GenerateDetectionRules(request.CodeExample);

        // Store suggestion for review
        await using var session = _driver.AsyncSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(@"
                CREATE (s:PatternSuggestion {
                    id: $id,
                    suggestedName: $suggestedName,
                    suggestedType: $suggestedType,
                    suggestedCategory: $suggestedCategory,
                    codeExample: $codeExample,
                    description: $description,
                    rationale: $rationale,
                    context: $context,
                    sessionId: $sessionId,
                    createdAt: datetime(),
                    status: 'pending'
                })",
                new {
                    id = Guid.NewGuid().ToString(),
                    suggestedName = suggestion.SuggestedName,
                    suggestedType = suggestion.SuggestedType.ToString(),
                    suggestedCategory = suggestion.SuggestedCategory.ToString(),
                    codeExample = request.CodeExample,
                    description = request.Description,
                    rationale = request.Rationale ?? "",
                    context = request.Context?.ToLowerInvariant() ?? "",
                    sessionId = request.SessionId ?? ""
                });
        });

        _logger.LogInformation("💡 Pattern suggestion created: '{Name}' ({Type}/{Category})", 
            suggestion.SuggestedName, suggestion.SuggestedType, suggestion.SuggestedCategory);

        return suggestion;
    }

    #endregion

    #region Analytics

    public async Task<PatternCatalogMetrics> GetCatalogMetricsAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var metrics = new PatternCatalogMetrics();

            // Get basic counts
            var countCursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WITH 
                    count(DISTINCT p.name) as totalPatterns,
                    count(CASE WHEN p.isActive AND NOT p.isDeprecated THEN 1 END) as activePatterns,
                    count(CASE WHEN p.isDeprecated THEN 1 END) as deprecatedPatterns,
                    count(p) as totalVersions,
                    avg(p.usefulnessScore) as avgUsefulness,
                    sum(p.timesDetected) as totalDetections
                RETURN totalPatterns, activePatterns, deprecatedPatterns, totalVersions, avgUsefulness, totalDetections");

            if (await countCursor.FetchAsync())
            {
                metrics.TotalPatterns = countCursor.Current["totalPatterns"].As<int>();
                metrics.ActivePatterns = countCursor.Current["activePatterns"].As<int>();
                metrics.DeprecatedPatterns = countCursor.Current["deprecatedPatterns"].As<int>();
                metrics.TotalVersions = countCursor.Current["totalVersions"].As<int>();
                metrics.AvgUsefulnessScore = (float)(countCursor.Current["avgUsefulness"].As<double?>() ?? 0.5);
                metrics.TotalDetections = countCursor.Current["totalDetections"].As<int>();
            }

            // Get patterns by type
            var typeCursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true
                RETURN p.type as type, count(*) as count");

            while (await typeCursor.FetchAsync())
            {
                var typeStr = typeCursor.Current["type"].As<string>();
                if (Enum.TryParse<PatternType>(typeStr, out var type))
                {
                    metrics.PatternsByType[type] = typeCursor.Current["count"].As<int>();
                }
            }

            // Get patterns by category
            var catCursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true
                RETURN p.category as category, count(*) as count");

            while (await catCursor.FetchAsync())
            {
                var catStr = catCursor.Current["category"].As<string>();
                if (Enum.TryParse<PatternCategory>(catStr, out var cat))
                {
                    metrics.PatternsByCategory[cat] = catCursor.Current["count"].As<int>();
                }
            }

            return metrics;
        });
    }

    public async Task<PatternMetrics> GetPatternMetricsAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern {name: $name})
                WITH p ORDER BY p.version DESC
                WITH collect(p) as versions, [v IN collect(p) WHERE v.isActive][0] as active
                RETURN 
                    $name as name,
                    active.version as activeVersion,
                    size(versions) as totalVersions,
                    active.timesDetected as timesDetected,
                    active.timesUseful as timesUseful,
                    active.timesNotUseful as timesNotUseful,
                    active.usefulnessScore as usefulnessScore,
                    active.confidence as confidence,
                    active.lastDetectedAt as lastDetectedAt,
                    active.isDeprecated as isDeprecated,
                    active.deprecationReason as deprecationReason",
                new { name });

            if (await cursor.FetchAsync())
            {
                return new PatternMetrics
                {
                    PatternName = name,
                    ActiveVersion = cursor.Current["activeVersion"].As<int>(),
                    TotalVersions = cursor.Current["totalVersions"].As<int>(),
                    TimesDetected = cursor.Current["timesDetected"].As<int>(),
                    TimesUseful = cursor.Current["timesUseful"].As<int>(),
                    TimesNotUseful = cursor.Current["timesNotUseful"].As<int>(),
                    UsefulnessScore = (float)(cursor.Current["usefulnessScore"].As<double?>() ?? 0.5),
                    Confidence = (float)(cursor.Current["confidence"].As<double?>() ?? 0.5),
                    IsDeprecated = cursor.Current["isDeprecated"].As<bool?>() ?? false,
                    DeprecationReason = cursor.Current["deprecationReason"].As<string?>()
                };
            }

            return new PatternMetrics { PatternName = name };
        });
    }

    public async Task<List<EvolvingPattern>> GetMostUsefulPatternsAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            // First try patterns with detections (proven useful)
            var cursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true AND p.isDeprecated = false AND p.timesDetected > 0
                RETURN p
                ORDER BY p.usefulnessScore DESC, p.timesDetected DESC
                LIMIT $limit",
                new { limit });

            var patterns = new List<EvolvingPattern>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            // If no detected patterns, return top patterns by usefulness score (for new projects)
            if (!patterns.Any())
            {
                cursor = await tx.RunAsync(@"
                    MATCH (p:EvolvingPattern)
                    WHERE p.isActive = true AND p.isDeprecated = false
                    RETURN p
                    ORDER BY p.usefulnessScore DESC, p.name
                    LIMIT $limit",
                    new { limit });

                while (await cursor.FetchAsync())
                {
                    var node = cursor.Current["p"].As<INode>();
                    patterns.Add(MapPatternFromNode(node));
                }
            }

            return patterns;
        });
    }

    public async Task<List<EvolvingPattern>> GetPatternsNeedingImprovementAsync(float threshold = 0.5f, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true AND p.isDeprecated = false 
                  AND p.timesDetected > 10 AND p.usefulnessScore < $threshold
                RETURN p
                ORDER BY p.usefulnessScore ASC
                LIMIT 50",
                new { threshold });

            var patterns = new List<EvolvingPattern>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    public async Task<List<EvolvingPattern>> GetRecentlyEvolvedAsync(int days = 30, int limit = 20, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WHERE p.version > 1 AND p.createdAt > datetime() - duration({days: $days})
                RETURN p
                ORDER BY p.createdAt DESC
                LIMIT $limit",
                new { days, limit });

            var patterns = new List<EvolvingPattern>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    /// <summary>
    /// Get patterns by type or category - useful for code generation prompts
    /// Returns patterns even if not yet detected (for new projects)
    /// </summary>
    public async Task<List<EvolvingPattern>> GetPatternsByTypeAsync(string type, int limit = 10, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true AND p.isDeprecated = false
                  AND (toLower(p.type) = toLower($type) OR toLower(p.category) CONTAINS toLower($type))
                RETURN p
                ORDER BY p.usefulnessScore DESC, p.timesDetected DESC
                LIMIT $limit",
                new { type, limit });

            var patterns = new List<EvolvingPattern>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    /// <summary>
    /// Search patterns by keyword - matches name, description, or recommendation
    /// </summary>
    public async Task<List<EvolvingPattern>> SearchPatternsAsync(string keyword, int limit = 10, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(@"
                MATCH (p:EvolvingPattern)
                WHERE p.isActive = true AND p.isDeprecated = false
                  AND (toLower(p.name) CONTAINS toLower($keyword) 
                       OR toLower(p.description) CONTAINS toLower($keyword)
                       OR toLower(p.recommendation) CONTAINS toLower($keyword))
                RETURN p
                ORDER BY p.usefulnessScore DESC, p.timesDetected DESC
                LIMIT $limit",
                new { keyword, limit });

            var patterns = new List<EvolvingPattern>();
            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                patterns.Add(MapPatternFromNode(node));
            }

            return patterns;
        });
    }

    #endregion

    #region Initialization

    public async Task InitializeFromStaticCatalogAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📋 Initializing evolving pattern catalog from static BestPracticesCatalog...");

        var count = 0;
        foreach (var (name, (type, category, recommendation, url)) in BestPracticesCatalog.Practices)
        {
            try
            {
                var existing = await GetPatternAsync(name, cancellationToken);
                if (existing == null)
                {
                    await CreatePatternAsync(name, type, category, recommendation, url, cancellationToken: cancellationToken);
                    count++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to migrate pattern '{Name}'", name);
            }
        }

        _logger.LogInformation("✅ Migrated {Count} patterns from static catalog", count);
    }

    public async Task<bool> IsInitializedAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync("MATCH (p:EvolvingPattern) RETURN count(p) as count");
            if (await cursor.FetchAsync())
            {
                return cursor.Current["count"].As<int>() > 0;
            }
            return false;
        });
    }

    #endregion

    #region Helpers

    private EvolvingPattern MapPatternFromNode(INode node)
    {
        var props = node.Properties;
        
        var pattern = new EvolvingPattern
        {
            Id = props["id"].As<string>(),
            Name = props["name"].As<string>(),
            Version = props["version"].As<int>(),
            Recommendation = props.ContainsKey("recommendation") ? props["recommendation"].As<string>() : "",
            ReferenceUrl = props.ContainsKey("referenceUrl") ? props["referenceUrl"].As<string>() : "",
            IsActive = props.ContainsKey("isActive") && props["isActive"].As<bool>(),
            IsDeprecated = props.ContainsKey("isDeprecated") && props["isDeprecated"].As<bool>(),
            DeprecationReason = props.ContainsKey("deprecationReason") ? props["deprecationReason"].As<string>() : null,
            SupersededBy = props.ContainsKey("supersededBy") ? props["supersededBy"].As<string>() : null,
            ParentVersionId = props.ContainsKey("parentVersionId") ? props["parentVersionId"].As<string>() : null,
            EvolutionReason = props.ContainsKey("evolutionReason") ? props["evolutionReason"].As<string>() : null,
            CreatedBy = props.ContainsKey("createdBy") ? props["createdBy"].As<string>() : "system",
            TimesDetected = props.ContainsKey("timesDetected") ? props["timesDetected"].As<int>() : 0,
            TimesUseful = props.ContainsKey("timesUseful") ? props["timesUseful"].As<int>() : 0,
            TimesNotUseful = props.ContainsKey("timesNotUseful") ? props["timesNotUseful"].As<int>() : 0,
            Confidence = props.ContainsKey("confidence") ? (float)props["confidence"].As<double>() : 0.5f
        };

        // Parse type
        if (props.ContainsKey("type") && Enum.TryParse<PatternType>(props["type"].As<string>(), out var type))
        {
            pattern.Type = type;
        }

        // Parse category
        if (props.ContainsKey("category") && Enum.TryParse<PatternCategory>(props["category"].As<string>(), out var cat))
        {
            pattern.Category = cat;
        }

        // Parse JSON fields
        if (props.ContainsKey("detectionRulesJson"))
        {
            try
            {
                pattern.DetectionRules = System.Text.Json.JsonSerializer.Deserialize<List<PatternDetectionRule>>(
                    props["detectionRulesJson"].As<string>()) ?? new();
            }
            catch { }
        }

        if (props.ContainsKey("examplesJson"))
        {
            try
            {
                pattern.Examples = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    props["examplesJson"].As<string>()) ?? new();
            }
            catch { }
        }

        return pattern;
    }

    private string GeneratePatternName(string description)
    {
        // Simple name generation from description
        var words = description.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(3)
            .ToArray();
        return string.Join("-", words);
    }

    private PatternType InferPatternType(string code, string description)
    {
        var combined = (code + " " + description).ToLower();

        if (combined.Contains("cache") || combined.Contains("redis"))
            return PatternType.Caching;
        if (combined.Contains("retry") || combined.Contains("polly") || combined.Contains("resilient"))
            return PatternType.Resilience;
        if (combined.Contains("valid") || combined.Contains("fluent"))
            return PatternType.Validation;
        if (combined.Contains("auth") || combined.Contains("jwt") || combined.Contains("security"))
            return PatternType.Security;
        if (combined.Contains("agent") || combined.Contains("llm") || combined.Contains("chat"))
            return PatternType.AgentFramework;

        return PatternType.Unknown;
    }

    private PatternCategory InferPatternCategory(string code, string description)
    {
        var combined = (code + " " + description).ToLower();

        if (combined.Contains("performance") || combined.Contains("fast") || combined.Contains("cache"))
            return PatternCategory.Performance;
        if (combined.Contains("security") || combined.Contains("auth") || combined.Contains("encrypt"))
            return PatternCategory.Security;
        if (combined.Contains("reliable") || combined.Contains("retry") || combined.Contains("fault"))
            return PatternCategory.Reliability;
        if (combined.Contains("agent") || combined.Contains("ai"))
            return PatternCategory.AIAgents;

        return PatternCategory.General;
    }

    private List<PatternDetectionRule> GenerateDetectionRules(string codeExample)
    {
        var rules = new List<PatternDetectionRule>();

        // Extract potential keywords
        var keywords = new[] { "using", "namespace", "class", "interface", "async", "await" };
        foreach (var keyword in keywords)
        {
            if (codeExample.Contains(keyword))
            {
                // Look for what follows the keyword
                var index = codeExample.IndexOf(keyword);
                var afterKeyword = codeExample.Substring(index, Math.Min(50, codeExample.Length - index));
                var match = System.Text.RegularExpressions.Regex.Match(afterKeyword, @"(\w+)\s+(\w+)");
                if (match.Success)
                {
                    rules.Add(new PatternDetectionRule
                    {
                        Type = DetectionRuleType.Keyword,
                        Pattern = match.Groups[2].Value,
                        Description = $"Contains {match.Groups[2].Value}",
                        ConfidenceBoost = 0.1f
                    });
                }
            }
        }

        return rules.Take(5).ToList();
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Dispose Neo4j driver connection on shutdown.
    /// CRITICAL: Prevents database corruption when container stops.
    /// </summary>
    public void Dispose()
    {
        _logger.LogInformation("EvolvingPatternCatalogService: Disposing Neo4j driver connection...");
        _driver?.Dispose();
        _logger.LogInformation("EvolvingPatternCatalogService: Neo4j driver disposed successfully");
    }

    #endregion
}

