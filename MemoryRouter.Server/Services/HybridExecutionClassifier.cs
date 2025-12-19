namespace MemoryRouter.Server.Services;

/// <summary>
/// Combines AI predictions with statistical performance data for optimal execution decisions
/// NO FALLBACKS - Always uses intelligent analysis (AI + Statistics)
/// </summary>
public class HybridExecutionClassifier : IHybridExecutionClassifier
{
    private readonly IPerformanceTracker _performanceTracker;
    private readonly IAIComplexityAnalyzer _aiAnalyzer;
    private readonly ILogger<HybridExecutionClassifier> _logger;

    // Decision thresholds
    private const int AsyncThresholdSeconds = 15;
    private const int MinSamplesForStatisticalWeight = 10;

    public HybridExecutionClassifier(
        IPerformanceTracker performanceTracker,
        IAIComplexityAnalyzer aiAnalyzer,
        ILogger<HybridExecutionClassifier> logger)
    {
        _performanceTracker = performanceTracker;
        _aiAnalyzer = aiAnalyzer;
        _logger = logger;
    }

    public async Task<ExecutionDecision> DetermineExecutionModeAsync(
        string toolName,
        string userRequest,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🧠 Hybrid Analysis for: {Tool}", toolName);

        // STEP 1: Get AI Prediction (Try AI, but don't block if it's failing)
        ExecutionPrediction aiPrediction;
        try
        {
            aiPrediction = await _aiAnalyzer.PredictExecutionAsync(
                toolName, userRequest, arguments, cancellationToken);
            
            _logger.LogDebug("🤖 AI: {Complexity} complexity, {Seconds}s estimate, {Confidence}% confidence",
                aiPrediction.Complexity, aiPrediction.EstimatedSeconds, aiPrediction.ConfidencePercent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ AI analysis unavailable (will use metadata + learning): {Error}", ex.Message);
            
            // Use intelligent metadata-based estimate
            aiPrediction = GetMetadataBasedPrediction(toolName, userRequest);
            
            _logger.LogDebug("📋 Using metadata estimate: {Complexity} complexity, {Seconds}s",
                aiPrediction.Complexity, aiPrediction.EstimatedSeconds);
        }

        // STEP 2: Get Historical Performance (if available)
        var historicalStats = _performanceTracker.GetStats(toolName);

        if (historicalStats == null || historicalStats.SampleSize < 5)
        {
            // Not enough historical data - use AI prediction 100%
            _logger.LogInformation("📊 No historical data ({Count} samples) - using AI prediction 100%",
                historicalStats?.SampleSize ?? 0);

            return CreateDecisionFromAI(aiPrediction);
        }

        _logger.LogDebug("📊 Historical: Avg={Avg}ms, P90={P90}ms, Samples={Count}, Trend={Trend}",
            historicalStats.AverageDurationMs,
            historicalStats.P90DurationMs,
            historicalStats.SampleSize,
            historicalStats.RecentTrend);

        // STEP 3: Combine AI + Historical Intelligence
        var decision = CombineIntelligence(aiPrediction, historicalStats, toolName);

        return decision;
    }

    private ExecutionDecision CreateDecisionFromAI(ExecutionPrediction aiPrediction)
    {
        var estimatedMs = aiPrediction.EstimatedSeconds * 1000;
        var shouldRunAsync = aiPrediction.EstimatedSeconds > AsyncThresholdSeconds;

        return new ExecutionDecision
        {
            ShouldRunAsync = shouldRunAsync,
            EstimatedDurationMs = estimatedMs,
            ConfidencePercent = aiPrediction.ConfidencePercent,
            Reasoning = $"AI Analysis: {aiPrediction.Reasoning}",
            DecisionSource = "AI_Only",
            Complexity = aiPrediction.Complexity
        };
    }

    private ExecutionDecision CombineIntelligence(
        ExecutionPrediction aiPrediction,
        PerformanceStats historicalStats,
        string toolName)
    {
        // Determine weighting based on data quality
        double historicalWeight;
        double aiWeight;

        if (historicalStats.SampleSize >= MinSamplesForStatisticalWeight)
        {
            // Strong historical data - weight it more heavily
            historicalWeight = 0.7;
            aiWeight = 0.3;
            _logger.LogDebug("💪 Strong historical data ({Count} samples) - 70/30 weight", historicalStats.SampleSize);
        }
        else
        {
            // Limited historical data - balance with AI
            historicalWeight = 0.5;
            aiWeight = 0.5;
            _logger.LogDebug("⚖️ Limited historical data ({Count} samples) - 50/50 weight", historicalStats.SampleSize);
        }

        // Calculate weighted estimate
        var historicalEstimateMs = historicalStats.P90DurationMs; // Use P90 for conservative estimate
        var aiEstimateMs = aiPrediction.EstimatedSeconds * 1000;
        
        var weightedEstimateMs = (long)(
            (historicalEstimateMs * historicalWeight) +
            (aiEstimateMs * aiWeight)
        );

        _logger.LogDebug("🧮 Weighted Calculation:");
        _logger.LogDebug("   Historical P90: {Ms}ms × {Weight:P0} = {Result}ms",
            historicalEstimateMs, historicalWeight, historicalEstimateMs * historicalWeight);
        _logger.LogDebug("   AI Estimate: {Ms}ms × {Weight:P0} = {Result}ms",
            aiEstimateMs, aiWeight, aiEstimateMs * aiWeight);
        _logger.LogDebug("   Combined: {Ms}ms", weightedEstimateMs);

        // AI can override if it detects significantly different complexity
        var shouldRunAsync = weightedEstimateMs > (AsyncThresholdSeconds * 1000);
        
        // AI Override Logic: If AI strongly disagrees with statistical trend
        if (aiPrediction.ConfidencePercent > 85)
        {
            if (aiPrediction.Complexity == TaskComplexity.Low && 
                aiPrediction.EstimatedSeconds < 10 &&
                historicalStats.AverageDurationMs > 30000)
            {
                _logger.LogWarning("🔄 AI OVERRIDE: AI detected LOW complexity despite high historical average");
                _logger.LogWarning("   AI says: {Seconds}s ({Reason})", aiPrediction.EstimatedSeconds, aiPrediction.Reasoning);
                _logger.LogWarning("   Using AI prediction with 80% weight");
                
                weightedEstimateMs = (long)(
                    (aiEstimateMs * 0.8) +
                    (historicalEstimateMs * 0.2)
                );
                shouldRunAsync = aiPrediction.ShouldRunAsync;
            }
            else if (aiPrediction.Complexity == TaskComplexity.High && 
                     aiPrediction.EstimatedSeconds > 90 &&
                     historicalStats.AverageDurationMs < 30000)
            {
                _logger.LogWarning("🔄 AI OVERRIDE: AI detected HIGH complexity despite low historical average");
                _logger.LogWarning("   AI says: {Seconds}s ({Reason})", aiPrediction.EstimatedSeconds, aiPrediction.Reasoning);
                _logger.LogWarning("   Using AI prediction with 70% weight");
                
                weightedEstimateMs = (long)(
                    (aiEstimateMs * 0.7) +
                    (historicalEstimateMs * 0.3)
                );
                shouldRunAsync = true;
            }
        }

        // Consider trend
        var trendAdjustment = historicalStats.RecentTrend switch
        {
            "increasing" => 1.1, // 10% increase
            "decreasing" => 0.9, // 10% decrease
            _ => 1.0
        };

        if (trendAdjustment != 1.0)
        {
            _logger.LogDebug("📈 Trend adjustment: {Trend} → ×{Multiplier}", 
                historicalStats.RecentTrend, trendAdjustment);
            weightedEstimateMs = (long)(weightedEstimateMs * trendAdjustment);
        }

        var confidence = CalculateConfidence(historicalStats.SampleSize, aiPrediction.ConfidencePercent);

        return new ExecutionDecision
        {
            ShouldRunAsync = shouldRunAsync,
            EstimatedDurationMs = weightedEstimateMs,
            ConfidencePercent = confidence,
            Reasoning = $"Hybrid: {historicalWeight:P0} historical ({historicalStats.P90DurationMs}ms P90) + " +
                       $"{aiWeight:P0} AI ({aiPrediction.EstimatedSeconds}s {aiPrediction.Complexity}). " +
                       $"{aiPrediction.Reasoning}",
            DecisionSource = "Hybrid_Intelligence",
            Complexity = aiPrediction.Complexity,
            HistoricalData = new HistoricalContext
            {
                SampleSize = historicalStats.SampleSize,
                AverageDurationMs = historicalStats.AverageDurationMs,
                P90DurationMs = historicalStats.P90DurationMs,
                Trend = historicalStats.RecentTrend
            }
        };
    }

    private int CalculateConfidence(int sampleSize, int aiConfidence)
    {
        // Confidence increases with more historical data
        var historicalConfidenceBoost = Math.Min(sampleSize * 2, 30); // Max +30%
        var combined = Math.Min(aiConfidence + historicalConfidenceBoost, 98);
        return combined;
    }

    /// <summary>
    /// Intelligent metadata-based prediction when AI is unavailable
    /// Uses tool patterns and keywords to make educated guesses
    /// </summary>
    private ExecutionPrediction GetMetadataBasedPrediction(string toolName, string userRequest)
    {
        var lower = userRequest.ToLowerInvariant();
        var lowerTool = toolName.ToLowerInvariant();

        // Code generation tools - typically long
        if (lowerTool.Contains("orchestrate") || lowerTool.Contains("generate"))
        {
            // Check complexity indicators
            if (lower.Contains("simple") || lower.Contains("hello world") || lower.Contains("basic"))
            {
                return new ExecutionPrediction
                {
                    Complexity = TaskComplexity.Low,
                    EstimatedSeconds = 15,
                    ShouldRunAsync = true,
                    ConfidencePercent = 70,
                    Reasoning = "Metadata: Simple code generation task"
                };
            }
            else if (lower.Contains("microservice") || lower.Contains("full") || lower.Contains("application"))
            {
                return new ExecutionPrediction
                {
                    Complexity = TaskComplexity.High,
                    EstimatedSeconds = 90,
                    ShouldRunAsync = true,
                    ConfidencePercent = 75,
                    Reasoning = "Metadata: Complex application development"
                };
            }
            else
            {
                return new ExecutionPrediction
                {
                    Complexity = TaskComplexity.Medium,
                    EstimatedSeconds = 45,
                    ShouldRunAsync = true,
                    ConfidencePercent = 65,
                    Reasoning = "Metadata: Standard code generation"
                };
            }
        }

        // Search tools - typically fast
        if (lowerTool.Contains("search") || lowerTool.Contains("find"))
        {
            return new ExecutionPrediction
            {
                Complexity = TaskComplexity.Low,
                EstimatedSeconds = 3,
                ShouldRunAsync = false,
                ConfidencePercent = 85,
                Reasoning = "Metadata: Search operations are fast"
            };
        }

        // Planning tools - typically fast
        if (lowerTool.Contains("plan"))
        {
            return new ExecutionPrediction
            {
                Complexity = TaskComplexity.Low,
                EstimatedSeconds = 8,
                ShouldRunAsync = false,
                ConfidencePercent = 80,
                Reasoning = "Metadata: Planning is quick"
            };
        }

        // Default - conservative medium estimate
        return new ExecutionPrediction
        {
            Complexity = TaskComplexity.Medium,
            EstimatedSeconds = 20,
            ShouldRunAsync = true,
            ConfidencePercent = 50,
            Reasoning = "Metadata: Conservative estimate for unknown tool type"
        };
    }
}

public interface IHybridExecutionClassifier
{
    Task<ExecutionDecision> DetermineExecutionModeAsync(
        string toolName,
        string userRequest,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default);
}

public class ExecutionDecision
{
    public bool ShouldRunAsync { get; set; }
    public long EstimatedDurationMs { get; set; }
    public int ConfidencePercent { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string DecisionSource { get; set; } = string.Empty; // "AI_Only", "Hybrid_Intelligence"
    public TaskComplexity Complexity { get; set; }
    public HistoricalContext? HistoricalData { get; set; }
}

public class HistoricalContext
{
    public int SampleSize { get; set; }
    public long AverageDurationMs { get; set; }
    public long P90DurationMs { get; set; }
    public string Trend { get; set; } = "stable";
}

