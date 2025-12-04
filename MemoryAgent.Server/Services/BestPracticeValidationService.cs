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
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state"),
        
        // AI Agent Core Patterns (23 patterns)
        // Category 1: Prompt Engineering & Guardrails (3)
        ["ai-system-prompt"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Define system prompts as constants or configuration for agent behavior control.",
            "https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/prompt-engineering"),
        ["ai-prompt-template"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Use prompt templates (Semantic Kernel, Microsoft Guidance) for reusable, parameterized prompts.",
            "https://learn.microsoft.com/en-us/semantic-kernel/"),
        ["ai-guardrail"] = (PatternType.Security, PatternCategory.Security,
            "Implement content safety guardrails using Azure Content Safety or prompt policies.",
            "https://learn.microsoft.com/en-us/azure/ai-services/content-safety/"),
        
        // Category 2: Memory & State (3)
        ["ai-short-term-memory"] = (PatternType.AgentLightning, PatternCategory.StateManagement,
            "Maintain chat history buffers for multi-turn agent conversations.",
            "https://learn.microsoft.com/en-us/semantic-kernel/"),
        ["ai-long-term-memory"] = (PatternType.AgentLightning, PatternCategory.StateManagement,
            "Use vector stores (Azure AI Search, Qdrant) for long-term agent memory and knowledge.",
            "https://learn.microsoft.com/en-us/azure/search/"),
        ["ai-user-profile-memory"] = (PatternType.AgentLightning, PatternCategory.StateManagement,
            "Store user/agent profiles for personalized agent interactions.",
            "https://learn.microsoft.com/en-us/semantic-kernel/"),
        
        // Category 3: Tools & Function Calling (3)
        ["ai-tool-registration"] = (PatternType.AgentLightning, PatternCategory.ToolIntegration,
            "Register agent tools/functions using [KernelFunction] or similar patterns.",
            "https://learn.microsoft.com/en-us/semantic-kernel/"),
        ["ai-tool-routing"] = (PatternType.AgentLightning, PatternCategory.ToolIntegration,
            "Implement tool routing to dispatch LLM function calls to appropriate handlers.",
            "https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/prompt-engineering"),
        ["ai-external-tool"] = (PatternType.AgentLightning, PatternCategory.ToolIntegration,
            "Create tools that integrate external services (APIs, databases, file systems).",
            "https://learn.microsoft.com/en-us/semantic-kernel/"),
        
        // Category 4: Planning & Autonomy (4)
        ["ai-task-planner"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Implement task planning to decompose goals into executable steps.",
            "https://learn.microsoft.com/en-us/semantic-kernel/"),
        ["ai-action-loop"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Use ReAct loops (Reason → Act → Observe) for autonomous multi-step agent behavior.",
            "https://arxiv.org/abs/2210.03629"),
        ["ai-multi-agent"] = (PatternType.AgentLightning, PatternCategory.MultiAgentOrchestration,
            "Orchestrate multiple specialized agents (Planner, Executor, Critic) for complex tasks.",
            "https://microsoft.github.io/autogen/"),
        ["ai-self-reflection"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Enable agents to critique and improve their own outputs through reflection.",
            "https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/prompt-engineering"),
        
        // Category 5: RAG & Knowledge (3)
        ["ai-embedding-generation"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Generate embeddings for semantic search and RAG using Azure OpenAI.",
            "https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/embeddings"),
        ["ai-rag-pipeline"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Implement RAG pipelines (Retrieve → Augment → Generate) for knowledge-enhanced agents.",
            "https://learn.microsoft.com/en-us/azure/search/"),
        ["ai-rag-orchestration"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Use conditional RAG, hybrid search, and reranking for optimized retrieval.",
            "https://learn.microsoft.com/en-us/azure/search/"),
        
        // Category 6: Safety & Governance (5)
        ["ai-content-moderation"] = (PatternType.Security, PatternCategory.Security,
            "Moderate harmful content using Azure Content Safety before and after LLM calls.",
            "https://learn.microsoft.com/en-us/azure/ai-services/content-safety/"),
        ["ai-pii-scrubber"] = (PatternType.Security, PatternCategory.Security,
            "Detect and redact PII using Microsoft Presidio or Azure AI Language before LLM calls.",
            "https://github.com/microsoft/presidio"),
        ["ai-tenant-boundary"] = (PatternType.Security, PatternCategory.Security,
            "Enforce tenant data boundaries in multi-tenant AI systems to prevent data leakage.",
            "https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/overview"),
        ["ai-token-budget"] = (PatternType.AgentLightning, PatternCategory.Cost,
            "Enforce token budgets per user/agent/project to prevent cost overruns.",
            "https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/manage-costs"),
        ["ai-redacted-logging"] = (PatternType.Security, PatternCategory.Operational,
            "Redact PII from logs when logging prompts and responses for debugging.",
            "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging"),
        
        // Category 7: FinOps & Cost Control (2)
        ["ai-token-metering"] = (PatternType.AgentLightning, PatternCategory.Cost,
            "Track token usage per user, agent, and project for cost attribution and chargeback.",
            "https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/manage-costs"),
        ["ai-cost-guardrail"] = (PatternType.AgentLightning, PatternCategory.Cost,
            "Implement budget guardrails with alerts and auto-disable to prevent runaway costs.",
            "https://learn.microsoft.com/en-us/azure/cost-management-billing/"),
        
        // Category 8: Observability & Evaluation (7 new - CRITICAL GAPS FILLED)
        ["ai-agent-tracing"] = (PatternType.AgentLightning, PatternCategory.Operational,
            "Implement OpenTelemetry for end-to-end agent tracing. Track LLM calls, tool executions, and decision flows.",
            "https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-overview"),
        ["ai-correlated-logging"] = (PatternType.AgentLightning, PatternCategory.Operational,
            "Log agent activities with correlation IDs to trace multi-step workflows across LLM calls and tools.",
            "https://learn.microsoft.com/en-us/dotnet/core/extensions/logging"),
        ["ai-eval-harness"] = (PatternType.AgentLightning, PatternCategory.Operational,
            "Use evaluation datasets to measure agent quality, accuracy, and consistency over time.",
            "https://learn.microsoft.com/en-us/azure/ai-studio/how-to/evaluate-generative-ai-app"),
        ["ai-ab-testing"] = (PatternType.AgentLightning, PatternCategory.Operational,
            "A/B test different agent configurations (prompts, models, parameters) to optimize quality and cost.",
            "https://learn.microsoft.com/en-us/azure/ai-studio/"),
        ["ai-group-chat"] = (PatternType.AgentLightning, PatternCategory.MultiAgentOrchestration,
            "Use group chat pattern for multiple agents to communicate and self-organize (AutoGen pattern).",
            "https://microsoft.github.io/autogen/docs/tutorial/conversation-patterns"),
        ["ai-sequential-orchestration"] = (PatternType.AgentLightning, PatternCategory.MultiAgentOrchestration,
            "Chain agent outputs sequentially. Agent A's result feeds into Agent B for pipelines.",
            "https://learn.microsoft.com/en-us/training/modules/agent-orchestration-patterns/"),
        ["ai-control-plane"] = (PatternType.AgentLightning, PatternCategory.ToolIntegration,
            "Encapsulate modular tool routing logic behind a single tool interface for scalability.",
            "https://arxiv.org/abs/2505.06817"),
        ["ai-agent-factory"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Use agent factory pattern for standardized agent creation with consistent configuration.",
            "https://devblogs.microsoft.com/ise/multi-agent-systems-at-scale/"),
        ["ai-agent-builder"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Use builder pattern with fluent API for readable, testable agent configuration.",
            "https://learn.microsoft.com/en-us/semantic-kernel/"),
        ["ai-self-improving"] = (PatternType.AgentLightning, PatternCategory.AIAgents,
            "Implement self-improving agents that monitor performance and trigger automatic retraining.",
            "https://www.shakudo.io/blog/5-agentic-ai-design-patterns-transforming-enterprise-operations-in-2025"),
        ["ai-performance-monitoring"] = (PatternType.AgentLightning, PatternCategory.Operational,
            "Monitor agent performance metrics (accuracy, latency, cost) to detect issues and optimize.",
            "https://learn.microsoft.com/en-us/azure/ai-studio/how-to/evaluate-generative-ai-app"),
        
        // Plugin Architecture Patterns (30 patterns across 6 categories)
        // Category 1: Plugin Loading & Isolation (6)
        ["plugin-assembly-load-context"] = (PatternType.PluginArchitecture, PatternCategory.PluginLoading,
            "Use custom AssemblyLoadContext to isolate plugin assemblies and their dependencies, preventing version conflicts and enabling side-by-side loading.",
            "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support"),
        ["plugin-dependency-resolver"] = (PatternType.PluginArchitecture, PatternCategory.PluginLoading,
            "Use AssemblyDependencyResolver to resolve plugin dependencies from .deps.json file, handling NuGet dependencies, native libraries, and satellite assemblies.",
            "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support"),
        ["plugin-enable-dynamic-loading"] = (PatternType.PluginArchitecture, PatternCategory.PluginLoading,
            "Set EnableDynamicLoading to true in plugin project files to copy all dependencies to output and prepare for dynamic loading.",
            "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support"),
        ["plugin-collectible-context"] = (PatternType.PluginArchitecture, PatternCategory.PluginLoading,
            "Use collectible AssemblyLoadContext to enable plugin unloading and hot reload without application restart.",
            "https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability"),
        ["plugin-private-false"] = (PatternType.PluginArchitecture, PatternCategory.PluginLoading,
            "Set Private=false and ExcludeAssets=runtime on plugin interface references to ensure plugins use the host's version of shared assemblies.",
            "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support"),
        ["plugin-native-libraries"] = (PatternType.PluginArchitecture, PatternCategory.PluginLoading,
            "Override LoadUnmanagedDll to load platform-specific native libraries for plugins with P/Invoke or native dependencies.",
            "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support"),
        
        // Category 2: Plugin Discovery & Composition (7)
        ["plugin-mef-catalog"] = (PatternType.PluginArchitecture, PatternCategory.PluginComposition,
            "Use MEF catalogs (DirectoryCatalog, AssemblyCatalog, TypeCatalog) to discover plugins from directories or assemblies at runtime.",
            "https://learn.microsoft.com/en-us/dotnet/framework/mef/"),
        ["plugin-mef-import-export"] = (PatternType.PluginArchitecture, PatternCategory.PluginComposition,
            "Use [Export] and [Import]/[ImportMany] attributes for declarative plugin registration and automatic composition via MEF.",
            "https://learn.microsoft.com/en-us/dotnet/framework/mef/"),
        ["plugin-mef-metadata"] = (PatternType.PluginArchitecture, PatternCategory.PluginComposition,
            "Use [ExportMetadata] to attach metadata (version, priority, capabilities) to plugins for filtering and selection without loading.",
            "https://learn.microsoft.com/en-us/dotnet/framework/mef/"),
        ["plugin-lazy-loading"] = (PatternType.PluginArchitecture, PatternCategory.PluginComposition,
            "Use Lazy<T> or Lazy<T, TMetadata> to defer plugin instantiation until first use, improving startup time and reducing memory footprint.",
            "https://learn.microsoft.com/en-us/dotnet/framework/mef/"),
        ["plugin-registry"] = (PatternType.PluginArchitecture, PatternCategory.PluginComposition,
            "Implement a central plugin registry with Register, Unregister, and Get methods to manage plugin lifecycle and discovery.",
            "https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/technical-reference/plugin-architecture"),
        ["plugin-type-scanning"] = (PatternType.PluginArchitecture, PatternCategory.PluginComposition,
            "Scan assemblies for types implementing plugin interfaces using reflection, providing full control over plugin instantiation without MEF dependency.",
            "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support"),
        ["plugin-config-discovery"] = (PatternType.PluginArchitecture, PatternCategory.PluginComposition,
            "Use JSON configuration files to declaratively specify plugins to load, enabling environment-specific plugin enablement.",
            "https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/technical-reference/plugin-architecture"),
        
        // Category 3: Plugin Lifecycle Management (5)
        ["plugin-interface"] = (PatternType.PluginArchitecture, PatternCategory.PluginLifecycle,
            "Define a standard IPlugin interface with lifecycle methods (Initialize, Execute, Dispose) for consistent plugin contracts.",
            "https://learn.microsoft.com/en-us/power-apps/developer/data-platform/write-plug-in"),
        ["plugin-stateless"] = (PatternType.PluginArchitecture, PatternCategory.PluginLifecycle,
            "Design plugins to be stateless by avoiding mutable member fields, ensuring thread-safety and scalability.",
            "https://learn.microsoft.com/en-us/power-apps/developer/data-platform/best-practices/business-logic/develop-iplugin-implementations-stateless"),
        ["plugin-health-check"] = (PatternType.PluginArchitecture, PatternCategory.PluginLifecycle,
            "Implement IHealthCheck for plugins to monitor health and availability, enabling detection of failing plugins.",
            "https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks"),
        ["plugin-start-stop"] = (PatternType.PluginArchitecture, PatternCategory.PluginLifecycle,
            "Implement IHostedService with StartAsync and StopAsync for explicit plugin lifecycle management and graceful startup and shutdown.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services"),
        ["plugin-dependency-injection"] = (PatternType.PluginArchitecture, PatternCategory.PluginLifecycle,
            "Inject services (ILogger, IServiceProvider, etc.) into plugin constructors for loose coupling and testability.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection"),
        
        // Category 4: Plugin Communication (4)
        ["plugin-event-bus"] = (PatternType.PluginArchitecture, PatternCategory.PluginCommunication,
            "Implement an event bus for publish-subscribe inter-plugin communication, enabling loose coupling and event-driven architecture.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber"),
        ["plugin-shared-service"] = (PatternType.PluginArchitecture, PatternCategory.PluginCommunication,
            "Plugins should expose services via shared interfaces, enabling plugin-to-plugin service calls and extensibility.",
            "https://learn.microsoft.com/en-us/dotnet/framework/mef/"),
        ["plugin-pipeline"] = (PatternType.PluginArchitecture, PatternCategory.PluginCommunication,
            "Use pipeline/middleware pattern to chain plugins for sequential request processing and interceptors.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/"),
        ["plugin-context"] = (PatternType.PluginArchitecture, PatternCategory.PluginCommunication,
            "Use a context object to pass shared state, correlation IDs, and properties through plugin execution pipeline.",
            "https://learn.microsoft.com/en-us/power-apps/developer/data-platform/write-plug-in"),
        
        // Category 5: Plugin Security & Governance (5)
        ["plugin-gatekeeper"] = (PatternType.PluginArchitecture, PatternCategory.PluginSecurity,
            "Implement gatekeeper pattern for centralized security enforcement before plugin execution, validating authentication and authorization.",
            "https://learn.microsoft.com/en-us/azure/well-architected/security/design-patterns"),
        ["plugin-sandboxing"] = (PatternType.PluginArchitecture, PatternCategory.PluginSecurity,
            "Execute plugins in isolated processes or containers with limited permissions to prevent malicious plugins from affecting the host.",
            "https://learn.microsoft.com/en-us/azure/well-architected/security/design-patterns"),
        ["plugin-circuit-breaker"] = (PatternType.PluginArchitecture, PatternCategory.PluginSecurity,
            "Apply circuit breaker pattern to isolate failing plugins and prevent cascading failures across the system.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker"),
        ["plugin-bulkhead"] = (PatternType.PluginArchitecture, PatternCategory.PluginSecurity,
            "Use bulkhead pattern to isolate plugin resources, preventing one plugin from exhausting system resources and starving others.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/bulkhead"),
        ["plugin-signature"] = (PatternType.PluginArchitecture, PatternCategory.PluginSecurity,
            "Verify plugin assembly signatures before loading to ensure trust and prevent tampering.",
            "https://learn.microsoft.com/en-us/dotnet/standard/assembly/strong-named"),
        
        // Category 6: Plugin Versioning & Compatibility (3)
        ["plugin-semver"] = (PatternType.PluginArchitecture, PatternCategory.PluginVersioning,
            "Use semantic versioning (SemVer) for plugins to clearly communicate breaking changes and enable version negotiation.",
            "https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning"),
        ["plugin-compatibility-matrix"] = (PatternType.PluginArchitecture, PatternCategory.PluginVersioning,
            "Maintain compatibility matrix with MinHostVersion/MaxHostVersion metadata to prevent incompatible plugin loading.",
            "https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning"),
        ["plugin-side-by-side"] = (PatternType.PluginArchitecture, PatternCategory.PluginVersioning,
            "Load multiple versions of the same plugin side-by-side using separate AssemblyLoadContext instances for gradual migration and A/B testing.",
            "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support"),
        
        // ==================== STATE MANAGEMENT PATTERNS (40 patterns) ====================
        // Based on: https://learn.microsoft.com/en-us/dotnet/architecture/blazor-for-web-forms-developers/state-management
        
        // Server-Side State Management (8 patterns)
        ["state-circuit"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Implement Blazor Server circuit state management with proper lifecycle handling and backing data stores for critical state.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models#blazor-server"),
        ["state-session"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use HTTP session state for per-user data storage with distributed cache for multi-server deployments.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state#session-state"),
        ["state-distributed-session"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Configure distributed session state backed by Redis or SQL Server for load-balanced applications with connection resilience.",
            "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed"),
        ["state-singleton"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use singleton services for application-wide state with thread-safe collections and backing stores for persistence.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection"),
        ["state-sticky-session"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Configure sticky sessions only when necessary, prefer distributed state for better scalability and failover.",
            "https://learn.microsoft.com/en-us/azure/app-service/configure-common#configure-general-settings"),
        ["state-memory-cache"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Implement in-memory caching with appropriate expiration policies, eviction callbacks, and cache priorities.",
            "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory"),
        ["state-distributed-cache"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use distributed cache (Redis, SQL Server) for scalable applications with efficient serialization and connection resilience.",
            "https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed"),
        ["state-tempdata"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use TempData provider for temporary data storage in redirect scenarios, keeping data small and using Peek to preserve values.",
            "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state#tempdata"),
        
        // Client-Side State Management (7 patterns)
        ["state-localstorage"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Avoid storing sensitive data in browser localStorage without encryption; use ProtectedLocalStorage instead.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#protected-browser-storage"),
        ["state-sessionstorage"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use browser sessionStorage for temporary tab-specific state, validating data on retrieval and avoiding sensitive information.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#protected-browser-storage"),
        ["state-protected-local"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use ProtectedLocalStorage for encrypted browser localStorage with server-side Data Protection API (Blazor Server).",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#protected-browser-storage"),
        ["state-protected-session"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use ProtectedSessionStorage for encrypted temporary storage that clears when browser tab closes (Blazor Server).",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#protected-browser-storage"),
        ["state-indexeddb"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use IndexedDB for large structured client-side data storage (>5MB) with versioning for schema changes.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app"),
        ["state-cookies"] = (PatternType.StateManagement, PatternCategory.Security,
            "Always mark cookies as HttpOnly, Secure, and SameSite; comply with GDPR/privacy regulations for cookie consent.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/gdpr"),
        ["state-querystring"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use query string parameters for shareable/bookmarkable state, but never put sensitive data in URLs.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing"),
        
        // Component State Management (9 patterns)
        ["state-component-parameter"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Make component parameters immutable when possible, use [EditorRequired] for mandatory parameters, implement OnParametersSet for changes.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components#component-parameters"),
        ["state-cascading-parameter"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use cascading parameters for cross-cutting concerns (theme, auth), name them to avoid conflicts, keep data immutable.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters"),
        ["state-app-container"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Implement state container pattern with StateChanged event and NotifyStateChanged method, unsubscribe in Dispose().",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#in-memory-state-container-service"),
        ["state-fluxor"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use Fluxor (Redux/Flux) for immutable state management with actions as pure functions and effects for side effects.",
            "https://github.com/mrpmorris/Fluxor"),
        ["state-mvvm"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Implement MVVM pattern with testable ViewModels, INotifyPropertyChanged, and command pattern for user actions.",
            "https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles#separation-of-concerns"),
        ["state-lifecycle"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Load data in OnInitializedAsync, react to parameter changes in OnParametersSet, clean up resources in Dispose().",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle"),
        ["state-eventcallback"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use EventCallback<T> for child-to-parent communication, always await invocations, prefer over Action for Blazor.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/event-handling#eventcallback"),
        ["state-two-way-binding"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Implement two-way binding with Value parameter and ValueChanged EventCallback, customize binding event when needed.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/data-binding"),
        ["state-render-fragment"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use RenderFragment for flexible component composition, provide default fragments, use typed RenderFragment<T> for data templates.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components"),
        
        // Cross-Component Communication (5 patterns)
        ["state-message-bus"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Implement message bus/event aggregator for loosely coupled components, unsubscribe in Dispose() to prevent memory leaks.",
            "https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api#implement-the-command-and-command-handler-patterns"),
        ["state-signalr"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use SignalR for real-time state updates, implement reconnection logic, handle connection state changes, secure hub methods.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor"),
        ["state-navigation"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use NavigationManager for navigation-based state, encode state in URLs for bookmarkability, validate navigation state.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing"),
        ["state-js-interop"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Minimize JS interop calls for performance, use batch operations, dispose of DotNetObjectReference, handle JS exceptions.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability"),
        ["state-shared-service"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use scoped services for request/circuit-specific state, singletons for application-wide state with thread safety.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection#service-lifetime"),
        
        // State Persistence (6 patterns)
        ["state-ef-core"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use DbContextFactory (not scoped DbContext) in Blazor Server for thread-safe EF Core with async methods and connection resiliency.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core"),
        ["state-dapper"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use parameterized queries in Dapper to prevent SQL injection, implement connection pooling and async methods.",
            "https://github.com/DapperLib/Dapper"),
        ["state-repository"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Implement repository pattern for abstracted data access, use async methods, combine with Unit of Work for transactions.",
            "https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design"),
        ["state-cqrs"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use CQRS for complex domains, optimize read and write models separately, implement validation in command handlers.",
            "https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs"),
        ["state-file"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Use file-based state for configuration/settings, implement file locking for concurrent access, use async file I/O.",
            "https://learn.microsoft.com/en-us/dotnet/standard/io/asynchronous-file-i-o"),
        ["state-azure-storage"] = (PatternType.StateManagement, PatternCategory.StateManagement,
            "Design partition keys for scalability in Azure Table/Cosmos DB, use batch operations, implement retry policies, monitor RU consumption.",
            "https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-overview"),
        
        // State Security (5 patterns)
        ["state-data-protection"] = (PatternType.StateManagement, PatternCategory.Security,
            "Use Data Protection API for sensitive data encryption, configure key storage (Azure Key Vault), use purpose strings for isolation.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction"),
        ["state-token-storage"] = (PatternType.StateManagement, PatternCategory.Security,
            "Store tokens in HttpOnly/Secure cookies or ProtectedBrowserStorage; never use plain localStorage (XSS risk); implement token rotation.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/authentication/"),
        ["state-anti-forgery"] = (PatternType.StateManagement, PatternCategory.Security,
            "Always validate anti-forgery tokens on state-changing operations, use for all forms that modify data, handle validation failures.",
            "https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery"),
        ["state-tenant-isolation"] = (PatternType.StateManagement, PatternCategory.Security,
            "Always filter by tenant ID, use EF Core global query filters, validate tenant access on every request, log tenant context.",
            "https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/overview"),
        ["state-audit-trail"] = (PatternType.StateManagement, PatternCategory.Security,
            "Log who/what/when for all state changes, use temporal tables for automatic tracking, implement soft deletes, comply with data retention policies.",
            "https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal-tables"),
        
        // Azure Web PubSub Patterns (Real-time messaging)
        ["webpubsub-service-client"] = (PatternType.AzureWebPubSub, PatternCategory.RealtimeMessaging,
            "Initialize WebPubSubServiceClient with connection string from configuration, never hardcode connection strings.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/overview"),
        ["webpubsub-broadcast"] = (PatternType.AzureWebPubSub, PatternCategory.RealtimeMessaging,
            "Use SendToAllAsync for broadcasting messages to all connected clients with proper error handling and async patterns.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/key-concepts"),
        ["webpubsub-group-messaging"] = (PatternType.AzureWebPubSub, PatternCategory.RealtimeMessaging,
            "Implement group messaging with SendToGroupAsync for targeted real-time updates to specific client groups.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/key-concepts"),
        ["webpubsub-user-messaging"] = (PatternType.AzureWebPubSub, PatternCategory.RealtimeMessaging,
            "Use SendToUserAsync to send messages to all connections for a specific user, useful for user-specific notifications.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/key-concepts"),
        ["webpubsub-authentication"] = (PatternType.AzureWebPubSub, PatternCategory.Security,
            "Use Azure AD / Entra ID authentication with ManagedIdentityCredential or DefaultAzureCredential instead of connection strings.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/howto-authorize-from-application"),
        ["webpubsub-client-token"] = (PatternType.AzureWebPubSub, PatternCategory.Security,
            "Generate secure client access tokens with GetClientAccessUri, always set expiration time, include user ID and roles for authorization.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/howto-authorize-from-application"),
        ["webpubsub-event-handlers"] = (PatternType.AzureWebPubSub, PatternCategory.EventHandlers,
            "Implement webhook event handlers for upstream events (connect, connected, disconnected, message) with signature validation.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-service-internals"),
        ["webpubsub-signature-validation"] = (PatternType.AzureWebPubSub, PatternCategory.Security,
            "CRITICAL: Always validate webhook signatures using WebPubSubEventHandler.IsValidSignature() to prevent spoofing attacks.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-service-internals"),
        ["webpubsub-hub-management"] = (PatternType.AzureWebPubSub, PatternCategory.RealtimeMessaging,
            "Configure hubs for logical grouping of connections, use different hubs to isolate different scenarios in your application.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/key-concepts"),
        ["webpubsub-group-management"] = (PatternType.AzureWebPubSub, PatternCategory.ConnectionManagement,
            "Use AddConnectionToGroupAsync and RemoveConnectionFromGroupAsync for dynamic group membership management.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/key-concepts"),
        ["webpubsub-connection-lifecycle"] = (PatternType.AzureWebPubSub, PatternCategory.ConnectionManagement,
            "Implement connection lifecycle management with reconnection logic, exponential backoff, logging, and graceful shutdown.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-performance"),
        ["webpubsub-error-handling"] = (PatternType.AzureWebPubSub, PatternCategory.Reliability,
            "Wrap all Web PubSub operations in try-catch blocks, implement retry policies for transient failures, log errors for diagnostics.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-performance"),
        ["webpubsub-message-size"] = (PatternType.AzureWebPubSub, PatternCategory.Reliability,
            "Validate message sizes before sending - Azure Web PubSub has a 1MB message size limit, split large messages if needed.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-performance"),
        ["webpubsub-client-reconnection"] = (PatternType.AzureWebPubSub, PatternCategory.Reliability,
            "Implement automatic reconnection with exponential backoff on client side, handle connection state changes, notify users of connection status.",
            "https://learn.microsoft.com/en-us/azure/azure-web-pubsub/concept-performance"),
        
        // Blazor Patterns (ASP.NET Core)
        ["blazor-component-lifecycle"] = (PatternType.Blazor, PatternCategory.Lifecycle,
            "Implement proper component lifecycle methods (OnInitializedAsync, OnParametersSetAsync, OnAfterRenderAsync, Dispose). Always call base methods and check firstRender in OnAfterRender.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle"),
        ["blazor-parameter-validation"] = (PatternType.Blazor, PatternCategory.ComponentModel,
            "Mark required parameters with [EditorRequired] attribute, add validation attributes ([Required], [Range], [StringLength]) to component parameters.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/"),
        ["blazor-event-callbacks"] = (PatternType.Blazor, PatternCategory.EventHandling,
            "Use EventCallback<T> for parent-child communication, always await InvokeAsync to ensure proper async handling and UI updates.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/"),
        ["blazor-data-binding"] = (PatternType.Blazor, PatternCategory.DataBinding,
            "Use @bind with :after modifier for better control over change notifications. Prefer two-way binding for form inputs.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/data-binding"),
        ["blazor-form-validation"] = (PatternType.Blazor, PatternCategory.Forms,
            "Use EditForm with DataAnnotationsValidator for built-in validation. Include ValidationSummary or ValidationMessage components to display errors.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/forms/"),
        ["blazor-dependency-injection"] = (PatternType.Blazor, PatternCategory.General,
            "Use @inject directive in .razor files or [Inject] attribute in code-behind. Ensure services are registered in Program.cs with appropriate lifetime (Scoped, Transient, Singleton).",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection"),
        ["blazor-js-interop"] = (PatternType.Blazor, PatternCategory.JavaScriptInterop,
            "Call JavaScript in OnAfterRender/OnAfterRenderAsync (check firstRender). Use IJSRuntime.InvokeAsync for return values, InvokeVoidAsync for fire-and-forget. Consider [JSImport]/[JSExport] in Blazor 8.0+ for better performance.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/"),
        ["blazor-render-modes"] = (PatternType.Blazor, PatternCategory.Rendering,
            "Choose appropriate render mode: InteractiveServer for real-time, InteractiveWebAssembly for offline, InteractiveAuto for best of both, Static for SSR performance. Apply at component or page level.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes"),
        ["blazor-state-management"] = (PatternType.Blazor, PatternCategory.StateManagement,
            "Use cascading parameters for component trees, scoped services for user sessions, and StateHasChanged() sparingly for manual updates. Avoid excessive StateHasChanged calls.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management"),
        ["blazor-error-boundaries"] = (PatternType.Blazor, PatternCategory.Reliability,
            "Implement ErrorBoundary components to catch and display errors gracefully. Prevent entire app crashes from component failures.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/handle-errors"),
        ["blazor-routing"] = (PatternType.Blazor, PatternCategory.Routing,
            "Use @page directive for routable components with route constraints for parameters. Use NavigationManager.NavigateTo for programmatic navigation (avoid forceLoad unless necessary).",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing"),
        ["blazor-disposal"] = (PatternType.Blazor, PatternCategory.Lifecycle,
            "Implement IDisposable or IAsyncDisposable for components with event handlers, subscriptions, or HttpClient. Always unsubscribe in Dispose to prevent memory leaks.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle"),
        ["blazor-render-fragments"] = (PatternType.Blazor, PatternCategory.ComponentModel,
            "Use RenderFragment and RenderFragment<T> for templated components. Enables flexible, reusable UI patterns and better component composition.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/"),
        ["blazor-cascading-parameters"] = (PatternType.Blazor, PatternCategory.ComponentModel,
            "Use CascadingValue and [CascadingParameter] for sharing state across component trees. More efficient than passing parameters through multiple levels.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters"),
        ["blazor-virtualization"] = (PatternType.Blazor, PatternCategory.Performance,
            "Use Virtualize component for rendering large lists efficiently. Only renders visible items, significantly improves performance for long data lists.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/virtualization"),
        ["blazor-prerendering"] = (PatternType.Blazor, PatternCategory.Performance,
            "Enable prerendering for faster initial page loads. Be aware of lifecycle (components rendered twice: server-side then client-side). Handle null IJSRuntime during prerender.",
            "https://learn.microsoft.com/en-us/aspnet/core/blazor/components/prerendering-and-integration"),
        
        // ===== FLUTTER PATTERNS =====
        // Widget & State Management
        ["flutter-stateless-widget"] = (PatternType.Flutter, PatternCategory.ComponentModel,
            "Use StatelessWidget with const constructor for immutable widgets. Enables compile-time optimization and widget reuse.",
            "https://docs.flutter.dev/development/ui/widgets-intro"),
        ["flutter-stateful-widget"] = (PatternType.Flutter, PatternCategory.StateManagement,
            "Use StatefulWidget only when you need mutable state. Always dispose controllers in dispose().",
            "https://docs.flutter.dev/development/ui/interactive"),
        ["flutter-const-widget"] = (PatternType.Flutter, PatternCategory.Performance,
            "Use const constructors for widgets to enable compile-time constants and prevent unnecessary rebuilds.",
            "https://docs.flutter.dev/perf/best-practices#use-const-widgets-when-possible"),
        ["flutter-provider"] = (PatternType.Flutter, PatternCategory.StateManagement,
            "Use Provider for state management. Use context.read() for actions, context.watch() for rebuilds.",
            "https://docs.flutter.dev/data-and-backend/state-mgmt/simple"),
        ["flutter-riverpod"] = (PatternType.Flutter, PatternCategory.StateManagement,
            "Use Riverpod for compile-safe state management. Use ref.watch for reactive, ref.read for one-time access.",
            "https://riverpod.dev/docs/introduction/getting_started"),
        ["flutter-bloc"] = (PatternType.Flutter, PatternCategory.StateManagement,
            "Use BLoC/Cubit to separate business logic from UI. Cubit for simple state, Bloc for event-driven.",
            "https://bloclibrary.dev/"),
        
        // Flutter Performance
        ["flutter-listview-builder"] = (PatternType.Flutter, PatternCategory.Performance,
            "Use ListView.builder for lazy loading. Only creates visible items for better performance with large lists.",
            "https://docs.flutter.dev/cookbook/lists/long-lists"),
        ["flutter-repaint-boundary"] = (PatternType.Flutter, PatternCategory.Performance,
            "Use RepaintBoundary to isolate repaints. Prevents cascading repaints on frequently updating widgets.",
            "https://docs.flutter.dev/perf/best-practices#use-repaintboundary-widgets"),
        ["flutter-cached-image"] = (PatternType.Flutter, PatternCategory.Performance,
            "Use CachedNetworkImage for network images. Prevents re-downloading on rebuilds.",
            "https://pub.dev/packages/cached_network_image"),
        ["flutter-compute-isolate"] = (PatternType.Flutter, PatternCategory.Performance,
            "Use compute() or Isolate.run() for CPU-intensive work to keep UI responsive.",
            "https://docs.flutter.dev/perf/isolates"),
        
        // Flutter Lifecycle
        ["flutter-dispose"] = (PatternType.Flutter, PatternCategory.Reliability,
            "Always override dispose() to clean up controllers, subscriptions, and listeners. Call super.dispose() last.",
            "https://api.flutter.dev/flutter/widgets/State/dispose.html"),
        ["flutter-init-state"] = (PatternType.Flutter, PatternCategory.ComponentModel,
            "Use initState for one-time initialization. Call super.initState() first. Don't use context here.",
            "https://api.flutter.dev/flutter/widgets/State/initState.html"),
        ["flutter-app-lifecycle"] = (PatternType.Flutter, PatternCategory.Reliability,
            "Use WidgetsBindingObserver to track app lifecycle (resumed, paused, inactive, detached).",
            "https://api.flutter.dev/flutter/widgets/WidgetsBindingObserver-class.html"),
        
        // Flutter Navigation
        ["flutter-go-router"] = (PatternType.Flutter, PatternCategory.Routing,
            "Use GoRouter for declarative routing with deep linking and web URL support.",
            "https://pub.dev/packages/go_router"),
        ["flutter-named-routes"] = (PatternType.Flutter, PatternCategory.Routing,
            "Use named routes to centralize navigation. Define routes in MaterialApp.routes.",
            "https://docs.flutter.dev/cookbook/navigation/named-routes"),
        ["flutter-deep-linking"] = (PatternType.Flutter, PatternCategory.Routing,
            "Configure deep linking for direct navigation to app content from external URLs.",
            "https://docs.flutter.dev/ui/navigation/deep-linking"),
        
        // Flutter UI
        ["flutter-responsive-layout"] = (PatternType.Flutter, PatternCategory.UserExperience,
            "Use MediaQuery.sizeOf and LayoutBuilder for responsive layouts across screen sizes.",
            "https://docs.flutter.dev/ui/layout/responsive"),
        ["flutter-theming"] = (PatternType.Flutter, PatternCategory.UserExperience,
            "Use Theme.of(context) for consistent styling. Define colors in ColorScheme for Material 3.",
            "https://docs.flutter.dev/cookbook/design/themes"),
        ["flutter-form-validation"] = (PatternType.Flutter, PatternCategory.Security,
            "Add validator to TextFormField for input validation. Also validate server-side.",
            "https://docs.flutter.dev/cookbook/forms/validation"),
        ["flutter-accessibility"] = (PatternType.Flutter, PatternCategory.UserExperience,
            "Use Semantics widget for screen reader support. Essential for inclusive apps.",
            "https://docs.flutter.dev/ui/accessibility-and-internationalization/accessibility"),
        
        // Flutter Networking
        ["flutter-future-builder"] = (PatternType.Flutter, PatternCategory.DataAccess,
            "Use FutureBuilder for async data. Handle connectionState, hasError, and hasData.",
            "https://api.flutter.dev/flutter/widgets/FutureBuilder-class.html"),
        ["flutter-stream-builder"] = (PatternType.Flutter, PatternCategory.DataAccess,
            "Use StreamBuilder for real-time data. Handle all ConnectionState values.",
            "https://api.flutter.dev/flutter/widgets/StreamBuilder-class.html"),
        ["flutter-dio"] = (PatternType.Flutter, PatternCategory.DataAccess,
            "Use Dio for HTTP with interceptors, timeout, cancel, and retry capabilities.",
            "https://pub.dev/packages/dio"),
        
        // Flutter Testing
        ["flutter-widget-test"] = (PatternType.Flutter, PatternCategory.Testing,
            "Write widget tests with testWidgets. Use pumpAndSettle() to wait for animations.",
            "https://docs.flutter.dev/cookbook/testing/widget/introduction"),
        ["flutter-unit-test"] = (PatternType.Flutter, PatternCategory.Testing,
            "Write unit tests for business logic. Use group() to organize related tests.",
            "https://docs.flutter.dev/cookbook/testing/unit/introduction"),
        ["flutter-mocking"] = (PatternType.Flutter, PatternCategory.Testing,
            "Use mocktail (null-safe) or mockito for mocking dependencies in tests.",
            "https://docs.flutter.dev/cookbook/testing/unit/mocking"),
        
        // Flutter Security
        ["flutter-secure-storage"] = (PatternType.Flutter, PatternCategory.Security,
            "Use FlutterSecureStorage for sensitive data like tokens and credentials.",
            "https://pub.dev/packages/flutter_secure_storage"),
        ["flutter-biometric-auth"] = (PatternType.Flutter, PatternCategory.Security,
            "Use local_auth package for biometric authentication with fallback methods.",
            "https://pub.dev/packages/local_auth"),
        ["flutter-ssl-pinning"] = (PatternType.Flutter, PatternCategory.Security,
            "Implement certificate pinning to prevent MITM attacks. Handle certificate rotation.",
            "https://api.dart.dev/stable/dart-io/SecurityContext-class.html"),
        
        // Flutter Animation
        ["flutter-implicit-animation"] = (PatternType.Flutter, PatternCategory.UserExperience,
            "Use implicit animations (AnimatedContainer, AnimatedOpacity) for simple UI transitions.",
            "https://docs.flutter.dev/ui/animations/implicit-animations"),
        ["flutter-animation-controller"] = (PatternType.Flutter, PatternCategory.UserExperience,
            "Use AnimationController with TickerProviderStateMixin. Always dispose the controller.",
            "https://docs.flutter.dev/ui/animations/tutorial"),
        ["flutter-hero-animation"] = (PatternType.Flutter, PatternCategory.UserExperience,
            "Use Hero widget for smooth transitions between routes. Requires unique tag property.",
            "https://docs.flutter.dev/ui/animations/hero-animations"),
        
        // ===== DART PATTERNS =====
        // Dart Async
        ["dart-async-await"] = (PatternType.Dart, PatternCategory.Reliability,
            "Use async/await for asynchronous operations. Always handle errors with try/catch.",
            "https://dart.dev/codelabs/async-await"),
        ["dart-future"] = (PatternType.Dart, PatternCategory.Reliability,
            "Use Future for single async operations. Prefer async/await over .then() chains.",
            "https://dart.dev/guides/libraries/library-tour#future"),
        ["dart-stream"] = (PatternType.Dart, PatternCategory.Reliability,
            "Cancel stream subscriptions in dispose(). Use StreamController.broadcast() for multiple listeners.",
            "https://dart.dev/tutorials/language/streams"),
        ["dart-isolate"] = (PatternType.Dart, PatternCategory.Performance,
            "Use Isolate.run() or compute() for CPU-intensive work to avoid blocking the UI thread.",
            "https://dart.dev/language/concurrency"),
        
        // Dart Null Safety
        ["dart-null-safety"] = (PatternType.Dart, PatternCategory.Correctness,
            "Use ?. for safe access, ?? for defaults. Avoid excessive ! operator usage.",
            "https://dart.dev/null-safety/understanding-null-safety"),
        ["dart-late-keyword"] = (PatternType.Dart, PatternCategory.Correctness,
            "Use 'late' sparingly. Prefer nullable types with null checks or constructor initialization.",
            "https://dart.dev/null-safety/understanding-null-safety#late-variables"),
        ["dart-required-parameter"] = (PatternType.Dart, PatternCategory.Correctness,
            "Use 'required' for mandatory named parameters for compile-time safety.",
            "https://dart.dev/language/functions#named-parameters"),
        
        // Dart Performance
        ["dart-const-constructor"] = (PatternType.Dart, PatternCategory.Performance,
            "Use const constructors for immutable objects. Enables compile-time constants.",
            "https://dart.dev/effective-dart/usage#use-const-constructors-whenever-possible"),
        ["dart-final-keyword"] = (PatternType.Dart, PatternCategory.Performance,
            "Use 'final' for variables that won't be reassigned. Improves code clarity and enables optimizations.",
            "https://dart.dev/effective-dart/usage#prefer-final-for-local-variables"),
        ["dart-string-buffer"] = (PatternType.Dart, PatternCategory.Performance,
            "Use StringBuffer for building strings incrementally. Much more efficient than concatenation.",
            "https://api.dart.dev/stable/dart-core/StringBuffer-class.html"),
        
        // Dart Error Handling
        ["dart-try-catch"] = (PatternType.Dart, PatternCategory.Reliability,
            "Use typed catch clauses (on SpecificException) when possible. Always log or handle errors.",
            "https://dart.dev/language/error-handling"),
        ["dart-custom-exception"] = (PatternType.Dart, PatternCategory.Reliability,
            "Create custom exceptions with meaningful messages and error codes for clear error semantics.",
            "https://dart.dev/language/error-handling#throw"),
        ["dart-result-type"] = (PatternType.Dart, PatternCategory.Reliability,
            "Use Result/Either types for explicit error handling. Better than throwing for expected failures.",
            "https://pub.dev/packages/fpdart"),
        
        // Dart Code Quality
        ["dart-extension-methods"] = (PatternType.Dart, PatternCategory.CodeQuality,
            "Use extensions to add functionality to existing types without modifying them.",
            "https://dart.dev/language/extension-methods"),
        ["dart-mixins"] = (PatternType.Dart, PatternCategory.CodeQuality,
            "Use mixins for code reuse without inheritance. Use 'mixin on' to constrain usage.",
            "https://dart.dev/language/mixins"),
        ["dart-factory-constructor"] = (PatternType.Dart, PatternCategory.CodeQuality,
            "Use factory constructors for returning cached instances or subtypes.",
            "https://dart.dev/language/constructors#factory-constructors"),
        ["dart-sealed-class"] = (PatternType.Dart, PatternCategory.CodeQuality,
            "Use sealed classes for exhaustive switch expressions and algebraic data types.",
            "https://dart.dev/language/class-modifiers#sealed"),
        ["dart-records"] = (PatternType.Dart, PatternCategory.CodeQuality,
            "Use records for immutable, value-based data structures. Great for returning multiple values.",
            "https://dart.dev/language/records"),
        
        // Dart Security
        ["dart-secure-storage"] = (PatternType.Dart, PatternCategory.Security,
            "Use flutter_secure_storage for sensitive data. Never hardcode credentials.",
            "https://pub.dev/packages/flutter_secure_storage"),
        ["dart-https-only"] = (PatternType.Dart, PatternCategory.Security,
            "Always use HTTPS for network requests. HTTP transmits data in plaintext.",
            "https://dart.dev/guides/libraries/library-tour#http-clients"),
        ["dart-parameterized-queries"] = (PatternType.Dart, PatternCategory.Security,
            "Use parameterized queries instead of string interpolation in SQL to prevent injection.",
            "https://pub.dev/packages/sqflite"),
        ["dart-input-validation"] = (PatternType.Dart, PatternCategory.Security,
            "Validate all user inputs. Client-side and server-side validation required.",
            "https://docs.flutter.dev/cookbook/forms/validation"),
        
        // Microsoft.Extensions.AI patterns - Unified AI abstractions for .NET
        ["meai-ichatclient-interface"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.AIAgents,
            "Implement IChatClient interface for unified chat AI service integration.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-streaming-responses"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Performance,
            "Use GetStreamingResponseAsync with IAsyncEnumerable<ChatResponseUpdate> for real-time streaming UX.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-function-calling"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.ToolIntegration,
            "Use AIFunctionFactory.Create to expose .NET methods as AI-callable tools.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-auto-function-invocation"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Interceptors,
            "Use ChatClientBuilder.UseFunctionInvocation() for automatic tool calling middleware.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-iembedding-generator"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.AIAgents,
            "Implement IEmbeddingGenerator<TInput, TEmbedding> for unified embedding generation.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-distributed-cache-middleware"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Performance,
            "Use UseDistributedCache middleware to cache AI responses for cost reduction.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-opentelemetry-middleware"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Observability,
            "Use UseOpenTelemetry middleware for AI request/response telemetry and observability.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-delegating-chat-client"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Interceptors,
            "Create custom middleware by extending DelegatingChatClient for composable AI pipelines.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-delegating-embedding-generator"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Interceptors,
            "Create custom middleware by extending DelegatingEmbeddingGenerator.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-chat-client-builder"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Interceptors,
            "Use ChatClientBuilder for composable middleware pipeline (caching, telemetry, function calling).",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-embedding-generator-builder"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Interceptors,
            "Use EmbeddingGeneratorBuilder for composable embedding middleware pipeline.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-dependency-injection"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.DependencyInjection,
            "Register IChatClient and IEmbeddingGenerator in DI container for testability and flexibility.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-chat-options-configuration"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Configuration,
            "Configure ChatOptions with Temperature, MaxTokens, and Tools for model behavior control.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-conversation-history"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.StateManagement,
            "Maintain List<ChatMessage> for multi-turn conversation history.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-tool-input-validation"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Security,
            "Validate function parameters in AI tools before execution to prevent injection attacks.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-error-handling"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Reliability,
            "Wrap GetResponseAsync and GenerateAsync in try/catch for network and API error handling.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-rate-limiting-middleware"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Performance,
            "Implement custom DelegatingChatClient with rate limiting to prevent API quota exhaustion.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-provider-portability"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.AIAgents,
            "Use abstractions (IChatClient) instead of provider-specific clients for multi-provider support.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-stateful-conversations"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.StateManagement,
            "Use ConversationId for stateful conversations - clear local history when service maintains state.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-stateless-conversations"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.StateManagement,
            "For stateless clients, maintain List<ChatMessage> history manually across requests.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-iimage-generator"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.AIAgents,
            "Implement IImageGenerator for text-to-image generation with unified interface.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-structured-output"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.AIAgents,
            "Use strongly-typed responses with JSON schema for validated, structured AI output.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai"),
        ["meai-response-format"] = (PatternType.MicrosoftExtensionsAI, PatternCategory.Configuration,
            "Define ResponseFormat with JSON schema to ensure AI responses match expected structure.",
            "https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai")
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

