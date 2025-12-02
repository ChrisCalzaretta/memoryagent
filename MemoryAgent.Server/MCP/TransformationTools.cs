using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;

namespace MemoryAgent.Server.MCP;

/// <summary>
/// MCP tools for Blazor/Razor transformation (exposed to Cursor)
/// </summary>
public class TransformationTools
{
    private readonly IPageTransformationService _pageService;
    private readonly ICSSTransformationService _cssService;
    private readonly IComponentExtractionService _componentService;
    private readonly ILogger<TransformationTools> _logger;
    
    public TransformationTools(
        IPageTransformationService pageService,
        ICSSTransformationService cssService,
        IComponentExtractionService componentService,
        ILogger<TransformationTools> logger)
    {
        _pageService = pageService;
        _cssService = cssService;
        _componentService = componentService;
        _logger = logger;
    }
    
    /// <summary>
    /// Transform a Blazor/Razor page to modern architecture with clean code and CSS
    /// </summary>
    [MCPTool("transform_page")]
    public async Task<PageTransformation> TransformPage(
        string sourcePath,
        bool extractComponents = true,
        bool modernizeCSS = true,
        bool addErrorHandling = true,
        bool addLoadingStates = true,
        bool addAccessibility = true,
        string? outputDirectory = null)
    {
        _logger.LogInformation("MCP: transform_page called for {SourcePath}", sourcePath);
        
        var options = new TransformationOptions
        {
            ExtractComponents = extractComponents,
            ModernizeCSS = modernizeCSS,
            AddErrorHandling = addErrorHandling,
            AddLoadingStates = addLoadingStates,
            AddAccessibility = addAccessibility,
            OutputDirectory = outputDirectory
        };
        
        return await _pageService.TransformPageAsync(sourcePath, options);
    }
    
    /// <summary>
    /// Learn transformation pattern from example (old → new)
    /// </summary>
    [MCPTool("learn_transformation")]
    public async Task<TransformationPattern> LearnTransformation(
        string exampleOldPath,
        string exampleNewPath,
        string patternName)
    {
        _logger.LogInformation("MCP: learn_transformation called: {OldPath} → {NewPath}",
            exampleOldPath, exampleNewPath);
        
        return await _pageService.LearnPatternAsync(
            exampleOldPath,
            exampleNewPath,
            patternName);
    }
    
    /// <summary>
    /// Apply learned transformation pattern to new page
    /// </summary>
    [MCPTool("apply_transformation")]
    public async Task<PageTransformation> ApplyTransformation(
        string patternId,
        string targetPath)
    {
        _logger.LogInformation("MCP: apply_transformation called: pattern={PatternId}, target={TargetPath}",
            patternId, targetPath);
        
        return await _pageService.ApplyPatternAsync(patternId, targetPath);
    }
    
    /// <summary>
    /// Get all learned transformation patterns
    /// </summary>
    [MCPTool("list_transformation_patterns")]
    public async Task<List<TransformationPattern>> ListTransformationPatterns(
        string? context = null)
    {
        _logger.LogInformation("MCP: list_transformation_patterns called");
        
        return await _pageService.GetPatternsAsync(context);
    }
    
    /// <summary>
    /// Scan project for reusable component patterns
    /// </summary>
    [MCPTool("detect_reusable_components")]
    public async Task<List<ComponentCandidate>> DetectReusableComponents(
        string projectPath,
        int minOccurrences = 2,
        float minSimilarity = 0.7f)
    {
        _logger.LogInformation("MCP: detect_reusable_components called for {ProjectPath}", projectPath);
        
        return await _componentService.DetectReusableComponentsAsync(
            projectPath,
            minOccurrences,
            minSimilarity);
    }
    
    /// <summary>
    /// Extract a detected component candidate
    /// </summary>
    [MCPTool("extract_component")]
    public async Task<ExtractedComponent> ExtractComponent(
        string componentCandidateJson,
        string outputPath)
    {
        _logger.LogInformation("MCP: extract_component called");
        
        // Parse candidate from JSON
        var candidate = System.Text.Json.JsonSerializer.Deserialize<ComponentCandidate>(
            componentCandidateJson);
        
        if (candidate == null)
        {
            throw new ArgumentException("Invalid component candidate JSON");
        }
        
        return await _componentService.ExtractComponentAsync(candidate, outputPath);
    }
    
    /// <summary>
    /// Transform CSS - extract inline styles, modernize, add variables
    /// </summary>
    [MCPTool("transform_css")]
    public async Task<CSSTransformation> TransformCSS(
        string sourcePath,
        bool generateVariables = true,
        bool modernizeLayout = true,
        bool addResponsive = true,
        bool addAccessibility = true,
        string? outputPath = null)
    {
        _logger.LogInformation("MCP: transform_css called for {SourcePath}", sourcePath);
        
        var options = new CSSTransformationOptions
        {
            GenerateVariables = generateVariables,
            ModernizeLayout = modernizeLayout,
            AddResponsive = addResponsive,
            AddAccessibility = addAccessibility,
            OutputCSSPath = outputPath
        };
        
        return await _cssService.TransformCSSAsync(sourcePath, options);
    }
    
    /// <summary>
    /// Analyze CSS quality and get recommendations
    /// </summary>
    [MCPTool("analyze_css")]
    public async Task<CSSAnalysisResult> AnalyzeCSS(string sourcePath)
    {
        _logger.LogInformation("MCP: analyze_css called for {SourcePath}", sourcePath);
        
        return await _cssService.AnalyzeCSSAsync(sourcePath);
    }
}

/// <summary>
/// Attribute to mark MCP tools
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class MCPToolAttribute : Attribute
{
    public string Name { get; }
    
    public MCPToolAttribute(string name)
    {
        Name = name;
    }
}

