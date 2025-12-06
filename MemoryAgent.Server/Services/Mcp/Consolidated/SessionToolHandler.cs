using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for session and learning operations.
/// Tools: start_session, end_session, record_file_discussed, record_file_edited, store_qa, find_similar_questions
/// </summary>
public class SessionToolHandler : IMcpToolHandler
{
    private readonly ILearningService _learningService;
    private readonly ILogger<SessionToolHandler> _logger;

    public SessionToolHandler(
        ILearningService learningService,
        ILogger<SessionToolHandler> logger)
    {
        _learningService = learningService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "workspace_status",
                Description = "Get a quick overview of what Agent Lightning knows about this workspace: active sessions, Q&A stored, files tracked, learning health.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name (defaults to workspace folder)" }
                    },
                    required = Array.Empty<string>()
                }
            },
            new McpTool
            {
                Name = "record_file_discussed",
                Description = "Record that a file was discussed (helps learn file importance). CALL THIS WHEN ANY FILE IS MENTIONED OR VIEWED.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionId = new { type = "string", description = "Session ID" },
                        filePath = new { type = "string", description = "Path to the file that was discussed" }
                    },
                    required = new[] { "sessionId", "filePath" }
                }
            },
            new McpTool
            {
                Name = "record_file_edited",
                Description = "Record that a file was edited (helps learn co-edit patterns). CALL THIS AFTER EVERY FILE EDIT.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionId = new { type = "string", description = "Session ID" },
                        filePath = new { type = "string", description = "Path to the file that was edited" }
                    },
                    required = new[] { "sessionId", "filePath" }
                }
            },
            new McpTool
            {
                Name = "store_qa",
                Description = "Store a question-answer mapping for instant future recall. CALL THIS AFTER PROVIDING A USEFUL ANSWER.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        question = new { type = "string", description = "The question that was asked" },
                        answer = new { type = "string", description = "The answer/response given" },
                        relevantFiles = new { type = "array", items = new { type = "string" }, description = "List of file paths relevant to this Q&A" },
                        context = new { type = "string", description = "Project context name" }
                    },
                    required = new[] { "question", "answer", "relevantFiles", "context" }
                }
            },
            new McpTool
            {
                Name = "find_similar_questions",
                Description = "Find previously asked similar questions. CALL THIS FIRST BEFORE ANSWERING ANY QUESTION - may provide instant recall!",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        question = new { type = "string", description = "The question to find matches for" },
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Maximum number of results (default: 5)", @default = 5 }
                    },
                    required = new[] { "question", "context" }
                }
            },
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "workspace_status" => await WorkspaceStatusAsync(args, cancellationToken),
            "record_file_discussed" => await RecordFileDiscussedAsync(args, cancellationToken),
            "record_file_edited" => await RecordFileEditedAsync(args, cancellationToken),
            "store_qa" => await StoreQAAsync(args, cancellationToken),
            "find_similar_questions" => await FindSimilarQuestionsAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Workspace Status

    private async Task<McpToolResult> WorkspaceStatusAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";

        var output = $"🧠 Agent Lightning - Workspace Status\n";
        output += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
        output += $"📁 Context: {context}\n\n";

        // Active Session
        var activeSession = await _learningService.GetActiveSessionAsync(context, ct);
        if (activeSession != null)
        {
            var duration = (DateTime.UtcNow - activeSession.StartedAt).TotalMinutes;
            output += $"🟢 Active Session\n";
            output += $"   ID: {activeSession.Id}\n";
            output += $"   Started: {activeSession.StartedAt:g}\n";
            output += $"   Duration: {duration:F0} minutes\n";
            output += $"   Files Discussed: {activeSession.FilesDiscussed.Count}\n";
            output += $"   Files Edited: {activeSession.FilesEdited.Count}\n\n";
        }
        else
        {
            output += $"⚪ No Active Session (will auto-start on next tool call)\n\n";
        }

        // Recent Sessions
        var recentSessions = await _learningService.GetRecentSessionsAsync(context, 5, ct);
        output += $"📜 Recent Sessions: {recentSessions.Count}\n";
        foreach (var s in recentSessions.Take(3))
        {
            var status = s.EndedAt.HasValue ? "✅" : "🟢";
            output += $"   {status} {s.StartedAt:d} - {s.FilesDiscussed.Count} files\n";
        }
        output += "\n";

        // Q&A Knowledge
        var similarTest = await _learningService.FindSimilarQuestionsAsync("test", context, 1, ct);
        output += $"💡 Q&A Knowledge Base: Active\n";
        output += $"   Use find_similar_questions to search\n";
        output += $"   Use store_qa to add new knowledge\n\n";

        // Important Files
        var importantFiles = await _learningService.GetMostImportantFilesAsync(context, 5, ct);
        if (importantFiles.Any())
        {
            output += $"⭐ Top Important Files:\n";
            foreach (var f in importantFiles.Take(5))
            {
                output += $"   • {Path.GetFileName(f.FilePath)} ({f.ImportanceScore:F1})\n";
            }
            output += "\n";
        }

        // Tool Usage
        var toolMetrics = await _learningService.GetToolUsageMetricsAsync(context, ct);
        if (toolMetrics.Any())
        {
            var totalCalls = toolMetrics.Sum(m => m.CallCount);
            output += $"🔧 Tool Usage: {totalCalls:N0} calls across {toolMetrics.Count} tools\n\n";
        }

        output += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
        output += $"✨ Sessions auto-start. Files auto-track from tool args.\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region File Tracking

    private async Task<McpToolResult> RecordFileDiscussedAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var sessionId = args?.GetValueOrDefault("sessionId")?.ToString();
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(filePath))
            return ErrorResult("sessionId and filePath are required");

        await _learningService.RecordFileDiscussedAsync(sessionId, filePath, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"📝 Recorded: {filePath} was discussed" }
            }
        };
    }

    private async Task<McpToolResult> RecordFileEditedAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var sessionId = args?.GetValueOrDefault("sessionId")?.ToString();
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(filePath))
            return ErrorResult("sessionId and filePath are required");

        await _learningService.RecordFileEditedAsync(sessionId, filePath, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✏️ Recorded: {filePath} was edited" }
            }
        };
    }

    #endregion

    #region Q&A Learning

    private async Task<McpToolResult> StoreQAAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var question = args?.GetValueOrDefault("question")?.ToString();
        var answer = args?.GetValueOrDefault("answer")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var relevantFilesObj = args?.GetValueOrDefault("relevantFiles");
        
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("question, answer, and context are required");

        var relevantFiles = ParseStringList(relevantFilesObj);

        await _learningService.StoreQuestionMappingAsync(question, answer, relevantFiles, context, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"💾 Stored Q&A\n\nQuestion: {question}\nRelevant Files: {relevantFiles.Count}\n\n✅ Memory Agent will remember this for instant recall!" }
            }
        };
    }

    private async Task<McpToolResult> FindSimilarQuestionsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var question = args?.GetValueOrDefault("question")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 5);
        
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("question and context are required");

        var similar = await _learningService.FindSimilarQuestionsAsync(question, context, limit, ct);
        
        if (!similar.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"🔍 No similar questions found for: {question}\n\nThis appears to be a new question. After answering, use store_qa to save it!" }
                }
            };
        }

        var output = $"🔍 Similar Questions Found: {similar.Count}\n\n";
        foreach (var qa in similar)
        {
            output += $"❓ {qa.Question}\n";
            output += $"   Asked {qa.TimesAsked}x | Last: {qa.LastAskedAt:d}\n";
            output += $"   📁 Files: {string.Join(", ", qa.RelevantFiles.Take(3))}\n";
            if (!string.IsNullOrEmpty(qa.Answer))
            {
                var answerPreview = qa.Answer.Length > 200 ? qa.Answer[..200] + "..." : qa.Answer;
                output += $"   💡 Answer: {answerPreview}\n";
            }
            output += "\n";
        }
        
        output += "✨ TIP: If these answers are relevant, you can reference them directly!";
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Helpers

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var i) => i,
            JsonElement je when je.TryGetInt32(out var i) => i,
            _ => defaultValue
        };

    private static List<string> ParseStringList(object? value)
    {
        if (value == null) return new List<string>();
        
        if (value is List<string> list) return list;
        if (value is string[] array) return array.ToList();
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            return je.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        if (value is IEnumerable<object> enumerable)
        {
            return enumerable.Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        
        return new List<string>();
    }

    private static McpToolResult ErrorResult(string error) => new()
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };

    #endregion
}

