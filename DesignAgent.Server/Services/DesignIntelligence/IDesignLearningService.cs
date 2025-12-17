using DesignAgent.Server.Models.DesignIntelligence;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for learning from designs and evolving prompts
/// </summary>
public interface IDesignLearningService
{
    /// <summary>
    /// Extract design patterns from an analyzed design
    /// </summary>
    /// <param name="design">Analyzed design</param>
    /// <returns>Extracted patterns</returns>
    Task<List<DesignPattern>> ExtractPatternsAsync(CapturedDesign design, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Process user feedback and update calibration
    /// </summary>
    /// <param name="designId">Design ID</param>
    /// <param name="rating">Rating (1=thumbs down, 5=thumbs up)</param>
    /// <param name="customName">Optional custom name</param>
    /// <returns>Feedback record</returns>
    Task<DesignFeedback> ProcessFeedbackAsync(string designId, int rating, string? customName = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze mismatch between human and LLM scores
    /// </summary>
    /// <param name="design">Design being rated</param>
    /// <param name="humanScore">Human score</param>
    /// <returns>Analysis insights</returns>
    Task<string> AnalyzeMismatchAsync(CapturedDesign design, double humanScore, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Evolve a prompt based on feedback
    /// </summary>
    /// <param name="promptName">Prompt to evolve</param>
    /// <param name="feedback">Recent feedback items</param>
    /// <returns>New prompt version</returns>
    Task<string> EvolvePromptAsync(string promptName, List<DesignFeedback> feedback, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update model performance calibration
    /// </summary>
    /// <param name="model">Model name</param>
    /// <param name="pageType">Page type</param>
    /// <param name="llmScore">LLM score</param>
    /// <param name="humanScore">Human score</param>
    Task UpdateModelCalibrationAsync(string model, string pageType, double llmScore, double humanScore, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detect pattern co-occurrence
    /// </summary>
    /// <param name="design">Design with multiple patterns</param>
    Task UpdatePatternCoOccurrenceAsync(CapturedDesign design, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Detect design trends over time
    /// </summary>
    /// <param name="timeWindowDays">Days to look back</param>
    /// <returns>Trend insights</returns>
    Task<List<string>> DetectTrendsAsync(int timeWindowDays = 30, CancellationToken cancellationToken = default);
}

