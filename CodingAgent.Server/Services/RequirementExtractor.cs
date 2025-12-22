namespace CodingAgent.Server.Services;

/// <summary>
/// Extracts missing requirements by asking clarifying questions
/// Prevents wrong assumptions and wasted iterations
/// </summary>
public interface IRequirementExtractor
{
    Task<RequirementSet> ExtractRequirementsAsync(
        string task,
        CodebaseContext? codebaseContext,
        CancellationToken cancellationToken);
    
    List<ClarifyingQuestion> GetClarifyingQuestions(string task);
}

public class RequirementExtractor : IRequirementExtractor
{
    private readonly ILogger<RequirementExtractor> _logger;
    
    public RequirementExtractor(ILogger<RequirementExtractor> logger)
    {
        _logger = logger;
    }
    
    public async Task<RequirementSet> ExtractRequirementsAsync(
        string task,
        CodebaseContext? codebaseContext,
        CancellationToken cancellationToken)
    {
        var requirements = new RequirementSet { OriginalTask = task };
        
        // Get questions
        var questions = GetClarifyingQuestions(task);
        
        _logger.LogInformation("📋 Need to ask {Count} clarifying questions", questions.Count);
        
        requirements.Questions = questions;
        requirements.NeedsUserInput = questions.Any();
        
        return requirements;
    }
    
    public List<ClarifyingQuestion> GetClarifyingQuestions(string task)
    {
        var questions = new List<ClarifyingQuestion>();
        var taskLower = task.ToLowerInvariant();
        
        // AUTHENTICATION questions
        if (taskLower.Contains("auth") || taskLower.Contains("login"))
        {
            questions.Add(new ClarifyingQuestion
            {
                Question = "Which authentication method?",
                Options = ["JWT", "Session-based", "OAuth2", "Azure AD", "Identity Server"],
                Category = "Authentication",
                Required = true
            });
            
            questions.Add(new ClarifyingQuestion
            {
                Question = "Should we support refresh tokens?",
                Options = ["Yes", "No"],
                Category = "Authentication",
                Required = true
            });
            
            questions.Add(new ClarifyingQuestion
            {
                Question = "Where to store tokens?",
                Options = ["HttpOnly cookies", "LocalStorage", "SessionStorage", "Both"],
                Category = "Authentication",
                Required = false
            });
        }
        
        // DATABASE questions
        if (taskLower.Contains("database") || taskLower.Contains("db") || taskLower.Contains("data"))
        {
            questions.Add(new ClarifyingQuestion
            {
                Question = "Which database?",
                Options = ["SQL Server", "PostgreSQL", "MySQL", "SQLite", "CosmosDB", "MongoDB"],
                Category = "Database",
                Required = true
            });
            
            questions.Add(new ClarifyingQuestion
            {
                Question = "Use Entity Framework or Dapper?",
                Options = ["Entity Framework", "Dapper", "ADO.NET"],
                Category = "Database",
                Required = true
            });
        }
        
        // UI FRAMEWORK questions
        if (taskLower.Contains("ui") || taskLower.Contains("interface") || taskLower.Contains("web"))
        {
            questions.Add(new ClarifyingQuestion
            {
                Question = "Which UI framework?",
                Options = ["Blazor Server", "Blazor WASM", "React", "Vue", "Angular", "MVC"],
                Category = "UI",
                Required = true
            });
            
            questions.Add(new ClarifyingQuestion
            {
                Question = "CSS framework?",
                Options = ["Bootstrap", "Tailwind", "Material UI", "Custom", "None"],
                Category = "UI",
                Required = false
            });
        }
        
        // API questions
        if (taskLower.Contains("api") || taskLower.Contains("rest") || taskLower.Contains("endpoint"))
        {
            questions.Add(new ClarifyingQuestion
            {
                Question = "API versioning strategy?",
                Options = ["URL path (/v1/)", "Query string (?version=1)", "Header", "None"],
                Category = "API",
                Required = false
            });
            
            questions.Add(new ClarifyingQuestion
            {
                Question = "Include Swagger/OpenAPI docs?",
                Options = ["Yes", "No"],
                Category = "API",
                Required = false
            });
        }
        
        // CACHING questions
        if (taskLower.Contains("cache") || taskLower.Contains("caching"))
        {
            questions.Add(new ClarifyingQuestion
            {
                Question = "Cache provider?",
                Options = ["Memory (IMemoryCache)", "Redis", "Distributed (SQL)", "Hybrid"],
                Category = "Caching",
                Required = true
            });
            
            questions.Add(new ClarifyingQuestion
            {
                Question = "Default cache expiration?",
                Options = ["1 minute", "5 minutes", "15 minutes", "1 hour", "Custom per item"],
                Category = "Caching",
                Required = false
            });
        }
        
        // GENERAL ARCHITECTURE
        questions.Add(new ClarifyingQuestion
        {
            Question = "Error handling strategy?",
            Options = ["Return error codes", "Throw exceptions", "Result<T> pattern", "Problem Details"],
            Category = "Architecture",
            Required = false
        });
        
        questions.Add(new ClarifyingQuestion
        {
            Question = "Logging level?",
            Options = ["Debug (verbose)", "Information (normal)", "Warning (production)", "Error (minimal)"],
            Category = "Architecture",
            Required = false
        });
        
        return questions;
    }
}

public class RequirementSet
{
    public string OriginalTask { get; set; } = "";
    public List<ClarifyingQuestion> Questions { get; set; } = new();
    public Dictionary<string, string> Answers { get; set; } = new();
    public bool NeedsUserInput { get; set; }
    public string EnhancedTask => BuildEnhancedTask();
    
    private string BuildEnhancedTask()
    {
        if (!Answers.Any()) return OriginalTask;
        
        var enhanced = new System.Text.StringBuilder();
        enhanced.AppendLine(OriginalTask);
        enhanced.AppendLine();
        enhanced.AppendLine("REQUIREMENTS:");
        
        foreach (var answer in Answers)
        {
            enhanced.AppendLine($"- {answer.Key}: {answer.Value}");
        }
        
        return enhanced.ToString();
    }
}

public class ClarifyingQuestion
{
    public string Question { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public string Category { get; set; } = "";
    public bool Required { get; set; }
    public string? DefaultAnswer { get; set; }
}


