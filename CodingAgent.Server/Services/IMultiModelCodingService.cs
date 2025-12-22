using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingAgent.Server.Services;

/// <summary>
/// 💻💻💻 MULTI-MODEL CODING SERVICE
/// Coordinates multiple models for code generation
/// Strategies: Solo, Duo, Trio, Collaborative
/// </summary>
public interface IMultiModelCodingService
{
    /// <summary>
    /// Generate code using SINGLE model (fast)
    /// </summary>
    Task<MultiModelCodeResult> GenerateSoloAsync(
        GenerateCodeRequest request,
        string modelName,
        string thinkingGuidance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate code using TWO models (review pattern)
    /// Model 1 generates, Model 2 reviews, Model 1 fixes
    /// </summary>
    Task<MultiModelCodeResult> GenerateDuoAsync(
        GenerateCodeRequest request,
        string generatorModel,
        string reviewerModel,
        string thinkingGuidance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate code using THREE models (parallel exploration)
    /// All 3 generate different approaches, vote on best or merge
    /// </summary>
    Task<MultiModelCodeResult> GenerateTrioAsync(
        GenerateCodeRequest request,
        string[] models,
        string thinkingGuidance,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate code using ALL models (collaborative)
    /// Multi-stage: draft → review → refine → finalize → verify
    /// </summary>
    Task<MultiModelCodeResult> GenerateCollaborativeAsync(
        GenerateCodeRequest request,
        string thinkingGuidance,
        bool includeCloud = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Smart strategy selection based on complexity and attempt number
    /// </summary>
    Task<MultiModelCodeResult> GenerateSmartAsync(
        GenerateCodeRequest request,
        int attemptNumber,
        string thinkingGuidance,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of multi-model code generation
/// </summary>
public class MultiModelCodeResult
{
    /// <summary>
    /// Generated files
    /// </summary>
    public List<FileChange> FileChanges { get; set; } = new();

    /// <summary>
    /// Which strategy was used
    /// </summary>
    public string Strategy { get; set; } = "solo";

    /// <summary>
    /// Models that participated
    /// </summary>
    public List<string> ParticipatingModels { get; set; } = new();

    /// <summary>
    /// Individual model contributions
    /// </summary>
    public List<ModelContribution> Contributions { get; set; } = new();

    /// <summary>
    /// Collaboration insights (reviews, suggestions, votes)
    /// </summary>
    public string CollaborationLog { get; set; } = "";

    /// <summary>
    /// Total time taken
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Was cloud API used?
    /// </summary>
    public bool UsedCloudAPI { get; set; }

    /// <summary>
    /// Estimated cost (if cloud used)
    /// </summary>
    public decimal EstimatedCost { get; set; }

    /// <summary>
    /// Convert to standard response
    /// </summary>
    public GenerateCodeResponse ToResponse(string primaryModel)
    {
        return new GenerateCodeResponse
        {
            Success = FileChanges.Any(),
            FileChanges = FileChanges,
            ModelUsed = primaryModel
        };
    }
}

/// <summary>
/// Individual model's contribution
/// </summary>
public class ModelContribution
{
    public string ModelName { get; set; } = "";
    public string Role { get; set; } = ""; // generator, reviewer, voter, verifier
    public string Output { get; set; } = "";
    public List<string> Suggestions { get; set; } = new();
    public List<string> Concerns { get; set; } = new();
    public double ConfidenceScore { get; set; } = 1.0;
    public long DurationMs { get; set; }
    public int GPU { get; set; }
}



