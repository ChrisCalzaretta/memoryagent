using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for validating Azure best practices in code
/// </summary>
public class BestPracticeValidationService : IBestPracticeValidationService
{
    private readonly IPatternIndexingService _patternService;
    private readonly IGraphService _graphService;
    private readonly ILogger<BestPracticeValidationService> _logger;

    // Azure best practices catalog (based on AZURE_PATTERNS_COMPREHENSIVE.md)
    private static readonly Dictionary<string, (PatternType Type, PatternCategory Category, string Recommendation, string AzureUrl)> BestPracticesCatalog = new()
    {
        // Caching patterns
        ["cache-aside"] = (PatternType.Caching, PatternCategory.Performance, 
            "Implement Cache-Aside pattern to reduce database load and improve response times.", 
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/cache-aside"),
        ["distributed-cache"] = (PatternType.Caching, PatternCategory.Performance,
            "Use distributed caching (Redis/Azure Cache) for scalability across multiple instances.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/caching"),
        ["response-cache"] = (PatternType.Caching, PatternCategory.Performance,
            "Implement response caching for HTTP endpoints to improve API performance.",
            "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response"),
        
        // Resilience patterns
        ["retry-logic"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Add retry policies (Polly) for transient fault handling in external service calls.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/retry"),
        ["circuit-breaker"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Implement circuit breaker pattern to prevent cascading failures.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker"),
        ["timeout-policy"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Configure timeout policies to prevent resource exhaustion from slow operations.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults"),
        
        // Validation patterns
        ["input-validation"] = (PatternType.Validation, PatternCategory.Security,
            "Add input validation (DataAnnotations/FluentValidation) to prevent injection attacks.",
            "https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation"),
        ["model-validation"] = (PatternType.Validation, PatternCategory.Security,
            "Implement model validation to ensure data integrity and business rules.",
            "https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation"),
        
        // Security patterns
        ["authentication"] = (PatternType.Security, PatternCategory.Security,
            "Implement JWT or Azure AD authentication for API security.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-security"),
        ["authorization"] = (PatternType.Security, PatternCategory.Security,
            "Add role-based or policy-based authorization to protect resources.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction"),
        ["data-encryption"] = (PatternType.Security, PatternCategory.Security,
            "Encrypt sensitive data at rest and in transit.",
            "https://learn.microsoft.com/en-us/azure/architecture/framework/security/design-storage"),
        
        // API Design patterns
        ["pagination"] = (PatternType.ApiDesign, PatternCategory.Performance,
            "Implement pagination for large result sets to improve performance.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#support-partial-responses-for-large-binary-resources"),
        ["versioning"] = (PatternType.ApiDesign, PatternCategory.Operational,
            "Use API versioning to maintain backward compatibility.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#versioning-a-restful-web-api"),
        ["rate-limiting"] = (PatternType.ApiDesign, PatternCategory.Reliability,
            "Add rate limiting to protect APIs from abuse and ensure fair usage.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/throttling"),
        
        // Monitoring patterns
        ["health-checks"] = (PatternType.Monitoring, PatternCategory.Reliability,
            "Implement health check endpoints for monitoring and orchestration.",
            "https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks"),
        ["structured-logging"] = (PatternType.Monitoring, PatternCategory.Operational,
            "Use structured logging (Serilog/Application Insights) for better diagnostics.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring"),
        ["metrics"] = (PatternType.Monitoring, PatternCategory.Operational,
            "Implement metrics collection for performance monitoring.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring"),
        
        // Background Jobs patterns
        ["background-tasks"] = (PatternType.BackgroundJobs, PatternCategory.Performance,
            "Use IHostedService or Hangfire for background task processing.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services"),
        ["message-queue"] = (PatternType.BackgroundJobs, PatternCategory.Performance,
            "Implement message queues (Azure Service Bus/RabbitMQ) for async processing.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/queue-based-load-leveling"),
        
        // Configuration patterns
        ["configuration-management"] = (PatternType.Configuration, PatternCategory.Operational,
            "Use Azure App Configuration or Key Vault for centralized configuration.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/configuration"),
        ["feature-flags"] = (PatternType.Configuration, PatternCategory.Operational,
            "Implement feature flags for controlled rollouts and A/B testing.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/feature-flags"),
        
        // AG-UI Integration patterns (Agent UI Protocol)
        ["agui-endpoint"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Use MapAGUI() to deploy agents as HTTP endpoints accessible by web/mobile clients with SSE streaming.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/getting-started"),
        ["agui-streaming"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Implement SSE streaming for real-time agent responses instead of blocking HTTP calls.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-thread-management"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Use AG-UI protocol thread management to maintain conversation context across requests.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-agentic-chat"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Implement AG-UI Feature 1: Agentic Chat with automatic tool calling for enhanced interactions.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/getting-started"),
        ["agui-backend-tools"] = (PatternType.AGUI, PatternCategory.ToolIntegration,
            "Implement AG-UI Feature 2: Backend Tool Rendering - execute tools server-side with streamed results.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/backend-tools"),
        ["agui-human-loop"] = (PatternType.AGUI, PatternCategory.HumanInLoop,
            "Implement AG-UI Feature 3: Human-in-the-Loop approval workflows for sensitive operations.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/human-in-loop"),
        ["agui-generative-ui"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Implement AG-UI Feature 4: Agentic Generative UI with async tools and progress updates.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/generative-ui"),
        ["agui-tool-ui"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Implement AG-UI Feature 5: Tool-based Generative UI for custom component rendering.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/generative-ui"),
        ["agui-shared-state"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Implement AG-UI Feature 6: Shared State for bidirectional client-server synchronization.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/shared-state"),
        ["agui-predictive-updates"] = (PatternType.AGUI, PatternCategory.Performance,
            "Implement AG-UI Feature 7: Predictive State Updates for optimistic UI responsiveness.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/shared-state"),
        ["agui-middleware"] = (PatternType.AGUI, PatternCategory.Interceptors,
            "Use AG-UI middleware pattern for approvals, state management, and custom logic.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-protocol-events"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Implement standardized AG-UI protocol events for reliable agent-client communication.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-copilotkit"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Use CopilotKit client library for polished AG-UI experiences supporting all 7 features.",
            "https://docs.copilotkit.ai/"),
        
        // AG-UI Enhanced Patterns (from deep research)
        ["agui-frontend-tools"] = (PatternType.AGUI, PatternCategory.ToolIntegration,
            "Implement frontend tool calls for client-side execution (GPS, camera, localStorage access).",
            "https://learn.microsoft.com/cs-cz/agent-framework/integrations/ag-ui/frontend-tools"),
        ["agui-multimodal-files"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Support multimodal file/attachment inputs for rich agent interactions.",
            "https://docs.ag-ui.com/"),
        ["agui-multimodal-images"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Support image inputs for visual agent capabilities and multimodal AI.",
            "https://docs.ag-ui.com/"),
        ["agui-multimodal-audio"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Support audio/voice inputs and transcripts for voice-enabled agents.",
            "https://docs.ag-ui.com/"),
        ["agui-complete-events"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Implement all 16 AG-UI event types for comprehensive protocol compliance.",
            "https://docs.ag-ui.com/concepts/architecture"),
        ["agui-state-delta"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Use JSON Patch for efficient incremental state updates instead of full snapshots.",
            "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management"),
        ["agui-event-sourced"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Implement event-sourced state management for history and collaborative editing.",
            "https://docs.ag-ui.com/"),
        ["agui-conflict-resolution"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Handle state conflicts from concurrent client/server updates gracefully.",
            "https://docs.ag-ui.com/"),
        ["agui-cancellation"] = (PatternType.AGUI, PatternCategory.Reliability,
            "Support cancellation tokens to allow stopping agent execution mid-flow.",
            "https://docs.ag-ui.com/"),
        ["agui-pause-resume"] = (PatternType.AGUI, PatternCategory.HumanInLoop,
            "Implement pause/resume for human intervention without losing state.",
            "https://docs.ag-ui.com/"),
        ["agui-retry"] = (PatternType.AGUI, PatternCategory.Reliability,
            "Support retrying failed operations while maintaining conversation context.",
            "https://docs.ag-ui.com/"),
        ["agui-websocket"] = (PatternType.AGUI, PatternCategory.Performance,
            "Use WebSocket transport for bidirectional real-time communication.",
            "https://docs.ag-ui.com/concepts/architecture"),
        
        // 100% Coverage - Final Patterns
        ["agui-copilotkit-hooks"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Use CopilotKit React hooks (useCopilotChat, useCopilotAction, useCopilotReadable) for AG-UI integration.",
            "https://docs.copilotkit.ai/"),
        ["agui-error-handling"] = (PatternType.AGUI, PatternCategory.Reliability,
            "Implement comprehensive error handling for AG-UI operations with logging and user feedback.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-exponential-backoff"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Use exponential backoff for AG-UI connection retries to handle transient failures.",
            "https://learn.microsoft.com/en-us/azure/architecture/best-practices/retry-service-specific"),
        ["agui-circuit-breaker"] = (PatternType.Resilience, PatternCategory.Reliability,
            "Implement circuit breaker to prevent cascading failures in AG-UI agents.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker"),
        ["agui-opentelemetry"] = (PatternType.AGUI, PatternCategory.Operational,
            "Use OpenTelemetry to trace AG-UI agent runs, tool calls, and streaming operations.",
            "https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable"),
        ["agui-structured-logging"] = (PatternType.AGUI, PatternCategory.Operational,
            "Use structured logging to capture AG-UI events with context (thread IDs, tool names, user IDs).",
            "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging"),
        ["agui-app-insights"] = (PatternType.AGUI, PatternCategory.Operational,
            "Use Application Insights to monitor AG-UI performance, errors, and usage patterns.",
            "https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview"),
        ["agui-json-schema"] = (PatternType.AGUI, PatternCategory.Reliability,
            "Define JSON Schemas for AG-UI shared state to ensure type safety and validate transitions.",
            "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management"),
        ["agui-typed-state-schema"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Use ChatResponseFormat.ForJsonSchema<T>() for type-safe structured state updates.",
            "https://learn.microsoft.com/tr-tr/agent-framework/integrations/ag-ui/state-management"),
        ["agui-thread-persistence"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Persist AG-UI thread IDs to maintain conversation context across sessions and reconnections.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-thread-management"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Centralize thread management for AG-UI to handle creation, storage, and cleanup.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-async-enumerable"] = (PatternType.AGUI, PatternCategory.Performance,
            "Use IAsyncEnumerable<T> for memory-efficient streaming of AG-UI events.",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-event-handlers"] = (PatternType.AGUI, PatternCategory.AIAgents,
            "Implement event handlers to process AG-UI streaming updates (text deltas, tool progress, state changes).",
            "https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/"),
        ["agui-jwt-auth"] = (PatternType.Security, PatternCategory.Security,
            "Secure AG-UI endpoints with JWT authentication to verify user identity.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/authentication/"),
        ["agui-authorization"] = (PatternType.Security, PatternCategory.Security,
            "Implement authorization policies to control AG-UI agent and tool access.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/authorization/"),
        ["agui-api-key"] = (PatternType.Security, PatternCategory.Security,
            "Use API keys for service-to-service AG-UI authentication, stored in Azure Key Vault.",
            "https://learn.microsoft.com/en-us/azure/key-vault/"),
        ["agui-rate-limiting"] = (PatternType.AGUI, PatternCategory.Performance,
            "Implement rate limiting on AG-UI endpoints to prevent abuse and ensure fair resource allocation.",
            "https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit"),
        ["agui-concurrency-limit"] = (PatternType.AGUI, PatternCategory.Performance,
            "Limit concurrent AG-UI connections per user to prevent resource exhaustion.",
            "https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit"),
        ["agui-session-management"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Use session management to associate AG-UI thread IDs with user sessions.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state"),
        ["agui-distributed-session"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Use Redis for distributed session storage to enable AG-UI scalability.",
            "https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/"),
        ["agui-session-timeout"] = (PatternType.AGUI, PatternCategory.StateManagement,
            "Implement session timeouts for AG-UI conversations to clean up inactive threads.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state")
    };

    public BestPracticeValidationService(
        IPatternIndexingService patternService,
        IGraphService graphService,
        ILogger<BestPracticeValidationService> logger)
    {
        _patternService = patternService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<BestPracticeValidationResponse> ValidateBestPracticesAsync(
        BestPracticeValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating best practices for context: {Context}", request.Context);

        var response = new BestPracticeValidationResponse
        {
            Context = request.Context
        };

        // Determine which practices to check
        var practicesToCheck = request.BestPractices?.Any() == true
            ? request.BestPractices.Where(p => BestPracticesCatalog.ContainsKey(p)).ToList()
            : BestPracticesCatalog.Keys.ToList();

        response.TotalPracticesChecked = practicesToCheck.Count;

        // Check each practice
        foreach (var practice in practicesToCheck)
        {
            var practiceInfo = BestPracticesCatalog[practice];
            var result = await ValidatePracticeAsync(
                practice,
                practiceInfo.Type,
                practiceInfo.Category,
                practiceInfo.Recommendation,
                practiceInfo.AzureUrl,
                request,
                cancellationToken);

            response.Results.Add(result);

            if (result.Implemented)
            {
                response.PracticesImplemented++;
            }
            else
            {
                response.PracticesMissing++;
            }
        }

        // Calculate overall score
        response.OverallScore = response.TotalPracticesChecked > 0
            ? (float)response.PracticesImplemented / response.TotalPracticesChecked
            : 0f;

        _logger.LogInformation(
            "Validation complete for {Context}: {Score:P0} ({Implemented}/{Total} practices implemented)",
            request.Context, response.OverallScore, response.PracticesImplemented, response.TotalPracticesChecked);

        return response;
    }

    private async Task<BestPracticeResult> ValidatePracticeAsync(
        string practiceName,
        PatternType patternType,
        PatternCategory category,
        string recommendation,
        string azureUrl,
        BestPracticeValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = new BestPracticeResult
        {
            Practice = practiceName,
            PatternType = patternType,
            Category = category,
            Recommendation = recommendation,
            AzureUrl = azureUrl
        };

        try
        {
            // Query Neo4j for patterns of this type
            var patterns = await _graphService.GetPatternsByTypeAsync(patternType, request.Context, cancellationToken);

            // Filter by practice name (fuzzy matching)
            var matchingPatterns = patterns
                .Where(p => p.Confidence >= request.MinimumConfidence)
                .Where(p => MatchesPractice(p, practiceName))
                .OrderByDescending(p => p.Confidence)
                .ToList();

            result.Count = matchingPatterns.Count;
            result.Implemented = matchingPatterns.Any();
            result.AverageConfidence = matchingPatterns.Any()
                ? matchingPatterns.Average(p => p.Confidence)
                : 0f;

            // Add examples if requested
            if (request.IncludeExamples && matchingPatterns.Any())
            {
                var examplePatterns = matchingPatterns
                    .Take(request.MaxExamplesPerPractice)
                    .ToList();

                foreach (var pattern in examplePatterns)
                {
                    result.Examples.Add(new PatternExample
                    {
                        FilePath = pattern.FilePath,
                        LineNumber = pattern.LineNumber,
                        Name = pattern.Name,
                        Implementation = pattern.Implementation,
                        CodeSnippet = TruncateCode(pattern.Content, 200),
                        Confidence = pattern.Confidence
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating practice: {Practice}", practiceName);
        }

        return result;
    }

    private bool MatchesPractice(CodePattern pattern, string practiceName)
    {
        // Map practice names to pattern properties
        var practiceKeywords = practiceName.ToLowerInvariant().Split('-', '_');
        var patternText = $"{pattern.Name} {pattern.BestPractice} {pattern.Implementation}".ToLowerInvariant();

        return practiceKeywords.Any(keyword => patternText.Contains(keyword));
    }

    private string TruncateCode(string code, int maxLength)
    {
        if (code.Length <= maxLength)
        {
            return code;
        }

        return code.Substring(0, maxLength - 3) + "...";
    }

    public Task<List<string>> GetAvailableBestPracticesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(BestPracticesCatalog.Keys.ToList());
    }
}

