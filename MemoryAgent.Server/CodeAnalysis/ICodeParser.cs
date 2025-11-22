using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Interface for parsing code files and extracting code elements
/// </summary>
public interface ICodeParser
{
    /// <summary>
    /// Parse a C# file and extract code elements
    /// </summary>
    Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse C# code from a string
    /// </summary>
    Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of parsing a code file
/// </summary>
public class ParseResult
{
    public List<CodeMemory> CodeElements { get; set; } = new();
    public List<CodeRelationship> Relationships { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success => !Errors.Any();
}

