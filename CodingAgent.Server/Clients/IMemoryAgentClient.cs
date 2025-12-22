using CodingAgent.Server.Services;

namespace CodingAgent.Server.Clients;

/// <summary>
/// Client for calling MemoryAgent's search and analysis tools
/// Exposes Qdrant (semantic search) and Neo4j (graph relationships)
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Smart search using Qdrant vector embeddings
    /// </summary>
    Task<List<SearchResult>> SmartSearchAsync(string query, int limit = 5, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get co-edited files using Neo4j graph
    /// </summary>
    Task<List<RelatedFile>> GetCoEditedFilesAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get file dependencies from Neo4j
    /// </summary>
    Task<List<string>> GetFileDependenciesAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Index a file in MemoryAgent (Qdrant vector embeddings + Neo4j relationships)
    /// </summary>
    Task IndexFileAsync(string filePath, string content, string? context = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Bulk index multiple files
    /// </summary>
    Task IndexFilesAsync(List<(string Path, string Content)> files, string? context = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Store a prompt in MemoryAgent (Lightning) - Neo4j + Qdrant
    /// </summary>
    Task StorePromptAsync(PromptMetadata prompt, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a prompt from MemoryAgent
    /// </summary>
    Task<PromptMetadata?> GetPromptAsync(string promptId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record prompt feedback (for learning/evolution)
    /// </summary>
    Task RecordPromptFeedbackAsync(string promptId, PromptUsageResult result, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Store a successful Q&A pair for learning
    /// </summary>
    Task StoreQAAsync(string question, string answer, int score, string language, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find similar questions that were asked before
    /// </summary>
    Task<List<QAPair>> FindSimilarQuestionsAsync(string question, int limit = 5, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Initialize Lightning context/session for a workspace
    /// </summary>
    Task<LightningContext> GetContextAsync(string workspacePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get current workspace status from Lightning
    /// </summary>
    Task<WorkspaceStatus> GetWorkspaceStatusAsync(string? context = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record that a file was edited/generated (for Lightning session tracking)
    /// </summary>
    Task RecordFileEditedAsync(string filePath, string context, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get Lightning's recommendations for the workspace
    /// </summary>
    Task<List<string>> GetRecommendationsAsync(string? context = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get important files in the workspace (Lightning's analysis)
    /// </summary>
    Task<List<string>> GetImportantFilesAsync(string workspacePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Call any MCP tool (generic method for tools not yet wrapped)
    /// </summary>
    Task<string?> CallMcpToolAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken = default);
}

public record SearchResult
{
    public string Path { get; init; } = "";
    public string Snippet { get; init; } = "";
    public double Score { get; init; }
}

public record RelatedFile
{
    public string Path { get; init; } = "";
    public double Score { get; init; }
    public string Relationship { get; init; } = "co-edited";
}

public class QAPair
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public int Score { get; set; }
    public string Language { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class LightningContext
{
    public string ContextName { get; set; } = "";
    public string WorkspacePath { get; set; } = "";
    public DateTime SessionStarted { get; set; }
    public List<string> DiscussedFiles { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class WorkspaceStatus
{
    public string WorkspacePath { get; set; } = "";
    public List<string> RecentFiles { get; set; } = new();
    public List<string> ImportantFiles { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public int TotalFilesIndexed { get; set; }
    public DateTime LastActivity { get; set; }
    public Dictionary<string, int> LanguageBreakdown { get; set; } = new();
}
