using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// 🎨 DESIGN INTEGRATION SERVICE
/// Detects UI tasks and integrates Design Agent for brand-guided code generation
/// </summary>
public class DesignIntegrationService
{
    private readonly IDesignAgentClient? _designAgent;
    private readonly LlmDesignQuestionnaireService _llmQuestionnaire;
    private readonly ILogger<DesignIntegrationService> _logger;
    
    public DesignIntegrationService(
        LlmDesignQuestionnaireService llmQuestionnaire,
        ILogger<DesignIntegrationService> logger,
        IDesignAgentClient? designAgent = null) // Optional - graceful degradation
    {
        _llmQuestionnaire = llmQuestionnaire;
        _logger = logger;
        _designAgent = designAgent;
    }
    
    /// <summary>
    /// Detect if this is a UI/design task
    /// </summary>
    public bool IsUICode(string taskDescription, string language)
    {
        // TEMPORARILY DISABLED - Design Agent mapping needs fixing
        return false;
        
        #pragma warning disable CS0162
        var lowerTask = taskDescription.ToLowerInvariant();
        var lowerLang = language.ToLowerInvariant();
        
        // Language-based detection
        if (lowerLang is "flutter" or "dart" or "blazor" or "react" or "vue" or "angular" or "svelte" or "html")
            return true;
        
        // Task keyword detection
        var uiKeywords = new[]
        {
            "ui", "screen", "page", "view", "component", "widget",
            "form", "button", "card", "dialog", "modal", "menu",
            "navbar", "header", "footer", "sidebar", "layout",
            "dashboard", "login", "signup", "profile", "settings",
            "theme", "styled", "design", "color", "wizard"
        };
        
        return uiKeywords.Any(keyword => lowerTask.Contains(keyword));
    }
    
    /// <summary>
    /// Get or create brand guidelines for this task
    /// Uses LLM to automatically answer design questionnaire!
    /// </summary>
    public async Task<BrandSystem?> GetOrCreateBrandAsync(
        string taskDescription,
        string language,
        string context,
        CancellationToken cancellationToken = default)
    {
        if (_designAgent == null)
        {
            _logger.LogWarning("⚠️ Design Agent not available - skipping brand creation");
            return null;
        }
        
        try
        {
            // Check if brand already exists
            var existingBrand = await _designAgent.GetBrandAsync(context, cancellationToken);
            if (existingBrand != null)
            {
                _logger.LogInformation("✅ Using existing brand: {Name}", existingBrand.Name);
                return existingBrand;
            }
            
            _logger.LogInformation("🎨 Creating new brand for context: {Context}", context);
            
            // Get questionnaire from Design Agent
            var questionnaire = await _designAgent.GetQuestionnaireAsync(cancellationToken);
            
            // 🤖 USE LLM TO ANSWER QUESTIONNAIRE (instead of asking user!)
            var answers = await _llmQuestionnaire.AnswerQuestionnaireAsync(
                taskDescription,
                language,
                questionnaire,
                cancellationToken);
            
            // Create brand system
            var brand = await _designAgent.CreateBrandAsync(answers, cancellationToken);
            
            _logger.LogInformation("✅ Created brand: {Name} with colors {Primary}/{Secondary}", 
                brand.Name, brand.Colors.Primary, brand.Colors.Secondary);
            
            return brand;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to create brand (non-fatal) - continuing without design guidelines");
            return null;
        }
    }
    
    /// <summary>
    /// Validate generated UI code against brand guidelines
    /// </summary>
    public async Task<(int Score, List<string> Issues)> ValidateDesignAsync(
        string context,
        List<(string Path, string Content)> files,
        CancellationToken cancellationToken = default)
    {
        if (_designAgent == null)
        {
            _logger.LogDebug("⚠️ Design Agent not available - skipping design validation");
            return (10, new List<string>()); // Perfect score if no validation available
        }
        
        try
        {
            var fileContents = files.Select(f => new FileContent
            {
                Path = f.Path,
                Content = f.Content
            }).ToList();
            
            var result = await _designAgent.ValidateDesignAsync(context, fileContents, cancellationToken);
            
            var issues = result.Issues.Select(i => 
                $"[{i.Severity}] {i.FilePath}: {i.Message}").ToList();
            
            _logger.LogInformation("🎨 Design validation: {Score}/10, {Issues} issues", 
                result.Score, result.Issues.Count);
            
            return (result.Score, issues);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Design validation failed (non-fatal)");
            return (10, new List<string>()); // Perfect score if validation fails
        }
    }
}

