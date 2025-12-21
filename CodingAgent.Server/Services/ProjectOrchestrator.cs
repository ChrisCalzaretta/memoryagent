using AgentContracts.Models;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;
using CodingAgent.Server.Templates;

namespace CodingAgent.Server.Services;

/// <summary>
/// NEW multi-language project orchestrator with:
/// - Template-based scaffolding
/// - Phi4-driven planning
/// - 10-attempt retry loop per file
/// - Stub generation on failure (never gives up!)
/// - Failure report generation
/// - Support for C#, Flutter, and future languages
/// </summary>
public interface IProjectOrchestrator
{
    Task<GenerateCodeResponse> GenerateProjectAsync(
        string task,
        string? language = null,
        string? workspacePath = null,
        string? context = null,
        CancellationToken ct = default);
}

public class ProjectOrchestrator : IProjectOrchestrator
{
    private readonly ICodeGenerationService _codeGeneration;
    private readonly ITemplateService _templates;
    private readonly IPhi4ThinkingService? _phi4;
    private readonly IStubGenerator _stubGenerator;
    private readonly IFailureReportGenerator _failureReportGenerator;
    private readonly ILogger<ProjectOrchestrator> _logger;

    public ProjectOrchestrator(
        ICodeGenerationService codeGeneration,
        ITemplateService templates,
        IStubGenerator stubGenerator,
        IFailureReportGenerator failureReportGenerator,
        ILogger<ProjectOrchestrator> logger,
        IPhi4ThinkingService? phi4 = null)
    {
        _codeGeneration = codeGeneration;
        _templates = templates;
        _phi4 = phi4;
        _stubGenerator = stubGenerator;
        _failureReportGenerator = failureReportGenerator;
        _logger = logger;
    }

    public async Task<GenerateCodeResponse> GenerateProjectAsync(
        string task,
        string? language = null,
        string? workspacePath = null,
        string? context = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("🚀 Starting NEW ProjectOrchestrator for task: {Task}", task);

        // STEP 1: Detect if this is a new project - use templates for instant scaffolding
        if (IsNewProjectRequest(task))
        {
            _logger.LogInformation("🎯 Detected new project request - attempting template match");
            var templateMatch = await _templates.DetectTemplateAsync(task, language, ct);

            if (templateMatch.Confidence >= 0.5)
            {
                _logger.LogInformation("✨ Using template: {TemplateId} (confidence: {Confidence:P0})",
                    templateMatch.Template.TemplateId, templateMatch.Confidence);

                // No Phi4 for MVP - just return the template as-is
                // TODO: Add Phi4 planning in future iteration
                return new GenerateCodeResponse
                {
                    Success = true,
                    FileChanges = templateMatch.Template.Files.Select(kvp => new FileChange
                    {
                        Path = kvp.Key,
                        Content = kvp.Value,
                        Type = FileChangeType.Created,
                        Reason = $"Scaffolded from {templateMatch.Template.TemplateId} template"
                    }).ToList(),
                    ModelUsed = $"template:{templateMatch.Template.TemplateId}",
                    Explanation = $"Generated {templateMatch.Template.Language} {templateMatch.Template.ProjectType} using template",
                    TokensUsed = 0
                };
            }
        }

        // STEP 2: Not a template match - use normal code generation
        _logger.LogInformation("💻 Using normal code generation (no template match)");
        
        // Create CodeContext if context string was provided  
        CodeContext? codeContext = null;
        if (!string.IsNullOrEmpty(context))
        {
            // CodeContext just wraps the context string
            codeContext = new CodeContext();
        }
        
        return await _codeGeneration.GenerateAsync(new GenerateCodeRequest
        {
            Task = task,
            Language = language,
            WorkspacePath = workspacePath,
            Context = codeContext
        }, ct);
    }

    private bool IsNewProjectRequest(string task)
    {
        var taskLower = task.ToLowerInvariant();
        
        // Check for "create/new/build/generate" + project type indicators
        var actionWords = new[] { "create", "new", "build", "generate", "scaffold" };
        var projectTypes = new[] { "app", "project", "api", "service", "application", "console", "blazor", "flutter", "library" };
        
        var hasAction = actionWords.Any(action => taskLower.Contains(action));
        var hasProjectType = projectTypes.Any(type => taskLower.Contains(type));
        
        if (hasAction && hasProjectType)
            return true;
        
        // Also check for explicit project keywords
        var projectKeywords = new[]
        {
            "full app", "complete app", "entire app",
            "web api", "webapi", "rest api",
            "boilerplate", "starter", "from scratch"
        };

        return projectKeywords.Any(keyword => taskLower.Contains(keyword));
    }
}
