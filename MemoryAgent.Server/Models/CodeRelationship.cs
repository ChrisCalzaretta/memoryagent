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
    ModifiedFrom        // Modified from original
}

