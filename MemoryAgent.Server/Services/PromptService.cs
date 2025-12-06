using MemoryAgent.Server.Models;
using Neo4j.Driver;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for managing versioned, evolving prompts with outcome tracking.
/// Stores prompts in Neo4j with full version history.
/// </summary>
public class PromptService : IPromptService
{
    private readonly IDriver _driver;
    private readonly ILogger<PromptService> _logger;
    private readonly Random _random = new();

    public PromptService(
        IConfiguration configuration,
        ILogger<PromptService> logger)
    {
        var neo4jUrl = configuration["Neo4j:Url"] ?? "bolt://localhost:7687";
        var neo4jUser = configuration["Neo4j:User"] ?? "neo4j";
        var neo4jPassword = configuration["Neo4j:Password"] ?? "memoryagent";

        _driver = GraphDatabase.Driver(neo4jUrl, AuthTokens.Basic(neo4jUser, neo4jPassword));
        _logger = logger;
    }

    #region Prompt Retrieval

    public async Task<PromptTemplate> GetPromptAsync(string name, bool allowTestVariant = true, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            // Get active prompt and any test variant
            var cypher = @"
                MATCH (p:PromptTemplate {name: $name})
                WHERE p.isActive = true OR p.isTestVariant = true
                RETURN p
                ORDER BY p.isActive DESC, p.version DESC";

            var cursor = await tx.RunAsync(cypher, new { name });
            var prompts = new List<PromptTemplate>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                prompts.Add(MapPromptFromNode(node));
            }

            if (!prompts.Any())
            {
                throw new InvalidOperationException($"Prompt '{name}' not found. Initialize default prompts first.");
            }

            var activePrompt = prompts.FirstOrDefault(p => p.IsActive);
            var testVariant = prompts.FirstOrDefault(p => p.IsTestVariant);

            // A/B testing logic
            if (allowTestVariant && testVariant != null && activePrompt != null)
            {
                var roll = _random.Next(100);
                if (roll < testVariant.TestTrafficPercent)
                {
                    _logger.LogDebug("A/B Test: Using test variant v{Version} for prompt '{Name}'", 
                        testVariant.Version, name);
                    return testVariant;
                }
            }

            return activePrompt ?? prompts.First();
        });
    }

    public async Task<PromptTemplate?> GetPromptVersionAsync(string name, int version, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:PromptTemplate {name: $name, version: $version})
                RETURN p";

            var cursor = await tx.RunAsync(cypher, new { name, version });

            if (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                return MapPromptFromNode(node);
            }

            return null;
        });
    }

    public async Task<List<PromptTemplate>> GetPromptHistoryAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:PromptTemplate {name: $name})
                RETURN p
                ORDER BY p.version DESC";

            var cursor = await tx.RunAsync(cypher, new { name });
            var prompts = new List<PromptTemplate>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                prompts.Add(MapPromptFromNode(node));
            }

            return prompts;
        });
    }

    public async Task<List<PromptTemplate>> ListPromptsAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = activeOnly
                ? "MATCH (p:PromptTemplate) WHERE p.isActive = true RETURN p ORDER BY p.name"
                : "MATCH (p:PromptTemplate) RETURN p ORDER BY p.name, p.version DESC";

            var cursor = await tx.RunAsync(cypher);
            var prompts = new List<PromptTemplate>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["p"].As<INode>();
                prompts.Add(MapPromptFromNode(node));
            }

            return prompts;
        });
    }

    public async Task<string> RenderPromptAsync(string name, Dictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        var prompt = await GetPromptAsync(name, allowTestVariant: true, cancellationToken);
        return RenderPrompt(prompt.Content, variables);
    }

    private string RenderPrompt(string template, Dictionary<string, string> variables)
    {
        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }

    #endregion

    #region Prompt Management

    public async Task<PromptTemplate> CreatePromptAsync(
        string name,
        string content,
        string description,
        List<PromptVariable>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = new PromptTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Version = 1,
            Content = content,
            Description = description,
            Variables = variables ?? ExtractVariables(content),
            IsActive = true,
            CreatedBy = "system"
        };

        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cypher = @"
                CREATE (p:PromptTemplate {
                    id: $id,
                    name: $name,
                    version: $version,
                    content: $content,
                    description: $description,
                    variablesJson: $variablesJson,
                    isActive: $isActive,
                    isTestVariant: false,
                    testTrafficPercent: 0,
                    createdAt: datetime($createdAt),
                    createdBy: $createdBy,
                    timesUsed: 0,
                    successCount: 0,
                    failureCount: 0,
                    avgConfidence: 0.5,
                    totalConfidence: 0.0,
                    avgResponseTimeMs: 0.0,
                    totalResponseTimeMs: 0
                })";

            await tx.RunAsync(cypher, new
            {
                id = prompt.Id,
                name = prompt.Name,
                version = prompt.Version,
                content = prompt.Content,
                description = prompt.Description,
                variablesJson = System.Text.Json.JsonSerializer.Serialize(prompt.Variables),
                isActive = prompt.IsActive,
                createdAt = prompt.CreatedAt.ToString("O"),
                createdBy = prompt.CreatedBy
            });
        });

        _logger.LogInformation("📝 Created prompt '{Name}' v{Version}", name, prompt.Version);
        return prompt;
    }

    public async Task<PromptTemplate> CreateVersionAsync(
        string name,
        string content,
        string evolutionReason,
        bool activateImmediately = false,
        CancellationToken cancellationToken = default)
    {
        // Get current version
        var history = await GetPromptHistoryAsync(name, cancellationToken);
        var current = history.FirstOrDefault() 
            ?? throw new InvalidOperationException($"Prompt '{name}' not found");

        var newVersion = new PromptTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Version = current.Version + 1,
            Content = content,
            Description = current.Description,
            Variables = ExtractVariables(content),
            IsActive = activateImmediately,
            ParentVersionId = current.Id,
            EvolutionReason = evolutionReason,
            CreatedBy = "evolution"
        };

        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // If activating immediately, deactivate current
            if (activateImmediately)
            {
                await tx.RunAsync(
                    "MATCH (p:PromptTemplate {name: $name, isActive: true}) SET p.isActive = false",
                    new { name });
            }

            // Create new version
            var cypher = @"
                CREATE (p:PromptTemplate {
                    id: $id,
                    name: $name,
                    version: $version,
                    content: $content,
                    description: $description,
                    variablesJson: $variablesJson,
                    isActive: $isActive,
                    isTestVariant: false,
                    testTrafficPercent: 0,
                    parentVersionId: $parentVersionId,
                    evolutionReason: $evolutionReason,
                    createdAt: datetime($createdAt),
                    createdBy: $createdBy,
                    timesUsed: 0,
                    successCount: 0,
                    failureCount: 0,
                    avgConfidence: 0.5,
                    totalConfidence: 0.0,
                    avgResponseTimeMs: 0.0,
                    totalResponseTimeMs: 0
                })";

            await tx.RunAsync(cypher, new
            {
                id = newVersion.Id,
                name = newVersion.Name,
                version = newVersion.Version,
                content = newVersion.Content,
                description = newVersion.Description,
                variablesJson = System.Text.Json.JsonSerializer.Serialize(newVersion.Variables),
                isActive = newVersion.IsActive,
                parentVersionId = newVersion.ParentVersionId,
                evolutionReason = newVersion.EvolutionReason,
                createdAt = newVersion.CreatedAt.ToString("O"),
                createdBy = newVersion.CreatedBy
            });

            // Create evolution relationship
            await tx.RunAsync(@"
                MATCH (old:PromptTemplate {id: $parentId})
                MATCH (new:PromptTemplate {id: $newId})
                CREATE (old)-[:EVOLVED_TO {reason: $reason, at: datetime()}]->(new)",
                new { parentId = current.Id, newId = newVersion.Id, reason = evolutionReason });
        });

        _logger.LogInformation("📝 Created prompt '{Name}' v{Version} (evolved from v{OldVersion}): {Reason}", 
            name, newVersion.Version, current.Version, evolutionReason);
        return newVersion;
    }

    public async Task ActivateVersionAsync(string name, int version, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Deactivate all versions
            await tx.RunAsync(
                "MATCH (p:PromptTemplate {name: $name}) SET p.isActive = false, p.isTestVariant = false",
                new { name });

            // Activate specified version
            await tx.RunAsync(
                "MATCH (p:PromptTemplate {name: $name, version: $version}) SET p.isActive = true",
                new { name, version });
        });

        _logger.LogInformation("✅ Activated prompt '{Name}' v{Version}", name, version);
    }

    public async Task RollbackAsync(string name, CancellationToken cancellationToken = default)
    {
        var history = await GetPromptHistoryAsync(name, cancellationToken);
        var active = history.FirstOrDefault(p => p.IsActive);

        if (active == null || string.IsNullOrEmpty(active.ParentVersionId))
        {
            throw new InvalidOperationException($"Cannot rollback prompt '{name}' - no previous version");
        }

        var previousVersion = history.FirstOrDefault(p => p.Id == active.ParentVersionId)
            ?? throw new InvalidOperationException($"Previous version not found for prompt '{name}'");

        await ActivateVersionAsync(name, previousVersion.Version, cancellationToken);
        _logger.LogWarning("⚠️ Rolled back prompt '{Name}' from v{Current} to v{Previous}", 
            name, active.Version, previousVersion.Version);
    }

    public async Task StartABTestAsync(string name, int testVersion, int trafficPercent = 10, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Clear any existing test variants
            await tx.RunAsync(
                "MATCH (p:PromptTemplate {name: $name}) SET p.isTestVariant = false, p.testTrafficPercent = 0",
                new { name });

            // Set new test variant
            await tx.RunAsync(@"
                MATCH (p:PromptTemplate {name: $name, version: $version}) 
                SET p.isTestVariant = true, p.testTrafficPercent = $trafficPercent, p.abTestStartedAt = datetime()",
                new { name, version = testVersion, trafficPercent });
        });

        _logger.LogInformation("🧪 Started A/B test for prompt '{Name}': v{Version} at {Percent}% traffic", 
            name, testVersion, trafficPercent);
    }

    public async Task StopABTestAsync(string name, bool promoteTestVersion = false, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        if (promoteTestVersion)
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                // Get test version
                var cursor = await tx.RunAsync(
                    "MATCH (p:PromptTemplate {name: $name, isTestVariant: true}) RETURN p.version as version",
                    new { name });

                if (await cursor.FetchAsync())
                {
                    var testVersion = cursor.Current["version"].As<int>();

                    // Deactivate current active, activate test version
                    await tx.RunAsync(
                        "MATCH (p:PromptTemplate {name: $name}) SET p.isActive = false, p.isTestVariant = false, p.testTrafficPercent = 0",
                        new { name });

                    await tx.RunAsync(
                        "MATCH (p:PromptTemplate {name: $name, version: $version}) SET p.isActive = true",
                        new { name, version = testVersion });

                    _logger.LogInformation("🏆 A/B test winner: Promoted prompt '{Name}' v{Version} to active", name, testVersion);
                }
            });
        }
        else
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    "MATCH (p:PromptTemplate {name: $name}) SET p.isTestVariant = false, p.testTrafficPercent = 0",
                    new { name });
            });

            _logger.LogInformation("🛑 Stopped A/B test for prompt '{Name}' (test version not promoted)", name);
        }
    }

    #endregion

    #region Execution Tracking

    public async Task<PromptExecution> RecordExecutionAsync(
        string promptId,
        string renderedPrompt,
        Dictionary<string, string> inputVariables,
        string response,
        long responseTimeMs,
        float? confidence = null,
        bool parseSuccess = true,
        string? parseError = null,
        string? sessionId = null,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        var execution = new PromptExecution
        {
            Id = Guid.NewGuid().ToString(),
            PromptId = promptId,
            RenderedPrompt = renderedPrompt,
            InputVariables = inputVariables,
            Response = response,
            ResponseTimeMs = responseTimeMs,
            Confidence = confidence,
            ParseSuccess = parseSuccess,
            ParseError = parseError,
            SessionId = sessionId,
            Context = context?.ToLowerInvariant()
        };

        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Get prompt name and version for the execution record
            var promptCursor = await tx.RunAsync(
                "MATCH (p:PromptTemplate {id: $promptId}) RETURN p.name as name, p.version as version",
                new { promptId });

            if (await promptCursor.FetchAsync())
            {
                execution.PromptName = promptCursor.Current["name"].As<string>();
                execution.PromptVersion = promptCursor.Current["version"].As<int>();
            }

            // Create execution record
            var cypher = @"
                CREATE (e:PromptExecution {
                    id: $id,
                    promptId: $promptId,
                    promptName: $promptName,
                    promptVersion: $promptVersion,
                    renderedPrompt: $renderedPrompt,
                    inputVariablesJson: $inputVariablesJson,
                    response: $response,
                    responseTimeMs: $responseTimeMs,
                    confidence: $confidence,
                    parseSuccess: $parseSuccess,
                    parseError: $parseError,
                    sessionId: $sessionId,
                    context: $context,
                    executedAt: datetime($executedAt),
                    outcomeRecorded: false
                })";

            await tx.RunAsync(cypher, new
            {
                id = execution.Id,
                promptId = execution.PromptId,
                promptName = execution.PromptName,
                promptVersion = execution.PromptVersion,
                renderedPrompt = execution.RenderedPrompt.Length > 10000 
                    ? execution.RenderedPrompt.Substring(0, 10000) + "..." 
                    : execution.RenderedPrompt,
                inputVariablesJson = System.Text.Json.JsonSerializer.Serialize(inputVariables),
                response = response.Length > 10000 ? response.Substring(0, 10000) + "..." : response,
                responseTimeMs = execution.ResponseTimeMs,
                confidence = confidence ?? 0.5f,
                parseSuccess = execution.ParseSuccess,
                parseError = execution.ParseError ?? "",
                sessionId = execution.SessionId ?? "",
                context = execution.Context ?? "",
                executedAt = execution.ExecutedAt.ToString("O")
            });

            // Link execution to prompt
            await tx.RunAsync(@"
                MATCH (p:PromptTemplate {id: $promptId})
                MATCH (e:PromptExecution {id: $executionId})
                CREATE (p)-[:HAS_EXECUTION]->(e)",
                new { promptId, executionId = execution.Id });

            // Update prompt metrics
            await tx.RunAsync(@"
                MATCH (p:PromptTemplate {id: $promptId})
                SET p.timesUsed = p.timesUsed + 1,
                    p.lastUsedAt = datetime(),
                    p.totalResponseTimeMs = p.totalResponseTimeMs + $responseTimeMs,
                    p.avgResponseTimeMs = toFloat(p.totalResponseTimeMs + $responseTimeMs) / (p.timesUsed + 1),
                    p.totalConfidence = p.totalConfidence + $confidence,
                    p.avgConfidence = (p.totalConfidence + $confidence) / (p.timesUsed + 1)",
                new { promptId, responseTimeMs, confidence = confidence ?? 0.5f });
        });

        _logger.LogDebug("📊 Recorded execution {ExecutionId} for prompt '{Name}' v{Version}", 
            execution.Id, execution.PromptName, execution.PromptVersion);

        return execution;
    }

    public async Task RecordOutcomeAsync(
        string executionId,
        bool wasSuccessful,
        int? userRating = null,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Update execution with outcome
            await tx.RunAsync(@"
                MATCH (e:PromptExecution {id: $executionId})
                SET e.outcomeRecorded = true,
                    e.wasSuccessful = $wasSuccessful,
                    e.userRating = $userRating,
                    e.feedbackComments = $comments,
                    e.outcomeRecordedAt = datetime()",
                new { executionId, wasSuccessful, userRating = userRating ?? 0, comments = comments ?? "" });

            // Get prompt ID and update metrics
            var cursor = await tx.RunAsync(
                "MATCH (e:PromptExecution {id: $executionId}) RETURN e.promptId as promptId",
                new { executionId });

            if (await cursor.FetchAsync())
            {
                var promptId = cursor.Current["promptId"].As<string>();
                var updateField = wasSuccessful ? "successCount" : "failureCount";

                await tx.RunAsync($@"
                    MATCH (p:PromptTemplate {{id: $promptId}})
                    SET p.{updateField} = p.{updateField} + 1",
                    new { promptId });
            }
        });

        _logger.LogInformation("📊 Recorded outcome for execution {ExecutionId}: {Outcome}", 
            executionId, wasSuccessful ? "SUCCESS" : "FAILURE");
    }

    public async Task RecordImplicitSuccessAsync(string executionId, string signal, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            // Get current signals
            var cursor = await tx.RunAsync(
                "MATCH (e:PromptExecution {id: $executionId}) RETURN e.implicitSuccessSignals as signals",
                new { executionId });

            var currentSignals = new List<string>();
            if (await cursor.FetchAsync())
            {
                var signalsJson = cursor.Current["signals"].As<string>();
                if (!string.IsNullOrEmpty(signalsJson))
                {
                    currentSignals = System.Text.Json.JsonSerializer.Deserialize<List<string>>(signalsJson) ?? new();
                }
            }

            currentSignals.Add(signal);

            await tx.RunAsync(@"
                MATCH (e:PromptExecution {id: $executionId})
                SET e.implicitSuccessSignals = $signals",
                new { executionId, signals = System.Text.Json.JsonSerializer.Serialize(currentSignals) });
        });
    }

    public async Task RecordImplicitFailureAsync(string executionId, string signal, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        await session.ExecuteWriteAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                "MATCH (e:PromptExecution {id: $executionId}) RETURN e.implicitFailureSignals as signals",
                new { executionId });

            var currentSignals = new List<string>();
            if (await cursor.FetchAsync())
            {
                var signalsJson = cursor.Current["signals"].As<string>();
                if (!string.IsNullOrEmpty(signalsJson))
                {
                    currentSignals = System.Text.Json.JsonSerializer.Deserialize<List<string>>(signalsJson) ?? new();
                }
            }

            currentSignals.Add(signal);

            await tx.RunAsync(@"
                MATCH (e:PromptExecution {id: $executionId})
                SET e.implicitFailureSignals = $signals",
                new { executionId, signals = System.Text.Json.JsonSerializer.Serialize(currentSignals) });
        });
    }

    public async Task<List<PromptExecution>> GetRecentExecutionsAsync(
        string promptName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (e:PromptExecution {promptName: $promptName})
                RETURN e
                ORDER BY e.executedAt DESC
                LIMIT $limit";

            var cursor = await tx.RunAsync(cypher, new { promptName, limit });
            var executions = new List<PromptExecution>();

            while (await cursor.FetchAsync())
            {
                var node = cursor.Current["e"].As<INode>();
                executions.Add(MapExecutionFromNode(node));
            }

            return executions;
        });
    }

    #endregion

    #region Analytics & Learning

    public async Task<PromptMetrics> GetPromptMetricsAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (p:PromptTemplate {name: $name})
                WITH p ORDER BY p.version DESC
                WITH collect(p) as versions
                WITH versions, versions[0] as latest, 
                     [v IN versions WHERE v.isActive][0] as active
                RETURN 
                    $name as name,
                    active.version as activeVersion,
                    size(versions) as totalVersions,
                    active.timesUsed as totalExecutions,
                    CASE WHEN active.timesUsed > 0 
                         THEN toFloat(active.successCount) / active.timesUsed 
                         ELSE 0.5 END as successRate,
                    active.avgConfidence as avgConfidence,
                    active.avgResponseTimeMs as avgResponseTimeMs,
                    active.isTestVariant as isABTesting,
                    active.lastUsedAt as lastUsed";

            var cursor = await tx.RunAsync(cypher, new { name });

            if (await cursor.FetchAsync())
            {
                return new PromptMetrics
                {
                    PromptName = name,
                    ActiveVersion = cursor.Current["activeVersion"].As<int>(),
                    TotalVersions = cursor.Current["totalVersions"].As<int>(),
                    TotalExecutions = cursor.Current["totalExecutions"].As<int>(),
                    SuccessRate = (float)cursor.Current["successRate"].As<double>(),
                    AvgConfidence = (float)cursor.Current["avgConfidence"].As<double>(),
                    AvgResponseTimeMs = cursor.Current["avgResponseTimeMs"].As<double>(),
                    IsABTesting = cursor.Current["isABTesting"].As<bool>()
                };
            }

            return new PromptMetrics { PromptName = name };
        });
    }

    public async Task<ABTestResult> GetABTestResultsAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var session = _driver.AsyncSession();

        return await session.ExecuteReadAsync(async tx =>
        {
            var cypher = @"
                MATCH (control:PromptTemplate {name: $name, isActive: true})
                MATCH (test:PromptTemplate {name: $name, isTestVariant: true})
                RETURN 
                    control.version as controlVersion,
                    test.version as testVersion,
                    control.timesUsed as controlExecutions,
                    test.timesUsed as testExecutions,
                    CASE WHEN control.timesUsed > 0 
                         THEN toFloat(control.successCount) / control.timesUsed 
                         ELSE 0.5 END as controlSuccessRate,
                    CASE WHEN test.timesUsed > 0 
                         THEN toFloat(test.successCount) / test.timesUsed 
                         ELSE 0.5 END as testSuccessRate,
                    control.avgConfidence as controlAvgConfidence,
                    test.avgConfidence as testAvgConfidence,
                    control.avgResponseTimeMs as controlAvgResponseMs,
                    test.avgResponseTimeMs as testAvgResponseMs,
                    test.abTestStartedAt as testStartedAt";

            var cursor = await tx.RunAsync(cypher, new { name });

            if (await cursor.FetchAsync())
            {
                var controlSuccessRate = (float)cursor.Current["controlSuccessRate"].As<double>();
                var testSuccessRate = (float)cursor.Current["testSuccessRate"].As<double>();
                var controlExecs = cursor.Current["controlExecutions"].As<int>();
                var testExecs = cursor.Current["testExecutions"].As<int>();

                var result = new ABTestResult
                {
                    PromptName = name,
                    ControlVersion = cursor.Current["controlVersion"].As<int>(),
                    TestVersion = cursor.Current["testVersion"].As<int>(),
                    ControlExecutions = controlExecs,
                    TestExecutions = testExecs,
                    ControlSuccessRate = controlSuccessRate,
                    TestSuccessRate = testSuccessRate,
                    ControlAvgConfidence = (float)cursor.Current["controlAvgConfidence"].As<double>(),
                    TestAvgConfidence = (float)cursor.Current["testAvgConfidence"].As<double>(),
                    ControlAvgResponseMs = cursor.Current["controlAvgResponseMs"].As<double>(),
                    TestAvgResponseMs = cursor.Current["testAvgResponseMs"].As<double>(),
                    SuccessRateDifference = testSuccessRate - controlSuccessRate
                };

                // Simple significance check (need at least 30 samples each)
                result.IsStatisticallySignificant = controlExecs >= 30 && testExecs >= 30;

                // Recommendation
                if (testExecs < 30)
                {
                    result.Recommendation = "continue";
                }
                else if (testSuccessRate > controlSuccessRate + 0.05f && result.IsStatisticallySignificant)
                {
                    result.Recommendation = "promote";
                }
                else if (testSuccessRate < controlSuccessRate - 0.05f && result.IsStatisticallySignificant)
                {
                    result.Recommendation = "reject";
                }
                else
                {
                    result.Recommendation = "continue";
                }

                return result;
            }

            return new ABTestResult { PromptName = name, Recommendation = "no_test_running" };
        });
    }

    public async Task<List<PromptImprovement>> SuggestImprovementsAsync(string name, CancellationToken cancellationToken = default)
    {
        var improvements = new List<PromptImprovement>();
        var metrics = await GetPromptMetricsAsync(name, cancellationToken);
        var executions = await GetRecentExecutionsAsync(name, 100, cancellationToken);

        // Low success rate
        if (metrics.SuccessRate < 0.7f && metrics.TotalExecutions > 10)
        {
            improvements.Add(new PromptImprovement
            {
                Type = "clarity",
                Description = "Low success rate indicates prompt may be unclear",
                SuggestedChange = "Add more specific examples and clearer output format instructions",
                ExpectedImpact = 0.15f,
                Evidence = $"Current success rate: {metrics.SuccessRate:P0}"
            });
        }

        // Low confidence
        if (metrics.AvgConfidence < 0.6f && metrics.TotalExecutions > 10)
        {
            improvements.Add(new PromptImprovement
            {
                Type = "constraints",
                Description = "Low average confidence in responses",
                SuggestedChange = "Add stricter output constraints and validation rules",
                ExpectedImpact = 0.1f,
                Evidence = $"Average confidence: {metrics.AvgConfidence:P0}"
            });
        }

        // Parse failures
        var parseFailures = executions.Count(e => !e.ParseSuccess);
        if (parseFailures > executions.Count * 0.1)
        {
            improvements.Add(new PromptImprovement
            {
                Type = "structure",
                Description = "High parse failure rate",
                SuggestedChange = "Enforce stricter JSON output format with examples",
                ExpectedImpact = 0.2f,
                Evidence = $"Parse failures: {parseFailures}/{executions.Count}"
            });
        }

        return improvements;
    }

    public async Task<PromptTemplate?> AutoEvolveAsync(string name, CancellationToken cancellationToken = default)
    {
        var metrics = await GetPromptMetricsAsync(name, cancellationToken);
        var improvements = await SuggestImprovementsAsync(name, cancellationToken);

        if (!improvements.Any() || metrics.SuccessRate >= 0.9f)
        {
            _logger.LogInformation("Prompt '{Name}' is performing well, no evolution needed", name);
            return null;
        }

        // This would ideally use LLM to generate improved prompt
        // For now, log that evolution is needed
        _logger.LogInformation("Prompt '{Name}' could benefit from evolution: {Improvements}", 
            name, string.Join(", ", improvements.Select(i => i.Type)));

        return null;
    }

    #endregion

    #region Initialization

    public async Task InitializeDefaultPromptsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📝 Initializing default prompts...");

        // Intent Classification Prompt
        await CreatePromptIfNotExistsAsync("intent_classification", 
            GetIntentClassificationPrompt(), 
            "Classifies user intent from natural language requests",
            new List<PromptVariable>
            {
                new() { Name = "userRequest", Description = "The user's request to classify", IsRequired = true },
                new() { Name = "context", Description = "Optional project context", IsRequired = false, DefaultValue = "" }
            },
            cancellationToken);

        // Page Transformation Prompt
        await CreatePromptIfNotExistsAsync("page_transformation",
            GetPageTransformationPrompt(),
            "Transforms Blazor/Razor pages to modern architecture",
            new List<PromptVariable>
            {
                new() { Name = "fileName", Description = "Source file name/path", IsRequired = true },
                new() { Name = "sourceCode", Description = "The source code to transform", IsRequired = true },
                new() { Name = "issues", Description = "Detected issues", IsRequired = true },
                new() { Name = "goals", Description = "Transformation goals checklist", IsRequired = true }
            },
            cancellationToken);

        // CSS Transformation Prompt
        await CreatePromptIfNotExistsAsync("css_transformation",
            GetCSSTransformationPrompt(),
            "Modernizes CSS with variables, grid, and accessibility",
            new List<PromptVariable>
            {
                new() { Name = "inlineStyleCount", Description = "Number of inline styles", IsRequired = true },
                new() { Name = "inlineStyles", Description = "Inline styles description", IsRequired = true },
                new() { Name = "issues", Description = "CSS issues detected", IsRequired = true },
                new() { Name = "recommendations", Description = "CSS recommendations", IsRequired = true },
                new() { Name = "qualityScore", Description = "Current quality score", IsRequired = true },
                new() { Name = "goals", Description = "Transformation goals", IsRequired = true }
            },
            cancellationToken);

        // Component Extraction Prompt
        await CreatePromptIfNotExistsAsync("component_extraction",
            GetComponentExtractionPrompt(),
            "Extracts reusable components from repeated UI patterns",
            new List<PromptVariable>
            {
                new() { Name = "componentName", Description = "Proposed component name", IsRequired = true },
                new() { Name = "description", Description = "Component description", IsRequired = true },
                new() { Name = "occurrences", Description = "Number of occurrences", IsRequired = true },
                new() { Name = "exampleCode", Description = "Example code from first occurrence", IsRequired = true },
                new() { Name = "parameters", Description = "Proposed parameters list", IsRequired = true },
                new() { Name = "events", Description = "Proposed events list", IsRequired = true },
                new() { Name = "filePath", Description = "Source file path", IsRequired = true },
                new() { Name = "lineStart", Description = "Start line number", IsRequired = true },
                new() { Name = "lineEnd", Description = "End line number", IsRequired = true }
            },
            cancellationToken);

        _logger.LogInformation("✅ Default prompts initialized");
    }

    private async Task CreatePromptIfNotExistsAsync(
        string name,
        string content,
        string description,
        List<PromptVariable> variables,
        CancellationToken cancellationToken)
    {
        try
        {
            await GetPromptAsync(name, allowTestVariant: false, cancellationToken);
            _logger.LogDebug("Prompt '{Name}' already exists", name);
        }
        catch
        {
            await CreatePromptAsync(name, content, description, variables, cancellationToken);
            _logger.LogInformation("Created default prompt '{Name}'", name);
        }
    }

    #endregion

    #region Default Prompts

    private string GetIntentClassificationPrompt() => @"You are an expert software architect analyzing user intent.

USER REQUEST: ""{{userRequest}}""
{{context}}

Analyze this request and classify the user's intent. Return ONLY valid JSON, no markdown, no explanation.

JSON Schema:
{
  ""projectType"": ""MobileApp | WebAPI | AIAgent | WebApp | DesktopApp | BackendService | Library | DataPipeline | MicroService | Unknown"",
  ""primaryGoal"": ""Performance | Security | Refactoring | NewFeature | BugFix | Migration | Testing | Observability | Scalability | CostOptimization | Unknown"",
  ""technologies"": [""Flutter"", ""Dart"", ""CSharp"", ""Python"", ""React"", ""Blazor"", ""AI"", etc.],
  ""relevantCategories"": [""Performance"", ""Security"", ""AIAgents"", ""StateManagement"", etc.],
  ""domain"": ""ecommerce | healthcare | fintech | general | etc."",
  ""complexity"": ""Simple | Medium | Complex | Enterprise"",
  ""confidence"": 0.0-1.0,
  ""reasoning"": ""Brief explanation""
}

EXAMPLES:

Request: ""Build secure Flutter e-commerce app""
Response: {""projectType"":""MobileApp"",""primaryGoal"":""NewFeature"",""technologies"":[""Flutter"",""Dart""],""relevantCategories"":[""Security"",""StateManagement"",""UIUX""],""domain"":""ecommerce"",""complexity"":""Complex"",""confidence"":0.95}

Request: ""Add caching to UserService""
Response: {""projectType"":""BackendService"",""primaryGoal"":""Performance"",""technologies"":[""CSharp""],""relevantCategories"":[""Performance"",""Caching""],""domain"":""general"",""complexity"":""Simple"",""confidence"":0.90}

Request: ""Migrate from Semantic Kernel to Microsoft.Extensions.AI""
Response: {""projectType"":""AIAgent"",""primaryGoal"":""Migration"",""technologies"":[""CSharp"",""AI"",""Microsoft.Extensions.AI""],""relevantCategories"":[""AIAgents"",""ToolIntegration""],""domain"":""general"",""complexity"":""Medium"",""confidence"":0.95}

Now classify this request:

USER REQUEST: ""{{userRequest}}""

Return ONLY the JSON object:";

    private string GetPageTransformationPrompt() => @"You are a Blazor refactoring expert. Transform this component to modern best practices.

SOURCE FILE: {{fileName}}
═══════════════════════════════════════════════════════

{{sourceCode}}

ANALYSIS:
═════════
Issues Detected:
{{issues}}

TRANSFORMATION GOALS:
═════════════════════
{{goals}}

REQUIREMENTS:
═════════════
1. Preserve ALL existing functionality
2. Use modern Blazor patterns (.NET 8+)
3. Extract components for repeated patterns (ProductCard, FormField, etc.)
4. Move ALL inline styles to component-scoped CSS
5. Use CSS variables for colors, spacing, fonts
6. Add error boundaries and loading states
7. Keep each component under 150 lines
8. Use descriptive, meaningful names
9. Add XML documentation comments
10. Add render modes where appropriate

OUTPUT INSTRUCTIONS:
═══════════════════
Return ONLY valid JSON. Use double quotes for all strings and property names. NO markdown, NO code blocks, NO explanatory text.

Example JSON structure:
{
  ""main_component"": {
    ""file_path"": ""Pages/YourPage.razor"",
    ""code"": ""...full razor component code...""
  },
  ""extracted_components"": [
    {
      ""name"": ""ComponentName"",
      ""file_path"": ""Components/ComponentName.razor"",
      ""code"": ""...full component code..."",
      ""css"": ""...component-scoped CSS...""
    }
  ],
  ""css_variables"": {
    ""--color-primary"": ""#007bff"",
    ""--color-success"": ""#28a745"",
    ""--spacing-sm"": ""0.5rem"",
    ""--spacing-md"": ""1rem""
  },
  ""site_css"": ""...main site CSS with variables..."",
  ""improvements"": [
    ""Extracted ProductCard component (12 occurrences → 1 reusable component)"",
    ""Moved 47 inline styles to external CSS"",
    ""Added error boundary wrapper"",
    ""Added loading state management""
  ],
  ""confidence"": 0.95,
  ""reasoning"": ""Transformation follows Blazor best practices...""
}";

    private string GetCSSTransformationPrompt() => @"You are a CSS modernization expert. Transform this CSS to modern best practices.

INLINE STYLES DETECTED ({{inlineStyleCount}}):
═══════════════════════════════════════
{{inlineStyles}}

CSS ANALYSIS:
═════════════
Issues:
{{issues}}

Recommendations:
{{recommendations}}

Quality Score: {{qualityScore}}/100

TRANSFORMATION GOALS:
═════════════════════
{{goals}}

REQUIREMENTS:
═════════════
1. Create CSS variables in :root for:
   - Colors (primary, secondary, success, danger, etc.)
   - Spacing (xs, sm, md, lg, xl)
   - Fonts (sizes, families, weights)
   - Borders (radius, widths)
   - Shadows

2. Modern layout patterns:
   - CSS Grid for page layouts
   - Flexbox for component layouts
   - NO floats or tables for layout

3. Responsive breakpoints:
   - Mobile: 320px - 767px
   - Tablet: 768px - 1023px
   - Desktop: 1024px+
   - Use mobile-first approach

4. Accessibility:
   - Focus styles (:focus, :focus-visible)
   - High contrast mode support
   - Reduced motion support
   - Color contrast WCAG AA minimum

Return VALID JSON:
{
  ""css"": ""...full modern CSS..."",
  ""variables"": {
    ""--color-primary"": ""#007bff"",
    ""--spacing-sm"": ""0.5rem""
  },
  ""uses_modern_layout"": true,
  ""is_responsive"": true,
  ""has_accessibility"": true,
  ""improvements"": [
    ""Extracted 47 inline styles to external CSS"",
    ""Added 15 CSS variables for consistent theming"",
    ""Converted float layout to CSS Grid"",
    ""Added mobile, tablet, desktop breakpoints""
  ]
}";

    private string GetComponentExtractionPrompt() => @"Generate a reusable Blazor component based on this pattern.

COMPONENT NAME: {{componentName}}
DESCRIPTION: {{description}}
OCCURRENCES: {{occurrences}}

EXAMPLE CODE:
═════════════
{{exampleCode}}

PROPOSED INTERFACE:
═══════════════════
Parameters:
{{parameters}}

Events:
{{events}}

Generate a complete, production-ready Blazor component.

Return JSON:
{
  ""component_code"": ""...full .razor code..."",
  ""component_css"": ""...component-scoped CSS..."",
  ""refactorings"": [
    {
      ""file_path"": ""{{filePath}}"",
      ""line_start"": {{lineStart}},
      ""line_end"": {{lineEnd}},
      ""old_code"": ""..."",
      ""new_code"": ""<{{componentName}} ... />""
    }
  ]
}";

    #endregion

    #region Helpers

    private List<PromptVariable> ExtractVariables(string content)
    {
        var variables = new List<PromptVariable>();
        var matches = Regex.Matches(content, @"\{\{(\w+)\}\}");

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            if (!variables.Any(v => v.Name == name))
            {
                variables.Add(new PromptVariable { Name = name, IsRequired = true });
            }
        }

        return variables;
    }

    private PromptTemplate MapPromptFromNode(INode node)
    {
        var props = node.Properties;
        return new PromptTemplate
        {
            Id = props["id"].As<string>(),
            Name = props["name"].As<string>(),
            Version = props["version"].As<int>(),
            Content = props["content"].As<string>(),
            Description = props.ContainsKey("description") ? props["description"].As<string>() : "",
            IsActive = props.ContainsKey("isActive") && props["isActive"].As<bool>(),
            IsTestVariant = props.ContainsKey("isTestVariant") && props["isTestVariant"].As<bool>(),
            TestTrafficPercent = props.ContainsKey("testTrafficPercent") ? props["testTrafficPercent"].As<int>() : 0,
            ParentVersionId = props.ContainsKey("parentVersionId") ? props["parentVersionId"].As<string>() : null,
            EvolutionReason = props.ContainsKey("evolutionReason") ? props["evolutionReason"].As<string>() : null,
            CreatedBy = props.ContainsKey("createdBy") ? props["createdBy"].As<string>() : "system",
            TimesUsed = props.ContainsKey("timesUsed") ? props["timesUsed"].As<int>() : 0,
            SuccessCount = props.ContainsKey("successCount") ? props["successCount"].As<int>() : 0,
            FailureCount = props.ContainsKey("failureCount") ? props["failureCount"].As<int>() : 0,
            AvgConfidence = props.ContainsKey("avgConfidence") ? (float)props["avgConfidence"].As<double>() : 0.5f,
            AvgResponseTimeMs = props.ContainsKey("avgResponseTimeMs") ? props["avgResponseTimeMs"].As<double>() : 0
        };
    }

    private PromptExecution MapExecutionFromNode(INode node)
    {
        var props = node.Properties;
        return new PromptExecution
        {
            Id = props["id"].As<string>(),
            PromptId = props["promptId"].As<string>(),
            PromptName = props.ContainsKey("promptName") ? props["promptName"].As<string>() : "",
            PromptVersion = props.ContainsKey("promptVersion") ? props["promptVersion"].As<int>() : 0,
            RenderedPrompt = props.ContainsKey("renderedPrompt") ? props["renderedPrompt"].As<string>() : "",
            Response = props.ContainsKey("response") ? props["response"].As<string>() : "",
            ResponseTimeMs = props.ContainsKey("responseTimeMs") ? props["responseTimeMs"].As<long>() : 0,
            Confidence = props.ContainsKey("confidence") ? (float)props["confidence"].As<double>() : null,
            ParseSuccess = !props.ContainsKey("parseSuccess") || props["parseSuccess"].As<bool>(),
            ParseError = props.ContainsKey("parseError") ? props["parseError"].As<string>() : null,
            SessionId = props.ContainsKey("sessionId") ? props["sessionId"].As<string>() : null,
            Context = props.ContainsKey("context") ? props["context"].As<string>() : null,
            OutcomeRecorded = props.ContainsKey("outcomeRecorded") && props["outcomeRecorded"].As<bool>(),
            WasSuccessful = props.ContainsKey("wasSuccessful") ? props["wasSuccessful"].As<bool?>() : null,
            UserRating = props.ContainsKey("userRating") ? props["userRating"].As<int?>() : null
        };
    }

    #endregion
}

