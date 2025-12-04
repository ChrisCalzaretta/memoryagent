using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for classifying user intent using LLM analysis
/// </summary>
public interface IIntentClassificationService
{
    /// <summary>
    /// Classify user intent from natural language request
    /// </summary>
    Task<UserIntent> ClassifyIntentAsync(
        string userRequest, 
        string? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggest relevant pattern categories based on intent
    /// </summary>
    Task<List<PatternCategory>> SuggestPatternCategoriesAsync(
        UserIntent intent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggest relevant best practices based on intent
    /// </summary>
    Task<List<string>> SuggestBestPracticesAsync(
        UserIntent intent,
        CancellationToken cancellationToken = default);
}

