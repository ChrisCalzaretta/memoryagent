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
    Monitors            // Health check monitoring
}

