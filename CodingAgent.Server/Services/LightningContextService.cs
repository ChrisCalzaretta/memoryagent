using AgentContracts.Models;
using AgentContracts.Responses;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Orchestrates AI Lightning integration - session management, Q&A learning, pattern recognition
/// Makes the system LEARN and get SMARTER over time
/// </summary>
public interface ILightningContextService
{
    Task<LightningContext> InitializeSessionAsync(string workspacePath, CancellationToken cancellationToken = default);
    Task<List<QAPair>> CheckSimilarTasksAsync(string task, CancellationToken cancellationToken = default);
    Task RecordFileGenerationsAsync(List<FileChange> files, string context, CancellationToken cancellationToken = default);
    Task RecordSuccessfulGenerationAsync(string task, List<FileChange> files, int score, string language, Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);
    Task<WorkspaceStatus> GetWorkspaceStatusAsync(string? context = null, CancellationToken cancellationToken = default);
    Task<List<string>> GetRecommendationsAsync(string? context = null, CancellationToken cancellationToken = default);
}

public class LightningContextService : ILightningContextService
{
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ILogger<LightningContextService> _logger;
    private LightningContext? _currentContext;
    
    public LightningContextService(
        IMemoryAgentClient memoryAgent,
        ILogger<LightningContextService> logger)
    {
        _memoryAgent = memoryAgent;
        _logger = logger;
    }
    
    public async Task<LightningContext> InitializeSessionAsync(string workspacePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🧠 Initializing Lightning session for workspace: {Path}", workspacePath);
            
            // Get context from Lightning (starts session automatically)
            _currentContext = await _memoryAgent.GetContextAsync(workspacePath, cancellationToken);
            
            // Get current workspace status
            var status = await _memoryAgent.GetWorkspaceStatusAsync(_currentContext.ContextName, cancellationToken);
            
            _logger.LogInformation("✅ Lightning session initialized: {Context}", _currentContext.ContextName);
            _logger.LogInformation("📊 Workspace status: {Files} files indexed, {Recent} recent files",
                status.TotalFilesIndexed, status.RecentFiles.Count);
            
            if (_currentContext.DiscussedFiles.Any())
            {
                _logger.LogInformation("💡 Found {Count} previously discussed files in this context",
                    _currentContext.DiscussedFiles.Count);
            }
            
            return _currentContext;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize Lightning session");
            
            // Return minimal context
            _currentContext = new LightningContext
            {
                ContextName = Path.GetFileName(workspacePath.TrimEnd('/', '\\')),
                WorkspacePath = workspacePath,
                SessionStarted = DateTime.UtcNow
            };
            
            return _currentContext;
        }
    }
    
    public async Task<List<QAPair>> CheckSimilarTasksAsync(string task, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Checking Lightning for similar tasks: {Task}",
                task.Length > 50 ? task.Substring(0, 50) + "..." : task);
            
            // Use Lightning's semantic search via Qdrant
            var similarQuestions = await _memoryAgent.FindSimilarQuestionsAsync(task, limit: 5, cancellationToken);
            
            if (similarQuestions.Any())
            {
                _logger.LogInformation("💡 Found {Count} similar tasks in Lightning history!", similarQuestions.Count);
                
                var highScorers = similarQuestions.Where(q => q.Score >= 8).ToList();
                if (highScorers.Any())
                {
                    _logger.LogInformation("⭐ {Count} of them have high scores (>= 8/10) - can reuse patterns!",
                        highScorers.Count);
                }
                
                return similarQuestions;
            }
            else
            {
                _logger.LogInformation("🆕 No similar tasks found - this is a new challenge!");
                return new List<QAPair>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to check similar tasks");
            return new List<QAPair>();
        }
    }
    
    public async Task RecordFileGenerationsAsync(List<FileChange> files, string context, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📝 Recording {Count} file generations in Lightning session", files.Count);
            
            var tasks = files.Select(file => 
                _memoryAgent.RecordFileEditedAsync(file.Path, context, cancellationToken)
            );
            
            await Task.WhenAll(tasks);
            
            _logger.LogDebug("✅ File generations recorded in Lightning");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to record file generations (non-fatal)");
        }
    }
    
    public async Task RecordSuccessfulGenerationAsync(
        string task,
        List<FileChange> files,
        int score,
        string language,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("💾 Recording successful generation in Lightning: Score {Score}/10, {Files} files",
                score, files.Count);
            
            // Prepare answer (serialize files)
            var answer = System.Text.Json.JsonSerializer.Serialize(new
            {
                files = files.Select(f => new { path = f.Path, size = f.Content.Length }).ToList(),
                file_count = files.Count,
                total_size = files.Sum(f => f.Content.Length)
            });
            
            // Enhance metadata
            var enhancedMetadata = metadata ?? new Dictionary<string, object>();
            enhancedMetadata["workspace"] = _currentContext?.WorkspacePath ?? "unknown";
            enhancedMetadata["context"] = _currentContext?.ContextName ?? "unknown";
            enhancedMetadata["files_generated"] = files.Select(f => f.Path).ToList();
            enhancedMetadata["timestamp"] = DateTime.UtcNow;
            
            // Store in Lightning (Qdrant + Neo4j)
            await _memoryAgent.StoreQAAsync(
                question: task,
                answer: answer,
                score: score,
                language: language,
                metadata: enhancedMetadata,
                cancellationToken: cancellationToken
            );
            
            _logger.LogInformation("✅ Successful generation recorded - Lightning learned from this!");
            
            // Also record individual file edits
            await RecordFileGenerationsAsync(files, _currentContext?.ContextName ?? "codegen", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to record successful generation (this is BAD - no learning!)");
        }
    }
    
    public async Task<WorkspaceStatus> GetWorkspaceStatusAsync(string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var contextName = context ?? _currentContext?.ContextName;
            
            _logger.LogDebug("📊 Getting workspace status from Lightning");
            
            var status = await _memoryAgent.GetWorkspaceStatusAsync(contextName, cancellationToken);
            
            if (status.TotalFilesIndexed > 0)
            {
                _logger.LogInformation("📊 Workspace has {Files} indexed files, {Recent} recent",
                    status.TotalFilesIndexed, status.RecentFiles.Count);
            }
            
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to get workspace status");
            return new WorkspaceStatus();
        }
    }
    
    public async Task<List<string>> GetRecommendationsAsync(string? context = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var contextName = context ?? _currentContext?.ContextName;
            
            _logger.LogDebug("💡 Getting Lightning recommendations");
            
            var recommendations = await _memoryAgent.GetRecommendationsAsync(contextName, cancellationToken);
            
            if (recommendations.Any())
            {
                _logger.LogInformation("💡 Lightning has {Count} recommendations", recommendations.Count);
            }
            
            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to get recommendations");
            return new List<string>();
        }
    }
}


