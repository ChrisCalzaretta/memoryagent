using System.Diagnostics;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace ValidationAgent.Server.Services;

/// <summary>
/// 🎯 Ensemble Validation Service - Model Collaboration for Higher Quality
/// 
/// Orchestrates multiple validation models to:
/// - Increase confidence through consensus
/// - Reduce false positives/negatives
/// - Leverage model specialization (security, patterns, architecture)
/// - Adaptive strategy based on iteration number
/// </summary>
public interface IValidationEnsembleService
{
    /// <summary>
    /// Validate code using ensemble strategy
    /// </summary>
    Task<ValidateCodeResponse> ValidateWithEnsembleAsync(
        ValidateCodeRequest request, 
        CancellationToken cancellationToken);
}

public class ValidationEnsembleService : IValidationEnsembleService
{
    private readonly IValidationModelSelector _modelSelector;
    private readonly ValidationService _validationService;
    private readonly ILogger<ValidationEnsembleService> _logger;
    private readonly IConfiguration _config;
    
    // Available validation models (in order of preference)
    private readonly List<string> _availableModels = new()
    {
        "phi4:latest",              // Fast, good at patterns
        "deepseek-coder:1.5b",      // Excellent at security
        "qwen2.5-coder:1.5b",       // Good architecture analysis
        "qwen2.5-coder:3b",         // Larger, more thorough
        "granite3-dense:2b",        // Alternative perspective
    };

    public ValidationEnsembleService(
        IValidationModelSelector modelSelector,
        ValidationService validationService,
        ILogger<ValidationEnsembleService> logger,
        IConfiguration config)
    {
        _modelSelector = modelSelector;
        _validationService = validationService;
        _logger = logger;
        _config = config;
    }

    public async Task<ValidateCodeResponse> ValidateWithEnsembleAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        var strategy = ParseEnsembleStrategy(request.EnsembleStrategy);
        
        _logger.LogInformation(
            "🎯 Ensemble validation: strategy={Strategy}, files={FileCount}, iteration={Iteration}/{Max}",
            strategy, request.Files.Count, request.IterationNumber, request.MaxIterations);

        return strategy switch
        {
            "single" => await ValidateSingleAsync(request, cancellationToken),
            "sequential" => await ValidateSequentialAsync(request, cancellationToken),
            "parallel" => await ValidateParallelVotingAsync(request, cancellationToken),
            "specialized" => await ValidateSpecializedAsync(request, cancellationToken),
            "adaptive" => await ValidateAdaptiveAsync(request, cancellationToken),
            "pessimistic" => await ValidatePessimisticAsync(request, cancellationToken),
            "optimistic" => await ValidateOptimisticAsync(request, cancellationToken),
            _ => await ValidateSingleAsync(request, cancellationToken)
        };
    }

    /// <summary>
    /// Strategy 1: Single Model (Default - Fastest)
    /// </summary>
    private async Task<ValidateCodeResponse> ValidateSingleAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        return await _validationService.ValidateAsync(request, cancellationToken);
    }

    /// <summary>
    /// Strategy 2: Sequential Ensemble (Recommended - Cost-Effective)
    /// Starts with fast model, adds more only if needed
    /// </summary>
    private async Task<ValidateCodeResponse> ValidateSequentialAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        var ensembleResults = new List<EnsembleMemberResult>();
        
        // Stage 1: Fast model (phi4) - screens obvious issues
        _logger.LogInformation("🔍 Sequential Stage 1: Fast screening");
        var (quickResult, quickDuration) = await ValidateWithTimingAsync(request, "phi4:latest", cancellationToken);
        
        ensembleResults.Add(new EnsembleMemberResult
        {
            Model = "phi4:latest",
            Score = quickResult.Score,
            IssueCount = quickResult.Issues.Count,
            DurationMs = quickDuration,
            WasWarm = true
        });

        // Very confident pass or fail - no need for more validation
        if (quickResult.Score >= 9)
        {
            _logger.LogInformation("✅ Sequential: Very confident PASS ({Score}/10), no second opinion needed", quickResult.Score);
            quickResult.Confidence = 0.9;
            quickResult.ModelsUsed = new List<string> { "phi4:latest" };
            quickResult.EnsembleResults = ensembleResults;
            return quickResult;
        }
        
        if (quickResult.Score <= 3)
        {
            _logger.LogInformation("❌ Sequential: Very confident FAIL ({Score}/10), no second opinion needed", quickResult.Score);
            quickResult.Confidence = 0.9;
            quickResult.ModelsUsed = new List<string> { "phi4:latest" };
            quickResult.EnsembleResults = ensembleResults;
            return quickResult;
        }
        
        // Stage 2: Borderline case (4-8) - get second opinion
        _logger.LogInformation("⚠️ Sequential Stage 2: Borderline score {Score}, getting second opinion", quickResult.Score);
        var (secondResult, secondDuration) = await ValidateWithTimingAsync(request, "deepseek-coder:1.5b", cancellationToken);
        
        ensembleResults.Add(new EnsembleMemberResult
        {
            Model = "deepseek-coder:1.5b",
            Score = secondResult.Score,
            IssueCount = secondResult.Issues.Count,
            DurationMs = secondDuration,
            WasWarm = false
        });

        // If models disagree significantly (> 2 points), get tiebreaker
        var scoreDiff = Math.Abs(quickResult.Score - secondResult.Score);
        if (scoreDiff > 2)
        {
            _logger.LogInformation(
                "🤔 Sequential Stage 3: Models disagree ({Score1} vs {Score2}, diff={Diff}), getting tiebreaker",
                quickResult.Score, secondResult.Score, scoreDiff);
            
            var (tiebreakerResult, tiebreakerDuration) = await ValidateWithTimingAsync(request, "qwen2.5-coder:3b", cancellationToken);
            
            ensembleResults.Add(new EnsembleMemberResult
            {
                Model = "qwen2.5-coder:3b",
                Score = tiebreakerResult.Score,
                IssueCount = tiebreakerResult.Issues.Count,
                DurationMs = tiebreakerDuration,
                WasWarm = false
            });
            
            return AggregateResults(
                new[] { quickResult, secondResult, tiebreakerResult },
                ensembleResults,
                AggregationMode.Voting);
        }
        
        // Models mostly agree - average them
        _logger.LogInformation("✅ Sequential: Models agree ({Score1} vs {Score2}), averaging", 
            quickResult.Score, secondResult.Score);
        
        return AggregateResults(
            new[] { quickResult, secondResult },
            ensembleResults,
            AggregationMode.Voting);
    }

    /// <summary>
    /// Strategy 3: Parallel Voting (Highest Quality - Most Expensive)
    /// Runs 3 models in parallel and votes on results
    /// </summary>
    private async Task<ValidateCodeResponse> ValidateParallelVotingAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 Parallel Voting: Running 3 models simultaneously");
        
        var models = new[] { "phi4:latest", "deepseek-coder:1.5b", "qwen2.5-coder:1.5b" };
        var sw = Stopwatch.StartNew();
        
        // Run all 3 validations in parallel
        var tasks = models.Select(model => 
            ValidateWithTimingAsync(request, model, cancellationToken));
        
        var results = await Task.WhenAll(tasks);
        sw.Stop();
        
        _logger.LogInformation("✅ Parallel Voting: Completed 3 validations in {Duration}ms", sw.ElapsedMilliseconds);
        
        var ensembleResults = results.Select(r => new EnsembleMemberResult
        {
            Model = models[Array.IndexOf(results, r)],
            Score = r.result.Score,
            IssueCount = r.result.Issues.Count,
            DurationMs = r.duration,
            WasWarm = models[Array.IndexOf(results, r)] == "phi4:latest"
        }).ToList();
        
        return AggregateResults(
            results.Select(r => r.result).ToArray(),
            ensembleResults,
            AggregationMode.Voting);
    }

    /// <summary>
    /// Strategy 4: Specialized Ensemble (Domain-Specific Models)
    /// Uses different models for different aspects
    /// </summary>
    private async Task<ValidateCodeResponse> ValidateSpecializedAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🎯 Specialized: Using domain-specific models");
        
        var results = new List<ValidateCodeResponse>();
        var ensembleResults = new List<EnsembleMemberResult>();
        
        // Security specialist (if security rules requested)
        if (request.Rules.Contains("security"))
        {
            _logger.LogInformation("🔒 Specialized: Running security specialist (deepseek-coder)");
            var (result, duration) = await ValidateWithTimingAsync(request, "deepseek-coder:1.5b", cancellationToken);
            results.Add(result);
            ensembleResults.Add(new EnsembleMemberResult
            {
                Model = "deepseek-coder:1.5b (security)",
                Score = result.Score,
                IssueCount = result.Issues.Count,
                DurationMs = duration,
                WasWarm = false
            });
        }
        
        // Pattern/architecture specialist (if patterns rules requested)
        if (request.Rules.Contains("patterns") || request.Rules.Contains("architecture"))
        {
            _logger.LogInformation("🏗️ Specialized: Running architecture specialist (qwen2.5-coder)");
            var (result, duration) = await ValidateWithTimingAsync(request, "qwen2.5-coder:3b", cancellationToken);
            results.Add(result);
            ensembleResults.Add(new EnsembleMemberResult
            {
                Model = "qwen2.5-coder:3b (architecture)",
                Score = result.Score,
                IssueCount = result.Issues.Count,
                DurationMs = duration,
                WasWarm = false
            });
        }
        
        // General code quality (always run)
        _logger.LogInformation("✨ Specialized: Running general quality model (phi4)");
        var (qualityResult, qualityDuration) = await ValidateWithTimingAsync(request, "phi4:latest", cancellationToken);
        results.Add(qualityResult);
        ensembleResults.Add(new EnsembleMemberResult
        {
            Model = "phi4:latest (quality)",
            Score = qualityResult.Score,
            IssueCount = qualityResult.Issues.Count,
            DurationMs = qualityDuration,
            WasWarm = true
        });
        
        return AggregateResults(results.ToArray(), ensembleResults, AggregationMode.WeightedAverage);
    }

    /// <summary>
    /// Strategy 5: Adaptive (Smart - Changes with Iteration)
    /// Early: Single model, Late: Sequential, Final: Full voting
    /// </summary>
    private async Task<ValidateCodeResponse> ValidateAdaptiveAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        var iteration = request.IterationNumber ?? 1;
        var maxIterations = request.MaxIterations ?? 10;
        
        // Early iterations (1-7): Fast single model
        if (iteration < maxIterations - 2)
        {
            _logger.LogInformation("⚡ Adaptive: Early iteration ({Iteration}/{Max}), using single model", 
                iteration, maxIterations);
            return await ValidateSingleAsync(request, cancellationToken);
        }
        
        // Late iterations (8-9): Sequential ensemble (adaptive)
        if (iteration < maxIterations)
        {
            _logger.LogInformation("🔄 Adaptive: Late iteration ({Iteration}/{Max}), using sequential ensemble", 
                iteration, maxIterations);
            return await ValidateSequentialAsync(request, cancellationToken);
        }
        
        // Final iteration (10): Full parallel voting for maximum confidence
        _logger.LogInformation("🎯 Adaptive: Final iteration ({Iteration}/{Max}), using full ensemble voting", 
            iteration, maxIterations);
        return await ValidateParallelVotingAsync(request, cancellationToken);
    }

    /// <summary>
    /// Strategy 6: Pessimistic (Safest - Lowest Score Wins)
    /// </summary>
    private async Task<ValidateCodeResponse> ValidatePessimisticAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("⚠️ Pessimistic: Taking lowest score (safest approach)");
        
        var models = new[] { "phi4:latest", "deepseek-coder:1.5b" };
        var tasks = models.Select(model => ValidateWithTimingAsync(request, model, cancellationToken));
        var results = await Task.WhenAll(tasks);
        
        var ensembleResults = results.Select(r => new EnsembleMemberResult
        {
            Model = models[Array.IndexOf(results, r)],
            Score = r.result.Score,
            IssueCount = r.result.Issues.Count,
            DurationMs = r.duration,
            WasWarm = models[Array.IndexOf(results, r)] == "phi4:latest"
        }).ToList();
        
        return AggregateResults(
            results.Select(r => r.result).ToArray(),
            ensembleResults,
            AggregationMode.Pessimistic);
    }

    /// <summary>
    /// Strategy 7: Optimistic (Fastest Iteration - Highest Score Wins)
    /// </summary>
    private async Task<ValidateCodeResponse> ValidateOptimisticAsync(
        ValidateCodeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("✅ Optimistic: Taking highest score (fastest iteration)");
        
        var models = new[] { "phi4:latest", "qwen2.5-coder:1.5b" };
        var tasks = models.Select(model => ValidateWithTimingAsync(request, model, cancellationToken));
        var results = await Task.WhenAll(tasks);
        
        var ensembleResults = results.Select(r => new EnsembleMemberResult
        {
            Model = models[Array.IndexOf(results, r)],
            Score = r.result.Score,
            IssueCount = r.result.Issues.Count,
            DurationMs = r.duration,
            WasWarm = models[Array.IndexOf(results, r)] == "phi4:latest"
        }).ToList();
        
        return AggregateResults(
            results.Select(r => r.result).ToArray(),
            ensembleResults,
            AggregationMode.Optimistic);
    }

    /// <summary>
    /// Validate with a specific model and track duration
    /// </summary>
    private async Task<(ValidateCodeResponse result, long duration)> ValidateWithTimingAsync(
        ValidateCodeRequest request,
        string forcedModel,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        
        // Temporarily override model selection by setting specific model in config
        // This is a bit hacky but works without changing ValidationService signature
        var originalModel = _config.GetValue<string>("Gpu:ValidationModel");
        _config["Gpu:ValidationModel"] = forcedModel;
        _config["Gpu:UseSmartModelSelection"] = "false"; // Force the specified model
        
        try
        {
            var result = await _validationService.ValidateAsync(request, cancellationToken);
            sw.Stop();
            return (result, sw.ElapsedMilliseconds);
        }
        finally
        {
            // Restore original config
            if (originalModel != null)
                _config["Gpu:ValidationModel"] = originalModel;
            _config["Gpu:UseSmartModelSelection"] = "true";
        }
    }

    /// <summary>
    /// Aggregate multiple validation results into a single consensus result
    /// </summary>
    private ValidateCodeResponse AggregateResults(
        ValidateCodeResponse[] results,
        List<EnsembleMemberResult> ensembleResults,
        AggregationMode mode)
    {
        if (results.Length == 0)
            throw new ArgumentException("No results to aggregate");

        if (results.Length == 1)
        {
            results[0].Confidence = 1.0;
            results[0].ModelsUsed = new List<string> { ensembleResults[0].Model };
            results[0].EnsembleResults = ensembleResults;
            return results[0];
        }

        ValidateCodeResponse aggregated;

        switch (mode)
        {
            case AggregationMode.Voting:
                // Average score, include issues that at least 2 models agree on
                var avgScore = (int)Math.Round(results.Average(r => r.Score));
                aggregated = results.First();
                aggregated.Score = avgScore;
                aggregated.Issues = AggregateIssues(results, minAgreement: 2);
                break;

            case AggregationMode.Pessimistic:
                // Take the LOWEST score (safest)
                aggregated = results.OrderBy(r => r.Score).First();
                aggregated.Issues = results.SelectMany(r => r.Issues).DistinctBy(i => i.Message).ToList();
                break;

            case AggregationMode.Optimistic:
                // Take the HIGHEST score (fastest iteration)
                aggregated = results.OrderByDescending(r => r.Score).First();
                break;

            case AggregationMode.WeightedAverage:
                // Weight by model reliability (for now, simple average)
                aggregated = results.First();
                aggregated.Score = (int)Math.Round(results.Average(r => r.Score));
                aggregated.Issues = AggregateIssues(results, minAgreement: 1);
                break;

            default:
                aggregated = results.First();
                break;
        }

        // Calculate confidence based on model agreement
        var scores = results.Select(r => r.Score).ToArray();
        var scoreMean = scores.Average();
        var scoreStdDev = Math.Sqrt(scores.Average(s => Math.Pow(s - scoreMean, 2)));
        aggregated.Confidence = Math.Max(0, 1.0 - (scoreStdDev / 5.0)); // Lower std dev = higher confidence

        aggregated.ModelsUsed = ensembleResults.Select(e => e.Model).ToList();
        aggregated.EnsembleResults = ensembleResults;
        aggregated.Passed = aggregated.Score >= 8;

        _logger.LogInformation(
            "📊 Ensemble aggregation: mode={Mode}, finalScore={Score}, confidence={Confidence:P0}, models={ModelCount}",
            mode, aggregated.Score, aggregated.Confidence, results.Length);

        return aggregated;
    }

    /// <summary>
    /// Aggregate issues from multiple models, requiring minimum agreement
    /// </summary>
    private List<ValidationIssue> AggregateIssues(ValidateCodeResponse[] results, int minAgreement)
    {
        var allIssues = results.SelectMany(r => r.Issues).ToList();
        
        // Group by message similarity
        var grouped = allIssues
            .GroupBy(i => (i.Message, i.File, i.Severity))
            .Where(g => g.Count() >= minAgreement)
            .Select(g => g.First())
            .ToList();

        return grouped;
    }

    private string ParseEnsembleStrategy(string strategy)
    {
        return strategy?.ToLowerInvariant() switch
        {
            "single" or "0" => "single",
            "sequential" or "1" => "sequential",
            "parallel" or "parallelvoting" or "2" => "parallel",
            "specialized" or "3" => "specialized",
            "adaptive" or "4" => "adaptive",
            "pessimistic" or "5" => "pessimistic",
            "optimistic" or "6" => "optimistic",
            _ => "single"
        };
    }
}

/// <summary>
/// How to aggregate multiple validation results
/// </summary>
internal enum AggregationMode
{
    Voting,           // Average scores, consensus on issues
    Pessimistic,      // Lowest score wins
    Optimistic,       // Highest score wins
    WeightedAverage   // Weight by model reliability
}



