using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// COMPREHENSIVE C# pattern detector based on ALL Azure best practices documentation
/// Implements 60+ patterns from Microsoft Azure Architecture Center
/// </summary>
public class CSharpPatternDetectorEnhanced : IPatternDetector
{
    private const string AzureCachingUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching";
    private const string AzureRetryUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults";
    private const string AzureApiDesignUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design";
    private const string AzureApiImplUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-implementation";
    private const string AzureMonitoringUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring";
    private const string AzureAutoScaleUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/auto-scaling";
    private const string AzureBackgroundJobsUrl = "https://learn.microsoft.com/en-us/azure/architecture/best-practices/background-jobs";
    private const string AzurePubSubUrl = "https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber";

    public string GetLanguage() => "C#";

    public List<PatternType> GetSupportedPatternTypes() => Enum.GetValues<PatternType>().ToList();

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // CACHING PATTERNS (7 patterns)
            patterns.AddRange(DetectCacheAsidePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectWriteThroughPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectWriteBehindPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCacheExpirationPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCacheStampedePrevent(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRefreshAheadPattern(root, filePath, context, sourceCode));
            
            // API DESIGN PATTERNS (8 patterns)
            patterns.AddRange(DetectHttpVerbPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPaginationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectFilteringSortingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectVersioningPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHateoasPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectContentNegotiationPattern(root, filePath, context, sourceCode));
            
            // API IMPLEMENTATION PATTERNS (5 patterns)
            patterns.AddRange(DetectIdempotencyPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAsyncOperationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPartialResponsePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectBatchOperationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectETagPattern(root, filePath, context, sourceCode));
            
            // BACKGROUND JOBS PATTERNS (3 patterns)
            patterns.AddRange(DetectHostedServicePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMessageQueuePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHangfirePattern(root, filePath, context, sourceCode));
            
            // MONITORING PATTERNS (3 patterns)
            patterns.AddRange(DetectCorrelationIdPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectHealthCheckPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTelemetryPattern(root, filePath, context, sourceCode));
            
            // DATA PARTITIONING PATTERNS (2 patterns)
            patterns.AddRange(DetectShardingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectVerticalPartitionPattern(root, filePath, context, sourceCode));
            
            // SECURITY PATTERNS (3 patterns)
            patterns.AddRange(DetectAuthPatterns(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCorsPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectRateLimitingPattern(root, filePath, context, sourceCode));
            
            // CONFIGURATION PATTERNS (2 patterns)
            patterns.AddRange(DetectOptionsPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectNamedOptionsPattern(root, filePath, context, sourceCode));
            
            // PUBLISHER-SUBSCRIBER PATTERNS (6 patterns)
            patterns.AddRange(DetectServiceBusTopicPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectEventGridPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectEventHubsPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMassTransitPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectNServiceBusPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectGenericPubSubPattern(root, filePath, context, sourceCode));
            
            // ============================================
            // AZURE ARCHITECTURE PATTERNS (36 NEW PATTERNS)
            // ============================================
            
            // DATA MANAGEMENT PATTERNS (6 patterns)
            patterns.AddRange(DetectCQRSPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectEventSourcingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectIndexTablePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMaterializedViewPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectStaticContentHostingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectValetKeyPattern(root, filePath, context, sourceCode));
            
            // DESIGN & IMPLEMENTATION PATTERNS (8 patterns)
            patterns.AddRange(DetectAmbassadorPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAntiCorruptionLayerPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectBackendsForFrontendsPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectComputeResourceConsolidationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectExternalConfigurationStorePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectGatewayAggregationPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectGatewayOffloadingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectGatewayRoutingPattern(root, filePath, context, sourceCode));
            
            // MESSAGING PATTERNS (10 patterns)
            patterns.AddRange(DetectAsyncRequestReplyPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectClaimCheckPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectChoreographyPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCompetingConsumersPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPipesAndFiltersPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPriorityQueuePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectQueueBasedLoadLevelingPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSchedulerAgentSupervisorPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSequentialConvoyPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMessagingBridgePattern(root, filePath, context, sourceCode));
            
            // RELIABILITY & RESILIENCY PATTERNS (7 patterns)
            patterns.AddRange(DetectBulkheadPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCircuitBreakerPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCompensatingTransactionPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLeaderElectionPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectGeodePattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectDeploymentStampsPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectThrottlingPattern(root, filePath, context, sourceCode));
            
            // SECURITY PATTERNS (2 patterns)
            patterns.AddRange(DetectFederatedIdentityPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectQuarantinePattern(root, filePath, context, sourceCode));
            
            // OPERATIONAL PATTERNS (3 patterns)
            patterns.AddRange(DetectSidecarPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectStranglerFigPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSagaPattern(root, filePath, context, sourceCode));
            
            // Plus all existing patterns from base detector...
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting enhanced patterns in {filePath}: {ex.Message}");
        }

        return patterns;
    }

    #region CACHING PATTERNS (Azure Best Practice)

    private List<CodePattern> DetectCacheAsidePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: if (!cache.TryGetValue) { load from source; cache.Set }
        var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
        
        foreach (var ifStmt in ifStatements)
        {
            var condition = ifStmt.Condition.ToString();
            
            // Check for TryGetValue with negation
            if ((condition.Contains("!") || condition.Contains("not")) && 
                (condition.Contains("TryGetValue") || condition.Contains("TryGet")))
            {
                // Check if statement body has Set/Add to cache
                var bodyText = ifStmt.Statement.ToString();
                if (bodyText.Contains(".Set(") || bodyText.Contains(".Add("))
                {
                    var pattern = CreatePattern(
                        name: "CacheAside_Pattern",
                        type: PatternType.Caching,
                        category: PatternCategory.Performance,
                        implementation: "Cache-Aside",
                        filePath: filePath,
                        node: ifStmt,
                        sourceCode: sourceCode,
                        bestPractice: "Cache-Aside pattern (lazy loading from source on cache miss)",
                        azureUrl: AzureCachingUrl,
                        context: context
                    );
                    
                    pattern.Metadata["cache_pattern"] = "cache-aside";
                    pattern.Metadata["lazy_loading"] = true;
                    pattern.Confidence = 0.95f;
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectWriteThroughPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: await cache.SetAsync(...); await database.UpdateAsync(...);
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var statements = method.DescendantNodes().OfType<ExpressionStatementSyntax>().ToList();
            
            for (int i = 0; i < statements.Count - 1; i++)
            {
                var stmt1 = statements[i].ToString();
                var stmt2 = statements[i + 1].ToString();
                
                // Look for cache set followed by database update
                if ((stmt1.Contains("cache") && stmt1.Contains("Set")) &&
                    (stmt2.Contains("database") || stmt2.Contains("db") || stmt2.Contains("Update") || stmt2.Contains("Save")))
                {
                    var pattern = CreatePattern(
                        name: "WriteThrough_Pattern",
                        type: PatternType.Caching,
                        category: PatternCategory.Performance,
                        implementation: "Write-Through",
                        filePath: filePath,
                        node: statements[i],
                        sourceCode: sourceCode,
                        bestPractice: "Write-Through pattern (synchronous cache and database update)",
                        azureUrl: AzureCachingUrl,
                        context: context
                    );
                    
                    pattern.Metadata["cache_pattern"] = "write-through";
                    pattern.Metadata["consistency"] = "strong";
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectWriteBehindPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: cache.Set + background queue (write-behind = cache first, persist async later)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodBody = method.Body?.ToString() ?? method.ExpressionBody?.ToString() ?? "";
            
            // Look for cache write followed by queue/background job
            var hasCacheWrite = methodBody.Contains("cache.Set") || methodBody.Contains("Cache.Set") || 
                                methodBody.Contains("_cache.Set") || methodBody.Contains(".SetAsync");
            var hasQueueWrite = methodBody.Contains("Enqueue") || methodBody.Contains("QueueBackgroundWorkItem") || 
                                methodBody.Contains("_queue") || methodBody.Contains("BackgroundJob");
            
            if (hasCacheWrite && hasQueueWrite)
            {
                var pattern = CreatePattern(
                    name: "WriteBehind_Pattern",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "Write-Behind",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Write-Behind pattern (async database update via queue)",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_pattern"] = "write-behind";
                pattern.Metadata["async_persistence"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCacheExpirationPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: MemoryCacheEntryOptions with expiration
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(o => o.Type.ToString().Contains("MemoryCacheEntryOptions"));

        foreach (var obj in objectCreations)
        {
            var initText = obj.ToString();
            var hasAbsolute = initText.Contains("AbsoluteExpiration");
            var hasSliding = initText.Contains("SlidingExpiration");
            var hasPriority = initText.Contains("Priority");

            if (hasAbsolute || hasSliding)
            {
                var expirationTypes = new List<string>();
                if (hasAbsolute) expirationTypes.Add("absolute");
                if (hasSliding) expirationTypes.Add("sliding");
                
                var pattern = CreatePattern(
                    name: "CacheExpiration_Policy",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: string.Join("+", expirationTypes),
                    filePath: filePath,
                    node: obj,
                    sourceCode: sourceCode,
                    bestPractice: $"Cache expiration policy ({string.Join(" and ", expirationTypes)})",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["absolute_expiration"] = hasAbsolute;
                pattern.Metadata["sliding_expiration"] = hasSliding;
                pattern.Metadata["has_priority"] = hasPriority;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCacheStampedePrevent(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: lock or SemaphoreSlim around cache operations
        var lockStatements = root.DescendantNodes().OfType<LockStatementSyntax>();
        var usingStatements = root.DescendantNodes().OfType<UsingStatementSyntax>();

        foreach (var lockStmt in lockStatements)
        {
            if (lockStmt.Statement.ToString().Contains("cache") || 
                lockStmt.Statement.ToString().Contains("TryGetValue"))
            {
                var pattern = CreatePattern(
                    name: "CacheStampede_Prevention",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "Lock",
                    filePath: filePath,
                    node: lockStmt,
                    sourceCode: sourceCode,
                    bestPractice: "Cache stampede prevention using lock",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["concurrency_control"] = "lock";
                patterns.Add(pattern);
            }
        }

        // Check for SemaphoreSlim
        foreach (var usingStmt in usingStatements)
        {
            if (usingStmt.ToString().Contains("SemaphoreSlim") || 
                usingStmt.ToString().Contains("WaitAsync"))
            {
                var pattern = CreatePattern(
                    name: "CacheStampede_SemaphoreSlim",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "SemaphoreSlim",
                    filePath: filePath,
                    node: usingStmt,
                    sourceCode: sourceCode,
                    bestPractice: "Cache stampede prevention using SemaphoreSlim",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["concurrency_control"] = "semaphore";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRefreshAheadPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: proactive cache refresh before expiration
        // Look for: if (timeUntilExpiration < threshold) { refresh }
        var ifStatements = root.DescendantNodes().OfType<IfStatementSyntax>();
        
        foreach (var ifStmt in ifStatements)
        {
            var condition = ifStmt.Condition.ToString().ToLower();
            var body = ifStmt.Statement.ToString();
            
            if ((condition.Contains("expir") || condition.Contains("ttl") || condition.Contains("threshold")) &&
                (body.Contains("Refresh") || body.Contains("Reload") || body.Contains("Task.Run")))
            {
                var pattern = CreatePattern(
                    name: "RefreshAhead_Pattern",
                    type: PatternType.Caching,
                    category: PatternCategory.Performance,
                    implementation: "Refresh-Ahead",
                    filePath: filePath,
                    node: ifStmt,
                    sourceCode: sourceCode,
                    bestPractice: "Refresh-ahead pattern (proactive cache refresh)",
                    azureUrl: AzureCachingUrl,
                    context: context
                );
                
                pattern.Metadata["cache_pattern"] = "refresh-ahead";
                pattern.Metadata["proactive"] = true;
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region API DESIGN PATTERNS

    private List<CodePattern> DetectHttpVerbPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var httpVerbAttrs = new[] { "HttpGet", "HttpPost", "HttpPut", "HttpPatch", "HttpDelete", "HttpHead", "HttpOptions" };
        
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            var attrName = attr.Name.ToString();
            if (httpVerbAttrs.Contains(attrName))
            {
                var pattern = CreatePattern(
                    name: $"HttpVerb_{attrName}",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: attrName,
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: $"Proper HTTP verb usage ({attrName})",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var metadata = GetHttpVerbMetadata(attrName);
                foreach (var kvp in metadata)
                {
                    pattern.Metadata[kvp.Key] = kvp.Value;
                }
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private Dictionary<string, object> GetHttpVerbMetadata(string verb) => verb switch
    {
        "HttpGet" => new() { ["safe"] = true, ["idempotent"] = true, ["cacheable"] = true },
        "HttpPost" => new() { ["safe"] = false, ["idempotent"] = false, ["cacheable"] = false },
        "HttpPut" => new() { ["safe"] = false, ["idempotent"] = true, ["cacheable"] = false },
        "HttpPatch" => new() { ["safe"] = false, ["idempotent"] = false, ["cacheable"] = false },
        "HttpDelete" => new() { ["safe"] = false, ["idempotent"] = true, ["cacheable"] = false },
        "HttpHead" => new() { ["safe"] = true, ["idempotent"] = true, ["cacheable"] = true },
        "HttpOptions" => new() { ["safe"] = true, ["idempotent"] = true, ["cacheable"] = false },
        _ => new()
    };

    private List<CodePattern> DetectPaginationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: class with PageNumber, PageSize, TotalCount properties
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
            var propNames = properties.Select(p => p.Identifier.Text.ToLower()).ToList();
            
            var hasPageNumber = propNames.Any(n => n.Contains("page") && n.Contains("number"));
            var hasPageSize = propNames.Any(n => n.Contains("page") && n.Contains("size"));
            var hasTotalCount = propNames.Any(n => n.Contains("total"));
            
            if (hasPageNumber && hasPageSize && hasTotalCount)
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_Pagination",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "Pagination",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Pagination support for large result sets",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var hasNext = propNames.Any(n => n.Contains("next"));
                var hasPrev = propNames.Any(n => n.Contains("prev"));
                
                pattern.Metadata["has_navigation_links"] = hasNext && hasPrev;
                pattern.Metadata["hateoas_compliant"] = hasNext && hasPrev;
                patterns.Add(pattern);
            }
        }

        // Also detect pagination parameters in methods (including methods without class wrappers)
        // For test code without class wrappers, look in CompilationUnit.Members
        var allMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        if (root is CompilationUnitSyntax compilationUnit)
        {
            var topLevelMethods = compilationUnit.Members.OfType<MethodDeclarationSyntax>();
            allMethods = allMethods.Concat(topLevelMethods);
        }
            
        foreach (var method in allMethods)
        {
            var parameters = method.ParameterList.Parameters;
            var paramNames = parameters.Select(p => p.Identifier.Text.ToLower()).ToList();
            
            // Check for pagination parameters: pageNumber/pageIndex AND pageSize/limit/take
            var hasPageParam = paramNames.Any(n => n.Contains("page") || n.Contains("skip") || n.Contains("offset"));
            var hasSizeParam = paramNames.Any(n => n.Contains("size") || n.Contains("limit") || n.Contains("take") || n.Contains("count"));
            
            if (hasPageParam && hasSizeParam)
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_PaginationParams",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "PaginationParameters",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Pagination parameters in API method",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectIdempotencyPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [FromHeader(Name = "Idempotency-Key")]
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        
        foreach (var param in parameters)
        {
            var attrs = param.AttributeLists.SelectMany(al => al.Attributes);
            foreach (var attr in attrs)
            {
                if (attr.ToString().Contains("FromHeader") && 
                    attr.ToString().Contains("Idempotency"))
                {
                    var pattern = CreatePattern(
                        name: "Idempotency_Key",
                        type: PatternType.ApiDesign,
                        category: PatternCategory.Reliability,
                        implementation: "IdempotencyKey",
                        filePath: filePath,
                        node: param,
                        sourceCode: sourceCode,
                        bestPractice: "Idempotency key for safe retries",
                        azureUrl: AzureApiImplUrl,
                        context: context
                    );
                    
                    pattern.Metadata["prevents_duplicate_operations"] = true;
                    patterns.Add(pattern);
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAsyncOperationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: return Accepted() with status URL
        var returnStatements = root.DescendantNodes().OfType<ReturnStatementSyntax>();
        
        foreach (var ret in returnStatements)
        {
            if (ret.ToString().Contains("Accepted(") || ret.ToString().Contains("StatusCode(202"))
            {
                var pattern = CreatePattern(
                    name: "LongRunning_AsyncOperation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "AsyncOperation",
                    filePath: filePath,
                    node: ret,
                    sourceCode: sourceCode,
                    bestPractice: "Long-running operation with 202 Accepted response",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["http_status"] = 202;
                pattern.Metadata["async_pattern"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region MONITORING PATTERNS

    private List<CodePattern> DetectCorrelationIdPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: CorrelationId in log scope or method
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("BeginScope") && inv.ToString().Contains("CorrelationId"))
            {
                var pattern = CreatePattern(
                    name: "CorrelationId_Logging",
                    type: PatternType.Monitoring,
                    category: PatternCategory.Operational,
                    implementation: "CorrelationId",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Correlation ID for distributed tracing",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["distributed_tracing"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHealthCheckPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: IHealthCheck implementation
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IHealthCheck")) == true)
            {
                var pattern = CreatePattern(
                    name: classDecl.Identifier.Text,
                    type: PatternType.Monitoring,
                    category: PatternCategory.Reliability,
                    implementation: "HealthCheck",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Health check implementation for monitoring",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["health_check"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectTelemetryPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: TelemetryClient.Track*
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            if (invText.Contains("TrackEvent") || invText.Contains("TrackMetric") || 
                invText.Contains("TrackDependency") || invText.Contains("TrackException"))
            {
                var trackType = invText.Contains("Event") ? "Event" :
                              invText.Contains("Metric") ? "Metric" :
                              invText.Contains("Dependency") ? "Dependency" : "Exception";
                
                var pattern = CreatePattern(
                    name: $"ApplicationInsights_{trackType}",
                    type: PatternType.Monitoring,
                    category: PatternCategory.Operational,
                    implementation: "ApplicationInsights",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: $"Application Insights telemetry ({trackType})",
                    azureUrl: AzureMonitoringUrl,
                    context: context
                );
                
                pattern.Metadata["telemetry_type"] = trackType;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region BACKGROUND JOB PATTERNS

    private List<CodePattern> DetectHostedServicePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IHostedService")) == true)
            {
                var pattern = CreatePattern(
                    name: classDecl.Identifier.Text,
                    type: PatternType.BackgroundJobs,
                    category: PatternCategory.Operational,
                    implementation: "IHostedService",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Background service using IHostedService",
                    azureUrl: AzureBackgroundJobsUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMessageQueuePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [ServiceBusTrigger], [QueueTrigger]
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString().Contains("ServiceBusTrigger") || 
                attr.Name.ToString().Contains("QueueTrigger"))
            {
                var pattern = CreatePattern(
                    name: "MessageQueue_Consumer",
                    type: PatternType.BackgroundJobs,
                    category: PatternCategory.Operational,
                    implementation: attr.Name.ToString(),
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: "Message queue consumer pattern",
                    azureUrl: AzureBackgroundJobsUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHangfirePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("BackgroundJob.Enqueue") || 
                inv.ToString().Contains("RecurringJob.AddOrUpdate"))
            {
                var pattern = CreatePattern(
                    name: "Hangfire_BackgroundJob",
                    type: PatternType.BackgroundJobs,
                    category: PatternCategory.Operational,
                    implementation: "Hangfire",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Hangfire background job scheduling",
                    azureUrl: AzureBackgroundJobsUrl,
                    context: context
                );
                
                pattern.Metadata["library"] = "Hangfire";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Additional API Design & Implementation Patterns

    private List<CodePattern> DetectFilteringSortingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [FromQuery] filter, sort, fields parameters
        // Check both nested methods AND top-level methods (for test code without class wrappers)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        if (root is CompilationUnitSyntax compilationUnit)
        {
            var topLevelMethods = compilationUnit.Members.OfType<MethodDeclarationSyntax>();
            methods = methods.Concat(topLevelMethods);
        }
        
        foreach (var method in methods)
        {
            var parameters = method.ParameterList.Parameters;
            var paramNamesLower = parameters.Select(p => p.Identifier.Text.ToLower()).ToList();
            
            var hasFilter = paramNamesLower.Any(n => n is "filter" or "search" or "query" || n.Contains("filter"));
            var hasSort = paramNamesLower.Any(n => n is "sort" or "orderby" or "sortby" || n.Contains("sort"));
            var hasFields = paramNamesLower.Any(n => n is "fields" or "select" || n.Contains("field"));
            
            // Also check for [FromQuery] attributes as a signal
            var hasFromQuery = parameters.Any(p => p.AttributeLists.SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString().Contains("FromQuery")));
            
            if ((hasFilter || hasSort || hasFields) && (hasFromQuery || parameters.Count >= 2))
            {
                var features = new List<string>();
                if (hasFilter) features.Add("filtering");
                if (hasSort) features.Add("sorting");
                if (hasFields) features.Add("field selection");
                
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_QueryCapabilities",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: string.Join("+", features),
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: $"API query capabilities: {string.Join(", ", features)}",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["supports_filtering"] = hasFilter;
                pattern.Metadata["supports_sorting"] = hasSort;
                pattern.Metadata["supports_field_selection"] = hasFields;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectVersioningPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: [Route("api/v1/...")] - URI versioning
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString() == "Route")
            {
                var routeArgs = attr.ArgumentList?.Arguments.ToString() ?? "";
                if (Regex.IsMatch(routeArgs, @"v\d+", RegexOptions.IgnoreCase))
                {
                    var pattern = CreatePattern(
                        name: "API_UriVersioning",
                        type: PatternType.ApiDesign,
                        category: PatternCategory.Operational,
                        implementation: "UriVersioning",
                        filePath: filePath,
                        node: attr,
                        sourceCode: sourceCode,
                        bestPractice: "API versioning via URI path",
                        azureUrl: AzureApiDesignUrl,
                        context: context
                    );
                    
                    pattern.Metadata["versioning_strategy"] = "uri";
                    patterns.Add(pattern);
                }
            }
            
            // Pattern 2: [ApiVersion("1.0")] - Header/Query versioning
            if (attr.Name.ToString().Contains("ApiVersion"))
            {
                var pattern = CreatePattern(
                    name: "API_HeaderVersioning",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: "ApiVersionAttribute",
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: "API versioning using ApiVersion attribute",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["versioning_strategy"] = "attribute";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectHateoasPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Classes with "Links" property or HATEOAS response structure
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>();
            var hasLinks = properties.Any(p => p.Identifier.Text is "Links" or "_links" or "Href");
            
            if (hasLinks)
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_HATEOAS",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: "HATEOAS",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "HATEOAS (Hypermedia as the Engine of Application State)",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["hypermedia_support"] = true;
                pattern.Metadata["api_discoverability"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectContentNegotiationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [Produces(...)] or [Consumes(...)] attributes
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            var attrName = attr.Name.ToString();
            if (attrName is "Produces" or "Consumes")
            {
                var pattern = CreatePattern(
                    name: $"{attrName}_ContentNegotiation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: attrName,
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: $"Content negotiation using {attrName} attribute",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var args = attr.ArgumentList?.Arguments.ToString() ?? "";
                pattern.Metadata["content_types"] = args;
                pattern.Metadata["supports_multiple_formats"] = args.Contains(",");
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPartialResponsePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Field selection / projection logic
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var bodyText = method.Body?.ToString() ?? "";
            
            // Look for Select(), projection, or field filtering
            if ((bodyText.Contains("Select(") && bodyText.Contains("fields")) ||
                bodyText.Contains("SelectFields") ||
                bodyText.Contains("ProjectTo") ||
                (bodyText.Contains("fields") && bodyText.Contains("Split")))
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_PartialResponse",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "SparseFieldsets",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Partial response / sparse fieldsets for bandwidth optimization",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["reduces_payload_size"] = true;
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectBatchOperationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Method accepting List<T> or IEnumerable<T> for batch operations
        // Check both nested methods AND top-level methods
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        if (root is CompilationUnitSyntax compilationUnit)
        {
            var topLevelMethods = compilationUnit.Members.OfType<MethodDeclarationSyntax>();
            methods = methods.Concat(topLevelMethods);
        }
        
        foreach (var method in methods)
        {
            var parameters = method.ParameterList.Parameters;
            var hasBatchParam = parameters.Any(p => 
                p.Type?.ToString().Contains("List<") == true || 
                p.Type?.ToString().Contains("IEnumerable<") == true ||
                p.Type?.ToString().Contains("[]") == true);
            
            var methodName = method.Identifier.Text.ToLower();
            var isBatchMethod = methodName.Contains("batch") || methodName.Contains("bulk") || methodName.Contains("multiple");
            
            if (hasBatchParam && (isBatchMethod || parameters.Count == 1))
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_BatchOperation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "BatchAPI",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Batch operation API for reduced round-trips",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["reduces_round_trips"] = true;
                pattern.Confidence = isBatchMethod ? 1.0f : 0.7f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectETagPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Setting ETag header
        var assignments = root.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        foreach (var assignment in assignments)
        {
            if (assignment.ToString().Contains("ETag") || assignment.ToString().Contains("\"ETag\""))
            {
                var pattern = CreatePattern(
                    name: "ETag_Generation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Performance,
                    implementation: "ETag",
                    filePath: filePath,
                    node: assignment,
                    sourceCode: sourceCode,
                    bestPractice: "ETag header for HTTP caching and concurrency control",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["supports_caching"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: If-Match / If-None-Match header validation
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        foreach (var param in parameters)
        {
            var attrs = param.AttributeLists.SelectMany(al => al.Attributes);
            if (attrs.Any(a => a.ToString().Contains("If-Match") || a.ToString().Contains("If-None-Match")))
            {
                var pattern = CreatePattern(
                    name: "ETag_Validation",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Reliability,
                    implementation: "ETagValidation",
                    filePath: filePath,
                    node: param,
                    sourceCode: sourceCode,
                    bestPractice: "ETag validation for optimistic concurrency control",
                    azureUrl: AzureApiImplUrl,
                    context: context
                );
                
                pattern.Metadata["prevents_lost_updates"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectShardingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Shard key calculation / routing logic
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodName = method.Identifier.Text.ToLower();
            var bodyText = method.Body?.ToString() ?? "";
            
            // Look for shard key calculation
            if ((methodName.Contains("shard") || methodName.Contains("partition")) ||
                (bodyText.Contains("% ") && (bodyText.Contains("shard") || bodyText.Contains("partition"))) ||
                bodyText.Contains("GetShard") ||
                bodyText.Contains("ShardKey"))
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_Sharding",
                    type: PatternType.DataAccess,
                    category: PatternCategory.Performance,
                    implementation: "HorizontalPartitioning",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Horizontal partitioning (sharding) for data distribution",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/data-partitioning",
                    context: context
                );
                
                pattern.Metadata["scalability_pattern"] = true;
                pattern.Metadata["partition_type"] = "horizontal";
                pattern.Confidence = methodName.Contains("shard") ? 1.0f : 0.7f;
                patterns.Add(pattern);
            }
        }

        // Also check for Dictionary/Map of multiple data stores
        var fields = root.DescendantNodes().OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            var fieldType = field.Declaration.Type.ToString();
            if ((fieldType.Contains("Dictionary") || fieldType.Contains("IDictionary")) &&
                (fieldType.Contains("Repository") || fieldType.Contains("DataSource") || fieldType.Contains("Connection")))
            {
                var pattern = CreatePattern(
                    name: "MultiShard_DataStore",
                    type: PatternType.DataAccess,
                    category: PatternCategory.Performance,
                    implementation: "ShardMap",
                    filePath: filePath,
                    node: field,
                    sourceCode: sourceCode,
                    bestPractice: "Multiple data store instances for sharding",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/data-partitioning",
                    context: context
                );
                
                pattern.Metadata["shard_map"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectVerticalPartitionPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Separate classes/repositories for hot vs cold data
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        var hotDataClasses = classes.Where(c => c.Identifier.Text.Contains("Profile") || 
                                                c.Identifier.Text.Contains("Core") ||
                                                c.Identifier.Text.Contains("Active")).ToList();
        var coldDataClasses = classes.Where(c => c.Identifier.Text.Contains("History") || 
                                                 c.Identifier.Text.Contains("Archive") ||
                                                 c.Identifier.Text.Contains("Audit")).ToList();
        
        foreach (var coldClass in coldDataClasses)
        {
            // Check if there's a corresponding hot class
            var baseName = coldClass.Identifier.Text.Replace("History", "").Replace("Archive", "").Replace("Audit", "");
            if (hotDataClasses.Any(h => h.Identifier.Text.Contains(baseName)))
            {
                var pattern = CreatePattern(
                    name: $"{coldClass.Identifier.Text}_VerticalPartition",
                    type: PatternType.DataAccess,
                    category: PatternCategory.Performance,
                    implementation: "VerticalPartitioning",
                    filePath: filePath,
                    node: coldClass,
                    sourceCode: sourceCode,
                    bestPractice: "Vertical partitioning (hot/cold data separation)",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/best-practices/data-partitioning",
                    context: context
                );
                
                pattern.Metadata["partition_type"] = "vertical";
                pattern.Metadata["data_temperature"] = "cold";
                pattern.Confidence = 0.8f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAuthPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: [Authorize] attributes
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString().Contains("Authorize"))
            {
                var args = attr.ArgumentList?.Arguments.ToString() ?? "";
                var hasPolicy = args.Contains("Policy");
                var hasRoles = args.Contains("Roles");
                var hasScheme = args.Contains("AuthenticationSchemes");
                
                var authType = hasPolicy ? "Policy-Based" : hasRoles ? "Role-Based" : hasScheme ? "Scheme-Based" : "Simple";
                
                var pattern = CreatePattern(
                    name: $"Authorization_{authType}",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: $"Authorize_{authType}",
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: $"{authType} authorization",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["auth_type"] = authType.ToLower();
                pattern.Metadata["has_policy"] = hasPolicy;
                pattern.Metadata["has_roles"] = hasRoles;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCorsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: AddCors configuration
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("AddCors") || inv.ToString().Contains("UseCors"))
            {
                var pattern = CreatePattern(
                    name: "CORS_Configuration",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: "CORS",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "CORS (Cross-Origin Resource Sharing) configuration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                var invText = inv.ToString();
                pattern.Metadata["with_origins"] = invText.Contains("WithOrigins");
                pattern.Metadata["allow_credentials"] = invText.Contains("AllowCredentials");
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectRateLimitingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: [EnableRateLimiting] or [RateLimit] attributes
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        
        foreach (var attr in attributes)
        {
            if (attr.Name.ToString().Contains("RateLimit") || attr.Name.ToString().Contains("Throttle"))
            {
                var pattern = CreatePattern(
                    name: "RateLimit_Attribute",
                    type: PatternType.Security,
                    category: PatternCategory.Reliability,
                    implementation: "RateLimiting",
                    filePath: filePath,
                    node: attr,
                    sourceCode: sourceCode,
                    bestPractice: "Rate limiting for API throttling",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["protects_from_abuse"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: AddRateLimiter service registration
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("AddRateLimiter"))
            {
                var pattern = CreatePattern(
                    name: "RateLimit_Service",
                    type: PatternType.Security,
                    category: PatternCategory.Reliability,
                    implementation: "RateLimiterService",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Rate limiter service configuration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectOptionsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: IOptions<T> injection
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        
        foreach (var param in parameters)
        {
            var paramType = param.Type?.ToString() ?? "";
            if (paramType.Contains("IOptions<") || paramType.Contains("IOptionsSnapshot<") || paramType.Contains("IOptionsMonitor<"))
            {
                var optionsType = paramType.Contains("IOptionsSnapshot") ? "Snapshot" :
                                paramType.Contains("IOptionsMonitor") ? "Monitor" : "Standard";
                
                var pattern = CreatePattern(
                    name: $"Options_{optionsType}",
                    type: PatternType.Configuration,
                    category: PatternCategory.Operational,
                    implementation: $"IOptions{(optionsType != "Standard" ? optionsType : "")}",
                    filePath: filePath,
                    node: param,
                    sourceCode: sourceCode,
                    bestPractice: $"Options pattern ({optionsType}) for strongly-typed configuration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["options_type"] = optionsType.ToLower();
                pattern.Metadata["strongly_typed_config"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Configure<T> service registration
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("Configure<"))
            {
                var pattern = CreatePattern(
                    name: "Options_Configuration",
                    type: PatternType.Configuration,
                    category: PatternCategory.Operational,
                    implementation: "ConfigureOptions",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Options configuration registration",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectNamedOptionsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Configure<T>("name", ...) - named options
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("Configure<") && inv.ArgumentList != null)
            {
                var args = inv.ArgumentList.Arguments;
                // Check if first argument is a string literal (name)
                if (args.Count > 0 && args[0].Expression is LiteralExpressionSyntax literal && 
                    literal.Kind() == SyntaxKind.StringLiteralExpression)
                {
                    var pattern = CreatePattern(
                        name: "NamedOptions_Configuration",
                        type: PatternType.Configuration,
                        category: PatternCategory.Operational,
                        implementation: "NamedOptions",
                        filePath: filePath,
                        node: inv,
                        sourceCode: sourceCode,
                        bestPractice: "Named options for multiple configurations",
                        azureUrl: AzureApiDesignUrl,
                        context: context
                    );
                    
                    pattern.Metadata["supports_multiple_configs"] = true;
                    patterns.Add(pattern);
                }
            }
        }

        // Also detect IOptionsSnapshot.Get("name")
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains(".Get(") && inv.ToString().Contains("options"))
            {
                var pattern = CreatePattern(
                    name: "NamedOptions_Retrieval",
                    type: PatternType.Configuration,
                    category: PatternCategory.Operational,
                    implementation: "NamedOptionsRetrieval",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Named options retrieval",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Confidence = 0.7f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region PUBLISHER-SUBSCRIBER PATTERNS (Azure Messaging)

    private List<CodePattern> DetectServiceBusTopicPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: ServiceBusClient / TopicClient / ServiceBusSender for topics
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        
        foreach (var obj in objectCreations)
        {
            var typeName = obj.Type.ToString();
            if (typeName.Contains("TopicClient") || typeName.Contains("ServiceBusSender") || typeName.Contains("ServiceBusClient"))
            {
                var pattern = CreatePattern(
                    name: "ServiceBus_Client",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: typeName.Contains("ServiceBusClient") ? "AzureServiceBus" : "AzureServiceBusTopics",
                    filePath: filePath,
                    node: obj,
                    sourceCode: sourceCode,
                    bestPractice: "Publisher-Subscriber pattern using Azure Service Bus",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Service Bus";
                pattern.Metadata["pattern_type"] = "pub-sub";
                pattern.Metadata["role"] = "client";
                pattern.Metadata["supports_filtering"] = true;
                pattern.Metadata["supports_sessions"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 1b: CreateSender / CreateReceiver method calls (newer SDK)
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            if (invText.Contains("CreateSender") || invText.Contains("SendMessageAsync") || invText.Contains("SendMessagesAsync"))
            {
                var pattern = CreatePattern(
                    name: "ServiceBus_TopicPublisher",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "AzureServiceBus",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Publisher-Subscriber pattern using Azure Service Bus",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Service Bus";
                pattern.Metadata["pattern_type"] = "pub-sub";
                pattern.Metadata["role"] = "publisher";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: SubscriptionClient / ServiceBusProcessor for subscriptions
        foreach (var obj in objectCreations)
        {
            var typeName = obj.Type.ToString();
            if (typeName.Contains("SubscriptionClient") || typeName.Contains("ServiceBusProcessor") || typeName.Contains("ServiceBusReceiver"))
            {
                var pattern = CreatePattern(
                    name: "ServiceBus_TopicSubscriber",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "AzureServiceBusSubscriptions",
                    filePath: filePath,
                    node: obj,
                    sourceCode: sourceCode,
                    bestPractice: "Subscriber pattern using Azure Service Bus Subscriptions",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Service Bus";
                pattern.Metadata["pattern_type"] = "pub-sub";
                pattern.Metadata["role"] = "subscriber";
                pattern.Metadata["supports_content_filtering"] = true;
                pattern.Metadata["supports_sql_filters"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 3: Topic/Subscription configuration
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            if (invText.Contains("CreateTopicAsync") || invText.Contains("CreateSubscriptionAsync"))
            {
                var isTopicCreation = invText.Contains("CreateTopicAsync");
                var pattern = CreatePattern(
                    name: isTopicCreation ? "ServiceBus_TopicCreation" : "ServiceBus_SubscriptionCreation",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Operational,
                    implementation: "ServiceBusManagement",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: $"Azure Service Bus {(isTopicCreation ? "Topic" : "Subscription")} provisioning",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["infrastructure_as_code"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectEventGridPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: EventGridPublisherClient
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        
        foreach (var obj in objectCreations)
        {
            var typeName = obj.Type.ToString();
            if (typeName.Contains("EventGridPublisherClient") || typeName.Contains("EventGridClient"))
            {
                var pattern = CreatePattern(
                    name: "EventGrid_Publisher",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "AzureEventGrid",
                    filePath: filePath,
                    node: obj,
                    sourceCode: sourceCode,
                    bestPractice: "Event-driven architecture using Azure Event Grid",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Event Grid";
                pattern.Metadata["pattern_type"] = "event-driven";
                pattern.Metadata["role"] = "publisher";
                pattern.Metadata["supports_filtering"] = true;
                pattern.Metadata["use_case"] = "event-routing";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Event Grid trigger in Azure Functions
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        foreach (var param in parameters)
        {
            var attrs = param.AttributeLists.SelectMany(al => al.Attributes);
            if (attrs.Any(a => a.Name.ToString().Contains("EventGridTrigger")))
            {
                var pattern = CreatePattern(
                    name: "EventGrid_Subscriber",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "EventGridTrigger",
                    filePath: filePath,
                    node: param,
                    sourceCode: sourceCode,
                    bestPractice: "Event Grid subscriber using Azure Functions trigger",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Event Grid";
                pattern.Metadata["pattern_type"] = "event-driven";
                pattern.Metadata["role"] = "subscriber";
                pattern.Metadata["serverless"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 3: EventGridEvent handling
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var hasEventGridParam = method.ParameterList.Parameters.Any(p => 
                p.Type?.ToString().Contains("EventGridEvent") == true);
            
            if (hasEventGridParam)
            {
                var pattern = CreatePattern(
                    name: $"{method.Identifier.Text}_EventGridHandler",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "EventGridEventHandler",
                    filePath: filePath,
                    node: method,
                    sourceCode: sourceCode,
                    bestPractice: "Event Grid event handler method",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["event_schema"] = "EventGridEvent";
                pattern.Confidence = 0.9f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectEventHubsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: EventHubProducerClient
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        
        foreach (var obj in objectCreations)
        {
            var typeName = obj.Type.ToString();
            if (typeName.Contains("EventHubProducerClient") || typeName.Contains("EventHubClient"))
            {
                var pattern = CreatePattern(
                    name: "EventHubs_Producer",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "AzureEventHubs",
                    filePath: filePath,
                    node: obj,
                    sourceCode: sourceCode,
                    bestPractice: "High-throughput event streaming using Azure Event Hubs",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Event Hubs";
                pattern.Metadata["pattern_type"] = "event-streaming";
                pattern.Metadata["role"] = "producer";
                pattern.Metadata["high_throughput"] = true;
                pattern.Metadata["use_case"] = "telemetry-ingestion";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: EventProcessorClient / EventHubConsumerClient
        foreach (var obj in objectCreations)
        {
            var typeName = obj.Type.ToString();
            if (typeName.Contains("EventProcessorClient") || typeName.Contains("EventHubConsumerClient"))
            {
                var pattern = CreatePattern(
                    name: "EventHubs_Consumer",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "EventHubsProcessor",
                    filePath: filePath,
                    node: obj,
                    sourceCode: sourceCode,
                    bestPractice: "Event Hubs consumer for processing event streams",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "Azure Event Hubs";
                pattern.Metadata["pattern_type"] = "event-streaming";
                pattern.Metadata["role"] = "consumer";
                pattern.Metadata["supports_checkpointing"] = true;
                pattern.Metadata["consumer_groups"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 3: Event Hubs trigger in Azure Functions
        var parameters = root.DescendantNodes().OfType<ParameterSyntax>();
        foreach (var param in parameters)
        {
            var attrs = param.AttributeLists.SelectMany(al => al.Attributes);
            if (attrs.Any(a => a.Name.ToString().Contains("EventHubTrigger")))
            {
                var pattern = CreatePattern(
                    name: "EventHubs_Trigger",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Performance,
                    implementation: "EventHubsTrigger",
                    filePath: filePath,
                    node: param,
                    sourceCode: sourceCode,
                    bestPractice: "Event Hubs trigger for serverless event processing",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["serverless"] = true;
                pattern.Metadata["auto_scaling"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMassTransitPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Check if IBus is used in the source code (field injection, constructor param, etc.)
        var hasIBus = sourceCode.Contains("IBus");
        
        // Pattern 1: IBus.Publish<T> or _bus.Publish (when IBus is injected)
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            // Match .Publish calls when IBus is present in the file (supports field injection like _bus.Publish)
            if ((invText.Contains("IBus") && invText.Contains(".Publish")) || 
                (hasIBus && invText.Contains(".Publish(") && !invText.Contains("redis") && !invText.Contains("channel")))
            {
                var pattern = CreatePattern(
                    name: "MassTransit_Publisher",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "MassTransit",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "Publisher-Subscriber using MassTransit messaging library",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "MassTransit";
                pattern.Metadata["pattern_type"] = "pub-sub";
                pattern.Metadata["role"] = "publisher";
                pattern.Metadata["supports_sagas"] = true;
                pattern.Metadata["library"] = "MassTransit";
                patterns.Add(pattern);
            }
        }

        // Pattern 2: IConsumer<T> implementation
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IConsumer<")) == true)
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_Consumer",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "MassTransitConsumer",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "MassTransit consumer implementation for message handling",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "MassTransit";
                pattern.Metadata["pattern_type"] = "pub-sub";
                pattern.Metadata["role"] = "consumer";
                pattern.Metadata["typed_consumer"] = true;
                patterns.Add(pattern);
            }
        }

        // Pattern 3: MassTransit configuration
        foreach (var inv in invocations)
        {
            if (inv.ToString().Contains("AddMassTransit"))
            {
                var pattern = CreatePattern(
                    name: "MassTransit_Configuration",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Operational,
                    implementation: "MassTransitSetup",
                    filePath: filePath,
                    node: inv,
                    sourceCode: sourceCode,
                    bestPractice: "MassTransit service bus configuration",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["dependency_injection"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectNServiceBusPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: IEndpointInstance.Publish<T>
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            if (invText.Contains("IEndpointInstance") || invText.Contains("IMessageSession"))
            {
                if (invText.Contains(".Publish"))
                {
                    var pattern = CreatePattern(
                        name: "NServiceBus_Publisher",
                        type: PatternType.PublisherSubscriber,
                        category: PatternCategory.Reliability,
                        implementation: "NServiceBus",
                        filePath: filePath,
                        node: inv,
                        sourceCode: sourceCode,
                        bestPractice: "Event publishing using NServiceBus",
                        azureUrl: AzurePubSubUrl,
                        context: context
                    );
                    
                    pattern.Metadata["messaging_technology"] = "NServiceBus";
                    pattern.Metadata["pattern_type"] = "pub-sub";
                    pattern.Metadata["role"] = "publisher";
                    pattern.Metadata["enterprise_service_bus"] = true;
                    pattern.Metadata["library"] = "NServiceBus";
                    patterns.Add(pattern);
                }
            }
        }

        // Pattern 2: IHandleMessages<T> implementation
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IHandleMessages<")) == true)
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_EventHandler",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.Reliability,
                    implementation: "NServiceBusHandler",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "NServiceBus message handler for subscriber pattern",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["messaging_technology"] = "NServiceBus";
                pattern.Metadata["pattern_type"] = "pub-sub";
                pattern.Metadata["role"] = "subscriber";
                pattern.Metadata["typed_handler"] = true;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectGenericPubSubPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Generic event aggregator / mediator pattern
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            // Look for common pub-sub method names
            if ((invText.Contains(".Publish") || invText.Contains(".Subscribe") || 
                 invText.Contains(".Emit") || invText.Contains(".On(")) &&
                (invText.Contains("Event") || invText.Contains("Message") || invText.Contains("Notification")))
            {
                var isPublisher = invText.Contains(".Publish") || invText.Contains(".Emit");
                var isSubscriber = invText.Contains(".Subscribe") || invText.Contains(".On(");
                
                if (isPublisher || isSubscriber)
                {
                    var pattern = CreatePattern(
                        name: isPublisher ? "Generic_EventPublisher" : "Generic_EventSubscriber",
                        type: PatternType.PublisherSubscriber,
                        category: PatternCategory.General,
                        implementation: "EventAggregator",
                        filePath: filePath,
                        node: inv,
                        sourceCode: sourceCode,
                        bestPractice: $"Generic {(isPublisher ? "publisher" : "subscriber")} pattern for loose coupling",
                        azureUrl: AzurePubSubUrl,
                        context: context
                    );
                    
                    pattern.Metadata["pattern_type"] = "pub-sub";
                    pattern.Metadata["role"] = isPublisher ? "publisher" : "subscriber";
                    pattern.Metadata["in_process"] = true;
                    pattern.Confidence = 0.7f;  // Lower confidence since it's generic
                    patterns.Add(pattern);
                }
            }
        }

        // Pattern 2: Observable pattern (IObservable/IObserver)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var baseTypes = classDecl.BaseList?.Types.Select(t => t.ToString()).ToList() ?? new List<string>();
            
            if (baseTypes.Any(t => t.Contains("IObservable<")))
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_Observable",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "ReactiveX",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Observable pattern (Reactive Extensions) for event streaming",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["pattern_type"] = "observable";
                pattern.Metadata["reactive_extensions"] = true;
                pattern.Metadata["role"] = "publisher";
                patterns.Add(pattern);
            }
            
            if (baseTypes.Any(t => t.Contains("IObserver<")))
            {
                var pattern = CreatePattern(
                    name: $"{classDecl.Identifier.Text}_Observer",
                    type: PatternType.PublisherSubscriber,
                    category: PatternCategory.General,
                    implementation: "ReactiveX",
                    filePath: filePath,
                    node: classDecl,
                    sourceCode: sourceCode,
                    bestPractice: "Observer pattern (Reactive Extensions) for event subscription",
                    azureUrl: AzurePubSubUrl,
                    context: context
                );
                
                pattern.Metadata["pattern_type"] = "observable";
                pattern.Metadata["reactive_extensions"] = true;
                pattern.Metadata["role"] = "subscriber";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    #endregion

    #region Helper Methods

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        SyntaxNode node,
        string sourceCode,
        string bestPractice,
        string azureUrl,
        string? context)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        var endLineNumber = lineSpan.EndLinePosition.Line + 1;
        var content = GetCodeContext(node, sourceCode, lineNumber, endLineNumber);

        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "C#",
            FilePath = filePath,
            LineNumber = lineNumber,
            EndLineNumber = endLineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default"
        };
    }

    // Overload for cases where we have explicit line numbers and content
    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        int lineNumber,
        int endLineNumber,
        string content,
        string bestPractice,
        string azureUrl,
        string? context)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "C#",
            FilePath = filePath,
            LineNumber = lineNumber,
            EndLineNumber = endLineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default"
        };
    }

    // Helper method to get line number from position
    private int GetLineNumber(string sourceCode, int position)
    {
        var lines = sourceCode.Substring(0, Math.Min(position, sourceCode.Length)).Split('\n');
        return lines.Length;
    }

    private string GetCodeContext(SyntaxNode node, string sourceCode, int startLine, int endLine)
    {
        var lines = sourceCode.Split('\n');
        var contextStart = Math.Max(0, startLine - 6);
        var contextEnd = Math.Min(lines.Length - 1, endLine + 4);
        var contextLines = lines.Skip(contextStart).Take(contextEnd - contextStart + 1);
        return string.Join("\n", contextLines).Trim();
    }

    #endregion

    #region AZURE ARCHITECTURE PATTERNS - DATA MANAGEMENT (6 patterns)

    private List<CodePattern> DetectCQRSPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Separate Command and Query interfaces/classes
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        var hasCommandInterface = interfaces.Any(i => i.Identifier.Text.Contains("Command") || i.Identifier.Text.Contains("Write"));
        var hasQueryInterface = interfaces.Any(i => i.Identifier.Text.Contains("Query") || i.Identifier.Text.Contains("Read"));

        if (hasCommandInterface && hasQueryInterface)
        {
                patterns.Add(new CodePattern
                {
                    Name = "CQRS_InterfaceSeparation",
                    Type = PatternType.CQRS,
                    Category = PatternCategory.DataManagement,
                    Implementation = "Command/Query Interface Segregation",
                    FilePath = filePath,
                    LineNumber = 1,
                    EndLineNumber = 1,
                    Content = "Separate Command and Query interfaces detected",
                    BestPractice = "CQRS pattern: Segregate operations that read data from operations that update data using separate interfaces",
                    AzureBestPracticeUrl = "https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs",
                    Context = context ?? "default",
                    Language = "C#"
                });
        }

        // Pattern 2: MediatR Command/Query handlers
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classes)
        {
            var baseTypes = classDecl.BaseList?.Types.Select(t => t.ToString()) ?? Enumerable.Empty<string>();
            if (baseTypes.Any(bt => bt.Contains("IRequestHandler") || bt.Contains("ICommandHandler") || bt.Contains("IQueryHandler")))
            {
                patterns.Add(CreatePattern(
                    name: $"CQRS_{classDecl.Identifier.Text}",
                    type: PatternType.CQRS,
                    category: PatternCategory.DataManagement,
                    implementation: "MediatR CQRS Handler",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "CQRS implementation using MediatR request handlers",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectEventSourcingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Event store, aggregate root, domain events
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            var classText = classDecl.ToString();
            
            // Check for event sourcing indicators
            var hasEventStore = className.Contains("EventStore") || classText.Contains("IEventStore");
            var hasAggregate = className.Contains("Aggregate") && (classText.Contains("UncommittedEvents") || classText.Contains("ApplyEvent"));
            var hasDomainEvent = className.EndsWith("Event") && classDecl.BaseList?.Types.Any(t => t.ToString().Contains("DomainEvent") || t.ToString().Contains("IEvent")) == true;
            
            if (hasEventStore || hasAggregate || hasDomainEvent)
            {
                patterns.Add(CreatePattern(
                    name: $"EventSourcing_{className}",
                    type: PatternType.EventSourcing,
                    category: PatternCategory.DataManagement,
                    implementation: hasEventStore ? "Event Store" : hasAggregate ? "Aggregate Root" : "Domain Event",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Event Sourcing: Use an append-only store to record the full series of events that describe actions taken on data",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectIndexTablePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Secondary index tables, lookup tables
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        
        foreach (var prop in properties)
        {
            var propType = prop.Type.ToString();
            var propName = prop.Identifier.Text;
            
            // Check for index/lookup patterns
            if ((propName.Contains("Index") || propName.Contains("Lookup") || propName.EndsWith("ById") || propName.EndsWith("ByKey")) &&
                (propType.Contains("Dictionary") || propType.Contains("HashSet") || propType.Contains("Index")))
            {
                patterns.Add(CreatePattern(
                    name: $"IndexTable_{propName}",
                    type: PatternType.IndexTable,
                    category: PatternCategory.DataManagement,
                    implementation: propType,
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, prop.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, prop.Span.End),
                    content: prop.ToString(),
                    bestPractice: "Index Table: Create indexes over fields in data stores that queries frequently reference",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/index-table",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMaterializedViewPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Precomputed views, denormalized data, read models
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            // Check for materialized view indicators
            if (className.Contains("View") || className.Contains("ReadModel") || className.Contains("Projection") || className.Contains("Denormalized"))
            {
                var properties = classDecl.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                if (properties.Count() > 5) // Complex view with multiple properties
                {
                    patterns.Add(CreatePattern(
                        name: $"MaterializedView_{className}",
                        type: PatternType.MaterializedView,
                        category: PatternCategory.DataManagement,
                        implementation: "Denormalized Read Model",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                        content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                        bestPractice: "Materialized View: Generate prepopulated views over data when data isn't ideally formatted for queries",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/materialized-view",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectStaticContentHostingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Azure Blob Storage for static content, CDN usage
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            if (invText.Contains("BlobClient") || invText.Contains("CloudBlob") || 
                invText.Contains("StaticFiles") && sourceCode.Contains("app.UseStaticFiles"))
            {
                patterns.Add(CreatePattern(
                    name: "StaticContentHosting_BlobStorage",
                    type: PatternType.StaticContentHosting,
                    category: PatternCategory.DataManagement,
                    implementation: invText.Contains("Blob") ? "Azure Blob Storage" : "Static Files Middleware",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, inv.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, inv.Span.End),
                    content: inv.ToString(),
                    bestPractice: "Static Content Hosting: Deploy static content to cloud-based storage for direct client delivery",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/static-content-hosting",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectValetKeyPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: SAS tokens, temporary access tokens
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            if (invText.Contains("GetSasUri") || invText.Contains("GenerateSasToken") || 
                invText.Contains("SharedAccessSignature") || invText.Contains("SasToken"))
            {
                patterns.Add(CreatePattern(
                    name: "ValetKey_SASToken",
                    type: PatternType.ValetKey,
                    category: PatternCategory.DataManagement,
                    implementation: "Azure SAS Token",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, inv.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, inv.Span.End),
                    content: inv.ToString(),
                    bestPractice: "Valet Key: Use a token to provide clients with restricted direct access to a specific resource",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/valet-key",
                    context: context
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AZURE ARCHITECTURE PATTERNS - DESIGN & IMPLEMENTATION (8 patterns)

    private List<CodePattern> DetectAmbassadorPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Proxy/ambassador service, client-side helper
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Proxy") || className.Contains("Ambassador") || className.Contains("ClientHelper"))
            {
                var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
                var hasHttpCall = methods.Any(m => m.ToString().Contains("HttpClient") || m.ToString().Contains("SendAsync"));
                
                if (hasHttpCall)
                {
                    patterns.Add(CreatePattern(
                        name: $"Ambassador_{className}",
                        type: PatternType.Ambassador,
                        category: PatternCategory.DesignImplementation,
                        implementation: "HTTP Proxy/Ambassador",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                        content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                        bestPractice: "Ambassador: Create helper services that send network requests on behalf of a consumer",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/ambassador",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAntiCorruptionLayerPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Adapter/facade between systems, legacy integration
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Adapter") || className.Contains("Facade") || className.Contains("Legacy") && className.Contains("Integration"))
            {
                patterns.Add(CreatePattern(
                    name: $"AntiCorruptionLayer_{className}",
                    type: PatternType.AntiCorruptionLayer,
                    category: PatternCategory.DesignImplementation,
                    implementation: "Adapter/Facade Layer",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Anti-Corruption Layer: Implement a façade between a modern application and a legacy system",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectBackendsForFrontendsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Separate backend APIs for different clients (Mobile, Web, Desktop)
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if ((className.Contains("Mobile") && className.Contains("Controller")) ||
                (className.Contains("Web") && className.Contains("Controller")) ||
                (className.Contains("Desktop") && className.Contains("Controller")) ||
                className.Contains("BFF"))
            {
                patterns.Add(CreatePattern(
                    name: $"BFF_{className}",
                    type: PatternType.BackendsForFrontends,
                    category: PatternCategory.DesignImplementation,
                    implementation: "Client-Specific Backend",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Backends for Frontends: Create separate backend services for specific frontend applications",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/backends-for-frontends",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectComputeResourceConsolidationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Containerization, Azure Functions, microservices consolidation
        if (sourceCode.Contains("IHostedService") || sourceCode.Contains("BackgroundService"))
        {
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            
            foreach (var classDecl in classes)
            {
                var baseTypes = classDecl.BaseList?.Types.Select(t => t.ToString()) ?? Enumerable.Empty<string>();
                if (baseTypes.Any(bt => bt.Contains("IHostedService") || bt.Contains("BackgroundService")))
                {
                    patterns.Add(CreatePattern(
                        name: $"ComputeConsolidation_{classDecl.Identifier.Text}",
                        type: PatternType.ComputeResourceConsolidation,
                        category: PatternCategory.DesignImplementation,
                        implementation: "Hosted Service Consolidation",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                        content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                        bestPractice: "Compute Resource Consolidation: Consolidate multiple tasks into a single computational unit",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/compute-resource-consolidation",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectExternalConfigurationStorePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Azure App Configuration, Key Vault, external config
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            if (invText.Contains("AddAzureAppConfiguration") || 
                invText.Contains("AddAzureKeyVault") ||
                invText.Contains("ConfigurationClient"))
            {
                patterns.Add(CreatePattern(
                    name: "ExternalConfig_AzureAppConfiguration",
                    type: PatternType.ExternalConfigurationStore,
                    category: PatternCategory.DesignImplementation,
                    implementation: invText.Contains("KeyVault") ? "Azure Key Vault" : "Azure App Configuration",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, inv.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, inv.Span.End),
                    content: inv.ToString(),
                    bestPractice: "External Configuration Store: Move configuration out of the application to a centralized location",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/external-configuration-store",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectGatewayAggregationPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: API Gateway, BFF aggregation, multiple service calls combined
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodText = method.ToString();
            var httpClientCalls = method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Count(inv => inv.ToString().Contains("HttpClient") || inv.ToString().Contains("GetAsync") || inv.ToString().Contains("SendAsync"));
            
            // If method makes multiple HTTP calls, it's likely aggregating
            if (httpClientCalls >= 2)
            {
                patterns.Add(CreatePattern(
                    name: $"GatewayAggregation_{method.Identifier.Text}",
                    type: PatternType.GatewayAggregation,
                    category: PatternCategory.DesignImplementation,
                    implementation: "Multi-Service Aggregation",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                    bestPractice: "Gateway Aggregation: Use a gateway to aggregate multiple individual requests into a single request",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/gateway-aggregation",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectGatewayOffloadingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Middleware for cross-cutting concerns
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Middleware") || className.EndsWith("Handler"))
            {
                var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
                if (methods.Any(m => m.Identifier.Text == "InvokeAsync" || m.Identifier.Text == "Invoke"))
                {
                    patterns.Add(CreatePattern(
                        name: $"GatewayOffloading_{className}",
                        type: PatternType.GatewayOffloading,
                        category: PatternCategory.DesignImplementation,
                        implementation: "Middleware/Handler",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                        content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                        bestPractice: "Gateway Offloading: Offload shared functionality to a gateway proxy",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/gateway-offloading",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectGatewayRoutingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Routing middleware, reverse proxy, Yarp
        if (sourceCode.Contains("MapReverseProxy") || sourceCode.Contains("Yarp") || 
            (sourceCode.Contains("app.Use") && sourceCode.Contains("routing")))
        {
            patterns.Add(CreatePattern(
                name: "GatewayRouting_ReverseProxy",
                type: PatternType.GatewayRouting,
                category: PatternCategory.DesignImplementation,
                implementation: sourceCode.Contains("Yarp") ? "YARP Reverse Proxy" : "Custom Routing",
                filePath: filePath,
                lineNumber: 1,
                endLineNumber: 1,
                content: "Gateway routing configuration detected",
                bestPractice: "Gateway Routing: Route requests to multiple services using a single endpoint",
                azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/gateway-routing",
                context: context
            ));
        }

        return patterns;
    }

    #endregion

    #region AZURE ARCHITECTURE PATTERNS - MESSAGING (10 patterns)

    private List<CodePattern> DetectAsyncRequestReplyPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Async operation with polling/callback
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Check for async HTTP202 Accepted with location header
            if (methodText.Contains("Accepted(") || methodText.Contains("StatusCode(202)") || 
                methodText.Contains("AcceptedAtAction"))
            {
                patterns.Add(CreatePattern(
                    name: $"AsyncRequestReply_{method.Identifier.Text}",
                    type: PatternType.AsyncRequestReply,
                    category: PatternCategory.MessagingPatterns,
                    implementation: "HTTP 202 Accepted Pattern",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                    bestPractice: "Asynchronous Request-Reply: Decouple backend processing from frontend with async responses",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/async-request-reply",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectClaimCheckPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Store large payload, send reference token
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Check for blob storage + message queue pattern
            if ((methodText.Contains("BlobClient") || methodText.Contains("UploadAsync")) &&
                (methodText.Contains("SendMessageAsync") || methodText.Contains("PublishAsync")))
            {
                patterns.Add(CreatePattern(
                    name: $"ClaimCheck_{method.Identifier.Text}",
                    type: PatternType.ClaimCheck,
                    category: PatternCategory.MessagingPatterns,
                    implementation: "Blob Storage + Message Reference",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                    bestPractice: "Claim Check: Split large message into claim check and payload to avoid overwhelming message bus",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/claim-check",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectChoreographyPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Event-driven, domain events, no central orchestrator
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("EventHandler") || className.Contains("DomainEventHandler"))
            {
                var baseTypes = classDecl.BaseList?.Types.Select(t => t.ToString()) ?? Enumerable.Empty<string>();
                if (baseTypes.Any(bt => bt.Contains("INotificationHandler") || bt.Contains("IEventHandler")))
                {
                    patterns.Add(CreatePattern(
                        name: $"Choreography_{className}",
                        type: PatternType.Choreography,
                        category: PatternCategory.MessagingPatterns,
                        implementation: "Event-Driven Domain Events",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                        content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                        bestPractice: "Choreography: Let services decide when and how business operations are processed via events",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/choreography",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCompetingConsumersPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Multiple consumers processing messages from same queue
        if (sourceCode.Contains("ProcessMessageAsync") || sourceCode.Contains("ReceiveMessageAsync"))
        {
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            
            foreach (var classDecl in classes)
            {
                var className = classDecl.Identifier.Text;
                
                if (className.Contains("Consumer") || className.Contains("Processor") || className.Contains("Handler"))
                {
                    var methods = classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    if (methods.Any(m => m.Identifier.Text.Contains("Process") || m.Identifier.Text.Contains("Handle")))
                    {
                        patterns.Add(CreatePattern(
                            name: $"CompetingConsumers_{className}",
                            type: PatternType.CompetingConsumers,
                            category: PatternCategory.MessagingPatterns,
                            implementation: "Message Queue Consumer",
                            filePath: filePath,
                            lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                            endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                            content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                            bestPractice: "Competing Consumers: Enable multiple concurrent consumers to process messages from the same channel",
                            azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/competing-consumers",
                            context: context
                        ));
                    }
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPipesAndFiltersPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Pipeline pattern, middleware chain, processing stages
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Pipeline") || className.Contains("Filter") || className.Contains("Stage"))
            {
                patterns.Add(CreatePattern(
                    name: $"PipesAndFilters_{className}",
                    type: PatternType.PipesAndFilters,
                    category: PatternCategory.MessagingPatterns,
                    implementation: "Processing Pipeline",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Pipes and Filters: Break down complex processing into a series of reusable elements",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/pipes-and-filters",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPriorityQueuePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Priority-based message processing
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var classText = classDecl.ToString();
            
            if (classText.Contains("PriorityQueue") || (classText.Contains("Priority") && classText.Contains("Queue")))
            {
                patterns.Add(CreatePattern(
                    name: $"PriorityQueue_{classDecl.Identifier.Text}",
                    type: PatternType.PriorityQueue,
                    category: PatternCategory.MessagingPatterns,
                    implementation: "Priority-Based Processing",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Priority Queue: Prioritize requests so higher-priority requests are processed more quickly",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/priority-queue",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectQueueBasedLoadLevelingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Queue as buffer between producer and consumer
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodText = method.ToString();
            
            // Check for enqueue followed by async processing
            if ((methodText.Contains("SendMessageAsync") || methodText.Contains("EnqueueAsync") || methodText.Contains("QueueBackgroundWorkItem")) &&
                method.Modifiers.Any(m => m.Text == "async"))
            {
                patterns.Add(CreatePattern(
                    name: $"QueueLoadLeveling_{method.Identifier.Text}",
                    type: PatternType.QueueBasedLoadLeveling,
                    category: PatternCategory.MessagingPatterns,
                    implementation: "Queue Buffer Pattern",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                    bestPractice: "Queue-Based Load Leveling: Use a queue as a buffer to smooth intermittent heavy loads",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/queue-based-load-leveling",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSchedulerAgentSupervisorPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Coordinator that schedules, monitors, and recovers distributed work
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if ((className.Contains("Scheduler") || className.Contains("Coordinator")) && 
                (className.Contains("Supervisor") || className.Contains("Monitor")))
            {
                patterns.Add(CreatePattern(
                    name: $"SchedulerAgentSupervisor_{className}",
                    type: PatternType.SchedulerAgentSupervisor,
                    category: PatternCategory.MessagingPatterns,
                    implementation: "Distributed Work Coordinator",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Scheduler Agent Supervisor: Coordinate distributed actions across services and resources",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/scheduler-agent-supervisor",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSequentialConvoyPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Session-based message ordering
        if (sourceCode.Contains("SessionId") || sourceCode.Contains("PartitionKey"))
        {
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            
            foreach (var method in methods)
            {
                var methodText = method.ToString();
                
                if (methodText.Contains("ProcessSessionMessageAsync") || 
                    (methodText.Contains("SessionId") && methodText.Contains("Process")))
                {
                    patterns.Add(CreatePattern(
                        name: $"SequentialConvoy_{method.Identifier.Text}",
                        type: PatternType.SequentialConvoy,
                        category: PatternCategory.MessagingPatterns,
                        implementation: "Session-Based Ordering",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                        content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                        bestPractice: "Sequential Convoy: Process related messages in order without blocking other groups",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/sequential-convoy",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMessagingBridgePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Bridge between different messaging systems
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Bridge") && (className.Contains("Message") || className.Contains("Event")))
            {
                patterns.Add(CreatePattern(
                    name: $"MessagingBridge_{className}",
                    type: PatternType.MessagingBridge,
                    category: PatternCategory.MessagingPatterns,
                    implementation: "Message System Bridge",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Messaging Bridge: Enable communication between incompatible messaging systems",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/messaging-bridge",
                    context: context
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AZURE ARCHITECTURE PATTERNS - RELIABILITY & RESILIENCY (7 patterns)

    private List<CodePattern> DetectBulkheadPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Polly Bulkhead, resource isolation
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            if (invText.Contains("BulkheadAsync") || invText.Contains("BulkheadPolicy") || 
                invText.Contains("SemaphoreSlim") && sourceCode.Contains("maxConcurrentCalls"))
            {
                patterns.Add(CreatePattern(
                    name: "Bulkhead_Isolation",
                    type: PatternType.Bulkhead,
                    category: PatternCategory.ResiliencyPatterns,
                    implementation: invText.Contains("Polly") ? "Polly Bulkhead" : "SemaphoreSlim Isolation",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, inv.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, inv.Span.End),
                    content: inv.ToString(),
                    bestPractice: "Bulkhead: Isolate elements into pools so if one fails, others continue to function",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/bulkhead",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCircuitBreakerPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Polly Circuit Breaker
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            if (invText.Contains("CircuitBreakerAsync") || invText.Contains("CircuitBreakerPolicy") || 
                invText.Contains("AdvancedCircuitBreakerAsync"))
            {
                patterns.Add(CreatePattern(
                    name: "CircuitBreaker_Polly",
                    type: PatternType.Resilience,
                    category: PatternCategory.ResiliencyPatterns,
                    implementation: "Polly Circuit Breaker",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, inv.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, inv.Span.End),
                    content: inv.ToString(),
                    bestPractice: "Circuit Breaker: Handle faults that might take variable time to fix when connecting to remote resources",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCompensatingTransactionPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Compensating actions for failed transactions
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodName = method.Identifier.Text;
            
            if (methodName.Contains("Compensate") || methodName.Contains("Rollback") || methodName.Contains("Undo"))
            {
                var methodText = method.ToString();
                if (methodText.Contains("try") && methodText.Contains("catch"))
                {
                    patterns.Add(CreatePattern(
                        name: $"CompensatingTransaction_{methodName}",
                        type: PatternType.CompensatingTransaction,
                        category: PatternCategory.ResiliencyPatterns,
                        implementation: "Compensating Action",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                        content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                        bestPractice: "Compensating Transaction: Undo work performed by a series of steps in an eventually consistent operation",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectLeaderElectionPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Leader election coordination
        if (sourceCode.Contains("LeaderElection") || sourceCode.Contains("BlobLease") || 
            sourceCode.Contains("AcquireLeaseAsync"))
        {
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            
            foreach (var method in methods)
            {
                var methodText = method.ToString();
                
                if (methodText.Contains("AcquireLeaseAsync") || methodText.Contains("TryAcquireLeadershipAsync"))
                {
                    patterns.Add(CreatePattern(
                        name: $"LeaderElection_{method.Identifier.Text}",
                        type: PatternType.LeaderElection,
                        category: PatternCategory.ResiliencyPatterns,
                        implementation: methodText.Contains("Blob") ? "Azure Blob Lease" : "Custom Leader Election",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                        content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                        bestPractice: "Leader Election: Coordinate actions by electing one instance as the leader",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/leader-election",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectGeodePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Multi-region deployment, geographic distribution
        if (sourceCode.Contains("TrafficManager") || sourceCode.Contains("CosmosDB") && sourceCode.Contains("MultiRegion") ||
            sourceCode.Contains("FrontDoor"))
        {
            patterns.Add(CreatePattern(
                name: "Geode_MultiRegion",
                type: PatternType.Geode,
                category: PatternCategory.ResiliencyPatterns,
                implementation: sourceCode.Contains("CosmosDB") ? "Cosmos DB Multi-Region" : "Azure Traffic Manager",
                filePath: filePath,
                lineNumber: 1,
                endLineNumber: 1,
                content: "Multi-region deployment configuration detected",
                bestPractice: "Geode: Deploy backend services into geographical nodes that can service any client request in any region",
                azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/geodes",
                context: context
            ));
        }

        return patterns;
    }

    private List<CodePattern> DetectDeploymentStampsPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Scale units, stamp deployment
        if (sourceCode.Contains("ScaleUnit") || sourceCode.Contains("Stamp") || sourceCode.Contains("DeploymentUnit"))
        {
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            
            foreach (var classDecl in classes)
            {
                var className = classDecl.Identifier.Text;
                
                if (className.Contains("ScaleUnit") || className.Contains("Stamp") || className.Contains("DeploymentUnit"))
                {
                    patterns.Add(CreatePattern(
                        name: $"DeploymentStamps_{className}",
                        type: PatternType.DeploymentStamps,
                        category: PatternCategory.ResiliencyPatterns,
                        implementation: "Scale Unit / Stamp",
                        filePath: filePath,
                        lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                        content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                        bestPractice: "Deployment Stamps: Deploy multiple independent copies of application components",
                        azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/deployment-stamp",
                        context: context
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectThrottlingPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Rate limiting, throttling middleware
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            if (invText.Contains("AddRateLimiter") || invText.Contains("EnableRateLimiting") || 
                invText.Contains("ThrottlingTroll") || invText.Contains("RateLimitPartition"))
            {
                patterns.Add(CreatePattern(
                    name: "Throttling_RateLimiter",
                    type: PatternType.Throttling,
                    category: PatternCategory.ResiliencyPatterns,
                    implementation: "ASP.NET Core Rate Limiter",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, inv.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, inv.Span.End),
                    content: inv.ToString(),
                    bestPractice: "Throttling: Control the consumption of resources used by an application, tenant, or service",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/throttling",
                    context: context
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AZURE ARCHITECTURE PATTERNS - SECURITY (2 patterns)

    private List<CodePattern> DetectFederatedIdentityPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: External identity providers (Azure AD, OAuth, OIDC)
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var inv in invocations)
        {
            var invText = inv.ToString();
            
            if (invText.Contains("AddMicrosoftIdentityWebApp") || invText.Contains("AddJwtBearer") || 
                invText.Contains("AddOpenIdConnect") || invText.Contains("AddAzureAD"))
            {
                patterns.Add(CreatePattern(
                    name: "FederatedIdentity_ExternalProvider",
                    type: PatternType.FederatedIdentity,
                    category: PatternCategory.SecurityPatterns,
                    implementation: invText.Contains("MicrosoftIdentity") ? "Azure AD / Entra ID" : 
                                    invText.Contains("JwtBearer") ? "JWT Bearer" : "OpenID Connect",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, inv.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, inv.Span.End),
                    content: inv.ToString(),
                    bestPractice: "Federated Identity: Delegate authentication to an external identity provider",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/federated-identity",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectQuarantinePattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Input validation, malware scanning, content verification
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        
        foreach (var method in methods)
        {
            var methodName = method.Identifier.Text;
            
            if (methodName.Contains("Quarantine") || methodName.Contains("Validate") && methodName.Contains("External") ||
                methodName.Contains("ScanForMalware") || methodName.Contains("VerifyContent"))
            {
                var methodText = method.ToString();
                patterns.Add(CreatePattern(
                    name: $"Quarantine_{methodName}",
                    type: PatternType.Quarantine,
                    category: PatternCategory.SecurityPatterns,
                    implementation: "External Asset Validation",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(300, method.ToString().Length)),
                    bestPractice: "Quarantine: Ensure external assets meet quality level before consumption",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/quarantine",
                    context: context
                ));
            }
        }

        return patterns;
    }

    #endregion

    #region AZURE ARCHITECTURE PATTERNS - OPERATIONAL (3 patterns)

    private List<CodePattern> DetectSidecarPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Sidecar container, companion process
        if (filePath.EndsWith("Dockerfile", StringComparison.OrdinalIgnoreCase) || 
            filePath.EndsWith("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
            filePath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceCode.Contains("sidecar") || sourceCode.Contains("dapr") || 
                (sourceCode.Contains("container") && sourceCode.Contains("companion")))
            {
                patterns.Add(CreatePattern(
                    name: "Sidecar_Container",
                    type: PatternType.Sidecar,
                    category: PatternCategory.OperationalPatterns,
                    implementation: sourceCode.Contains("dapr") ? "Dapr Sidecar" : "Container Sidecar",
                    filePath: filePath,
                    lineNumber: 1,
                    endLineNumber: 1,
                    content: "Sidecar deployment configuration detected",
                    bestPractice: "Sidecar: Deploy components into a separate process or container for isolation and encapsulation",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/sidecar",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectStranglerFigPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Incremental migration, feature toggle, routing between old/new
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Legacy") && (className.Contains("Proxy") || className.Contains("Router") || className.Contains("Facade")))
            {
                patterns.Add(CreatePattern(
                    name: $"StranglerFig_{className}",
                    type: PatternType.StranglerFig,
                    category: PatternCategory.OperationalPatterns,
                    implementation: "Legacy System Migration Proxy",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Strangler Fig: Incrementally migrate a legacy system by replacing functionality with new services",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig",
                    context: context
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSagaPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: Distributed transaction coordination, saga orchestration
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        
        foreach (var classDecl in classes)
        {
            var className = classDecl.Identifier.Text;
            
            if (className.Contains("Saga") || (className.Contains("Orchestrator") && className.Contains("Transaction")))
            {
                patterns.Add(CreatePattern(
                    name: $"Saga_{className}",
                    type: PatternType.Saga,
                    category: PatternCategory.OperationalPatterns,
                    implementation: className.Contains("Orchestrator") ? "Saga Orchestrator" : "Saga Coordinator",
                    filePath: filePath,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Saga: Manage data consistency across microservices in distributed transaction scenarios",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/saga",
                    context: context
                ));
            }
        }

        return patterns;
    }

    #endregion
}

