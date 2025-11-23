using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for validating Azure best practices in code
/// </summary>
public interface IBestPracticeValidationService
{
    /// <summary>
    /// Validate best practices for a project
    /// </summary>
    Task<BestPracticeValidationResponse> ValidateBestPracticesAsync(
        BestPracticeValidationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available best practices
    /// </summary>
    Task<List<string>> GetAvailableBestPracticesAsync(CancellationToken cancellationToken = default);
}

