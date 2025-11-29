# Blazor & ASP.NET Core State Management Patterns - Deep Research

## 📚 Source Documentation
- [State Management - Blazor for Web Forms Developers](https://learn.microsoft.com/en-us/dotnet/architecture/blazor-for-web-forms-developers/state-management)
- [ASP.NET Core Blazor State Management](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management)
- [Session and State Management in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state)

---

## 🎯 Pattern Categories (6 Categories, 40+ Patterns)

### 1️⃣ SERVER-SIDE STATE MANAGEMENT (8 Patterns)

#### 1.1 Circuit State Management (Blazor Server)
**Description:** Blazor Server maintains component state in server memory during active connections (circuits).

**Detection Signals:**
- `@inject NavigationManager`
- `CircuitHandler` base class usage
- `OnCircuitOpenedAsync`, `OnCircuitClosedAsync`, `OnConnectionDownAsync`
- Blazor Server hosting configuration

**Best Practices:**
- ✅ Don't rely solely on in-memory state
- ✅ Implement sticky sessions for load balancing
- ✅ Use backing data stores for critical state
- ✅ Handle circuit reconnection gracefully
- ⚠️ Be aware of memory pressure on server

**Azure Reference:** [Blazor Server Hosting Model](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models#blazor-server)

**CWE:** CWE-539 (Information Exposure Through Persistent Cookies)

---

#### 1.2 Session State (ISession)
**Description:** Per-user state storage using HTTP session with dictionary-like interface.

**Detection Signals:**
- `ISession` interface usage
- `HttpContext.Session`
- `services.AddSession()` in configuration
- `app.UseSession()` middleware
- Session extension methods: `SetString()`, `GetString()`, `SetInt32()`, `GetInt32()`

**Best Practices:**
- ✅ Use distributed session for multi-server deployments
- ✅ Configure session timeout appropriately
- ✅ Don't store large objects in session
- ✅ Use for temporary, user-specific data only
- ⚠️ Requires cookies - handle cookie consent

**Azure Reference:** [Session State in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state#session-state)

**CWE:** CWE-311 (Missing Encryption of Sensitive Data)

---

#### 1.3 Distributed Session State
**Description:** Session state backed by distributed cache (Redis, SQL Server, NCache).

**Detection Signals:**
- `services.AddStackExchangeRedisCache()`
- `services.AddDistributedSqlServerCache()`
- `IDistributedCache` interface
- Redis connection strings in configuration
- Session configuration with distributed cache

**Best Practices:**
- ✅ Use for load-balanced/scaled applications
- ✅ Configure appropriate cache expiration
- ✅ Implement connection resilience (circuit breaker)
- ✅ Secure cache connections (TLS/SSL)
- ✅ Monitor cache performance and memory

**Azure Reference:** [Distributed Caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)

**CWE:** CWE-319 (Cleartext Transmission of Sensitive Information)

---

#### 1.4 Application State (Singleton Services)
**Description:** Application-wide state using singleton services in DI container.

**Detection Signals:**
- `services.AddSingleton<TService>()`
- Classes with application-wide state fields
- State accessed via dependency injection
- Thread-safe state management (locks, concurrent collections)

**Best Practices:**
- ✅ Use thread-safe collections (`ConcurrentDictionary`, etc.)
- ✅ Implement proper locking for mutable state
- ✅ Use backing stores for persistence
- ✅ Keep singleton services stateless when possible
- ⚠️ Be aware of memory leaks in long-running singletons

**Azure Reference:** [Dependency Injection in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection)

**CWE:** CWE-362 (Concurrent Execution using Shared Resource with Improper Synchronization)

---

#### 1.5 Sticky Session (Session Affinity)
**Description:** Load balancer routes requests from same user to same server.

**Detection Signals:**
- `arr-disable-session-affinity` header usage
- Application Request Routing (ARR) configuration
- Load balancer session affinity configuration
- Cookie-based routing headers

**Best Practices:**
- ✅ Use when circuit state/in-memory state required
- ✅ Implement graceful failover handling
- ✅ Use distributed state as backup
- ✅ Configure health checks appropriately
- ⚠️ Reduces load balancing effectiveness

**Azure Reference:** [Session Affinity in Azure](https://learn.microsoft.com/en-us/azure/app-service/configure-common#configure-general-settings)

---

#### 1.6 In-Memory Cache
**Description:** High-performance server-side caching using IMemoryCache.

**Detection Signals:**
- `IMemoryCache` interface usage
- `services.AddMemoryCache()`
- `cache.Set()`, `cache.Get()`, `cache.TryGetValue()`
- `MemoryCacheEntryOptions` configuration

**Best Practices:**
- ✅ Set appropriate expiration policies
- ✅ Implement cache eviction callbacks
- ✅ Use cache priorities for memory pressure
- ✅ Monitor cache hit/miss ratios
- ⚠️ Lost on server restart

**Azure Reference:** [In-Memory Caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory)

**CWE:** CWE-524 (Use of Cache Containing Sensitive Information)

---

#### 1.7 Distributed Cache
**Description:** Shared cache across multiple servers using IDistributedCache.

**Detection Signals:**
- `IDistributedCache` interface
- `cache.SetAsync()`, `cache.GetAsync()`, `cache.RemoveAsync()`
- Redis, SQL Server, or NCache configuration
- `DistributedCacheEntryOptions`

**Best Practices:**
- ✅ Use for scalable applications
- ✅ Serialize data efficiently (JSON, MessagePack, Protobuf)
- ✅ Implement sliding/absolute expiration
- ✅ Handle cache failures gracefully
- ✅ Use compression for large cached objects

**Azure Reference:** [Distributed Caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)

---

#### 1.8 TempData Provider
**Description:** Temporary data storage for redirect scenarios.

**Detection Signals:**
- `ITempDataProvider` interface
- `TempData` property in controllers/pages
- `TempData["key"]` access patterns
- Cookie or session-based TempData configuration

**Best Practices:**
- ✅ Use for redirect-after-post patterns
- ✅ Keep data small (serialization overhead)
- ✅ Peek instead of read to preserve data
- ✅ Don't use for long-term storage
- ⚠️ Cleared after being read

**Azure Reference:** [TempData in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state#tempdata)

---

### 2️⃣ CLIENT-SIDE STATE MANAGEMENT (7 Patterns)

#### 2.1 localStorage
**Description:** Browser-based persistent storage scoped to origin (domain).

**Detection Signals:**
- JavaScript `localStorage.setItem()`, `localStorage.getItem()`
- `JSRuntime.InvokeAsync("localStorage.setItem")`
- Blazor localStorage interop
- `ProtectedLocalStorage` usage

**Best Practices:**
- ✅ Don't store sensitive data (no encryption by default)
- ✅ Implement size limits (5-10MB browser limit)
- ✅ Use for user preferences, UI state
- ✅ Validate and sanitize data read from localStorage
- ⚠️ Persists across browser sessions

**Azure Reference:** [JavaScript Interop in Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability)

**CWE:** CWE-922 (Insecure Storage of Sensitive Information)

---

#### 2.2 sessionStorage
**Description:** Browser-based storage scoped to browser tab/window session.

**Detection Signals:**
- JavaScript `sessionStorage.setItem()`, `sessionStorage.getItem()`
- `JSRuntime.InvokeAsync("sessionStorage.setItem")`
- `ProtectedSessionStorage` usage

**Best Practices:**
- ✅ Use for temporary, tab-specific state
- ✅ Cleared when tab/window closes
- ✅ Validate data on retrieval
- ✅ Don't rely on for critical data (user can close tab)
- ⚠️ Not shared across tabs

**Azure Reference:** [JavaScript Interop in Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability)

**CWE:** CWE-922 (Insecure Storage of Sensitive Information)

---

#### 2.3 ProtectedBrowserStorage (ProtectedLocalStorage)
**Description:** Encrypted browser localStorage using ASP.NET Core Data Protection API.

**Detection Signals:**
- `ProtectedLocalStorage` class usage
- `Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage` namespace
- `SetAsync()`, `GetAsync()`, `DeleteAsync()` methods
- Data Protection configuration

**Best Practices:**
- ✅ Use for sensitive client-side data
- ✅ Data protected with server-side keys
- ✅ Implement proper key management
- ✅ Handle deserialization failures gracefully
- ⚠️ Requires server-side rendering (Blazor Server)

**Azure Reference:** [Protected Browser Storage](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#protected-browser-storage)

**CWE:** CWE-311 (Missing Encryption of Sensitive Data)

---

#### 2.4 ProtectedSessionStorage
**Description:** Encrypted browser sessionStorage using Data Protection API.

**Detection Signals:**
- `ProtectedSessionStorage` class usage
- Same namespace and methods as ProtectedLocalStorage
- Temporary encrypted storage

**Best Practices:**
- ✅ Use for sensitive temporary data
- ✅ Cleared when browser tab closes
- ✅ Server-side encryption
- ✅ Handle reconnection scenarios
- ⚠️ Blazor Server only

**Azure Reference:** [Protected Browser Storage](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#protected-browser-storage)

---

#### 2.5 IndexedDB
**Description:** Browser-based NoSQL database for large structured data.

**Detection Signals:**
- JavaScript `indexedDB.open()`
- Blazor IndexedDB libraries (Blazored.LocalStorage, etc.)
- Large client-side data storage
- Offline-first patterns

**Best Practices:**
- ✅ Use for large datasets (>5MB)
- ✅ Implement versioning for schema changes
- ✅ Handle browser compatibility
- ✅ Use for offline-capable apps
- ⚠️ Asynchronous API complexity

**Azure Reference:** [Progressive Web Apps with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app)

---

#### 2.6 Cookies
**Description:** HTTP cookies for small client-side state.

**Detection Signals:**
- `HttpContext.Response.Cookies.Append()`
- `HttpContext.Request.Cookies`
- Cookie authentication
- `CookieOptions` configuration

**Best Practices:**
- ✅ Mark sensitive cookies as `HttpOnly`, `Secure`, `SameSite`
- ✅ Use for authentication tokens
- ✅ Set appropriate expiration
- ✅ Comply with GDPR/privacy regulations
- ⚠️ Size limit (4KB)

**Azure Reference:** [HTTP Cookies in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/gdpr)

**CWE:** CWE-614 (Sensitive Cookie in HTTPS Session Without 'Secure' Attribute)

---

#### 2.7 Query String State
**Description:** State passed via URL query parameters.

**Detection Signals:**
- `NavigationManager.Uri` parsing
- `[SupplyParameterFromQuery]` attribute
- Query string parameter binding
- URL state management

**Best Practices:**
- ✅ Use for shareable/bookmarkable state
- ✅ Don't put sensitive data in URLs
- ✅ Validate and sanitize query parameters
- ✅ Implement URL encoding/decoding
- ⚠️ Limited size, visible to users

**Azure Reference:** [Routing in Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing)

**CWE:** CWE-598 (Use of GET Request Method With Sensitive Query Strings)

---

### 3️⃣ COMPONENT STATE MANAGEMENT (9 Patterns)

#### 3.1 Component Parameters
**Description:** Parent-to-child data flow via `[Parameter]` properties.

**Detection Signals:**
- `[Parameter]` attribute on properties
- `@bind-Value` directives
- Parent component setting child parameters
- Parameter change detection (`OnParametersSet`)

**Best Practices:**
- ✅ Make parameters immutable when possible
- ✅ Use `[EditorRequired]` for mandatory parameters
- ✅ Implement `OnParametersSet` for parameter changes
- ✅ Don't mutate parameter objects directly
- ⚠️ Can cause performance issues with frequent updates

**Azure Reference:** [Component Parameters](https://learn.microsoft.com/en-us/aspnet/core/blazor/components#component-parameters)

---

#### 3.2 Cascading Parameters
**Description:** Ancestor-to-descendant data flow without explicit parameter passing.

**Detection Signals:**
- `[CascadingParameter]` attribute
- `<CascadingValue>` component usage
- Implicit data flow to nested components
- Named cascading values

**Best Practices:**
- ✅ Use for cross-cutting concerns (theme, auth, etc.)
- ✅ Name cascading values to avoid conflicts
- ✅ Keep cascading data immutable
- ✅ Don't overuse - prefer explicit parameters
- ⚠️ Hidden dependencies can make code harder to understand

**Azure Reference:** [Cascading Values and Parameters](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters)

---

#### 3.3 AppState Container Pattern
**Description:** Observable state container service for cross-component state.

**Detection Signals:**
- State container class with `StateChanged` event
- `NotifyStateChanged()` method pattern
- Component subscription to state changes
- State service injected into multiple components

**Best Practices:**
- ✅ Implement `INotifyPropertyChanged` or custom events
- ✅ Call `StateHasChanged()` on component when state changes
- ✅ Unsubscribe from events in `Dispose()`
- ✅ Use for shared application state
- ⚠️ Can cause multiple re-renders

**Azure Reference:** [State Management - State Container](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#in-memory-state-container-service)

---

#### 3.4 Fluxor (Redux Pattern)
**Description:** Flux/Redux architecture for Blazor with immutable state, actions, reducers.

**Detection Signals:**
- Fluxor NuGet package (`Fluxor.Blazor.Web`)
- `IState<T>` interface usage
- `[FeatureState]` attribute
- Action classes and reducer methods
- `IDispatcher.Dispatch()` calls

**Best Practices:**
- ✅ Keep state immutable
- ✅ Use actions for all state changes
- ✅ Implement reducers as pure functions
- ✅ Use effects for side effects (API calls, etc.)
- ✅ Enable Redux DevTools for debugging

**Azure Reference:** [Fluxor Documentation](https://github.com/mrpmorris/Fluxor)

---

#### 3.5 MVVM Pattern (Model-View-ViewModel)
**Description:** Separation of UI from business logic using view models.

**Detection Signals:**
- ViewModel classes injected into components
- `INotifyPropertyChanged` implementation
- Data binding to ViewModel properties
- Command pattern for user actions

**Best Practices:**
- ✅ Keep ViewModels testable (no Blazor dependencies)
- ✅ Implement property change notifications
- ✅ Use Commands for user interactions
- ✅ Keep Views thin (logic in ViewModel)
- ⚠️ Can be overkill for simple components

**Azure Reference:** [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles#separation-of-concerns)

---

#### 3.6 Component Lifecycle State
**Description:** Managing state through component lifecycle methods.

**Detection Signals:**
- `OnInitialized`, `OnInitializedAsync` overrides
- `OnParametersSet`, `OnParametersSetAsync`
- `OnAfterRender`, `OnAfterRenderAsync`
- `Dispose`, `DisposeAsync` implementation

**Best Practices:**
- ✅ Load data in `OnInitializedAsync`
- ✅ React to parameter changes in `OnParametersSet`
- ✅ Clean up resources in `Dispose`
- ✅ Avoid expensive operations in `OnAfterRender`
- ⚠️ Understand render timing and async behavior

**Azure Reference:** [Blazor Lifecycle](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/lifecycle)

---

#### 3.7 EventCallback Pattern
**Description:** Child-to-parent communication via callbacks.

**Detection Signals:**
- `EventCallback<T>` parameter types
- `[Parameter] public EventCallback<T> OnSomethingChanged`
- `await OnSomethingChanged.InvokeAsync(value)`
- Two-way binding with `@bind-Value:event="OnValueChanged"`

**Best Practices:**
- ✅ Use for child-to-parent communication
- ✅ Always await EventCallback invocations
- ✅ Use `EventCallback` instead of `Action` for Blazor
- ✅ Trigger re-renders automatically
- ⚠️ Can create callback chains

**Azure Reference:** [EventCallback](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/event-handling#eventcallback)

---

#### 3.8 Two-Way Binding
**Description:** Synchronizing component state with form inputs.

**Detection Signals:**
- `@bind-Value` directive
- `@bind-Value:event` customization
- Custom two-way binding with `[Parameter]` and `EventCallback`
- Getter/setter patterns

**Best Practices:**
- ✅ Use `@bind` for simple bindings
- ✅ Customize binding event when needed (`oninput` vs `onchange`)
- ✅ Implement validation with binding
- ✅ Handle null values appropriately
- ⚠️ Can cause performance issues with complex objects

**Azure Reference:** [Data Binding](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/data-binding)

---

#### 3.9 Render Fragments
**Description:** Template patterns for component content composition.

**Detection Signals:**
- `RenderFragment` or `RenderFragment<T>` parameters
- `[Parameter] public RenderFragment ChildContent`
- Template parameters in components
- `@context` usage in templates

**Best Practices:**
- ✅ Use for flexible component composition
- ✅ Provide default render fragments when appropriate
- ✅ Use typed `RenderFragment<T>` for data templates
- ✅ Keep render fragments simple
- ⚠️ Can complicate component API

**Azure Reference:** [Templated Components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/templated-components)

---

### 4️⃣ CROSS-COMPONENT COMMUNICATION (5 Patterns)

#### 4.1 Message Bus / Event Aggregator
**Description:** Decoupled pub/sub messaging between components.

**Detection Signals:**
- Event aggregator service pattern
- `Subscribe<TMessage>()`, `Publish<TMessage>()` methods
- Mediator pattern implementations
- Weak reference subscriptions

**Best Practices:**
- ✅ Use for loosely coupled components
- ✅ Unsubscribe in `Dispose()` to prevent memory leaks
- ✅ Use typed messages
- ✅ Consider using MediatR library
- ⚠️ Can make data flow hard to trace

**Azure Reference:** [Mediator Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api#implement-the-command-and-command-handler-patterns)

---

#### 4.2 SignalR Real-Time Updates
**Description:** Server-to-client push notifications for state synchronization.

**Detection Signals:**
- `HubConnection` usage
- `hubConnection.On<T>()` event handlers
- `hubConnection.SendAsync()` calls
- SignalR hub configuration

**Best Practices:**
- ✅ Use for real-time collaborative features
- ✅ Implement reconnection logic
- ✅ Handle connection state changes
- ✅ Use strongly-typed hubs
- ✅ Secure hub methods with authorization

**Azure Reference:** [SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor)

**CWE:** CWE-306 (Missing Authentication for Critical Function)

---

#### 4.3 NavigationManager State
**Description:** Navigation-based state management and routing.

**Detection Signals:**
- `NavigationManager` injection
- `NavigateTo()` method calls
- `LocationChanged` event subscription
- Query string parsing for state

**Best Practices:**
- ✅ Use for wizard/multi-step flows
- ✅ Encode state in URLs for bookmarkability
- ✅ Handle navigation events for cleanup
- ✅ Validate navigation state
- ⚠️ Don't put sensitive data in URLs

**Azure Reference:** [Navigation and Routing](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing)

---

#### 4.4 JS Interop State Bridge
**Description:** State synchronization between .NET and JavaScript.

**Detection Signals:**
- `IJSRuntime` injection
- `InvokeAsync<T>()` calls
- `DotNetObjectReference` for callbacks
- `[JSInvokable]` attribute on methods

**Best Practices:**
- ✅ Minimize JS interop calls (performance overhead)
- ✅ Use batch operations when possible
- ✅ Dispose of DotNetObjectReference
- ✅ Handle JS exceptions gracefully
- ⚠️ Async-only in Blazor Server

**Azure Reference:** [JavaScript Interop](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability)

---

#### 4.5 Shared Service State
**Description:** Scoped or singleton services for shared state.

**Detection Signals:**
- Scoped service registration
- Service injection into multiple components
- Shared data properties in service
- Thread-safe state access

**Best Practices:**
- ✅ Use scoped services for request/circuit-specific state
- ✅ Use singleton for application-wide state
- ✅ Implement thread safety for singletons
- ✅ Raise events for state changes
- ⚠️ Scoped services = per-circuit in Blazor Server

**Azure Reference:** [Service Lifetimes](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/dependency-injection#service-lifetime)

---

### 5️⃣ STATE PERSISTENCE (6 Patterns)

#### 5.1 Entity Framework Core
**Description:** ORM for database state persistence.

**Detection Signals:**
- `DbContext` derived classes
- `DbSet<T>` properties
- LINQ queries on DbContext
- `SaveChanges()`, `SaveChangesAsync()` calls

**Best Practices:**
- ✅ Use async methods in Blazor Server
- ✅ Use DbContextFactory for Blazor Server (avoid scoped DbContext)
- ✅ Implement proper error handling
- ✅ Use migrations for schema changes
- ✅ Enable connection resiliency

**Azure Reference:** [EF Core with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core)

**CWE:** CWE-89 (SQL Injection) if raw SQL used

---

#### 5.2 Dapper Micro-ORM
**Description:** Lightweight data access using SQL queries and mapping.

**Detection Signals:**
- Dapper NuGet package
- `connection.Query<T>()`, `connection.Execute()` methods
- SQL string parameters
- Parameterized queries

**Best Practices:**
- ✅ Use parameterized queries (prevent SQL injection)
- ✅ Implement connection pooling
- ✅ Use async methods
- ✅ Handle null results gracefully
- ⚠️ No change tracking (manual updates)

**CWE:** CWE-89 (SQL Injection)

---

#### 5.3 Repository Pattern
**Description:** Abstraction layer over data access logic.

**Detection Signals:**
- `IRepository<T>` interface
- Generic repository methods (GetById, Add, Update, Delete)
- Unit of Work pattern
- Repository injection into services

**Best Practices:**
- ✅ Use for testability and abstraction
- ✅ Implement async methods
- ✅ Use specifications for complex queries
- ✅ Combine with Unit of Work for transactions
- ⚠️ Can be over-engineering for simple apps

**Azure Reference:** [Repository Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)

---

#### 5.4 CQRS (Command Query Responsibility Segregation)
**Description:** Separate read and write models for state.

**Detection Signals:**
- MediatR library usage
- Command and Query classes
- Separate read and write DbContexts
- `IRequest<T>` and `IRequestHandler<T>` interfaces

**Best Practices:**
- ✅ Use for complex domains
- ✅ Optimize read and write models separately
- ✅ Implement validation in command handlers
- ✅ Use event sourcing with CQRS for audit trails
- ⚠️ Adds complexity - only use when needed

**Azure Reference:** [CQRS Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)

---

#### 5.5 File-Based State
**Description:** Persisting state to JSON/XML files.

**Detection Signals:**
- `File.ReadAllText`, `File.WriteAllText` usage
- `JsonSerializer.Serialize`, `JsonSerializer.Deserialize`
- Configuration file management
- File system watchers

**Best Practices:**
- ✅ Use for configuration/settings files
- ✅ Implement file locking for concurrent access
- ✅ Use async file I/O
- ✅ Handle file not found exceptions
- ⚠️ Not suitable for high-volume data

**CWE:** CWE-732 (Incorrect Permission Assignment for Critical Resource)

---

#### 5.6 Azure Table Storage / Cosmos DB
**Description:** NoSQL cloud storage for state.

**Detection Signals:**
- Azure.Data.Tables NuGet package
- `TableClient`, `TableEntity` usage
- Cosmos DB SDK usage
- Partition key and row key patterns

**Best Practices:**
- ✅ Design partition keys for scalability
- ✅ Use batch operations for performance
- ✅ Implement retry policies
- ✅ Monitor RU consumption (Cosmos DB)
- ✅ Use async methods

**Azure Reference:** [Azure Table Storage](https://learn.microsoft.com/en-us/azure/storage/tables/table-storage-overview)

---

### 6️⃣ STATE SECURITY (5 Patterns)

#### 6.1 Data Protection API
**Description:** Encryption/decryption of sensitive state data.

**Detection Signals:**
- `IDataProtectionProvider` interface
- `IDataProtector.Protect()`, `Unprotect()` methods
- `services.AddDataProtection()` configuration
- Key ring configuration

**Best Practices:**
- ✅ Use for sensitive data encryption
- ✅ Configure key storage (Azure Key Vault, file system)
- ✅ Set key lifetime appropriately
- ✅ Use purpose strings for isolation
- ✅ Handle decryption failures (key rotation)

**Azure Reference:** [Data Protection in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction)

**CWE:** CWE-311 (Missing Encryption of Sensitive Data)

---

#### 6.2 Secure Token Storage
**Description:** Storing authentication tokens securely.

**Detection Signals:**
- JWT token storage patterns
- `ProtectedLocalStorage` for tokens
- HttpOnly cookies for tokens
- Token refresh logic

**Best Practices:**
- ✅ Use HttpOnly, Secure cookies for tokens
- ✅ Encrypt tokens in browser storage
- ✅ Implement token rotation
- ✅ Use short-lived access tokens + refresh tokens
- ⚠️ Never store tokens in localStorage (XSS risk)

**CWE:** CWE-522 (Insufficiently Protected Credentials)

---

#### 6.3 Anti-Forgery Token State
**Description:** CSRF protection using anti-forgery tokens.

**Detection Signals:**
- `[ValidateAntiForgeryToken]` attribute
- `@Html.AntiForgeryToken()` in forms
- `IAntiforgery` service usage
- `<form>` element anti-forgery validation

**Best Practices:**
- ✅ Always validate on state-changing operations
- ✅ Use for all forms that modify data
- ✅ Configure token settings appropriately
- ✅ Handle validation failures gracefully
- ✅ Automatic in Blazor forms

**Azure Reference:** [Anti-Forgery in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery)

**CWE:** CWE-352 (Cross-Site Request Forgery)

---

#### 6.4 Tenant Isolation State
**Description:** Multi-tenant state separation and isolation.

**Detection Signals:**
- Tenant ID in state/queries
- Row-level security patterns
- Tenant-specific DbContext configuration
- Tenant discriminator in EF Core

**Best Practices:**
- ✅ Always filter by tenant ID
- ✅ Use global query filters in EF Core
- ✅ Validate tenant access on every request
- ✅ Log tenant context for audit
- ✅ Use separate databases for strict isolation

**Azure Reference:** [Multi-Tenancy](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/overview)

**CWE:** CWE-566 (Authorization Bypass Through User-Controlled SQL Primary Key)

---

#### 6.5 Audit Trail State
**Description:** Tracking state changes for compliance and debugging.

**Detection Signals:**
- Audit log tables/entities
- Change tracking in EF Core
- `ChangeTracker.Entries()` usage
- Temporal tables (SQL Server)

**Best Practices:**
- ✅ Log who, what, when for all state changes
- ✅ Use temporal tables for automatic tracking
- ✅ Implement soft deletes (mark as deleted, don't remove)
- ✅ Store audit logs immutably
- ✅ Comply with data retention policies

**Azure Reference:** [Temporal Tables](https://learn.microsoft.com/en-us/sql/relational-databases/tables/temporal-tables)

**CWE:** CWE-778 (Insufficient Logging)

---

## 📊 Pattern Summary

| Category | Pattern Count | Key Focus |
|----------|---------------|-----------|
| Server-Side State | 8 | Circuit, Session, Cache, Distribution |
| Client-Side State | 7 | Browser Storage, Protection, Security |
| Component State | 9 | Parameters, Lifecycle, Binding, Templates |
| Cross-Component Comm. | 5 | Events, SignalR, Navigation, JS Interop |
| State Persistence | 6 | Database, Repository, CQRS, NoSQL |
| State Security | 5 | Encryption, Tokens, CSRF, Tenancy, Audit |
| **TOTAL** | **40** | **Comprehensive State Management** |

---

## 🎯 Critical Best Practices (Top 10)

1. **Never store sensitive data in browser storage without encryption** (CWE-922)
2. **Always use distributed cache/session for load-balanced apps** (avoid sticky sessions when possible)
3. **Use `DbContextFactory` in Blazor Server, not scoped `DbContext`** (threading issues)
4. **Implement proper disposal** - unsubscribe events, dispose state handlers
5. **Use parameterized queries** - prevent SQL injection (CWE-89)
6. **Set `HttpOnly`, `Secure`, `SameSite` on cookies** (CWE-614)
7. **Use Data Protection API for sensitive state** (CWE-311)
8. **Implement tenant isolation** - always filter by tenant ID (CWE-566)
9. **Handle circuit/connection failures** - implement reconnection logic
10. **Audit critical state changes** - compliance and debugging (CWE-778)

---

## 🔗 Key Microsoft Documentation Links

1. [ASP.NET Core Blazor State Management](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management)
2. [Session and State Management in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state)
3. [Distributed Caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
4. [Data Protection API](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction)
5. [Entity Framework Core with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-ef-core)
6. [Component Parameters and Cascading Values](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/cascading-values-and-parameters)
7. [SignalR with Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/tutorials/signalr-blazor)
8. [Protected Browser Storage](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management#protected-browser-storage)

---

## ✅ Next Steps

1. ✅ Create `StateManagementPatternDetector.cs` with 40 detection methods
2. ✅ Add 40 state management best practices to `BestPracticeValidationService.cs`
3. ✅ Create comprehensive unit tests for all patterns
4. ✅ Update `RoslynParser.cs` to integrate detector
5. ✅ Create documentation summarizing implementation
6. ✅ Index files and validate with MCP tools

