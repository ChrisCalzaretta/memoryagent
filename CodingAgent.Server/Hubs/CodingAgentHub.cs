using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace CodingAgent.Server.Hubs;

/// <summary>
/// SignalR Hub for real-time bidirectional communication with clients
/// Enables: Live progress updates, Q&A during generation, interactive refinement
/// </summary>
public class CodingAgentHub : Hub
{
    private readonly ILogger<CodingAgentHub> _logger;
    private static readonly ConcurrentDictionary<string, string> _userConnections = new();
    
    public CodingAgentHub(ILogger<CodingAgentHub> logger)
    {
        _logger = logger;
    }
    
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("🔌 Client connected: {ConnectionId}", connectionId);
        
        // Send welcome message
        await Clients.Caller.SendAsync("Connected", new
        {
            ConnectionId = connectionId,
            Message = "Connected to CodingAgent",
            Timestamp = DateTime.UtcNow
        });
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("🔌 Client disconnected: {ConnectionId}", connectionId);
        
        // Clean up user connection mapping
        _userConnections.TryRemove(connectionId, out _);
        
        await base.OnDisconnectedAsync(exception);
    }
    
    // ═══════════════════════════════════════════════════════════
    // CLIENT → SERVER METHODS (Clients can call these)
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// Client answers a question posed by the agent
    /// </summary>
    public async Task AnswerQuestion(string questionId, string answer)
    {
        _logger.LogInformation("💬 Client answered question {QuestionId}: {Answer}", questionId, answer);
        
        // Store answer for retrieval by ConversationManager
        ConversationManager.RecordAnswer(Context.ConnectionId, questionId, answer);
        
        // Acknowledge
        await Clients.Caller.SendAsync("AnswerReceived", new
        {
            QuestionId = questionId,
            Answer = answer,
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// Client cancels a running job
    /// </summary>
    public async Task CancelJob(string jobId)
    {
        _logger.LogInformation("🛑 Client requested cancellation of job {JobId}", jobId);
        
        // Signal cancellation to JobManager
        ConversationManager.SignalCancellation(jobId);
        
        await Clients.Caller.SendAsync("CancellationRequested", new
        {
            JobId = jobId,
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// Client provides feedback/guidance mid-generation
    /// </summary>
    public async Task ProvideFeedback(string jobId, string feedback)
    {
        _logger.LogInformation("💬 Client provided feedback for job {JobId}: {Feedback}", jobId, feedback);
        
        ConversationManager.RecordFeedback(jobId, feedback);
        
        await Clients.Caller.SendAsync("FeedbackReceived", new
        {
            JobId = jobId,
            Feedback = feedback,
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// Client approves or rejects a generated file
    /// </summary>
    public async Task ReviewFile(string jobId, string fileName, bool approved, string? comments = null)
    {
        _logger.LogInformation("📝 Client reviewed file {FileName}: {Approved}", fileName, approved);
        
        ConversationManager.RecordFileReview(jobId, fileName, approved, comments);
        
        await Clients.Caller.SendAsync("FileReviewReceived", new
        {
            JobId = jobId,
            FileName = fileName,
            Approved = approved,
            Comments = comments,
            Timestamp = DateTime.UtcNow
        });
    }
    
    // ═══════════════════════════════════════════════════════════
    // SERVER → CLIENT METHODS (defined here for reference)
    // ═══════════════════════════════════════════════════════════
    // These are called via IHubContext<CodingAgentHub> from services:
    //
    // - JobStarted(jobId, task, language)
    // - ThinkingUpdate(jobId, message, data)
    // - ToolCallExecuted(jobId, toolName, arguments, result)
    // - QuestionAsked(questionId, question, options, category)
    // - FileGenerated(jobId, fileName, content, preview)
    // - CompilationStarted(jobId)
    // - CompilationResult(jobId, success, errors)
    // - ValidationResult(jobId, score, issues)
    // - ProgressUpdate(jobId, percentage, currentStep, totalSteps)
    // - JobCompleted(jobId, success, files, score)
    // - ErrorOccurred(jobId, error, suggestion)
}

/// <summary>
/// Static conversation manager to coordinate between Hub and JobManager
/// </summary>
public static class ConversationManager
{
    private static readonly ConcurrentDictionary<string, ConversationState> _conversations = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    
    public static ConversationState GetOrCreateConversation(string jobId, string connectionId)
    {
        return _conversations.GetOrAdd(jobId, _ => new ConversationState
        {
            JobId = jobId,
            ConnectionId = connectionId,
            StartedAt = DateTime.UtcNow
        });
    }
    
    public static void RecordAnswer(string connectionId, string questionId, string answer)
    {
        var conversation = _conversations.Values.FirstOrDefault(c => c.ConnectionId == connectionId);
        if (conversation != null)
        {
            conversation.Answers[questionId] = answer;
            conversation.LastAnswerReceived = DateTime.UtcNow;
        }
    }
    
    public static async Task<string> WaitForAnswerAsync(string jobId, string questionId, TimeSpan timeout)
    {
        if (!_conversations.TryGetValue(jobId, out var conversation))
            throw new InvalidOperationException($"No conversation found for job {jobId}");
        
        var endTime = DateTime.UtcNow.Add(timeout);
        
        while (DateTime.UtcNow < endTime)
        {
            if (conversation.Answers.TryGetValue(questionId, out var answer))
            {
                return answer;
            }
            
            await Task.Delay(100); // Poll every 100ms
        }
        
        throw new TimeoutException($"No answer received for question {questionId} within {timeout}");
    }
    
    public static void SignalCancellation(string jobId)
    {
        if (_cancellationTokens.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
        }
    }
    
    public static CancellationToken GetCancellationToken(string jobId)
    {
        var cts = _cancellationTokens.GetOrAdd(jobId, _ => new CancellationTokenSource());
        return cts.Token;
    }
    
    public static void RecordFeedback(string jobId, string feedback)
    {
        if (_conversations.TryGetValue(jobId, out var conversation))
        {
            conversation.UserFeedback.Add(new UserFeedback
            {
                Timestamp = DateTime.UtcNow,
                Message = feedback
            });
        }
    }
    
    public static void RecordFileReview(string jobId, string fileName, bool approved, string? comments)
    {
        if (_conversations.TryGetValue(jobId, out var conversation))
        {
            conversation.FileReviews[fileName] = new FileReview
            {
                Approved = approved,
                Comments = comments,
                ReviewedAt = DateTime.UtcNow
            };
        }
    }
    
    public static void Cleanup(string jobId)
    {
        _conversations.TryRemove(jobId, out _);
        if (_cancellationTokens.TryGetValue(jobId, out var cts))
        {
            cts.Dispose();
            _cancellationTokens.TryRemove(jobId, out _);
        }
    }
}

public class ConversationState
{
    public string JobId { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? LastAnswerReceived { get; set; }
    public ConcurrentDictionary<string, string> Answers { get; set; } = new();
    public List<UserFeedback> UserFeedback { get; set; } = new();
    public ConcurrentDictionary<string, FileReview> FileReviews { get; set; } = new();
}

public class UserFeedback
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
}

public class FileReview
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
    public DateTime ReviewedAt { get; set; }
}


