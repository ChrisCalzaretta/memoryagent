namespace CodingAgent.Server.Templates;

/// <summary>
/// Interface for project templates
/// Templates define the initial structure for different project types
/// </summary>
public interface IProjectTemplate
{
    /// <summary>
    /// Unique identifier for this template
    /// </summary>
    string TemplateId { get; }
    
    /// <summary>
    /// Display name for the template
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Language/platform (csharp, flutter, python, etc.)
    /// </summary>
    string Language { get; }
    
    /// <summary>
    /// Project type (Console, WebAPI, BlazorWasm, FlutterIOS, etc.)
    /// </summary>
    string ProjectType { get; }
    
    /// <summary>
    /// Description of what this template creates
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Keywords for matching user requests to templates
    /// </summary>
    string[] Keywords { get; }
    
    /// <summary>
    /// Default folder structure to create
    /// </summary>
    string[] FolderStructure { get; }
    
    /// <summary>
    /// Template files with their content (path -> content)
    /// Use {{ProjectName}}, {{project_name}}, etc. for placeholders
    /// </summary>
    Dictionary<string, string> Files { get; }
    
    /// <summary>
    /// Required packages/dependencies
    /// </summary>
    string[] RequiredPackages { get; }
    
    /// <summary>
    /// Complexity level (1-10) for model selection
    /// </summary>
    int Complexity { get; }
    
    /// <summary>
    /// Generate the project files with placeholders replaced
    /// </summary>
    Dictionary<string, string> GenerateFiles(ProjectContext context);
}

/// <summary>
/// Context for generating a project from a template
/// </summary>
public record ProjectContext
{
    /// <summary>
    /// Name of the project (PascalCase for C#, snake_case for Flutter)
    /// </summary>
    public required string ProjectName { get; init; }
    
    /// <summary>
    /// Project description
    /// </summary>
    public string Description { get; init; } = "";
    
    /// <summary>
    /// Target namespace (for C#)
    /// </summary>
    public string? Namespace { get; init; }
    
    /// <summary>
    /// Target framework version (e.g., "net9.0")
    /// </summary>
    public string? TargetFramework { get; init; }
    
    /// <summary>
    /// Output directory for generated files
    /// </summary>
    public string OutputDirectory { get; init; } = ".";
    
    /// <summary>
    /// Additional customizations
    /// </summary>
    public Dictionary<string, string> Customizations { get; init; } = new();
}

/// <summary>
/// Base class for project templates with common functionality
/// </summary>
public abstract class ProjectTemplateBase : IProjectTemplate
{
    public abstract string TemplateId { get; }
    public abstract string DisplayName { get; }
    public abstract string Language { get; }
    public abstract string ProjectType { get; }
    public abstract string Description { get; }
    public abstract string[] Keywords { get; }
    public abstract string[] FolderStructure { get; }
    public abstract Dictionary<string, string> Files { get; }
    public virtual string[] RequiredPackages => Array.Empty<string>();
    public virtual int Complexity => 5;
    
    /// <summary>
    /// Generate files with placeholders replaced
    /// </summary>
    public virtual Dictionary<string, string> GenerateFiles(ProjectContext context)
    {
        var result = new Dictionary<string, string>();
        
        foreach (var (path, content) in Files)
        {
            var processedPath = ReplacePlaceholders(path, context);
            var processedContent = ReplacePlaceholders(content, context);
            result[processedPath] = processedContent;
        }
        
        return result;
    }
    
    /// <summary>
    /// Replace template placeholders with actual values
    /// </summary>
    protected virtual string ReplacePlaceholders(string input, ProjectContext context)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var result = input;
        
        // PascalCase name (C# style)
        result = result.Replace("{{ProjectName}}", context.ProjectName);
        result = result.Replace("{{projectName}}", ToCamelCase(context.ProjectName));
        
        // snake_case name (Flutter/Dart style)
        result = result.Replace("{{project_name}}", ToSnakeCase(context.ProjectName));
        
        // Namespace
        var ns = context.Namespace ?? context.ProjectName;
        result = result.Replace("{{Namespace}}", ns);
        
        // Description
        result = result.Replace("{{Description}}", context.Description);
        
        // Target framework
        var framework = context.TargetFramework ?? "net9.0";
        result = result.Replace("{{TargetFramework}}", framework);
        
        // Custom placeholders
        foreach (var (key, value) in context.Customizations)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        
        return result;
    }
    
    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToLowerInvariant(input[0]) + input[1..];
    }
    
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
}



