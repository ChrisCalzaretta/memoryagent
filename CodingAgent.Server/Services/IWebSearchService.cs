namespace CodingAgent.Server.Services;

/// <summary>
/// 🌐 WEB SEARCH SERVICE - Augments LLMs with real-time web knowledge
/// Searches official documentation + general web for code examples and best practices
/// </summary>
public interface IWebSearchService
{
    /// <summary>
    /// Research a task by searching official docs + web
    /// Returns ranked results with code snippets and examples
    /// </summary>
    Task<List<WebSearchResult>> ResearchTaskAsync(
        string task,
        string language,
        int maxResults = 10,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search official documentation for a specific language
    /// (e.g., Microsoft Docs for C#, python.org for Python)
    /// </summary>
    Task<List<WebSearchResult>> SearchOfficialDocsAsync(
        string language,
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// General web search across multiple sources
    /// (Stack Overflow, GitHub, dev blogs, etc.)
    /// </summary>
    Task<List<WebSearchResult>> SearchWebAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Search result with source attribution
/// </summary>
public record WebSearchResult
{
    public required string Source { get; init; }  // "Microsoft Docs", "Stack Overflow", "GitHub"
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string Snippet { get; init; }  // Code snippet or text excerpt
    public DateTime? PublishedDate { get; init; }
    public int Relevance { get; init; }  // 0-100 relevance score
    public List<string> Tags { get; init; } = new();  // ["csharp", "blazor", "best-practices"]
}

