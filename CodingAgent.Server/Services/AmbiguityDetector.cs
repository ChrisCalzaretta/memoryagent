namespace CodingAgent.Server.Services;

/// <summary>
/// Detects ambiguous terms in tasks and provides smart defaults
/// Prevents wrong assumptions and wasted iterations
/// </summary>
public interface IAmbiguityDetector
{
    List<Ambiguity> DetectAmbiguities(string task, string? workspacePath = null);
    Task<Dictionary<string, string>> ResolveAmbiguitiesAsync(
        List<Ambiguity> ambiguities, 
        Func<Ambiguity, Task<string>> askUserFunc,
        CancellationToken cancellationToken);
}

public class AmbiguityDetector : IAmbiguityDetector
{
    private readonly ILogger<AmbiguityDetector> _logger;
    
    public AmbiguityDetector(ILogger<AmbiguityDetector> logger)
    {
        _logger = logger;
    }
    
    public List<Ambiguity> DetectAmbiguities(string task, string? workspacePath = null)
    {
        var ambiguities = new List<Ambiguity>();
        var taskLower = task.ToLowerInvariant();
        
        // ═══════════════════════════════════════════════════════════
        // AUTHENTICATION AMBIGUITIES
        // ═══════════════════════════════════════════════════════════
        if (ContainsAny(taskLower, "auth", "login", "authentication", "sign in"))
        {
            ambiguities.Add(new Ambiguity
            {
                Term = "authentication",
                PossibleMeanings = new()
                {
                    { "JWT", "JSON Web Tokens - Stateless, modern, API-friendly" },
                    { "Session", "Cookie-based sessions - Traditional, server-side state" },
                    { "OAuth2", "Third-party login (Google, Microsoft, GitHub)" },
                    { "Azure AD", "Enterprise SSO with Microsoft identity" },
                    { "Identity Server", "Full-featured identity provider" }
                },
                Question = "Which authentication method should be used?",
                SmartDefault = DetectAuthDefault(taskLower, workspacePath),
                Category = "Authentication",
                Impact = "High"
            });
            
            ambiguities.Add(new Ambiguity
            {
                Term = "token storage",
                PossibleMeanings = new()
                {
                    { "HttpOnly Cookies", "Secure, can't be accessed by JavaScript" },
                    { "LocalStorage", "Persistent, accessible by JS (less secure)" },
                    { "SessionStorage", "Tab-scoped, cleared on close" },
                    { "Memory only", "Most secure, lost on refresh" }
                },
                Question = "Where should authentication tokens be stored?",
                SmartDefault = "HttpOnly Cookies",
                Category = "Authentication",
                Impact = "Medium"
            });
        }
        
        // ═══════════════════════════════════════════════════════════
        // DATABASE AMBIGUITIES
        // ═══════════════════════════════════════════════════════════
        if (ContainsAny(taskLower, "database", "db", "data", "storage", "persistence"))
        {
            ambiguities.Add(new Ambiguity
            {
                Term = "database",
                PossibleMeanings = new()
                {
                    { "SQL Server", "Microsoft's enterprise DB, Windows-optimized" },
                    { "PostgreSQL", "Open source, advanced features, cross-platform" },
                    { "MySQL", "Popular, web-friendly, good performance" },
                    { "SQLite", "Embedded, file-based, zero config" },
                    { "CosmosDB", "Azure NoSQL, globally distributed" },
                    { "MongoDB", "Document DB, schema-less" }
                },
                Question = "Which database should be used?",
                SmartDefault = DetectDatabaseDefault(workspacePath),
                Category = "Database",
                Impact = "High"
            });
            
            ambiguities.Add(new Ambiguity
            {
                Term = "data access",
                PossibleMeanings = new()
                {
                    { "Entity Framework Core", "Full ORM, migrations, LINQ" },
                    { "Dapper", "Micro-ORM, fast, more control" },
                    { "ADO.NET", "Low-level, maximum performance" }
                },
                Question = "Which data access technology?",
                SmartDefault = DetectDataAccessDefault(workspacePath),
                Category = "Database",
                Impact = "Medium"
            });
        }
        
        // ═══════════════════════════════════════════════════════════
        // CACHING AMBIGUITIES
        // ═══════════════════════════════════════════════════════════
        if (ContainsAny(taskLower, "cache", "caching"))
        {
            ambiguities.Add(new Ambiguity
            {
                Term = "cache provider",
                PossibleMeanings = new()
                {
                    { "IMemoryCache", "In-memory, single server, simple" },
                    { "Redis", "Distributed, persistent, scalable" },
                    { "SQL Server", "Distributed cache using SQL" },
                    { "Hybrid", "L1 (memory) + L2 (Redis)" }
                },
                Question = "Which caching provider?",
                SmartDefault = DetectCachingDefault(taskLower),
                Category = "Caching",
                Impact = "Medium"
            });
            
            ambiguities.Add(new Ambiguity
            {
                Term = "cache expiration",
                PossibleMeanings = new()
                {
                    { "1 minute", "Very short, for rapidly changing data" },
                    { "5 minutes", "Short, good default" },
                    { "15 minutes", "Medium, common choice" },
                    { "1 hour", "Long, for stable data" },
                    { "Sliding", "Reset on access" }
                },
                Question = "Default cache expiration time?",
                SmartDefault = "5 minutes",
                Category = "Caching",
                Impact = "Low"
            });
        }
        
        // ═══════════════════════════════════════════════════════════
        // UI FRAMEWORK AMBIGUITIES
        // ═══════════════════════════════════════════════════════════
        if (ContainsAny(taskLower, "ui", "interface", "web", "frontend", "page", "component"))
        {
            if (!ContainsAny(taskLower, "blazor", "react", "vue", "angular", "mvc"))
            {
                ambiguities.Add(new Ambiguity
                {
                    Term = "ui framework",
                    PossibleMeanings = new()
                    {
                        { "Blazor Server", ".NET, server-side rendering, SignalR" },
                        { "Blazor WASM", ".NET, client-side, WebAssembly" },
                        { "React", "JavaScript, component-based, popular" },
                        { "Vue", "JavaScript, progressive framework" },
                        { "Angular", "TypeScript, full framework" },
                        { "ASP.NET MVC", "Traditional server-side rendering" }
                    },
                    Question = "Which UI framework?",
                    SmartDefault = DetectUIDefault(workspacePath),
                    Category = "UI",
                    Impact = "High"
                });
            }
            
            ambiguities.Add(new Ambiguity
            {
                Term = "css framework",
                PossibleMeanings = new()
                {
                    { "Bootstrap", "Popular, large ecosystem, responsive" },
                    { "Tailwind CSS", "Utility-first, modern, customizable" },
                    { "Material UI", "Google's design system" },
                    { "Custom", "Write custom CSS" },
                    { "None", "No CSS framework" }
                },
                Question = "Which CSS framework?",
                SmartDefault = "Bootstrap",
                Category = "UI",
                Impact = "Low"
            });
        }
        
        // ═══════════════════════════════════════════════════════════
        // API AMBIGUITIES
        // ═══════════════════════════════════════════════════════════
        if (ContainsAny(taskLower, "api", "rest", "endpoint", "service"))
        {
            ambiguities.Add(new Ambiguity
            {
                Term = "api style",
                PossibleMeanings = new()
                {
                    { "REST", "Standard HTTP verbs, resource-based" },
                    { "GraphQL", "Query language, flexible" },
                    { "gRPC", "High performance, protobuf" },
                    { "Minimal APIs", ".NET 6+ minimal syntax" }
                },
                Question = "Which API style?",
                SmartDefault = "REST",
                Category = "API",
                Impact = "Medium"
            });
            
            ambiguities.Add(new Ambiguity
            {
                Term = "api documentation",
                PossibleMeanings = new()
                {
                    { "Swagger/OpenAPI", "Interactive docs, code generation" },
                    { "None", "No automatic documentation" }
                },
                Question = "Include API documentation?",
                SmartDefault = "Swagger/OpenAPI",
                Category = "API",
                Impact = "Low"
            });
        }
        
        // ═══════════════════════════════════════════════════════════
        // ERROR HANDLING AMBIGUITIES
        // ═══════════════════════════════════════════════════════════
        if (ambiguities.Any()) // If ANY ambiguities, ask about error handling
        {
            ambiguities.Add(new Ambiguity
            {
                Term = "error handling",
                PossibleMeanings = new()
                {
                    { "Exceptions", "Throw/catch exceptions (traditional)" },
                    { "Result<T>", "Return success/failure objects (functional)" },
                    { "Status codes", "Return HTTP status codes" },
                    { "Problem Details", "RFC 7807 standard error format" }
                },
                Question = "Error handling pattern?",
                SmartDefault = DetectErrorHandlingDefault(workspacePath),
                Category = "Architecture",
                Impact = "Medium"
            });
        }
        
        if (ambiguities.Any())
        {
            _logger.LogInformation("⚠️ Detected {Count} ambiguities in task", ambiguities.Count);
            foreach (var amb in ambiguities)
            {
                _logger.LogDebug("  - {Term}: {Options} (default: {Default})", 
                    amb.Term, amb.PossibleMeanings.Count, amb.SmartDefault);
            }
        }
        
        return ambiguities;
    }
    
    public async Task<Dictionary<string, string>> ResolveAmbiguitiesAsync(
        List<Ambiguity> ambiguities,
        Func<Ambiguity, Task<string>> askUserFunc,
        CancellationToken cancellationToken)
    {
        var resolutions = new Dictionary<string, string>();
        
        foreach (var ambiguity in ambiguities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                // Ask user (or use default if user doesn't respond)
                var answer = await askUserFunc(ambiguity);
                resolutions[ambiguity.Term] = answer;
                
                _logger.LogInformation("✅ Resolved '{Term}': {Answer}", ambiguity.Term, answer);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to resolve '{Term}', using default: {Default}", 
                    ambiguity.Term, ambiguity.SmartDefault);
                resolutions[ambiguity.Term] = ambiguity.SmartDefault;
            }
        }
        
        return resolutions;
    }
    
    // ═══════════════════════════════════════════════════════════
    // SMART DEFAULT DETECTION
    // ═══════════════════════════════════════════════════════════
    
    private string DetectAuthDefault(string task, string? workspacePath)
    {
        if (task.Contains("api") || task.Contains("microservice"))
            return "JWT";
        
        if (task.Contains("enterprise") || task.Contains("azure"))
            return "Azure AD";
        
        if (task.Contains("google") || task.Contains("social"))
            return "OAuth2";
        
        return "JWT"; // Most common default
    }
    
    private string DetectDatabaseDefault(string? workspacePath)
    {
        // Check existing project
        if (!string.IsNullOrEmpty(workspacePath))
        {
            var appSettings = Path.Combine(workspacePath, "appsettings.json");
            if (File.Exists(appSettings))
            {
                try
                {
                    var content = File.ReadAllText(appSettings);
                    if (content.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                        return "SQL Server";
                    if (content.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) || 
                        content.Contains("Postgres", StringComparison.OrdinalIgnoreCase))
                        return "PostgreSQL";
                    if (content.Contains("MySQL", StringComparison.OrdinalIgnoreCase))
                        return "MySQL";
                    if (content.Contains("SQLite", StringComparison.OrdinalIgnoreCase))
                        return "SQLite";
                }
                catch { }
            }
        }
        
        return "SQL Server"; // Most common for .NET
    }
    
    private string DetectDataAccessDefault(string? workspacePath)
    {
        if (!string.IsNullOrEmpty(workspacePath))
        {
            var csprojFiles = Directory.GetFiles(workspacePath, "*.csproj", SearchOption.AllDirectories);
            foreach (var csproj in csprojFiles)
            {
                try
                {
                    var content = File.ReadAllText(csproj);
                    if (content.Contains("EntityFrameworkCore"))
                        return "Entity Framework Core";
                    if (content.Contains("Dapper"))
                        return "Dapper";
                }
                catch { }
            }
        }
        
        return "Entity Framework Core"; // Most popular
    }
    
    private string DetectCachingDefault(string task)
    {
        if (task.Contains("distributed") || task.Contains("scale") || task.Contains("cluster"))
            return "Redis";
        
        if (task.Contains("response") || task.Contains("http"))
            return "IMemoryCache";
        
        return "IMemoryCache"; // Simplest default
    }
    
    private string DetectUIDefault(string? workspacePath)
    {
        if (!string.IsNullOrEmpty(workspacePath))
        {
            var csprojFiles = Directory.GetFiles(workspacePath, "*.csproj", SearchOption.AllDirectories);
            foreach (var csproj in csprojFiles)
            {
                try
                {
                    var content = File.ReadAllText(csproj);
                    if (content.Contains("Microsoft.AspNetCore.Components"))
                        return "Blazor Server";
                    if (content.Contains("React"))
                        return "React";
                }
                catch { }
            }
        }
        
        return "Blazor Server"; // Default for .NET
    }
    
    private string DetectErrorHandlingDefault(string? workspacePath)
    {
        if (!string.IsNullOrEmpty(workspacePath))
        {
            // Check if Result<T> pattern exists
            var csFiles = Directory.GetFiles(workspacePath, "*.cs", SearchOption.AllDirectories);
            foreach (var file in csFiles.Take(50)) // Check first 50 files
            {
                try
                {
                    var content = File.ReadAllText(file);
                    if (content.Contains("Result<T>") || content.Contains("class Result"))
                        return "Result<T>";
                }
                catch { }
            }
        }
        
        return "Exceptions"; // Traditional .NET
    }
    
    private bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}

public class Ambiguity
{
    public string Term { get; set; } = "";
    public Dictionary<string, string> PossibleMeanings { get; set; } = new();
    public string Question { get; set; } = "";
    public string SmartDefault { get; set; } = "";
    public string Category { get; set; } = "";
    public string Impact { get; set; } = "Medium"; // Low, Medium, High
}


