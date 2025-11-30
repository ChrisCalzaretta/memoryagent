# 📚 Complete Pattern Catalog - What We Detect

## **Overview:**

The system detects **110+ coding patterns** across **17 categories**, automatically finding best practices, anti-patterns, and framework usage in your code.

**Latest Addition:** Azure Web PubSub patterns (14+ patterns) for real-time WebSocket messaging across C#, Python, VB.NET, and JavaScript!

---

## **Pattern Categories:**

| Category | Pattern Count | Description |
|----------|---------------|-------------|
| **Caching** | 2 | Memory caching, distributed caching |
| **Resilience** | 3 | Retry, circuit breaker, timeout policies |
| **Validation** | 2 | Input validation, model validation |
| **Security** | 6 | Auth, authorization, encryption, API keys |
| **API Design** | 3 | Pagination, versioning, rate limiting |
| **Monitoring** | 3 | Health checks, logging, metrics |
| **Background Jobs** | 2 | Hosted services, message queues |
| **Configuration** | 2 | Config management, feature flags |
| **Publisher-Subscriber** | **25+** | **Event-driven messaging patterns** |
| **Azure Web PubSub** | **14+** | **Real-time WebSocket messaging (NEW!)** |
| **AG-UI (Agent UI)** | 40+ | Complete AG-UI protocol patterns |
| **Agent Framework** | 5+ | Microsoft Agent Framework patterns |
| **Semantic Kernel** | Legacy | Legacy patterns (migrating to Agent Framework) |
| **AutoGen** | Legacy | Legacy patterns (migrating to Agent Framework) |
| **Agent Lightning** | RL-based | Reinforcement learning optimization |

---

## **🔥 Core Patterns (Azure Well-Architected):**

### **1. Caching Patterns**

| Pattern | Description | Azure Docs |
|---------|-------------|------------|
| **distributed-caching** | Use Redis/Azure Cache for cross-instance caching | [Link](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/) |
| **memory-caching** | IMemoryCache for fast in-process caching | [Link](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory) |

**Detects:**
- `IMemoryCache` usage
- `IDistributedCache` usage
- Redis/StackExchange.Redis
- Expiration policies (sliding, absolute)
- Null checks after cache fetch
- Concurrency protection

---

### **2. Resilience Patterns**

| Pattern | Description | Azure Docs |
|---------|-------------|------------|
| **retry-policy** | Polly retry for transient fault handling | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/retry) |
| **circuit-breaker** | Prevent cascading failures | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker) |
| **timeout-policy** | Prevent resource exhaustion from slow ops | [Link](https://learn.microsoft.com/en-us/azure/architecture/best-practices/transient-faults) |

**Detects:**
- Polly policies (`AsyncRetryPolicy`, `CircuitBreakerPolicy`)
- Exponential backoff
- Max retry counts
- Timeout configurations
- Circuit breaker states

---

### **3. Validation Patterns**

| Pattern | Description | Azure Docs |
|---------|-------------|------------|
| **input-validation** | DataAnnotations/FluentValidation for security | [Link](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation) |
| **model-validation** | Data integrity and business rules | [Link](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation) |

**Detects:**
- `[Required]`, `[StringLength]`, `[Range]` attributes
- FluentValidation validators
- `ModelState.IsValid` checks
- Custom validation attributes

---

### **4. Security Patterns**

| Pattern | Description | Azure Docs |
|---------|-------------|------------|
| **authentication** | JWT or Azure AD for API security | [Link](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-security) |
| **authorization** | Role-based or policy-based access control | [Link](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction) |
| **data-encryption** | Encrypt sensitive data at rest/transit | [Link](https://learn.microsoft.com/en-us/azure/architecture/framework/security/design-storage) |

**Detects:**
- `[Authorize]` attributes
- JWT token handling
- Azure AD configuration
- Role checks
- Policy requirements
- Data encryption methods

---

### **5. API Design Patterns**

| Pattern | Description | Azure Docs |
|---------|-------------|------------|
| **pagination** | Large result sets performance | [Link](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design) |
| **versioning** | Backward compatibility | [Link](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#versioning-a-restful-web-api) |
| **rate-limiting** | Protect from abuse | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/throttling) |

**Detects:**
- Skip/Take pagination
- PagedResult patterns
- API version attributes
- Rate limiting middleware
- Throttling policies

---

### **6. Monitoring Patterns**

| Pattern | Description | Azure Docs |
|---------|-------------|------------|
| **health-checks** | Endpoints for orchestration | [Link](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) |
| **structured-logging** | Serilog/Application Insights | [Link](https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring) |
| **metrics** | Performance monitoring | [Link](https://learn.microsoft.com/en-us/azure/architecture/best-practices/monitoring) |

**Detects:**
- `MapHealthChecks` endpoints
- Serilog configuration
- Application Insights telemetry
- Custom metrics
- `ILogger<T>` usage

---

### **7. Publisher-Subscriber Patterns** ✨ **NEW!**

| Pattern | Description | Azure Docs |
|---------|-------------|------------|
| **azure-service-bus** | Topics and subscriptions for reliable messaging | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber) |
| **azure-event-grid** | Event-driven architecture and routing | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber) |
| **azure-event-hubs** | High-throughput event streaming | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber) |
| **rabbitmq-amqp** | Message queue pub/sub | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber) |
| **redis-pubsub** | Lightweight message broadcasting | [Link](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber) |

**Detects - C# (6 patterns):**
- Azure Service Bus (Topics, Subscriptions, ServiceBusClient, ServiceBusSender/Receiver)
- Azure Event Grid (EventGridPublisherClient, EventGridTrigger, Event routing)
- Azure Event Hubs (EventHubProducerClient, EventProcessorClient, streaming)
- MassTransit (IBus.Publish, IConsumer<T>, configuration)
- NServiceBus (IEndpointInstance.Publish, IHandleMessages<T>)
- Generic Observable patterns (IObservable<T>, IObserver<T>)

**Detects - Python (6 patterns):**
- Azure Service Bus SDK (ServiceBusClient, send_messages, receive_messages)
- Azure Event Grid SDK (EventGridPublisherClient, send_events)
- Azure Event Hubs SDK (EventHubProducerClient, EventHubConsumerClient)
- Redis Pub/Sub (redis.publish, redis.subscribe, pubsub())
- RabbitMQ/Pika (exchange_declare, basic_publish, basic_consume)
- Generic event emitters (.emit, .on, .subscribe)

**Detects - JavaScript/TypeScript (9 patterns):**
- Node.js EventEmitter (emit, on, addListener, removeListener)
- RxJS Observables (Observable, Subject, BehaviorSubject, subscribe)
- WebSocket (new WebSocket, Socket.IO)
- Server-Sent Events (EventSource for server push)
- Azure Service Bus (@azure/service-bus SDK)
- RabbitMQ (amqplib, AMQP protocol)
- Redis Pub/Sub (redis.publish, redis.subscribe)
- Apache Kafka (kafkajs, producer.send, consumer.subscribe)
- Custom EventBus/EventAggregator implementations

**Detects - VB.NET (6 patterns):**
- Azure Service Bus (ServiceBusClient, SendMessageAsync, ReceiveMessageAsync)
- Azure Event Grid (EventGridPublisherClient, EventGridTrigger)
- Azure Event Hubs (EventHubProducerClient, EventProcessorClient)
- .NET Events (Public Event, RaiseEvent, AddHandler/RemoveHandler)
- MassTransit (IBus.Publish, Implements IConsumer)
- NServiceBus (IEndpointInstance.Publish, Implements IHandleMessages)

**Validation Rules (10 checks):**
- ✅ Idempotency handling (MessageId, DeduplicationId)
- ✅ Error handling and dead-letter queue configuration
- ✅ Message expiration (TTL) settings
- ✅ Subscription filtering (for topics)
- ✅ Message ordering (PartitionKey, SessionId)
- ✅ Retry policy configuration
- ✅ Authentication/security (Managed Identity vs connection strings)
- ✅ Telemetry and logging
- ✅ Consumer concurrency configuration
- ✅ Proper disposal and resource management

**Best Practices Enforced:**
- Use Managed Identity instead of connection strings (Security Score -3 if violated)
- Implement message deduplication for idempotency
- Configure dead-letter queues for poison messages
- Set appropriate TTL on messages
- Use subscription filters to reduce processing overhead
- Configure retry policies with exponential backoff
- Add telemetry for message processing events
- Implement proper concurrency controls

---

## **🌐 Azure Web PubSub Patterns (14+ Patterns):** ✨ **NEW!**

Azure Web PubSub is a fully managed service for real-time publish-subscribe messaging using WebSockets. The Memory Agent now detects all major Azure Web PubSub implementation patterns across **C#, Python, VB.NET, and JavaScript/TypeScript**.

### **Service Client Patterns (3)**

| Pattern | Description | Best Practice |
|---------|-------------|---------------|
| **webpubsub-service-client** | WebPubSubServiceClient initialization | Store connection string in configuration, never hardcode |
| **webpubsub-authentication** | Azure AD / Entra ID authentication | Use ManagedIdentityCredential for Azure-hosted apps |
| **webpubsub-hub-management** | Hub configuration for connection grouping | Use separate hubs for different application scenarios |

**Detects:**
- `new WebPubSubServiceClient(connectionString, hubName)` (C#)
- `WebPubSubServiceClient.from_connection_string()` (Python)
- `new WebPubSubClient({ url })` (JavaScript)
- ManagedIdentityCredential / DefaultAzureCredential usage
- Connection string from configuration vs hardcoded

### **Messaging Patterns (3)**

| Pattern | Description | Best Practice |
|---------|-------------|---------------|
| **webpubsub-broadcast** | Send messages to all clients | Use SendToAllAsync with error handling |
| **webpubsub-group-messaging** | Send to specific groups | Implement SendToGroupAsync for targeted updates |
| **webpubsub-user-messaging** | Send to specific users | Use SendToUserAsync for user notifications |

**Detects:**
- `SendToAllAsync(message)` - Broadcast to all connections
- `SendToGroupAsync(groupName, message)` - Group messaging
- `SendToUserAsync(userId, message)` - User-specific messaging
- Async/await patterns
- Error handling (try-catch blocks)
- Logging for message delivery

### **Connection Management (4)**

| Pattern | Description | Best Practice |
|---------|-------------|---------------|
| **webpubsub-group-management** | Add/remove connections from groups | Use AddConnectionToGroupAsync / RemoveConnectionFromGroupAsync |
| **webpubsub-connection-lifecycle** | Connection open/close/reconnection | Implement retry with exponential backoff |
| **webpubsub-client-token** | Secure client access tokens | Always set token expiration, include user ID and roles |
| **webpubsub-client-reconnection** | Automatic reconnection logic | Use exponential backoff, notify users of connection status |

**Detects:**
- `AddConnectionToGroupAsync()` - Adding clients to groups
- `RemoveConnectionFromGroupAsync()` - Removing from groups
- `CloseClientConnectionAsync()` - Graceful connection close
- `GetClientAccessUri(userId, roles, expiresAfter)` - Token generation
- Reconnection logic with retry
- Connection state tracking

### **Event Handler Patterns (3)**

| Pattern | Description | Best Practice |
|---------|-------------|---------------|
| **webpubsub-event-handlers** | Process upstream events | Implement handlers for connect, connected, disconnected, message events |
| **webpubsub-signature-validation** | Webhook signature verification | **CRITICAL**: Always validate signatures to prevent spoofing |
| **webpubsub-webhook-endpoint** | HTTP webhook endpoints | Validate signatures, handle all event types, implement idempotency |

**Detects:**
- `WebPubSubEventHandler` base class usage (C#)
- Webhook endpoints (`[HttpPost("/eventhandler")]`)
- Signature validation (`VerifySignature()`)
- Event type handling (connected, disconnected, message)
- Event handlers: `client.on('connected', ...)` (JavaScript)

### **Best Practices & Validation:**

**✅ Quality Checks:**
- Connection string from configuration (not hardcoded) - **CRITICAL**
- Async/await pattern usage
- Error handling with try-catch
- Logging for diagnostics
- Token expiration set
- Webhook signature validation - **CRITICAL for security**
- Message size validation (1MB limit)
- Reconnection with exponential backoff

**🚨 Security Requirements:**
- Use Azure AD authentication instead of connection strings
- Always validate webhook signatures (prevents spoofing attacks)
- Set token expiration times
- Include user ID and roles in tokens
- Use HTTPS for webhook endpoints

**📊 Supported Languages:**
- **C#**: Full detection (10 patterns)
- **Python**: Full detection (10 patterns)
- **VB.NET**: Full detection (10 patterns)
- **JavaScript/TypeScript**: Client-side patterns (12 patterns)

---

## **🤖 AG-UI Patterns (40+ Patterns):**

### **Core Integration (3)**

| Pattern | Description |
|---------|-------------|
| **agui-endpoint** | `MapAGUI()` for HTTP endpoints with SSE streaming |
| **agui-streaming** | Server-Sent Events for real-time responses |
| **agui-thread-management** | Conversation context across requests |

### **7 Features of AG-UI (7)**

| Feature | Pattern | Description |
|---------|---------|-------------|
| Feature 1 | **agui-agentic-chat** | Basic streaming chat with tool calling |
| Feature 2 | **agui-backend-tools** | Server-side tool execution with results |
| Feature 3 | **agui-human-loop** | Approval workflows for sensitive ops |
| Feature 4 | **agui-generative-ui** | Async tools with progress updates |
| Feature 5 | **agui-tool-ui** | Custom UI component rendering |
| Feature 6 | **agui-shared-state** | Bidirectional client-server sync |
| Feature 7 | **agui-predictive-updates** | Optimistic UI for responsiveness |

### **Protocol & Integration (8)**

- **agui-protocol-events** - 16 standardized event types
- **agui-middleware** - Custom logic injection
- **agui-copilotkit** - CopilotKit client library
- **agui-frontend-tools** - Client-side execution (GPS, camera)
- **agui-websocket** - Bidirectional real-time transport
- **agui-cancellation** - Stop agent mid-execution
- **agui-pause-resume** - Human intervention without state loss
- **agui-retry** - Retry failed operations

### **Multimodal Support (3)**

- **agui-multimodal-files** - File/attachment inputs
- **agui-multimodal-images** - Image inputs for visual AI
- **agui-multimodal-audio** - Voice/audio transcripts

### **State Management (7)**

- **agui-state-delta** - JSON Patch for incremental updates
- **agui-event-sourced** - History and collaborative editing
- **agui-conflict-resolution** - Concurrent update handling
- **agui-json-schema** - Type-safe state validation
- **agui-typed-state-schema** - `ChatResponseFormat.ForJsonSchema<T>()`
- **agui-thread-persistence** - Maintain context across sessions
- **agui-session-management** - Associate threads with sessions

### **Reliability & Monitoring (6)**

- **agui-error-handling** - Comprehensive error handling
- **agui-exponential-backoff** - Connection retry strategies
- **agui-circuit-breaker** - Prevent cascading failures
- **agui-opentelemetry** - Distributed tracing
- **agui-structured-logging** - Context-aware logging
- **agui-app-insights** - Performance monitoring

### **Security & Performance (6)**

- **agui-jwt-auth** - JWT authentication
- **agui-authorization** - Access control policies
- **agui-api-key** - Service-to-service auth
- **agui-rate-limiting** - Abuse prevention
- **agui-concurrency-limit** - Resource protection
- **agui-distributed-session** - Redis for scalability

### **Implementation Helpers (3)**

- **agui-copilotkit-hooks** - React hooks integration
- **agui-async-enumerable** - Memory-efficient streaming
- **agui-event-handlers** - Process streaming updates

---

## **🚀 Agent Framework Patterns:**

### **Microsoft Agent Framework**

| Pattern | Description |
|---------|-------------|
| **Agent** | AgentBuilder pattern usage |
| **Tools** | Tool registration and execution |
| **Workflows** | Multi-agent workflows |
| **Timeout** | Timeout/cancellation configs |
| **Telemetry** | OpenTelemetry integration |

**Detects:**
- `AgentBuilder` usage
- Tool decorators
- Workflow definitions
- CancellationToken usage
- Telemetry providers

---

### **Semantic Kernel (Legacy)**

**Status:** Flagged for migration to Agent Framework

**Detects:**
- Old Kernel initialization
- Legacy Planner usage
- Function registration

**Migration Path:** Provided automatically

---

### **AutoGen (Legacy)**

**Status:** Flagged for migration to Agent Framework

**Detects:**
- AutoGen agent patterns
- AssistantAgent/UserProxy
- GroupChat patterns

**Migration Path:** Step-by-step guidance provided

---

### **Agent Lightning (RL-Based)**

| Pattern | Description |
|---------|-------------|
| **Reward Signals** | Reinforcement learning feedback |
| **Optimization** | RL-based agent improvement |

**Detects:**
- Reward signal implementations
- Training loop patterns
- Optimization configurations

---

## **🔍 How Patterns Are Detected:**

### **1. Automatic Detection During Indexing**

```
File indexed (McpService.cs)
    ↓
Roslyn parses code
    ↓
3 Pattern Detectors Run:
  1. CSharpPatternDetectorEnhanced (Azure best practices)
  2. AgentFrameworkPatternDetector (AI frameworks)
  3. AGUIPatternDetector (AG-UI protocol)
    ↓
Patterns stored in:
  - Qdrant (for semantic search)
  - Neo4j (for relationships)
```

### **2. Pattern Metadata Stored**

```json
{
  "name": "UserService_MemoryCache",
  "type": "Caching",
  "category": "Performance",
  "implementation": "IMemoryCache with expiration",
  "best_practice": "Azure Cache for Redis best practices",
  "azure_url": "https://learn.microsoft.com/...",
  "confidence": 0.95,
  "is_positive_pattern": true,
  "content": "await _cache.GetOrCreateAsync..."
}
```

---

## **🎯 Using Patterns:**

### **Search for Patterns:**

```
@memory search for caching patterns
@memory search for retry logic
@memory search for validation
@memory search for authentication
```

### **Get Pattern Recommendations:**

```
@memory get recommendations for MemoryAgent
```

Returns prioritized list of missing best practices!

### **Validate Pattern Quality:**

```
@memory validate pattern quality for pattern_id_123
```

Returns:
- Quality score (0-10)
- Grade (A-F)
- Issues found
- Auto-fix code (if available)

### **Find Anti-Patterns:**

```
@memory find anti-patterns in MemoryAgent
```

Returns badly implemented or legacy patterns.

---

## **📊 Pattern Categories (Enum):**

```csharp
public enum PatternCategory
{
    Performance,        // Caching, pagination, async
    Reliability,        // Retry, circuit breaker, health checks
    Security,           // Auth, encryption, validation
    Operational,        // Logging, monitoring, config
    CostOptimization,   // Resource efficiency
    AIAgents,           // Agent frameworks, AG-UI
    StateManagement,    // Sessions, shared state
    ToolIntegration,    // Tool calling, frontend tools
    HumanInLoop,        // Approval workflows
    Interceptors        // Middleware patterns
}
```

---

## **🏆 Best Practices Validation:**

The system tracks **70+ Azure best practices** and can:

✅ **Detect** which ones you're using  
✅ **Recommend** which ones you're missing  
✅ **Validate** implementation quality  
✅ **Auto-fix** common issues  
✅ **Track** compliance over time  

---

## **Summary:**

| What | Count |
|------|-------|
| **Total Patterns** | 70+ |
| **Pattern Types** | 15 |
| **Pattern Categories** | 10 |
| **Azure Best Practices** | 70+ |
| **AG-UI Patterns** | 40+ |
| **Agent Frameworks** | 4 (Agent Framework, SK, AutoGen, Lightning) |

**All patterns are automatically detected, indexed, and made searchable!** 🎉

