using System.Text;

namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a code element in memory (file, class, method, etc.)
/// </summary>
public class CodeMemory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public CodeMemoryType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public float[]? Embedding { get; set; }
    
    // NEW: Semantic enrichment fields for smarter embeddings
    public string Summary { get; set; } = string.Empty;         // XML doc/JSDoc/docstring summary
    public string Signature { get; set; } = string.Empty;       // Method/class signature only
    public string Purpose { get; set; } = string.Empty;         // Extracted purpose/description
    public List<string> Tags { get; set; } = new();             // ["async", "public", "crud", "api"]
    public List<string> Dependencies { get; set; } = new();     // ["IUserRepository", "IJwtService"]
    
    /// <summary>
    /// Generates optimized text for embedding that prioritizes semantic metadata.
    /// Ensures critical information (type, signature, purpose) is always included within token limits.
    /// </summary>
    /// <remarks>
    /// Target: 1800 characters (~450 tokens for mxbai-embed-large 512 token limit)
    /// Structure:
    /// - [TYPE] prefix (always first)
    /// - Signature (if exists)
    /// - Purpose/Summary (if exists)
    /// - Tags (if exists)
    /// - Dependencies (if exists)
    /// - Code content (truncated if needed using head+tail strategy)
    /// </remarks>
    public string GetEmbeddingText()
    {
        const int MaxChars = 1800; // Safe limit for 512 token embedding models
        
        var builder = new StringBuilder();
        
        // 1. Type prefix (always first) - ~20 chars
        builder.AppendLine($"[{Type.ToString().ToUpper()}] {Name}");
        
        // 2. Signature (if exists) - ~100 chars
        if (!string.IsNullOrWhiteSpace(Signature))
        {
            builder.AppendLine($"Signature: {Signature}");
        }
        
        // 3. Purpose/Summary (if exists) - ~200 chars
        if (!string.IsNullOrWhiteSpace(Purpose))
        {
            builder.AppendLine($"Purpose: {Purpose}");
        }
        else if (!string.IsNullOrWhiteSpace(Summary))
        {
            builder.AppendLine($"Summary: {Summary}");
        }
        
        // 4. Tags (if exists) - ~50 chars
        if (Tags.Any())
        {
            builder.AppendLine($"Tags: {string.Join(", ", Tags)}");
        }
        
        // 5. Dependencies (if exists) - ~100 chars
        if (Dependencies.Any())
        {
            builder.AppendLine($"Dependencies: {string.Join(", ", Dependencies)}");
        }
        
        // 6. Code content (remaining budget)
        if (!string.IsNullOrWhiteSpace(Content))
        {
            builder.AppendLine("Code:");
            builder.Append(Content);
        }
        
        var fullText = builder.ToString();
        
        // Smart truncation: Never truncate metadata, only code section
        return TruncateSmartly(fullText, MaxChars);
    }
    
    /// <summary>
    /// Intelligently truncates text while preserving critical metadata.
    /// Only truncates the "Code:" section using head+tail strategy.
    /// </summary>
    private string TruncateSmartly(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }
        
        // Find where the code section starts
        var codeStart = text.IndexOf("Code:", StringComparison.Ordinal);
        
        if (codeStart == -1)
        {
            // No code section, just truncate from the end
            return text.Substring(0, maxChars);
        }
        
        // Preserve all metadata (everything before "Code:")
        var prefix = text.Substring(0, codeStart + 6); // Include "Code:\n"
        var remainingBudget = maxChars - prefix.Length;
        
        if (remainingBudget < 100)
        {
            // Metadata too long (rare), fallback to simple truncation
            return text.Substring(0, maxChars);
        }
        
        var codeContent = text.Substring(codeStart + 6);
        
        if (codeContent.Length <= remainingBudget)
        {
            // Code fits within budget
            return text;
        }
        
        // Head+Tail strategy: 60% from start, 40% from end
        var headSize = (int)(remainingBudget * 0.6);
        var tailSize = remainingBudget - headSize - 3; // -3 for "..."
        
        var head = codeContent.Substring(0, Math.Min(headSize, codeContent.Length));
        var tail = codeContent.Length > headSize + tailSize 
            ? codeContent.Substring(codeContent.Length - tailSize) 
            : string.Empty;
        
        return prefix + head + "..." + tail;
    }
}

public enum CodeMemoryType
{
    File,
    Class,
    Method,
    Property,
    Interface,
    Pattern,
    
    // New types
    Test,               // Unit/Integration tests
    Enum,               // Enumerations
    Record,             // C# records
    Struct,             // Value types
    Delegate,           // Delegates
    Event,              // Events
    Constant,           // Constants
    
    // Architecture patterns
    Repository,         // Data access repositories
    Service,            // Service layer
    Controller,         // API controllers
    Middleware,         // ASP.NET middleware
    Filter,             // Action/Exception filters
    
    // Data access
    DbContext,          // EF DbContext
    Entity,             // Database entities
    Migration,          // DB migrations
    
    // Frontend
    Component,          // React/Vue/Angular components
    Hook,               // React hooks
    
    // API
    Endpoint           // API endpoints
}

