using DesignAgent.Server.Models.DesignIntelligence;
using AgentContracts.Services;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace DesignAgent.Server.Services.DesignIntelligence;

/// <summary>
/// Service for learning from designs, extracting patterns, and evolving prompts
/// </summary>
public class DesignLearningService : IDesignLearningService
{
    private readonly IOllamaClient _ollamaClient;
    private readonly IDesignIntelligenceStorage _storage;
    private readonly ILogger<DesignLearningService> _logger;
    private readonly DesignIntelligenceOptions _options;

    public DesignLearningService(
        IOllamaClient ollamaClient,
        IDesignIntelligenceStorage storage,
        ILogger<DesignLearningService> logger,
        IOptions<DesignIntelligenceOptions> options)
    {
        _ollamaClient = ollamaClient;
        _storage = storage;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Extract design patterns from an analyzed design
    /// </summary>
    public async Task<List<DesignPattern>> ExtractPatternsAsync(CapturedDesign design, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔍 Extracting patterns from: {Url}", design.Url);

        var patterns = new List<DesignPattern>();

        // Extract patterns from each high-scoring page
        foreach (var page in design.Pages.Where(p => p.OverallPageScore >= 8.0))
        {
            try
            {
                var pagePatterns = await ExtractPatternsFromPageAsync(page, design, cancellationToken);
                patterns.AddRange(pagePatterns);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract patterns from page: {Url}", page.Url);
            }
        }

        // Store patterns and update co-occurrence
        foreach (var pattern in patterns)
        {
            await _storage.StorePatternAsync(pattern, cancellationToken);
        }

        await UpdatePatternCoOccurrenceAsync(design, cancellationToken);

        _logger.LogInformation("✅ Extracted {Count} patterns from {Url}", patterns.Count, design.Url);

        return patterns;
    }

    /// <summary>
    /// Process user feedback and update calibration
    /// </summary>
    public async Task<DesignFeedback> ProcessFeedbackAsync(string designId, int rating, string? customName = null, CancellationToken cancellationToken = default)
    {
        var design = await _storage.GetDesignAsync(designId, cancellationToken);
        if (design == null)
        {
            throw new ArgumentException($"Design not found: {designId}");
        }

        // Map rating to score: 1=👎 (4.0), 5=👍 (9.0)
        var humanScore = rating == 1 ? 4.0 : 9.0;
        var llmScore = design.OverallScore;
        var mismatch = Math.Abs(humanScore - llmScore);

        var feedback = new DesignFeedback
        {
            DesignId = designId,
            Rating = rating,
            HumanScore = humanScore,
            LlmScore = llmScore,
            Mismatch = mismatch,
            CustomName = customName,
            ProvidedAt = DateTime.UtcNow
        };

        // Store feedback
        await _storage.StoreFeedbackAsync(feedback, cancellationToken);

        // Update custom name if provided
        if (!string.IsNullOrEmpty(customName))
        {
            design.Name = customName;
            await _storage.StoreDesignAsync(design, cancellationToken);
        }

        _logger.LogInformation("📝 Feedback recorded: {DesignId} - Rating: {Rating}, Mismatch: {Mismatch:F1}",
            designId, rating == 1 ? "👎" : "👍", mismatch);

        // Analyze significant mismatches
        if (mismatch >= _options.MismatchThreshold)
        {
            _logger.LogInformation("🔍 Significant mismatch detected ({Mismatch:F1}), analyzing...", mismatch);
            var analysis = await AnalyzeMismatchAsync(design, humanScore, cancellationToken);
            _logger.LogInformation("📊 Mismatch analysis: {Analysis}", analysis);
        }

        // Update model calibration
        var homepagePage = design.Pages.FirstOrDefault(p => p.PageType == "homepage");
        if (homepagePage != null)
        {
            await UpdateModelCalibrationAsync(
                homepagePage.AnalysisModel,
                homepagePage.PageType,
                homepagePage.OverallPageScore,
                humanScore,
                cancellationToken);
        }

        // Check if we should evolve prompts
        var recentFeedback = await _storage.GetRecentFeedbackAsync(_options.MinFeedbackForEvolution, cancellationToken);
        if (recentFeedback.Count >= _options.MinFeedbackForEvolution)
        {
            var mismatchedCount = recentFeedback.Count(f => f.Mismatch >= _options.MismatchThreshold);
            if (mismatchedCount >= _options.MinFeedbackForEvolution / 2)
            {
                _logger.LogInformation("🔄 Triggering prompt evolution ({MismatchedCount}/{Total} mismatched)",
                    mismatchedCount, recentFeedback.Count);

                feedback.TriggeredEvolution = true;
                await _storage.StoreFeedbackAsync(feedback, cancellationToken);

                // Evolve homepage prompt (most important)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await EvolvePromptAsync("design_analysis_homepage", recentFeedback, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to evolve prompt");
                    }
                }, CancellationToken.None);
            }
        }

        return feedback;
    }

    /// <summary>
    /// Analyze mismatch between human and LLM scores
    /// </summary>
    public async Task<string> AnalyzeMismatchAsync(CapturedDesign design, double humanScore, CancellationToken cancellationToken = default)
    {
        var systemPrompt = await _storage.GetPromptAsync("design_feedback_analysis", cancellationToken)
            ?? GetFallbackFeedbackAnalysisPrompt();

        var userPrompt = BuildMismatchAnalysisPrompt(design, humanScore);

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        return response.Response;
    }

    /// <summary>
    /// Evolve a prompt based on feedback
    /// </summary>
    public async Task<string> EvolvePromptAsync(string promptName, List<DesignFeedback> feedback, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🧬 Evolving prompt: {PromptName} (based on {FeedbackCount} feedback items)",
            promptName, feedback.Count);

        // Get current prompt
        var currentPrompt = await _storage.GetPromptAsync(promptName, cancellationToken)
            ?? "No current prompt found";

        var systemPrompt = GetFallbackPromptEvolutionPrompt();
        var userPrompt = BuildPromptEvolutionPrompt(promptName, currentPrompt, feedback);

        var response = await _ollamaClient.GenerateAsync(
            _options.TextModel,
            userPrompt,
            systemPrompt,
            cancellationToken: cancellationToken);

        var newPrompt = ExtractNewPrompt(response.Response);

        // Update prompt in Lightning (version + 1)
        await _storage.UpdatePromptAsync(promptName, newPrompt, version: 2, cancellationToken);

        _logger.LogInformation("✅ Prompt evolved: {PromptName}", promptName);

        return newPrompt;
    }

    /// <summary>
    /// Update model performance calibration
    /// </summary>
    public async Task UpdateModelCalibrationAsync(string model, string pageType, double llmScore, double humanScore, CancellationToken cancellationToken = default)
    {
        var performance = await _storage.GetModelPerformanceAsync(model, pageType, cancellationToken);

        if (performance == null)
        {
            // Create new calibration record
            performance = new ModelPerformance
            {
                Model = model,
                PageType = pageType,
                AverageBias = llmScore - humanScore,
                StandardDeviation = 0,
                Accuracy = 1.0,
                SampleSize = 1,
                LastUpdated = DateTime.UtcNow
            };
        }
        else
        {
            // Update existing calibration using incremental average
            var newBias = llmScore - humanScore;
            var n = performance.SampleSize;

            performance.AverageBias = ((performance.AverageBias * n) + newBias) / (n + 1);
            performance.SampleSize = n + 1;
            performance.LastUpdated = DateTime.UtcNow;

            // Simple accuracy: how often are we within 1 point?
            var withinRange = Math.Abs(llmScore - humanScore) <= 1.0;
            performance.Accuracy = ((performance.Accuracy * n) + (withinRange ? 1.0 : 0.0)) / (n + 1);
        }

        await _storage.StoreModelPerformanceAsync(performance, cancellationToken);

        _logger.LogDebug("📊 Updated calibration for {Model}/{PageType}: Bias={Bias:F2}, Samples={Samples}",
            model, pageType, performance.AverageBias, performance.SampleSize);
    }

    /// <summary>
    /// Detect pattern co-occurrence
    /// </summary>
    public async Task UpdatePatternCoOccurrenceAsync(CapturedDesign design, CancellationToken cancellationToken = default)
    {
        var patternIds = design.ExtractedPatternIds;

        if (patternIds.Count < 2)
        {
            return; // Need at least 2 patterns for co-occurrence
        }

        // For each pair of patterns, increment co-occurrence count
        for (int i = 0; i < patternIds.Count; i++)
        {
            for (int j = i + 1; j < patternIds.Count; j++)
            {
                var pattern1 = await _storage.GetPatternAsync(patternIds[i], cancellationToken);
                var pattern2 = await _storage.GetPatternAsync(patternIds[j], cancellationToken);

                if (pattern1 != null && pattern2 != null)
                {
                    // Update pattern1's co-occurrence with pattern2
                    if (pattern1.CoOccurringPatterns.ContainsKey(pattern2.Id))
                    {
                        pattern1.CoOccurringPatterns[pattern2.Id]++;
                    }
                    else
                    {
                        pattern1.CoOccurringPatterns[pattern2.Id] = 1;
                    }

                    await _storage.StorePatternAsync(pattern1, cancellationToken);

                    // Update pattern2's co-occurrence with pattern1
                    if (pattern2.CoOccurringPatterns.ContainsKey(pattern1.Id))
                    {
                        pattern2.CoOccurringPatterns[pattern1.Id]++;
                    }
                    else
                    {
                        pattern2.CoOccurringPatterns[pattern1.Id] = 1;
                    }

                    await _storage.StorePatternAsync(pattern2, cancellationToken);
                }
            }
        }

        _logger.LogDebug("🔗 Updated co-occurrence for {Count} patterns", patternIds.Count);
    }

    /// <summary>
    /// Detect design trends over time
    /// </summary>
    public async Task<List<string>> DetectTrendsAsync(int timeWindowDays = 30, CancellationToken cancellationToken = default)
    {
        // Get top patterns from recent designs
        var patterns = await _storage.GetTopPatternsAsync(50, cancellationToken);

        var recentPatterns = patterns
            .Where(p => p.LearnedAt >= DateTime.UtcNow.AddDays(-timeWindowDays))
            .OrderByDescending(p => p.ObservationCount)
            .Take(10)
            .ToList();

        var trends = new List<string>();

        foreach (var pattern in recentPatterns)
        {
            trends.Add($"{pattern.Name} ({pattern.Category}) - observed {pattern.ObservationCount} times");
        }

        _logger.LogInformation("📈 Detected {Count} trends in last {Days} days", trends.Count, timeWindowDays);

        return trends;
    }

    // ===== PRIVATE HELPERS =====

    private async Task<List<DesignPattern>> ExtractPatternsFromPageAsync(PageAnalysis page, CapturedDesign design, CancellationToken cancellationToken)
    {
        var patterns = new List<DesignPattern>();

        // Extract patterns from high-scoring categories
        foreach (var (category, score) in page.CategoryScores.Where(kvp => kvp.Value >= 8.5))
        {
            var categoryDetails = page.CategoryDetails.ContainsKey(category) 
                ? page.CategoryDetails[category] 
                : null;

            if (categoryDetails == null)
                continue;

            var pattern = new DesignPattern
            {
                Name = $"{page.PageType}_{category}_{design.Url.GetHashCode():X}",
                Category = category,
                Type = "component",
                Description = string.Join("; ", categoryDetails.Strengths.Take(3)),
                QualityScore = score,
                ObservationCount = 1,
                SourceDesignIds = new List<string> { design.Id },
                Tags = ExtractTags(categoryDetails.Strengths),
                HtmlStructure = ExtractRelevantHtml(page.ExtractedHtml, category),
                CssStyle = ExtractRelevantCss(page.ExtractedCss, category),
                LearnedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            patterns.Add(pattern);
        }

        return patterns;
    }

    private List<string> ExtractTags(List<string> strengths)
    {
        var tags = new List<string>();

        // Extract keywords from strengths
        var keywords = new[] { "minimal", "gradient", "modern", "clean", "bold", "colorful", "dark", "light", "animation", "responsive" };

        foreach (var strength in strengths)
        {
            foreach (var keyword in keywords)
            {
                if (strength.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    tags.Add(keyword.ToLower());
                }
            }
        }

        return tags.Distinct().ToList();
    }

    private string? ExtractRelevantHtml(string? html, string category)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        // Simplified extraction - take first 1000 chars
        return html.Length > 1000 ? html.Substring(0, 1000) : html;
    }

    private string? ExtractRelevantCss(string? css, string category)
    {
        if (string.IsNullOrEmpty(css))
            return null;

        // Simplified extraction - take first 500 chars
        return css.Length > 500 ? css.Substring(0, 500) : css;
    }

    private string BuildMismatchAnalysisPrompt(CapturedDesign design, double humanScore)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Analyze why the LLM score and human rating differ:");
        sb.AppendLine();
        sb.AppendLine($"Design: {design.Url}");
        sb.AppendLine($"LLM Score: {design.OverallScore:F1}/10");
        sb.AppendLine($"Human Score: {humanScore:F1}/10");
        sb.AppendLine($"Mismatch: {Math.Abs(design.OverallScore - humanScore):F1} points");
        sb.AppendLine();
        sb.AppendLine("Page Scores:");

        foreach (var page in design.Pages)
        {
            sb.AppendLine($"- {page.PageType}: {page.OverallPageScore:F1}/10");
            sb.AppendLine($"  Strengths: {string.Join(", ", page.Strengths.Take(3))}");
        }

        sb.AppendLine();
        sb.AppendLine("What might the LLM have missed or overvalued?");
        sb.AppendLine("What aspects should be weighted differently?");

        return sb.ToString();
    }

    private string BuildPromptEvolutionPrompt(string promptName, string currentPrompt, List<DesignFeedback> feedback)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Evolve this prompt to improve scoring accuracy:");
        sb.AppendLine();
        sb.AppendLine($"Prompt Name: {promptName}");
        sb.AppendLine();
        sb.AppendLine("Current Prompt:");
        sb.AppendLine(currentPrompt);
        sb.AppendLine();
        sb.AppendLine("Feedback Data:");

        foreach (var fb in feedback.Take(10))
        {
            sb.AppendLine($"- LLM: {fb.LlmScore:F1}, Human: {fb.HumanScore:F1}, Mismatch: {fb.Mismatch:F1}");
        }

        sb.AppendLine();
        sb.AppendLine("Improve the prompt to:");
        sb.AppendLine("1. Reduce mismatch between LLM and human scores");
        sb.AppendLine("2. Capture aspects humans value that LLM might miss");
        sb.AppendLine("3. Maintain specificity and actionable criteria");
        sb.AppendLine();
        sb.AppendLine("Return only the improved prompt text.");

        return sb.ToString();
    }

    private string GetFallbackFeedbackAnalysisPrompt()
    {
        return @"You are analyzing mismatches between LLM design scores and human ratings.

Identify:
1. What the LLM might have missed (animations, subtle details, brand alignment)
2. What the LLM might have overvalued (flashy but poor UX, complexity)
3. Which scoring categories need adjustment
4. What criteria should be added or emphasized

Be specific and actionable.";
    }

    private string GetFallbackPromptEvolutionPrompt()
    {
        return @"You are evolving a design analysis prompt to improve accuracy.

Based on feedback showing mismatches between LLM and human scores, refine the prompt to:
1. Better capture subtle quality indicators
2. Adjust weighting of different aspects
3. Add missing evaluation criteria
4. Remove or de-emphasize less important factors

Keep the prompt clear, specific, and focused on visual design quality.";
    }

    private string ExtractNewPrompt(string response)
    {
        // Extract the new prompt from the response
        // For now, just return the full response (cleaned up)
        return response.Trim();
    }
}

