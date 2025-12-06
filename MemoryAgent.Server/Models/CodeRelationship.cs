namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a relationship between code elements
/// </summary>
public class CodeRelationship
{
    public string FromName { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public RelationshipType Type { get; set; }
    public string Context { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum RelationshipType
{
    // Structural relationships
    Inherits,           // Class inherits from base class
    Implements,         // Class implements interface
    Defines,            // File defines class, class defines method, etc.
    
    // Dependency relationships
    Uses,               // General usage (field references, object creation)
    Calls,              // Method calls another method
    Injects,            // Constructor injection (DI)
    Imports,            // Using/import statement
    
    // Type relationships
    HasType,            // Property/parameter has a type
    UsesGeneric,        // Uses generic type parameter
    ReturnsType,        // Method returns a type
    AcceptsType,        // Method parameter accepts a type
    
    // Metadata relationships
    HasAttribute,       // Has attribute/annotation
    Throws,             // Method throws exception
    Catches,            // Method catches exception
    
    // Organizational relationships
    InNamespace,        // Element is in namespace
    InContext,          // Element is in context
    Contains,           // Container contains element (solution contains project, etc.)
    DependsOn,          // Project depends on NuGet package
    References,         // Project references another project
    
    // Pattern relationships
    FollowsPattern,     // Follows a code pattern
    SimilarTo,          // Similar to another element
    EvolvedFrom,        // Evolved from previous version
    ModifiedFrom,       // Modified from original
    
    // ASP.NET Core semantic relationships
    Exposes,            // Endpoint exposes a controller action
    Authorizes,         // Requires authorization (role/policy)
    Accesses,           // Accesses database entity/table
    Queries,            // Executes query against entity
    Projects,           // Projects entity to DTO
    Includes,           // EF Include/ThenInclude eager loading
    GroupsBy,           // Groups query results
    Registers,          // DI registration (services.Add*)
    ImplementsRegistration, // DI interface → implementation
    Configures,         // Configures a service/component
    Validates,          // Validates a model/property
    ValidatesProperty,  // Validates specific property
    RequiresPolicy,     // Requires authorization policy
    DefinesPolicy,      // Defines authorization policy
    RequiresRole,       // Requires specific role
    RequiresClaim,      // Requires specific claim
    Handles,            // Handles message/command/query (MediatR)
    Schedules,          // Schedules background job
    UsesMiddleware,     // Uses middleware component
    Precedes,           // Middleware execution order
    ReadsConfig,        // Reads configuration section
    BindsConfig,        // Binds configuration to class
    BackgroundTask,     // Background/hosted service task
    Monitors,           // Health check monitoring
    
    // API & Infrastructure patterns
    HasApiVersion,      // Has API version attribute
    Documents,          // API documentation (Swagger)
    AllowsOrigin,       // CORS policy allows origin
    Caches,             // Response caching
    Binds,              // Custom model binder
    Filters,            // Action/Exception filter
    RateLimits,         // Rate limiting policy
    HandlesException,   // Exception filter handling
    
    // ═══════════════════════════════════════════════════════════════
    // LEARNING & SESSION RELATIONSHIPS (Agent Lightning)
    // These relationships enable the Memory Agent to learn and remember
    // ═══════════════════════════════════════════════════════════════
    
    // Session & Conversation Context
    DiscussedIn,        // Code was discussed in a session
    ModifiedDuring,     // Code was modified during a session
    AccessedIn,         // Code was accessed in a session
    QuestionedAbout,    // Question was asked about this code
    AnsweredWith,       // Code was used to answer a question
    
    // Edit History & Co-occurrence
    CoEditedWith,       // Files frequently edited together
    EditedAfter,        // This file was edited after another
    EditedBefore,       // This file was edited before another
    AlwaysEditedWith,   // Strong co-edit pattern (always together)
    
    // Semantic Understanding
    RelatedTo,          // Semantically related via embedding similarity
    AlternativeTo,      // Alternative implementation
    DeprecatedFor,      // Deprecated in favor of another
    ReplacedBy,         // Replaced by newer code
    Supersedes,         // This code supersedes older code
    
    // Documentation & Examples
    DocumentedIn,       // Documented in README/wiki/docs
    HasExample,         // Has usage example
    TestedBy,           // Has corresponding test
    ExampleOf,          // Is an example of a pattern/concept
    
    // Temporal & History
    CreatedBefore,      // Temporal ordering
    ModifiedAfter,      // Modification sequence
    BrokenBy,           // Code broke after this change
    FixedBy,            // Code fixed by this change
    RequiresUpdate,     // Needs update when dependency changes
    
    // Bug & Feature Tracking
    FixedBug,           // Fixed a specific bug
    ImplementedFeature, // Implemented a specific feature
    ReviewedBy,         // Code reviewed (link to review)
    
    // Business Domain
    BelongsToDomain,    // Belongs to business domain (Auth, Billing, etc.)
    CrossesDomain,      // Crosses domain boundaries
    
    // Importance & Priority
    IsCriticalFor,      // Critical for a feature/system
    IsEntryPointFor,    // Entry point for a workflow
    DependedOnBy        // Many things depend on this (high impact)
}

