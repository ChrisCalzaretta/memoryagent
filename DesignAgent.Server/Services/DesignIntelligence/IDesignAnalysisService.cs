using DesignAgent.Server.Models.DesignIntelligence;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for analyzing designs using LLaVA and other LLMs
/// </summary>
public interface IDesignAnalysisService
{
    /// <summary>
    /// Analyze a complete design (all pages)
    /// </summary>
    /// <param name="design">Captured design with all pages</param>
    /// <returns>Updated design with scores</returns>
    Task<CapturedDesign> AnalyzeDesignAsync(CapturedDesign design, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze a single page using page-type-specific prompt
    /// </summary>
    /// <param name="page">Page to analyze</param>
    /// <returns>Updated page with scores and analysis</returns>
    Task<PageAnalysis> AnalyzePageAsync(PageAnalysis page, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Synthesize site-wide design DNA from all pages
    /// </summary>
    /// <param name="pages">All analyzed pages</param>
    /// <returns>Design DNA profile</returns>
    Task<DesignDNA> SynthesizeDesignDnaAsync(List<PageAnalysis> pages, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detect design system/framework from CSS and DOM
    /// </summary>
    /// <param name="pages">All pages to analyze</param>
    /// <returns>Detected design system</returns>
    Task<DetectedDesignSystem> DetectDesignSystemAsync(List<PageAnalysis> pages, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze UX copy on a page
    /// </summary>
    /// <param name="page">Page to analyze</param>
    /// <returns>UX copy analysis</returns>
    Task<UxCopyAnalysis> AnalyzeCopyAsync(PageAnalysis page, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Perform accessibility audit
    /// </summary>
    /// <param name="page">Page to audit</param>
    /// <returns>Accessibility audit results</returns>
    Task<AccessibilityAudit> AuditAccessibilityAsync(PageAnalysis page, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze animations and motion design
    /// </summary>
    /// <param name="page">Page to analyze (with video if available)</param>
    /// <returns>Animation analysis</returns>
    Task<AnimationAnalysis?> AnalyzeAnimationsAsync(PageAnalysis page, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculate overall site score from page scores
    /// </summary>
    /// <param name="pages">All analyzed pages</param>
    /// <returns>Weighted average site score</returns>
    Task<double> CalculateSiteScoreAsync(List<PageAnalysis> pages, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if design passes quality gate
    /// </summary>
    /// <param name="score">Site score</param>
    /// <param name="trustScore">Source trust score</param>
    /// <returns>True if passes</returns>
    Task<bool> PassesQualityGateAsync(double score, double trustScore, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Apply bias correction to score based on model calibration
    /// </summary>
    /// <param name="rawScore">Raw LLM score</param>
    /// <param name="model">Model name</param>
    /// <param name="pageType">Page type</param>
    /// <returns>Calibrated score</returns>
    Task<double> ApplyBiasCorrectionAsync(double rawScore, string model, string pageType, CancellationToken cancellationToken = default);
}

