using Microsoft.AspNetCore.SignalR;
using CodingAgent.Server.Hubs;
using AgentContracts.Responses;

namespace CodingAgent.Server.Services;

/// <summary>
/// Service for managing real-time conversations with clients via SignalR
/// Abstracts WebSocket complexity from JobManager
/// </summary>
public interface IConversationService
{
    Task<ConversationSession> StartConversationAsync(string jobId, string? connectionId);
    Task SendThinkingUpdateAsync(string jobId, string message, object? data = null);
    Task SendToolCallAsync(string jobId, string toolName, object? arguments, object? result);
    Task<string> AskQuestionAsync(string jobId, string question, List<string>? options = null, string? category = null, TimeSpan? timeout = null);
    Task SendFileGeneratedAsync(string jobId, string fileName, string? preview = null);
    Task SendCompilationResultAsync(string jobId, bool success, List<string>? errors = null);
    Task SendValidationResultAsync(string jobId, int score, List<ValidationIssue>? issues = null);
    Task SendProgressUpdateAsync(string jobId, int percentage, string currentStep, int totalSteps);
    Task SendJobCompletedAsync(string jobId, bool success, List<string> files, int score);
    Task SendErrorAsync(string jobId, string error, string? suggestion = null);
    bool IsConversationActive(string jobId);
}

public class ConversationService : IConversationService
{
    private readonly IHubContext<CodingAgentHub> _hubContext;
    private readonly ILogger<ConversationService> _logger;
    
    public ConversationService(
        IHubContext<CodingAgentHub> hubContext,
        ILogger<ConversationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }
    
    public async Task<ConversationSession> StartConversationAsync(string jobId, string? connectionId)
    {
        _logger.LogInformation("💬 Starting conversation for job {JobId}", jobId);
        
        var session = new ConversationSession
        {
            JobId = jobId,
            ConnectionId = connectionId,
            StartedAt = DateTime.UtcNow,
            IsActive = !string.IsNullOrEmpty(connectionId)
        };
        
        if (!string.IsNullOrEmpty(connectionId))
        {
            // Initialize conversation state
            ConversationManager.GetOrCreateConversation(jobId, connectionId);
            
            // Notify client that job has started
            await _hubContext.Clients.Client(connectionId).SendAsync("JobStarted", new
            {
                JobId = jobId,
                Timestamp = DateTime.UtcNow
            });
        }
        
        return session;
    }
    
    public async Task SendThinkingUpdateAsync(string jobId, string message, object? data = null)
    {
        _logger.LogDebug("💭 Thinking update for {JobId}: {Message}", jobId, message);
        
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("ThinkingUpdate", new
            {
                JobId = jobId,
                Message = message,
                Data = data,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public async Task SendToolCallAsync(string jobId, string toolName, object? arguments, object? result)
    {
        _logger.LogDebug("🔧 Tool call for {JobId}: {Tool}", jobId, toolName);
        
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("ToolCallExecuted", new
            {
                JobId = jobId,
                ToolName = toolName,
                Arguments = arguments,
                Result = result,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public async Task<string> AskQuestionAsync(
        string jobId, 
        string question, 
        List<string>? options = null, 
        string? category = null,
        TimeSpan? timeout = null)
    {
        var conversation = TryGetConversation(jobId);
        if (conversation == null)
        {
            // No active conversation - use default
            _logger.LogWarning("⚠️ No active conversation for {JobId}, using first option as default", jobId);
            return options?.FirstOrDefault() ?? "default";
        }
        
        var questionId = Guid.NewGuid().ToString("N");
        timeout ??= TimeSpan.FromMinutes(5); // Default 5-minute timeout
        
        _logger.LogInformation("❓ Asking question {QuestionId} for job {JobId}: {Question}", questionId, jobId, question);
        
        // Send question to client
        await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("QuestionAsked", new
        {
            QuestionId = questionId,
            JobId = jobId,
            Question = question,
            Options = options,
            Category = category,
            Timestamp = DateTime.UtcNow
        });
        
        try
        {
            // Wait for answer
            var answer = await ConversationManager.WaitForAnswerAsync(jobId, questionId, timeout.Value);
            
            _logger.LogInformation("✅ Received answer for {QuestionId}: {Answer}", questionId, answer);
            return answer;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("⏱️ Question {QuestionId} timed out, using first option", questionId);
            return options?.FirstOrDefault() ?? "default";
        }
    }
    
    public async Task SendFileGeneratedAsync(string jobId, string fileName, string? preview = null)
    {
        _logger.LogDebug("📄 File generated for {JobId}: {FileName}", jobId, fileName);
        
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("FileGenerated", new
            {
                JobId = jobId,
                FileName = fileName,
                Preview = preview,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public async Task SendCompilationResultAsync(string jobId, bool success, List<string>? errors = null)
    {
        _logger.LogDebug("🔨 Compilation result for {JobId}: {Success}", jobId, success);
        
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("CompilationResult", new
            {
                JobId = jobId,
                Success = success,
                Errors = errors,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public async Task SendValidationResultAsync(string jobId, int score, List<ValidationIssue>? issues = null)
    {
        _logger.LogDebug("📊 Validation result for {JobId}: {Score}/10", jobId, score);
        
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("ValidationResult", new
            {
                JobId = jobId,
                Score = score,
                Issues = issues,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public async Task SendProgressUpdateAsync(string jobId, int percentage, string currentStep, int totalSteps)
    {
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("ProgressUpdate", new
            {
                JobId = jobId,
                Percentage = percentage,
                CurrentStep = currentStep,
                TotalSteps = totalSteps,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public async Task SendJobCompletedAsync(string jobId, bool success, List<string> files, int score)
    {
        _logger.LogInformation("✅ Job completed: {JobId}, Success: {Success}, Score: {Score}/10", jobId, success, score);
        
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("JobCompleted", new
            {
                JobId = jobId,
                Success = success,
                Files = files,
                Score = score,
                Timestamp = DateTime.UtcNow
            });
        }
        
        // Cleanup conversation state
        ConversationManager.Cleanup(jobId);
    }
    
    public async Task SendErrorAsync(string jobId, string error, string? suggestion = null)
    {
        _logger.LogError("❌ Error for job {JobId}: {Error}", jobId, error);
        
        var conversation = TryGetConversation(jobId);
        if (conversation != null)
        {
            await _hubContext.Clients.Client(conversation.ConnectionId).SendAsync("ErrorOccurred", new
            {
                JobId = jobId,
                Error = error,
                Suggestion = suggestion,
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public bool IsConversationActive(string jobId)
    {
        return TryGetConversation(jobId) != null;
    }
    
    private ConversationState? TryGetConversation(string jobId)
    {
        // This is a simplified lookup - in production you'd query the static manager
        return null; // Will be properly implemented when integrated
    }
}

public class ConversationSession
{
    public string JobId { get; set; } = "";
    public string? ConnectionId { get; set; }
    public DateTime StartedAt { get; set; }
    public bool IsActive { get; set; }
}


