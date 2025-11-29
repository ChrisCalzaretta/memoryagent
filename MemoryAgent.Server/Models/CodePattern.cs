namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a detected coding pattern (best practice, anti-pattern, etc.)
/// </summary>
public class CodePattern
{
    /// <summary>
    /// Unique identifier for the pattern instance
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Pattern name (e.g., "UserService_MemoryCache", "Polly_RetryPolicy")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Pattern type (Caching, Resilience, Validation, DependencyInjection, Logging, ErrorHandling)
    /// </summary>
    public PatternType Type { get; set; }

    /// <summary>
    /// Pattern category for Azure Well-Architected Framework
    /// (Performance, Reliability, Security, Operational, Cost)
    /// </summary>
    public PatternCategory Category { get; set; }

    /// <summary>
    /// Specific implementation detected (e.g., "IMemoryCache", "Polly", "Pydantic")
    /// </summary>
    public string Implementation { get; set; } = string.Empty;

    /// <summary>
    /// Programming language
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// File path where pattern was found
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number where pattern starts
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// End line number (for multi-line patterns)
    /// </summary>
    public int EndLineNumber { get; set; }

    /// <summary>
    /// Code snippet showing the pattern (with context)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Description of the best practice being followed
    /// </summary>
    public string BestPractice { get; set; } = string.Empty;

    /// <summary>
    /// Azure best practice documentation URL
    /// </summary>
    public string AzureBestPracticeUrl { get; set; } = string.Empty;

    /// <summary>
    /// Pattern confidence score (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; } = 1.0f;

    /// <summary>
    /// Whether this is a positive pattern (best practice) or negative (anti-pattern)
    /// </summary>
    public bool IsPositivePattern { get; set; } = true;

    /// <summary>
    /// Additional metadata about the pattern
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Project/context this pattern belongs to
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// When this pattern was detected
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Types of coding patterns
/// </summary>
public enum PatternType
{
    Caching,
    Resilience,
    Validation,
    DependencyInjection,
    Logging,
    ErrorHandling,
    Security,
    Performance,
    Configuration,
    Testing,
    Monitoring,
    DataAccess,
    ApiDesign,
    Messaging,
    BackgroundJobs,
    PublisherSubscriber,  // Pub/Sub messaging pattern (Azure Event Grid, Service Bus Topics, Event Hubs)
    
    // AI Agent Frameworks
    AgentFramework,      // Microsoft Agent Framework
    AGUI,                // AG-UI Protocol Integration (Agent UI)
    SemanticKernel,      // Semantic Kernel (legacy, migrating to Agent Framework)
    AutoGen,             // AutoGen (legacy, migrating to Agent Framework)
    AgentLightning,      // AI Agent Core Patterns (prompt, memory, RAG, planning, safety, FinOps)
    
    // Plugin Architecture Patterns
    PluginArchitecture,  // Plugin loading, discovery, composition, lifecycle, communication, security
    
    // State Management Patterns (Blazor & ASP.NET Core)
    StateManagement,     // Server-side, client-side, component, cross-component, persistence, security
    
    // Real-time Messaging Patterns
    AzureWebPubSub,      // Azure Web PubSub service patterns (WebSocket, pub/sub, connection management, event handlers)
    
    // Azure Architecture Patterns (Complete Coverage)
    Ambassador,                   // Helper services that send network requests on behalf of consumer
    AntiCorruptionLayer,         // Façade between modern app and legacy system
    AsyncRequestReply,           // Decouple backend processing from frontend
    BackendsForFrontends,        // Separate backends for specific frontends
    Bulkhead,                    // Isolate elements into pools for fault tolerance
    CircuitBreaker,              // Handle faults with circuit breaker pattern  
    Choreography,                // Decentralized service coordination via events
    ClaimCheck,                  // Split large messages into claim check and payload
    CompensatingTransaction,     // Undo work in distributed operations
    CompetingConsumers,          // Multiple concurrent consumers on same channel
    ComputeResourceConsolidation, // Consolidate tasks into single computational unit
    CQRS,                        // Command Query Responsibility Segregation
    DeploymentStamps,            // Deploy multiple independent copies
    EventSourcing,               // Append-only event store
    ExternalConfigurationStore,  // Centralized configuration management
    FederatedIdentity,           // External identity provider delegation
    GatewayAggregation,          // Aggregate multiple requests into one
    GatewayOffloading,           // Offload functionality to gateway proxy
    GatewayRouting,              // Route to multiple services via single endpoint
    Geode,                       // Geographically distributed nodes
    IndexTable,                  // Indexes over frequently queried fields
    LeaderElection,              // Elect leader for coordinated actions
    MaterializedView,            // Prepopulated views for efficient querying
    MessagingBridge,             // Bridge between incompatible messaging systems
    PipesAndFilters,             // Break task into reusable processing elements
    PriorityQueue,               // Prioritize requests by importance
    Quarantine,                  // Validate external assets before consumption
    QueueBasedLoadLeveling,      // Queue as buffer to smooth heavy loads
    Saga,                        // Manage data consistency across microservices
    SchedulerAgentSupervisor,    // Coordinate distributed actions
    SequentialConvoy,            // Process related messages in order
    Sidecar,                     // Deploy components in separate process/container
    StaticContentHosting,        // Cloud-based static content delivery
    StranglerFig,                // Incremental legacy system migration
    Throttling,                  // Control resource consumption
    ValetKey,                    // Restricted direct resource access via token
    
    Unknown
}

/// <summary>
/// Azure Well-Architected Framework categories + AI Agent specific categories
/// </summary>
public enum PatternCategory
{
    Performance,
    Reliability,
    Security,
    Operational,
    Cost,
    General,
    
    // AI Agent Framework specific categories
    AIAgents,                    // AI agent creation and configuration
    MultiAgentOrchestration,     // Multi-agent workflows and patterns
    StateManagement,             // Thread-based state, checkpointing
    ToolIntegration,             // MCP servers, plugins, tools
    Interceptors,                // Middleware, filters, safety checks
    HumanInLoop,                 // Human interaction patterns
    AgentOptimization,           // RL-based agent training and optimization (Agent Lightning)
    AntiPatterns,                // Anti-patterns and migration recommendations
    Observability,               // Tracing, logging, evaluation, A/B testing
    AdvancedMultiAgent,          // Group chat, sequential orchestration, control plane
    AgentLifecycle,              // Agent factory, builder, self-improvement, performance monitoring
    
    // Plugin Architecture specific categories
    PluginLoading,               // Assembly loading, isolation, dependency resolution
    PluginComposition,           // MEF, discovery, lazy loading, registry
    PluginLifecycle,             // Initialization, health checks, start/stop, DI
    PluginCommunication,         // Event bus, shared services, pipeline, context
    PluginSecurity,              // Gatekeeper, sandboxing, circuit breaker, bulkhead, signing
    PluginVersioning,            // SemVer, compatibility, side-by-side versioning
    
    // Real-time Messaging specific categories
    RealtimeMessaging,           // WebSocket connections, pub/sub patterns, real-time communication
    ConnectionManagement,        // Connection lifecycle, retry, reconnection, health monitoring
    EventHandlers,               // Webhook event handlers, upstream events, event validation
    
    // Azure Architecture Pattern specific categories
    DataManagement,              // CQRS, Event Sourcing, Index Table, Materialized View, Static Content, Valet Key
    DesignImplementation,        // Ambassador, Anti-Corruption, BFF, Consolidation, Config, Gateway patterns
    MessagingPatterns,           // Async Request-Reply, Claim Check, Choreography, Competing Consumers, Pipes/Filters, Priority Queue, Queue Load Leveling, Scheduler-Agent, Sequential Convoy, Messaging Bridge
    ResiliencyPatterns,          // Bulkhead, Circuit Breaker, Compensating Transaction, Leader Election, Geode, Deployment Stamps, Throttling
    SecurityPatterns,            // Federated Identity, Quarantine
    OperationalPatterns,         // Sidecar, Strangler Fig, Saga
    DistributedSystems           // Patterns for distributed system coordination
}

/// <summary>
/// Result of best practice validation
/// </summary>
public class BestPracticeValidation
{
    public string Project { get; set; } = string.Empty;
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    public float OverallScore { get; set; }
    public List<PatternValidationResult> Results { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class PatternValidationResult
{
    public string Practice { get; set; } = string.Empty;
    public PatternType Type { get; set; }
    public bool Implemented { get; set; }
    public int Count { get; set; }
    public List<string> Examples { get; set; } = new();
    public string AzureUrl { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

