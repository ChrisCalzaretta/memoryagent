using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Lightweight test double for IPromptService used in integration tests.
/// Provides deterministic stub data and avoids external dependencies (Neo4j/LLM).
/// </summary>
public class TestPromptService : IPromptService
{
    public Task<PromptTemplate> GetPromptAsync(string name, bool allowTestVariant = true, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PromptTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Version = 1,
            Content = "Test prompt content",
            Description = "Test prompt",
            IsActive = true,
            Variables = new List<PromptVariable>()
        });

    public Task<PromptTemplate?> GetPromptVersionAsync(string name, int version, CancellationToken cancellationToken = default) =>
        Task.FromResult<PromptTemplate?>(null);

    public Task<List<PromptTemplate>> GetPromptHistoryAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<PromptTemplate>());

    public Task<List<PromptTemplate>> ListPromptsAsync(bool activeOnly = true, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<PromptTemplate>());

    public Task<string> RenderPromptAsync(string name, Dictionary<string, string> variables, CancellationToken cancellationToken = default) =>
        Task.FromResult("Rendered test prompt");

    public Task<PromptTemplate> CreatePromptAsync(string name, string content, string description, List<PromptVariable>? variables = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PromptTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Version = 1,
            Content = content,
            Description = description,
            IsActive = true,
            Variables = variables ?? new List<PromptVariable>()
        });

    public Task<PromptTemplate> CreateVersionAsync(string name, string content, string evolutionReason, bool activateImmediately = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PromptTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Version = 2,
            Content = content,
            Description = evolutionReason,
            IsActive = activateImmediately
        });

    public Task ActivateVersionAsync(string name, int version, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartABTestAsync(string name, int testVersion, int trafficPercent = 10, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopABTestAsync(string name, bool promoteTestVersion = false, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PromptExecution> RecordExecutionAsync(
        string promptId,
        string renderedPrompt,
        Dictionary<string, string> inputVariables,
        string response,
        long responseTimeMs,
        float? confidence = null,
        bool parseSuccess = true,
        string? parseError = null,
        string? sessionId = null,
        string? context = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PromptExecution
        {
            Id = Guid.NewGuid().ToString("N"),
            PromptId = promptId,
            RenderedPrompt = renderedPrompt,
            InputVariables = inputVariables,
            Response = response,
            ResponseTimeMs = responseTimeMs,
            Confidence = confidence,
            ParseSuccess = parseSuccess,
            ParseError = parseError,
            SessionId = sessionId,
            Context = context,
            ExecutedAt = DateTime.UtcNow
        });

    public Task RecordOutcomeAsync(string executionId, bool wasSuccessful, int? userRating = null, string? comments = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RecordImplicitSuccessAsync(string executionId, string signal, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RecordImplicitFailureAsync(string executionId, string signal, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<List<PromptExecution>> GetRecentExecutionsAsync(string promptName, int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<PromptExecution>());

    public Task<PromptMetrics> GetPromptMetricsAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PromptMetrics { PromptName = name });

    public Task<ABTestResult> GetABTestResultsAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ABTestResult { PromptName = name });

    public Task<List<PromptImprovement>> SuggestImprovementsAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<PromptImprovement>());

    public Task<PromptTemplate?> AutoEvolveAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<PromptTemplate?>(null);

    public Task InitializeDefaultPromptsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

