// 🎯 ENSEMBLE VALIDATION EXAMPLES
// This file demonstrates how to use ensemble validation in different scenarios

using AgentContracts.Requests;
using AgentContracts.Responses;
using ValidationAgent.Server.Services;

namespace ValidationAgent.Server.Examples;

/// <summary>
/// Examples of ensemble validation usage
/// </summary>
public class EnsembleValidationExamples
{
    private readonly IValidationEnsembleService _ensembleService;
    private readonly ILogger<EnsembleValidationExamples> _logger;

    public EnsembleValidationExamples(
        IValidationEnsembleService ensembleService,
        ILogger<EnsembleValidationExamples> logger)
    {
        _ensembleService = ensembleService;
        _logger = logger;
    }

    /// <summary>
    /// Example 1: Adaptive Strategy in Retry Loop (RECOMMENDED)
    /// </summary>
    public async Task<ValidateCodeResponse> Example1_AdaptiveRetryLoop(
        List<CodeFile> files,
        int maxIterations = 10,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 1: Adaptive ensemble in retry loop");

        for (int iteration = 1; iteration <= maxIterations; iteration++)
        {
            var request = new ValidateCodeRequest
            {
                Files = files,
                Context = "example",
                EnsembleStrategy = "adaptive",  // 👈 Smart strategy
                IterationNumber = iteration,
                MaxIterations = maxIterations
            };

            var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

            _logger.LogInformation(
                "Iteration {Iteration}: Score={Score}/10, Confidence={Confidence:P0}, Models={Models}",
                iteration, validation.Score, validation.Confidence,
                string.Join(", ", validation.ModelsUsed));

            // Check if good enough
            if (validation.Score >= 8 && validation.Confidence >= 0.7)
            {
                _logger.LogInformation("✅ Validation passed with high confidence!");
                return validation;
            }

            // Log individual model results
            if (validation.EnsembleResults != null)
            {
                foreach (var result in validation.EnsembleResults)
                {
                    _logger.LogInformation(
                        "  └─ {Model}: {Score}/10 ({Issues} issues, {Duration}ms, warm={Warm})",
                        result.Model, result.Score, result.IssueCount,
                        result.DurationMs, result.WasWarm);
                }
            }

            // In real retry loop, you'd regenerate code here based on feedback
            _logger.LogInformation("⚠️ Score too low, would retry with feedback...");
        }

        throw new Exception("Failed to achieve passing score after all iterations");
    }

    /// <summary>
    /// Example 2: Sequential Ensemble (Cost-Effective)
    /// </summary>
    public async Task<ValidateCodeResponse> Example2_SequentialValidation(
        List<CodeFile> files,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 2: Sequential ensemble (cost-effective)");

        var request = new ValidateCodeRequest
        {
            Files = files,
            Context = "example",
            EnsembleStrategy = "sequential"  // 👈 Adaptive depth
        };

        var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

        _logger.LogInformation(
            "Sequential result: Score={Score}/10, Confidence={Confidence:P0}, ModelsUsed={Count}",
            validation.Score, validation.Confidence, validation.ModelsUsed.Count);

        // Sequential uses 1-3 models depending on confidence
        // - Score >= 9 or <= 3: Uses 1 model (confident)
        // - Score 4-8, models agree: Uses 2 models
        // - Score 4-8, models disagree: Uses 3 models (tiebreaker)

        return validation;
    }

    /// <summary>
    /// Example 3: Parallel Voting (Highest Quality)
    /// </summary>
    public async Task<ValidateCodeResponse> Example3_ParallelVoting(
        List<CodeFile> files,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 3: Parallel voting (highest quality)");

        var request = new ValidateCodeRequest
        {
            Files = files,
            Context = "example",
            EnsembleStrategy = "parallel"  // 👈 3 models in parallel
        };

        var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

        _logger.LogInformation(
            "Parallel voting: Score={Score}/10 (average of 3 models), Confidence={Confidence:P0}",
            validation.Score, validation.Confidence);

        // Always uses 3 models for maximum confidence
        // Best for final validation or critical code

        return validation;
    }

    /// <summary>
    /// Example 4: Specialized Ensemble (Domain Experts)
    /// </summary>
    public async Task<ValidateCodeResponse> Example4_SpecializedValidation(
        List<CodeFile> files,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 4: Specialized ensemble (domain experts)");

        var request = new ValidateCodeRequest
        {
            Files = files,
            Context = "example",
            Rules = new List<string> { "security", "patterns", "best_practices" },
            EnsembleStrategy = "specialized"  // 👈 Domain-specific models
        };

        var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

        _logger.LogInformation(
            "Specialized result: Score={Score}/10, Experts={Experts}",
            validation.Score, string.Join(", ", validation.ModelsUsed));

        // Uses different models for different aspects:
        // - Security: deepseek-coder (security expert)
        // - Patterns: qwen2.5-coder (architecture expert)
        // - General: phi4 (quality expert)

        return validation;
    }

    /// <summary>
    /// Example 5: Pessimistic Ensemble (Safest)
    /// </summary>
    public async Task<ValidateCodeResponse> Example5_PessimisticValidation(
        List<CodeFile> files,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 5: Pessimistic ensemble (safest)");

        var request = new ValidateCodeRequest
        {
            Files = files,
            Context = "example",
            EnsembleStrategy = "pessimistic"  // 👈 Takes lowest score
        };

        var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

        _logger.LogInformation(
            "Pessimistic result: Score={Score}/10 (lowest of 2 models)",
            validation.Score);

        // Takes the LOWEST score from 2 models
        // Safest approach - prevents false positives
        // Good for production deployments

        return validation;
    }

    /// <summary>
    /// Example 6: Confidence-Based Decision Making
    /// </summary>
    public async Task<string> Example6_ConfidenceBasedDecisions(
        List<CodeFile> files,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 6: Confidence-based decision making");

        var request = new ValidateCodeRequest
        {
            Files = files,
            Context = "example",
            EnsembleStrategy = "parallel"  // Use voting for confidence
        };

        var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

        // Decision logic based on confidence
        if (validation.Score >= 8 && validation.Confidence >= 0.9)
        {
            _logger.LogInformation("✅ HIGH CONFIDENCE PASS - Ship it!");
            return "SHIP";
        }
        else if (validation.Score >= 8 && validation.Confidence >= 0.7)
        {
            _logger.LogInformation("✅ GOOD CONFIDENCE PASS - Proceed with caution");
            return "PROCEED";
        }
        else if (validation.Score >= 8 && validation.Confidence < 0.7)
        {
            _logger.LogWarning("⚠️ LOW CONFIDENCE PASS - Models disagree, needs human review");
            return "REVIEW_NEEDED";
        }
        else if (validation.Score < 8 && validation.Confidence >= 0.7)
        {
            _logger.LogWarning("❌ CONFIDENT FAIL - Clear issues, retry with feedback");
            return "RETRY";
        }
        else
        {
            _logger.LogWarning("❌ LOW CONFIDENCE FAIL - Models disagree on severity");
            return "REVIEW_NEEDED";
        }
    }

    /// <summary>
    /// Example 7: Analyzing Ensemble Results
    /// </summary>
    public async Task Example7_AnalyzeEnsembleResults(
        List<CodeFile> files,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 7: Analyzing ensemble results");

        var request = new ValidateCodeRequest
        {
            Files = files,
            Context = "example",
            EnsembleStrategy = "parallel"
        };

        var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

        // Analyze individual model results
        if (validation.EnsembleResults != null)
        {
            _logger.LogInformation("📊 Ensemble Analysis:");
            _logger.LogInformation("  Overall Score: {Score}/10", validation.Score);
            _logger.LogInformation("  Confidence: {Confidence:P0}", validation.Confidence);
            _logger.LogInformation("  Models Used: {Count}", validation.EnsembleResults.Count);
            _logger.LogInformation("");

            foreach (var result in validation.EnsembleResults)
            {
                _logger.LogInformation("  Model: {Model}", result.Model);
                _logger.LogInformation("    Score: {Score}/10", result.Score);
                _logger.LogInformation("    Issues Found: {Count}", result.IssueCount);
                _logger.LogInformation("    Duration: {Duration}ms", result.DurationMs);
                _logger.LogInformation("    Was Warm: {Warm}", result.WasWarm ? "Yes (instant)" : "No (cold start)");
                _logger.LogInformation("");
            }

            // Calculate score variance
            var scores = validation.EnsembleResults.Select(r => r.Score).ToArray();
            var mean = scores.Average();
            var variance = scores.Average(s => Math.Pow(s - mean, 2));
            var stdDev = Math.Sqrt(variance);

            _logger.LogInformation("  Score Statistics:");
            _logger.LogInformation("    Mean: {Mean:F1}", mean);
            _logger.LogInformation("    Std Dev: {StdDev:F2}", stdDev);
            _logger.LogInformation("    Range: {Min}-{Max}", scores.Min(), scores.Max());

            if (stdDev < 1.0)
                _logger.LogInformation("    ✅ Models strongly agree");
            else if (stdDev < 2.0)
                _logger.LogInformation("    ⚠️ Models somewhat agree");
            else
                _logger.LogInformation("    ❌ Models disagree significantly");
        }
    }

    /// <summary>
    /// Example 8: Production Deployment Validation
    /// </summary>
    public async Task<bool> Example8_ProductionDeploymentValidation(
        List<CodeFile> files,
        CancellationToken ct = default)
    {
        _logger.LogInformation("📚 Example 8: Production deployment validation");

        // For production, use pessimistic or parallel voting
        var request = new ValidateCodeRequest
        {
            Files = files,
            Context = "production",
            Rules = new List<string> { "security", "best_practices", "patterns" },
            ValidationMode = "enterprise",  // Strict mode
            EnsembleStrategy = "pessimistic"  // Safest approach
        };

        var validation = await _ensembleService.ValidateWithEnsembleAsync(request, ct);

        // Strict criteria for production
        var passedValidation = validation.Score >= 9;  // Higher bar
        var hasHighConfidence = validation.Confidence >= 0.8;
        var noCriticalIssues = !validation.Issues.Any(i => i.Severity == "critical");

        if (passedValidation && hasHighConfidence && noCriticalIssues)
        {
            _logger.LogInformation("✅ PRODUCTION READY - All checks passed");
            return true;
        }
        else
        {
            _logger.LogWarning("❌ NOT PRODUCTION READY:");
            if (!passedValidation)
                _logger.LogWarning("  - Score too low: {Score}/10 (need >= 9)", validation.Score);
            if (!hasHighConfidence)
                _logger.LogWarning("  - Confidence too low: {Confidence:P0} (need >= 80%)", validation.Confidence);
            if (!noCriticalIssues)
                _logger.LogWarning("  - Has critical issues: {Count}", 
                    validation.Issues.Count(i => i.Severity == "critical"));

            return false;
        }
    }
}

/// <summary>
/// Quick test runner for examples
/// </summary>
public class EnsembleExampleRunner
{
    public static async Task RunAllExamples(IValidationEnsembleService ensembleService, ILogger logger)
    {
        var examples = new EnsembleValidationExamples(
            ensembleService,
            logger as ILogger<EnsembleValidationExamples> ?? 
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<EnsembleValidationExamples>());

        // Sample code to validate
        var sampleFiles = new List<CodeFile>
        {
            new CodeFile
            {
                Path = "UserService.cs",
                Content = @"
public class UserService
{
    public async Task<User> GetUserAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        return user; // Potential null reference
    }
}",
                IsNew = true
            }
        };

        try
        {
            logger.LogInformation("🚀 Running Ensemble Validation Examples...\n");

            // Example 1: Adaptive
            await examples.Example1_AdaptiveRetryLoop(sampleFiles, maxIterations: 3);
            logger.LogInformation("\n" + new string('=', 80) + "\n");

            // Example 2: Sequential
            await examples.Example2_SequentialValidation(sampleFiles);
            logger.LogInformation("\n" + new string('=', 80) + "\n");

            // Example 6: Confidence-based decisions
            var decision = await examples.Example6_ConfidenceBasedDecisions(sampleFiles);
            logger.LogInformation("Decision: {Decision}", decision);
            logger.LogInformation("\n" + new string('=', 80) + "\n");

            // Example 7: Analyze results
            await examples.Example7_AnalyzeEnsembleResults(sampleFiles);

            logger.LogInformation("\n✅ All examples completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Example failed");
        }
    }
}



