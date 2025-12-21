using CodingAgent.Server.Templates.CSharp;
using CodingAgent.Server.Templates.Flutter;

namespace CodingAgent.Server.Templates;

/// <summary>
/// Service for managing and selecting project templates
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Get all available templates
    /// </summary>
    IReadOnlyList<IProjectTemplate> GetAllTemplates();
    
    /// <summary>
    /// Get templates for a specific language
    /// </summary>
    IReadOnlyList<IProjectTemplate> GetTemplatesForLanguage(string language);
    
    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    IProjectTemplate? GetTemplateById(string templateId);
    
    /// <summary>
    /// Auto-detect the best template for a given task description
    /// </summary>
    Task<TemplateMatch> DetectTemplateAsync(string taskDescription, string? preferredLanguage = null, CancellationToken ct = default);
    
    /// <summary>
    /// Generate project files from a template
    /// </summary>
    Dictionary<string, string> GenerateProjectFiles(string templateId, ProjectContext context);
}

/// <summary>
/// Result of template detection
/// </summary>
public record TemplateMatch
{
    public required IProjectTemplate Template { get; init; }
    public required double Confidence { get; init; }
    public required string[] MatchedKeywords { get; init; }
    public string? AlternativeTemplateId { get; init; }
}

/// <summary>
/// Implementation of template service
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly ILogger<TemplateService> _logger;
    private readonly List<IProjectTemplate> _templates;
    
    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger;
        _templates = RegisterTemplates();
    }
    
    private static List<IProjectTemplate> RegisterTemplates()
    {
        return new List<IProjectTemplate>
        {
            // C# Templates
            new ConsoleAppTemplate(),
            new WebApiTemplate(),
            new BlazorWasmTemplate(),
            new ClassLibraryTemplate(),
            
            // Flutter Templates
            new FlutterIosTemplate(),
            new FlutterAndroidTemplate(),
            new FlutterWebTemplate()
        };
    }
    
    public IReadOnlyList<IProjectTemplate> GetAllTemplates() => _templates.AsReadOnly();
    
    public IReadOnlyList<IProjectTemplate> GetTemplatesForLanguage(string language)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        return _templates
            .Where(t => NormalizeLanguage(t.Language) == normalizedLanguage)
            .ToList()
            .AsReadOnly();
    }
    
    public IProjectTemplate? GetTemplateById(string templateId)
    {
        return _templates.FirstOrDefault(t => 
            t.TemplateId.Equals(templateId, StringComparison.OrdinalIgnoreCase));
    }
    
    public Task<TemplateMatch> DetectTemplateAsync(
        string taskDescription, 
        string? preferredLanguage = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Detecting template for task: {Task}", taskDescription);
        
        var lowerTask = taskDescription.ToLowerInvariant();
        var scores = new Dictionary<IProjectTemplate, (double score, List<string> matchedKeywords)>();
        
        // Filter by language if specified
        var candidates = preferredLanguage != null 
            ? GetTemplatesForLanguage(preferredLanguage).ToList() 
            : _templates;
        
        foreach (var template in candidates)
        {
            var matchedKeywords = new List<string>();
            double score = 0;
            
            // Check keywords - HIGHER WEIGHT for primary keywords
            foreach (var keyword in template.Keywords)
            {
                if (lowerTask.Contains(keyword.ToLowerInvariant()))
                {
                    matchedKeywords.Add(keyword);
                    // Primary keywords get higher weight
                    if (keyword.ToLowerInvariant() is "console" or "webapi" or "blazor" or "flutter")
                    {
                        score += 30; // Primary identifier
                    }
                    else if (keyword.ToLowerInvariant() is "app" or "api" or "project")
                    {
                        score += 20; // Strong signal
                    }
                    else
                    {
                        score += 15; // Supporting keyword
                    }
                }
            }
            
            // Check project type mention
            if (lowerTask.Contains(template.ProjectType.ToLowerInvariant()))
            {
                score += 30;
                matchedKeywords.Add(template.ProjectType);
            }
            
            // Check language mention
            if (lowerTask.Contains(template.Language.ToLowerInvariant()))
            {
                score += 25;
            }
            
            // Check for "create" or "new" + project type combo
            if ((lowerTask.Contains("create") || lowerTask.Contains("new") || lowerTask.Contains("build") || lowerTask.Contains("generate")) &&
                lowerTask.Contains(template.ProjectType.ToLowerInvariant()))
            {
                score += 20; // Strong intent signal
            }
            
            // Platform-specific boosts
            if (template.Language == "flutter")
            {
                if (lowerTask.Contains("ios") || lowerTask.Contains("iphone") || lowerTask.Contains("apple"))
                {
                    if (template.ProjectType == "FlutterIOS")
                        score += 40; // Increased from 25
                }
                else if (lowerTask.Contains("android") || lowerTask.Contains("google play"))
                {
                    if (template.ProjectType == "FlutterAndroid")
                        score += 40; // Increased from 25
                }
                else if (lowerTask.Contains("web") || lowerTask.Contains("browser"))
                {
                    if (template.ProjectType == "FlutterWeb")
                        score += 40; // Increased from 25
                }
            }
            
            if (template.Language == "csharp")
            {
                if (lowerTask.Contains("api") || lowerTask.Contains("rest") || lowerTask.Contains("endpoint"))
                {
                    if (template.ProjectType == "WebAPI")
                        score += 35;
                }
                else if (lowerTask.Contains("blazor") || lowerTask.Contains("spa") || lowerTask.Contains("web app"))
                {
                    if (template.ProjectType == "BlazorWasm")
                        score += 35;
                }
                else if (lowerTask.Contains("library") || lowerTask.Contains("nuget") || lowerTask.Contains("package"))
                {
                    if (template.ProjectType == "ClassLibrary")
                        score += 35;
                }
                else if (lowerTask.Contains("console") || lowerTask.Contains("cli") || lowerTask.Contains("command"))
                {
                    if (template.ProjectType == "ConsoleApp")
                        score += 35;
                }
            }
            
            scores[template] = (score, matchedKeywords);
        }
        
        // Get best match
        var best = scores
            .OrderByDescending(s => s.Value.score)
            .FirstOrDefault();
        
        if (best.Key == null)
        {
            // Default to console app for C# or Android for Flutter
            var defaultTemplate = preferredLanguage?.ToLowerInvariant() switch
            {
                "flutter" or "dart" => _templates.First(t => t.TemplateId == "flutter-android"),
                _ => _templates.First(t => t.TemplateId == "csharp-console")
            };
            
            _logger.LogWarning("No template match found, defaulting to: {Template}", defaultTemplate.TemplateId);
            
            return Task.FromResult(new TemplateMatch
            {
                Template = defaultTemplate,
                Confidence = 0.3,
                MatchedKeywords = Array.Empty<string>()
            });
        }
        
        var confidence = Math.Min(best.Value.score / 100.0, 1.0);
        
        // Get alternative (second best)
        var alternative = scores
            .OrderByDescending(s => s.Value.score)
            .Skip(1)
            .FirstOrDefault();
        
        _logger.LogInformation(
            "Template detected: {Template} (confidence: {Confidence:P0}, keywords: {Keywords})",
            best.Key.TemplateId, 
            confidence,
            string.Join(", ", best.Value.matchedKeywords));
        
        return Task.FromResult(new TemplateMatch
        {
            Template = best.Key,
            Confidence = confidence,
            MatchedKeywords = best.Value.matchedKeywords.ToArray(),
            AlternativeTemplateId = alternative.Key?.TemplateId
        });
    }
    
    public Dictionary<string, string> GenerateProjectFiles(string templateId, ProjectContext context)
    {
        var template = GetTemplateById(templateId);
        if (template == null)
        {
            throw new ArgumentException($"Template not found: {templateId}", nameof(templateId));
        }
        
        _logger.LogInformation(
            "Generating project files from template: {Template} for project: {Project}",
            templateId, context.ProjectName);
        
        return template.GenerateFiles(context);
    }
    
    private static string NormalizeLanguage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "c#" or "cs" or "dotnet" or ".net" => "csharp",
            "dart" => "flutter",
            _ => language.ToLowerInvariant()
        };
    }
}

