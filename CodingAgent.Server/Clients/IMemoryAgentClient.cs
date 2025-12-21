using AgentContracts.Models;

namespace CodingAgent.Server.Clients;

/// <summary>
/// Client for communicating with MemoryAgent.Server (Lightning)
/// </summary>
public interface IMemoryAgentClient
{
    /// <summary>
    /// Get the active prompt by name from Lightning
    /// </summary>
    Task<PromptInfo?> GetPromptAsync(string promptName, CancellationToken cancellationToken);

    /// <summary>
    /// Find similar past solutions for learning
    /// </summary>
    Task<List<SimilarSolution>> FindSimilarSolutionsAsync(string task, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Get relevant patterns for the task
    /// </summary>
    Task<List<PatternInfo>> GetPatternsAsync(string task, string context, CancellationToken cancellationToken);

    /// <summary>
    /// Record feedback on prompt performance
    /// </summary>
    Task RecordPromptFeedbackAsync(string promptName, bool wasSuccessful, int? rating, CancellationToken cancellationToken);

    /// <summary>
    /// Check if MemoryAgent is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// 🔍 SEARCH BEFORE WRITE: Find existing code that might already solve the task
    /// Returns existing services, methods, and patterns to avoid duplication
    /// </summary>
    Task<ExistingCodeContext> SearchExistingCodeAsync(string task, string context, string? workspacePath, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Record model performance for future selection
    /// </summary>
    Task RecordModelPerformanceAsync(ModelPerformanceRecord record, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Query the best model for a task based on historical performance
    /// </summary>
    Task<BestModelResponse> QueryBestModelAsync(BestModelRequest request, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🧠 MODEL LEARNING: Get aggregated stats for all models
    /// </summary>
    Task<List<ModelStats>> GetModelStatsAsync(string? language, string? taskType, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🎨 DESIGN AGENT: Get brand guidelines for a project context
    /// </summary>
    Task<BrandInfo?> GetBrandAsync(string context, CancellationToken cancellationToken);
    
    /// <summary>
    /// 🎨 DESIGN AGENT: Validate UI code against brand guidelines
    /// Returns validation score and issues to fix
    /// </summary>
    Task<DesignValidationResult?> ValidateDesignAsync(string context, string code, CancellationToken cancellationToken);
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

/// <summary>
/// Similar solution from Q&A memory
/// </summary>
public class SimilarSolution
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public List<string> RelevantFiles { get; set; } = new();
    public double Similarity { get; set; }
}

/// <summary>
/// Pattern from Lightning
/// </summary>
public class PatternInfo
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? CodeExample { get; set; }
    public string? BestPractice { get; set; }
}

/// <summary>
/// 🔍 Existing code context to prevent duplication (Search Before Write)
/// </summary>
public class ExistingCodeContext
{
    /// <summary>
    /// Existing services/interfaces found that might be relevant
    /// </summary>
    public List<ExistingService> ExistingServices { get; set; } = new();
    
    /// <summary>
    /// Existing methods that might already solve the task
    /// </summary>
    public List<ExistingMethod> ExistingMethods { get; set; } = new();
    
    /// <summary>
    /// Similar implementations found in the codebase
    /// </summary>
    public List<SimilarImplementation> SimilarImplementations { get; set; } = new();
    
    /// <summary>
    /// Patterns that are already implemented (don't recreate!)
    /// </summary>
    public List<string> ImplementedPatterns { get; set; } = new();
    
    /// <summary>
    /// Files that might need to be modified (not created)
    /// </summary>
    public List<string> FilesToModify { get; set; } = new();
    
    /// <summary>
    /// Whether we found existing code that could solve this task
    /// </summary>
    public bool HasReusableCode => ExistingServices.Any() || ExistingMethods.Any() || SimilarImplementations.Any();
    
    /// <summary>
    /// Summary for the LLM prompt
    /// </summary>
    public string GetPromptSummary()
    {
        var lines = new List<string>();
        
        if (ExistingServices.Any())
        {
            lines.Add("=== ⚠️ EXISTING SERVICES - DO NOT RECREATE ===");
            foreach (var svc in ExistingServices)
            {
                lines.Add($"• {svc.Name} ({svc.FilePath})");
                if (svc.Methods.Any())
                {
                    lines.Add($"  Methods: {string.Join(", ", svc.Methods)}");
                }
                if (!string.IsNullOrEmpty(svc.Description))
                {
                    lines.Add($"  Purpose: {svc.Description}");
                }
            }
            lines.Add("");
        }
        
        if (ExistingMethods.Any())
        {
            lines.Add("=== ⚠️ EXISTING METHODS - REUSE THESE ===");
            foreach (var method in ExistingMethods)
            {
                lines.Add($"• {method.FullSignature}");
                lines.Add($"  In: {method.ClassName} ({method.FilePath})");
                if (!string.IsNullOrEmpty(method.Description))
                {
                    lines.Add($"  Does: {method.Description}");
                }
            }
            lines.Add("");
        }
        
        if (SimilarImplementations.Any())
        {
            lines.Add("=== 📚 SIMILAR CODE EXISTS - LEARN FROM THESE ===");
            foreach (var impl in SimilarImplementations)
            {
                lines.Add($"• {impl.FilePath} ({impl.Similarity:P0} similar)");
                lines.Add($"  {impl.Description}");
            }
            lines.Add("");
        }
        
        if (ImplementedPatterns.Any())
        {
            lines.Add("=== ✅ PATTERNS ALREADY IMPLEMENTED ===");
            foreach (var pattern in ImplementedPatterns)
            {
                lines.Add($"• {pattern}");
            }
            lines.Add("");
        }
        
        if (FilesToModify.Any())
        {
            lines.Add("=== 📝 MODIFY THESE FILES (don't create new) ===");
            foreach (var file in FilesToModify)
            {
                lines.Add($"• {file}");
            }
            lines.Add("");
        }
        
        return string.Join("\n", lines);
    }
}

/// <summary>
/// An existing service/interface found in the codebase
/// </summary>
public class ExistingService
{
    public required string Name { get; set; }
    public required string FilePath { get; set; }
    public string? Description { get; set; }
    public List<string> Methods { get; set; } = new();
    public bool IsInterface { get; set; }
}

/// <summary>
/// An existing method that might be reusable
/// </summary>
public class ExistingMethod
{
    public required string Name { get; set; }
    public required string ClassName { get; set; }
    public required string FilePath { get; set; }
    public required string FullSignature { get; set; }
    public string? Description { get; set; }
    public double Relevance { get; set; }
}

/// <summary>
/// A similar implementation found in the codebase
/// </summary>
public class SimilarImplementation
{
    public required string FilePath { get; set; }
    public required string Description { get; set; }
    public double Similarity { get; set; }
    public string? CodeSnippet { get; set; }
}

/// <summary>
/// Brand information from Design Agent
/// </summary>
public class BrandInfo
{
    public required string BrandName { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? FontFamily { get; set; }
    public string? ThemePreference { get; set; }
    public string? VisualStyle { get; set; }
    public List<string> ComponentGuidelines { get; set; } = new();
    public string? FullBrandJson { get; set; } // Full brand system as JSON
}

/// <summary>
/// Design validation result from Design Agent
/// </summary>
public class DesignValidationResult
{
    public int Score { get; set; } // 0-10
    public string? Grade { get; set; } // A, B, C, D, F
    public List<DesignIssue> Issues { get; set; } = new();
    public string? Summary { get; set; }
}

/// <summary>
/// A design issue found during validation
/// </summary>
public class DesignIssue
{
    public required string Type { get; set; } // color, typography, spacing, accessibility, etc.
    public required string Message { get; set; }
    public string? Suggestion { get; set; }
    public string? Severity { get; set; } // critical, warning, info
}



