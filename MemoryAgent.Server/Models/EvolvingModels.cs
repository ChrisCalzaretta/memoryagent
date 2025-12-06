namespace MemoryAgent.Server.Models;

/// <summary>
/// Represents an evolving pattern that can be versioned, tracked, and learned from.
/// Replaces static BestPracticesCatalog entries with learnable patterns.
/// </summary>
public class EvolvingPattern
{
    /// <summary>
    /// Unique identifier for this pattern version
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Human-readable pattern name (e.g., "cache-aside", "retry-logic")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Version number (increments with each evolution)
    /// </summary>
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// Pattern type classification
    /// </summary>
    public PatternType Type { get; set; }
    
    /// <summary>
    /// Pattern category
    /// </summary>
    public PatternCategory Category { get; set; }
    
    /// <summary>
    /// Human-readable recommendation/description
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference URL (Azure docs, etc.)
    /// </summary>
    public string ReferenceUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Detection rules (regex patterns, AST patterns, keywords)
    /// </summary>
    public List<PatternDetectionRule> DetectionRules { get; set; } = new();
    
    /// <summary>
    /// Example code snippets that demonstrate this pattern
    /// </summary>
    public List<string> Examples { get; set; } = new();
    
    /// <summary>
    /// Anti-pattern examples (what NOT to do)
    /// </summary>
    public List<string> AntiPatternExamples { get; set; } = new();
    
    /// <summary>
    /// Is this the currently active version?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Is this pattern deprecated?
    /// </summary>
    public bool IsDeprecated { get; set; } = false;
    
    /// <summary>
    /// Deprecation reason if deprecated
    /// </summary>
    public string? DeprecationReason { get; set; }
    
    /// <summary>
    /// Pattern that supersedes this one (if deprecated)
    /// </summary>
    public string? SupersededBy { get; set; }
    
    /// <summary>
    /// Parent version ID (null if this is version 1)
    /// </summary>
    public string? ParentVersionId { get; set; }
    
    /// <summary>
    /// When this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who/what created this version
    /// </summary>
    public string CreatedBy { get; set; } = "system";
    
    /// <summary>
    /// Reason for this version (evolution reason)
    /// </summary>
    public string? EvolutionReason { get; set; }
    
    #region Learning Metrics
    
    /// <summary>
    /// How many times this pattern was detected in code
    /// </summary>
    public int TimesDetected { get; set; } = 0;
    
    /// <summary>
    /// How many times detection was marked as correct/useful
    /// </summary>
    public int TimesUseful { get; set; } = 0;
    
    /// <summary>
    /// How many times detection was marked as incorrect/not useful
    /// </summary>
    public int TimesNotUseful { get; set; } = 0;
    
    /// <summary>
    /// Calculated usefulness score (0.0 - 1.0)
    /// </summary>
    public float UsefulnessScore => TimesDetected > 0 
        ? (float)TimesUseful / TimesDetected 
        : 0.5f;
    
    /// <summary>
    /// Confidence in this pattern (based on detection accuracy)
    /// </summary>
    public float Confidence { get; set; } = 0.5f;
    
    /// <summary>
    /// Last time this pattern was detected
    /// </summary>
    public DateTime? LastDetectedAt { get; set; }
    
    /// <summary>
    /// Last time this pattern's usefulness was evaluated
    /// </summary>
    public DateTime? LastEvaluatedAt { get; set; }
    
    #endregion
    
    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Rule for detecting a pattern in code
/// </summary>
public class PatternDetectionRule
{
    /// <summary>
    /// Type of detection rule
    /// </summary>
    public DetectionRuleType Type { get; set; }
    
    /// <summary>
    /// The pattern/expression to match
    /// </summary>
    public string Pattern { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this rule detects
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Languages this rule applies to
    /// </summary>
    public List<string> Languages { get; set; } = new() { "csharp" };
    
    /// <summary>
    /// Confidence boost when this rule matches (0.0 - 1.0)
    /// </summary>
    public float ConfidenceBoost { get; set; } = 0.1f;
    
    /// <summary>
    /// Is this rule required for pattern detection?
    /// </summary>
    public bool IsRequired { get; set; } = false;
}

/// <summary>
/// Types of pattern detection rules
/// </summary>
public enum DetectionRuleType
{
    /// <summary>Regex pattern match</summary>
    Regex,
    /// <summary>Simple keyword/contains match</summary>
    Keyword,
    /// <summary>AST node type match</summary>
    AstNodeType,
    /// <summary>Method/class name pattern</summary>
    NamePattern,
    /// <summary>Attribute presence</summary>
    Attribute,
    /// <summary>Interface implementation</summary>
    Interface,
    /// <summary>Inheritance check</summary>
    BaseClass,
    /// <summary>Using/import statement</summary>
    Import
}

/// <summary>
/// Represents a prompt template that can be versioned and evolved
/// </summary>
public class PromptTemplate
{
    /// <summary>
    /// Unique identifier for this prompt version
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Prompt name/key (e.g., "intent_classification", "page_transformation")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Version number
    /// </summary>
    public int Version { get; set; } = 1;
    
    /// <summary>
    /// The actual prompt content with placeholders
    /// Placeholders use {{variableName}} syntax
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this prompt does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Is this the currently active version?
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Is this prompt in A/B testing mode?
    /// </summary>
    public bool IsTestVariant { get; set; } = false;
    
    /// <summary>
    /// Traffic percentage for A/B testing (0-100)
    /// </summary>
    public int TestTrafficPercent { get; set; } = 10;
    
    /// <summary>
    /// Parent version ID (null if version 1)
    /// </summary>
    public string? ParentVersionId { get; set; }
    
    /// <summary>
    /// When this version was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who/what created this version
    /// </summary>
    public string CreatedBy { get; set; } = "system";
    
    /// <summary>
    /// Reason for creating this version
    /// </summary>
    public string? EvolutionReason { get; set; }
    
    /// <summary>
    /// Variables/placeholders expected in this prompt
    /// </summary>
    public List<PromptVariable> Variables { get; set; } = new();
    
    #region Learning Metrics
    
    /// <summary>
    /// Total number of times this prompt was used
    /// </summary>
    public int TimesUsed { get; set; } = 0;
    
    /// <summary>
    /// Number of successful executions (based on feedback)
    /// </summary>
    public int SuccessCount { get; set; } = 0;
    
    /// <summary>
    /// Number of failed/poor executions
    /// </summary>
    public int FailureCount { get; set; } = 0;
    
    /// <summary>
    /// Calculated success rate (0.0 - 1.0)
    /// </summary>
    public float SuccessRate => TimesUsed > 0 
        ? (float)SuccessCount / TimesUsed 
        : 0.5f;
    
    /// <summary>
    /// Average confidence from LLM responses
    /// </summary>
    public float AvgConfidence { get; set; } = 0.5f;
    
    /// <summary>
    /// Sum of all confidence scores (for calculating average)
    /// </summary>
    public float TotalConfidence { get; set; } = 0f;
    
    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AvgResponseTimeMs { get; set; } = 0;
    
    /// <summary>
    /// Total response time (for calculating average)
    /// </summary>
    public long TotalResponseTimeMs { get; set; } = 0;
    
    /// <summary>
    /// Last time this prompt was used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }
    
    #endregion
    
    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Variable/placeholder definition for a prompt
/// </summary>
public class PromptVariable
{
    /// <summary>
    /// Variable name (without braces)
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what this variable should contain
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Is this variable required?
    /// </summary>
    public bool IsRequired { get; set; } = true;
    
    /// <summary>
    /// Default value if not provided
    /// </summary>
    public string? DefaultValue { get; set; }
    
    /// <summary>
    /// Example value for documentation
    /// </summary>
    public string? Example { get; set; }
}

/// <summary>
/// Record of a single prompt execution
/// </summary>
public class PromptExecution
{
    /// <summary>
    /// Unique execution identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// ID of the prompt template used
    /// </summary>
    public string PromptId { get; set; } = string.Empty;
    
    /// <summary>
    /// Prompt name (for quick lookup)
    /// </summary>
    public string PromptName { get; set; } = string.Empty;
    
    /// <summary>
    /// Prompt version used
    /// </summary>
    public int PromptVersion { get; set; }
    
    /// <summary>
    /// The rendered prompt (with variables filled in)
    /// </summary>
    public string RenderedPrompt { get; set; } = string.Empty;
    
    /// <summary>
    /// Input variables used
    /// </summary>
    public Dictionary<string, string> InputVariables { get; set; } = new();
    
    /// <summary>
    /// The LLM response
    /// </summary>
    public string Response { get; set; } = string.Empty;
    
    /// <summary>
    /// Response time in milliseconds
    /// </summary>
    public long ResponseTimeMs { get; set; }
    
    /// <summary>
    /// Confidence score from parsing (if applicable)
    /// </summary>
    public float? Confidence { get; set; }
    
    /// <summary>
    /// Was parsing successful?
    /// </summary>
    public bool ParseSuccess { get; set; } = true;
    
    /// <summary>
    /// Parse error message if failed
    /// </summary>
    public string? ParseError { get; set; }
    
    /// <summary>
    /// Session ID (if in a session)
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Context/workspace
    /// </summary>
    public string? Context { get; set; }
    
    /// <summary>
    /// When execution occurred
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    
    #region Outcome Tracking
    
    /// <summary>
    /// Has outcome been recorded?
    /// </summary>
    public bool OutcomeRecorded { get; set; } = false;
    
    /// <summary>
    /// Was the execution successful (user feedback)?
    /// </summary>
    public bool? WasSuccessful { get; set; }
    
    /// <summary>
    /// User rating (1-5) if provided
    /// </summary>
    public int? UserRating { get; set; }
    
    /// <summary>
    /// Feedback comments
    /// </summary>
    public string? FeedbackComments { get; set; }
    
    /// <summary>
    /// When outcome was recorded
    /// </summary>
    public DateTime? OutcomeRecordedAt { get; set; }
    
    /// <summary>
    /// Implicit success signals detected
    /// </summary>
    public List<string> ImplicitSuccessSignals { get; set; } = new();
    
    /// <summary>
    /// Implicit failure signals detected
    /// </summary>
    public List<string> ImplicitFailureSignals { get; set; } = new();
    
    #endregion
}

/// <summary>
/// Tracks pattern detection feedback for learning
/// </summary>
public class PatternDetectionFeedback
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Pattern ID that was detected
    /// </summary>
    public string PatternId { get; set; } = string.Empty;
    
    /// <summary>
    /// Pattern name
    /// </summary>
    public string PatternName { get; set; } = string.Empty;
    
    /// <summary>
    /// File where pattern was detected
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Line number of detection
    /// </summary>
    public int LineNumber { get; set; }
    
    /// <summary>
    /// Code snippet that was matched
    /// </summary>
    public string CodeSnippet { get; set; } = string.Empty;
    
    /// <summary>
    /// Detection confidence at time of detection
    /// </summary>
    public float DetectionConfidence { get; set; }
    
    /// <summary>
    /// Was the detection correct/useful?
    /// </summary>
    public bool? WasCorrect { get; set; }
    
    /// <summary>
    /// Feedback type
    /// </summary>
    public PatternFeedbackType FeedbackType { get; set; }
    
    /// <summary>
    /// Feedback comments
    /// </summary>
    public string? Comments { get; set; }
    
    /// <summary>
    /// Suggested improvement to detection
    /// </summary>
    public string? SuggestedImprovement { get; set; }
    
    /// <summary>
    /// Session ID
    /// </summary>
    public string? SessionId { get; set; }
    
    /// <summary>
    /// Context/workspace
    /// </summary>
    public string? Context { get; set; }
    
    /// <summary>
    /// When detected
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When feedback was provided
    /// </summary>
    public DateTime? FeedbackAt { get; set; }
}

/// <summary>
/// Types of pattern feedback
/// </summary>
public enum PatternFeedbackType
{
    /// <summary>Detection was correct and useful</summary>
    Correct,
    /// <summary>Detection was correct but not useful for this context</summary>
    CorrectButNotUseful,
    /// <summary>Detection was incorrect (false positive)</summary>
    FalsePositive,
    /// <summary>Detection was missed (false negative)</summary>
    FalseNegative,
    /// <summary>Suggestion for new pattern</summary>
    NewPatternSuggestion
}

/// <summary>
/// Request to suggest a new pattern based on code
/// </summary>
public class PatternSuggestionRequest
{
    /// <summary>
    /// Code example demonstrating the pattern
    /// </summary>
    public string CodeExample { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of what the pattern does
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Suggested pattern name
    /// </summary>
    public string? SuggestedName { get; set; }
    
    /// <summary>
    /// Why this should be a pattern
    /// </summary>
    public string? Rationale { get; set; }
    
    /// <summary>
    /// Context/workspace
    /// </summary>
    public string? Context { get; set; }
    
    /// <summary>
    /// Session ID
    /// </summary>
    public string? SessionId { get; set; }
}

