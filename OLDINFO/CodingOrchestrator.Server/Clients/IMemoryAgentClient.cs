using AgentContracts.Requests;

namespace CodingOrchestrator.Server.Clients;

/// <summary>
/// Client for communicating with MemoryAgent.Server
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Get context for a task (similar questions, patterns, etc.)
    /// </summary>
    Task<CodeContext?> GetContextAsync(string task, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Store a successful Q&A for future recall
    /// </summary>
    Task StoreQaAsync(string question, string answer, List<string> relevantFiles, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Get active prompt by name from Lightning
    /// </summary>
    Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken);

    /// <summary>
    /// Record feedback on prompt performance
    /// </summary>
    Task RecordPromptFeedbackAsync(string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken);

    /// <summary>
    /// Check if MemoryAgent is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 TASK LEARNING: Record detailed task failure for future avoidance
    /// </summary>
    Task RecordTaskFailureAsync(TaskFailureRecord failure, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 TASK LEARNING: Query lessons learned from similar failed tasks
    /// </summary>
    Task<TaskLessonsResult> QueryTaskLessonsAsync(string taskDescription, List<string> keywords, string language, CancellationToken cancellationToken);
    
    // ═══════════════════════════════════════════════════════════════════════
    // 🚀 SMART CODE GENERATION - Phase 1: Foundation
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// 📋 Generate a plan with checklist before code generation
    /// </summary>
    Task<TaskPlan> GeneratePlanAsync(string task, string language, string context, CancellationToken cancellationToken);
    
    /// <summary>
    /// 📋 Update plan checklist status
    /// </summary>
    Task UpdatePlanStatusAsync(string planId, string stepId, string status, CancellationToken cancellationToken);
    
    /// <summary>
    /// 📁 Index a generated file immediately (for context awareness)
    /// </summary>
    Task IndexFileAsync(string filePath, string content, string language, string context, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🔍 Smart search for relevant code/context (searches Qdrant + Neo4j)
    /// </summary>
    Task<List<SmartSearchResult>> SmartSearchAsync(string query, string context, int limit, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🔍 Get all symbols (classes, methods) in the project context
    /// </summary>
    Task<ProjectSymbols> GetProjectSymbolsAsync(string context, CancellationToken cancellationToken);
    
    /// <summary>
    /// ✅ Validate imports before Docker execution
    /// </summary>
    Task<ImportValidationResult> ValidateImportsAsync(string code, string language, string context, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🎉 Store successful task approach for future learning
    /// </summary>
    Task StoreSuccessfulTaskAsync(TaskSuccessRecord success, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🔎 Query similar successful tasks for guidance
    /// </summary>
    Task<SimilarTasksResult> QuerySimilarSuccessfulTasksAsync(string task, string language, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🎨 Query design system for UI tasks
    /// </summary>
    Task<DesignContext?> GetDesignContextAsync(string context, CancellationToken cancellationToken);
}

/// <summary>
/// Record of a failed task for learning
/// </summary>
public class TaskFailureRecord
{
    public required string TaskDescription { get; set; }
    public List<string> TaskKeywords { get; set; } = new();
    public required string Language { get; set; }
    public required string FailurePhase { get; set; }  // code_generation, validation, docker_build, docker_run
    public required string ErrorMessage { get; set; }
    public string ErrorPattern { get; set; } = "unknown";  // Categorized error type
    public List<string> ApproachesTried { get; set; } = new();
    public List<string> ModelsUsed { get; set; } = new();
    public int IterationsAttempted { get; set; }
    public string LessonsLearned { get; set; } = "";
    public string Context { get; set; } = "default";
}

/// <summary>
/// Result of querying task lessons
/// </summary>
public class TaskLessonsResult
{
    public int FoundLessons { get; set; }
    public string AvoidanceAdvice { get; set; } = "";
    public List<string> SuggestedApproaches { get; set; } = new();
    public List<TaskLesson> Lessons { get; set; } = new();
}

/// <summary>
/// A single lesson learned from a past failure
/// </summary>
public class TaskLesson
{
    public string TaskDescription { get; set; } = "";
    public string Language { get; set; } = "";
    public string FailurePhase { get; set; } = "";
    public string ErrorPattern { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public List<string> ApproachesTried { get; set; } = new();
    public string LessonsLearned { get; set; } = "";
}

/// <summary>
/// Prompt information from Lightning
/// </summary>
public class PromptInfo
{
    public required string Name { get; set; }
    public required string Content { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════
// 🚀 SMART CODE GENERATION - Models
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Task plan with checklist generated before code execution
/// </summary>
public class TaskPlan
{
    public string PlanId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Task { get; set; } = "";
    public string Language { get; set; } = "";
    public string Context { get; set; } = "";
    public List<PlanStep> Steps { get; set; } = new();
    public string SemanticBreakdown { get; set; } = "";
    public List<string> RequiredClasses { get; set; } = new();
    public List<string> RequiredMethods { get; set; } = new();
    public List<string> DependencyOrder { get; set; } = new();  // Which files to generate first
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single step in the task plan
/// </summary>
public class PlanStep
{
    public string StepId { get; set; } = "";
    public int Order { get; set; }
    public string Description { get; set; } = "";
    public string FileName { get; set; } = "";  // Which file this step creates/modifies
    public string Status { get; set; } = "pending";  // pending, in_progress, completed, failed
    public List<string> Dependencies { get; set; } = new();  // Which steps must complete first
    public string SemanticSpec { get; set; } = "";  // Detailed spec for this step
}

/// <summary>
/// All symbols (classes, methods, etc.) in a project context
/// </summary>
public class ProjectSymbols
{
    public string Context { get; set; } = "";
    public List<string> Files { get; set; } = new();
    public List<ClassSymbol> Classes { get; set; } = new();
    public List<FunctionSymbol> Functions { get; set; } = new();
    public Dictionary<string, string> ImportPaths { get; set; } = new();  // symbol -> import statement
}

/// <summary>
/// A class symbol with its methods and properties
/// </summary>
public class ClassSymbol
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public string Signature { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Methods { get; set; } = new();
    public List<string> Properties { get; set; } = new();
    public string ImportStatement { get; set; } = "";
}

/// <summary>
/// A function/method symbol
/// </summary>
public class FunctionSymbol
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public string ClassName { get; set; } = "";  // Empty if standalone function
    public string Signature { get; set; } = "";
    public string Description { get; set; } = "";
    public string ReturnType { get; set; } = "";
    public List<string> Parameters { get; set; } = new();
}

/// <summary>
/// Result of import validation
/// </summary>
public class ImportValidationResult
{
    public bool IsValid { get; set; }
    public List<ImportCheck> Imports { get; set; } = new();
    public List<string> AvailableModules { get; set; } = new();
    public string Summary { get; set; } = "";
}

/// <summary>
/// Status of a single import check
/// </summary>
public class ImportCheck
{
    public string ImportStatement { get; set; } = "";
    public string Module { get; set; } = "";
    public string Symbol { get; set; } = "";
    public bool IsValid { get; set; }
    public string Reason { get; set; } = "";
    public string Suggestion { get; set; } = "";
}

/// <summary>
/// Record of a successful task for learning
/// </summary>
public class TaskSuccessRecord
{
    public required string TaskDescription { get; set; }
    public required string Language { get; set; }
    public required string Context { get; set; }
    public string ApproachUsed { get; set; } = "";
    public List<string> PatternsUsed { get; set; } = new();
    public List<string> FilesGenerated { get; set; } = new();
    public List<CodeSnippet> UsefulSnippets { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public int IterationsNeeded { get; set; }
    public int FinalScore { get; set; }
    public string ModelUsed { get; set; } = "";
    public string SemanticStructure { get; set; } = "";  // The class/method structure that worked
}

/// <summary>
/// A reusable code snippet
/// </summary>
public class CodeSnippet
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string Language { get; set; } = "";
}

/// <summary>
/// Result of querying similar successful tasks
/// </summary>
public class SimilarTasksResult
{
    public int FoundTasks { get; set; }
    public List<SimilarTask> Tasks { get; set; } = new();
    public List<CodeSnippet> ReusableSnippets { get; set; } = new();
    public string SuggestedApproach { get; set; } = "";
    public string SuggestedStructure { get; set; } = "";
}

/// <summary>
/// A similar successful task
/// </summary>
public class SimilarTask
{
    public string TaskDescription { get; set; } = "";
    public float Similarity { get; set; }
    public string ApproachUsed { get; set; } = "";
    public string Structure { get; set; } = "";
    public List<string> FilesGenerated { get; set; } = new();
}

/// <summary>
/// Design context for UI tasks
/// </summary>
public class DesignContext
{
    public string BrandName { get; set; } = "";
    public Dictionary<string, string> Colors { get; set; } = new();
    public Dictionary<string, string> Typography { get; set; } = new();
    public Dictionary<string, string> Spacing { get; set; } = new();
    public List<ComponentPattern> Components { get; set; } = new();
    public List<string> AccessibilityRules { get; set; } = new();
}

/// <summary>
/// A UI component pattern
/// </summary>
public class ComponentPattern
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Template { get; set; } = "";
    public Dictionary<string, string> Props { get; set; } = new();
}

/// <summary>
/// Result from smart search (Qdrant + Neo4j)
/// </summary>
public class SmartSearchResult
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string Type { get; set; } = "";  // class, method, file
    public string Content { get; set; } = "";
    public float Score { get; set; }
}

