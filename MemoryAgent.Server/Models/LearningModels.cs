namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents a user session/conversation for context tracking.
/// Sessions help the Memory Agent remember what was discussed and when.
/// </summary>
public class Session
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Workspace context this session belongs to
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// When the session started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the session ended (null if still active)
    /// </summary>
    public DateTime? EndedAt { get; set; }
    
    /// <summary>
    /// Summary of what was discussed/done in this session
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// Files that were discussed or accessed in this session
    /// </summary>
    public List<string> FilesDiscussed { get; set; } = new();
    
    /// <summary>
    /// Files that were edited/modified in this session
    /// </summary>
    public List<string> FilesEdited { get; set; } = new();
    
    /// <summary>
    /// Questions asked during this session
    /// </summary>
    public List<string> QuestionsAsked { get; set; } = new();
    
    /// <summary>
    /// Additional metadata about the session
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Maps questions to relevant code for instant recall.
/// When a similar question is asked again, we can immediately return relevant code.
/// </summary>
public class QuestionMapping
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The question that was asked
    /// </summary>
    public string Question { get; set; } = string.Empty;
    
    /// <summary>
    /// Embedding vector of the question for semantic matching
    /// </summary>
    public float[]? QuestionEmbedding { get; set; }
    
    /// <summary>
    /// The answer/response that was given
    /// </summary>
    public string Answer { get; set; } = string.Empty;
    
    /// <summary>
    /// Files that were relevant to answering this question
    /// </summary>
    public List<string> RelevantFiles { get; set; } = new();
    
    /// <summary>
    /// Classes/methods that were relevant
    /// </summary>
    public List<string> RelevantCodeElements { get; set; } = new();
    
    /// <summary>
    /// Workspace context
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// How many times this question (or similar) was asked
    /// </summary>
    public int TimesAsked { get; set; } = 1;
    
    /// <summary>
    /// When this question was first asked
    /// </summary>
    public DateTime FirstAskedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this question was last asked
    /// </summary>
    public DateTime LastAskedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Was the answer helpful? (for learning)
    /// </summary>
    public bool? WasHelpful { get; set; }
}

/// <summary>
/// Tracks importance metrics for code elements.
/// Used to prioritize what to remember and surface in searches.
/// </summary>
public class ImportanceMetric
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// File path this metric is for
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Code element name (class, method, etc.)
    /// </summary>
    public string ElementName { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of element (Class, Method, File, etc.)
    /// </summary>
    public CodeMemoryType ElementType { get; set; }
    
    /// <summary>
    /// Workspace context
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// How many times this element was accessed/viewed
    /// </summary>
    public int AccessCount { get; set; } = 0;
    
    /// <summary>
    /// How many times this element was edited
    /// </summary>
    public int EditCount { get; set; } = 0;
    
    /// <summary>
    /// How many times this element was discussed in sessions
    /// </summary>
    public int DiscussionCount { get; set; } = 0;
    
    /// <summary>
    /// How many times this element was returned in search results
    /// </summary>
    public int SearchResultCount { get; set; } = 0;
    
    /// <summary>
    /// How many times this element was selected/clicked from search results
    /// </summary>
    public int SelectedCount { get; set; } = 0;
    
    /// <summary>
    /// When this element was last accessed
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }
    
    /// <summary>
    /// When this element was last edited
    /// </summary>
    public DateTime? LastEditedAt { get; set; }
    
    /// <summary>
    /// Calculated importance score (0.0 - 1.0)
    /// Higher = more important, should be prioritized in search results
    /// </summary>
    public float ImportanceScore { get; set; } = 0.5f;
    
    /// <summary>
    /// Recency score (0.0 - 1.0) - decays over time
    /// </summary>
    public float RecencyScore { get; set; } = 0.5f;
    
    /// <summary>
    /// Frequency score (0.0 - 1.0) - based on access patterns
    /// </summary>
    public float FrequencyScore { get; set; } = 0.5f;
    
    /// <summary>
    /// How many other elements reference this one
    /// </summary>
    public int IncomingReferenceCount { get; set; } = 0;
    
    /// <summary>
    /// How many elements this one references
    /// </summary>
    public int OutgoingReferenceCount { get; set; } = 0;
    
    /// <summary>
    /// Is this element on a critical path (high-impact if changed)
    /// </summary>
    public bool IsCriticalPath { get; set; } = false;
    
    /// <summary>
    /// Is this an entry point (controller, API endpoint, etc.)
    /// </summary>
    public bool IsEntryPoint { get; set; } = false;
}

/// <summary>
/// Tracks co-editing patterns between files.
/// Files that are frequently edited together are likely related.
/// </summary>
public class CoEditMetric
{
    /// <summary>
    /// First file in the pair
    /// </summary>
    public string FilePath1 { get; set; } = string.Empty;
    
    /// <summary>
    /// Second file in the pair
    /// </summary>
    public string FilePath2 { get; set; } = string.Empty;
    
    /// <summary>
    /// Workspace context
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// How many times these files were edited in the same session
    /// </summary>
    public int CoEditCount { get; set; } = 0;
    
    /// <summary>
    /// Session IDs where these files were co-edited
    /// </summary>
    public List<string> SessionIds { get; set; } = new();
    
    /// <summary>
    /// When these files were first co-edited
    /// </summary>
    public DateTime FirstCoEditAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When these files were last co-edited
    /// </summary>
    public DateTime LastCoEditAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Calculated co-edit strength (0.0 - 1.0)
    /// Higher = more likely to be related
    /// </summary>
    public float CoEditStrength { get; set; } = 0.0f;
}

/// <summary>
/// Business domain tag for semantic organization
/// </summary>
public class DomainTag
{
    /// <summary>
    /// Domain name (e.g., "Authentication", "Billing", "Orders")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Domain description
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Keywords that indicate this domain
    /// </summary>
    public List<string> Keywords { get; set; } = new();
    
    /// <summary>
    /// Confidence that the code belongs to this domain (0.0 - 1.0)
    /// </summary>
    public float Confidence { get; set; } = 0.0f;
}

/// <summary>
/// Reward signal for reinforcement learning
/// Tracks whether search results were helpful
/// </summary>
public class RewardSignal
{
    /// <summary>
    /// The query that was made
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// The result that was interacted with
    /// </summary>
    public string ResultPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of signal
    /// </summary>
    public RewardType Type { get; set; }
    
    /// <summary>
    /// Reward value (-1.0 to +2.0)
    /// </summary>
    public float Reward { get; set; }
    
    /// <summary>
    /// When this signal was recorded
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Session this occurred in
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Types of reward signals for learning
/// </summary>
public enum RewardType
{
    /// <summary>
    /// User clicked/selected this result (positive)
    /// </summary>
    Selected = 1,
    
    /// <summary>
    /// User ignored this result (slightly negative)
    /// </summary>
    Ignored = 2,
    
    /// <summary>
    /// User edited this file after viewing (strong positive)
    /// </summary>
    EditedAfterView = 3,
    
    /// <summary>
    /// User explicitly marked as helpful (strong positive)
    /// </summary>
    MarkedHelpful = 4,
    
    /// <summary>
    /// User explicitly marked as not helpful (strong negative)
    /// </summary>
    MarkedNotHelpful = 5,
    
    /// <summary>
    /// Result was discussed in conversation (positive)
    /// </summary>
    Discussed = 6
}

/// <summary>
/// Aggregated metrics for MCP tool usage.
/// Tracks how tools are being used for optimization and learning.
/// </summary>
public class ToolUsageMetric
{
    /// <summary>
    /// Name of the MCP tool (e.g., "query", "index_file", "start_session")
    /// </summary>
    public string ToolName { get; set; } = string.Empty;
    
    /// <summary>
    /// Workspace context
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of times this tool was called
    /// </summary>
    public int CallCount { get; set; } = 0;
    
    /// <summary>
    /// Number of successful calls
    /// </summary>
    public int SuccessCount { get; set; } = 0;
    
    /// <summary>
    /// Number of failed calls
    /// </summary>
    public int ErrorCount { get; set; } = 0;
    
    /// <summary>
    /// Average execution time in milliseconds
    /// </summary>
    public double AvgDurationMs { get; set; } = 0;
    
    /// <summary>
    /// Total execution time in milliseconds (for calculating average)
    /// </summary>
    public long TotalDurationMs { get; set; } = 0;
    
    /// <summary>
    /// When this tool was first called
    /// </summary>
    public DateTime FirstCalledAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When this tool was last called
    /// </summary>
    public DateTime LastCalledAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last query/question used with this tool
    /// </summary>
    public string? LastQuery { get; set; }
    
    /// <summary>
    /// Most common queries used with this tool (top 5)
    /// </summary>
    public List<string> CommonQueries { get; set; } = new();
}

/// <summary>
/// Detailed log of individual tool invocations.
/// Enables learning from tool usage patterns.
/// </summary>
public class ToolInvocation
{
    /// <summary>
    /// Unique invocation identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Name of the MCP tool
    /// </summary>
    public string ToolName { get; set; } = string.Empty;
    
    /// <summary>
    /// Workspace context
    /// </summary>
    public string Context { get; set; } = string.Empty;
    
    /// <summary>
    /// Session ID this invocation occurred in
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Query or question extracted from arguments
    /// </summary>
    public string? Query { get; set; }
    
    /// <summary>
    /// Serialized tool arguments (JSON)
    /// </summary>
    public string? ArgumentsJson { get; set; }
    
    /// <summary>
    /// Whether the call succeeded
    /// </summary>
    public bool Success { get; set; } = true;
    
    /// <summary>
    /// Error message if the call failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Execution time in milliseconds
    /// </summary>
    public long DurationMs { get; set; } = 0;
    
    /// <summary>
    /// Summary of the result (truncated)
    /// </summary>
    public string? ResultSummary { get; set; }
    
    /// <summary>
    /// Number of results returned (if applicable)
    /// </summary>
    public int? ResultCount { get; set; }
    
    /// <summary>
    /// When this invocation occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

