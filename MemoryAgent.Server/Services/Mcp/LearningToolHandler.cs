using MemoryAgent.Server.Models;
using System.Text.Json;

namespace MemoryAgent.Server.Services.Mcp;

/// <summary>
/// MCP Tool Handler for Agent Lightning learning features.
/// Provides tools for session tracking, Q&A learning, importance scoring, and co-edit analysis.
/// </summary>
public class LearningToolHandler : IMcpToolHandler
{
    private readonly ILearningService _learningService;
    private readonly ILogger<LearningToolHandler> _logger;

    public LearningToolHandler(
        ILearningService learningService,
        ILogger<LearningToolHandler> logger)
    {
        _learningService = learningService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            // Session Management
            new McpTool
            {
                Name = "start_session",
                Description = "Start a new learning session to track context. The Memory Agent will remember what files are discussed and edited during this session.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "end_session",
                Description = "End the current session with an optional summary",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sessionId = new { type = "string", description = "Session ID to end" },
                        summary = new { type = "string", description = "Optional summary of what was accomplished" }
                    },
                    required = new[] { "sessionId" }
                }
            },
            new McpTool
            {
                Name = "get_active_session",
                Description = "Get the currently active session for a context",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "record_file_discussed",
                Description = "Record that a file was discussed in the current session (helps Memory Agent learn what's important)",
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
                Description = "Record that a file was edited in the current session (helps Memory Agent learn co-edit patterns)",
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
            
            // Q&A Learning
            new McpTool
            {
                Name = "store_qa",
                Description = "Store a question-answer mapping for future instant recall. When similar questions are asked, the Memory Agent can immediately return relevant code.",
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
                Description = "Find previously asked questions similar to this one. Returns cached Q&A for instant answers.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        question = new { type = "string", description = "The question to find matches for" },
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Maximum number of results (default: 5)" }
                    },
                    required = new[] { "question", "context" }
                }
            },
            
            // Importance & Analytics
            new McpTool
            {
                Name = "get_important_files",
                Description = "Get the most important files in a project based on access patterns, edit frequency, and discussion history",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Maximum number of files (default: 20)" }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_coedited_files",
                Description = "Get files that are frequently edited together with a given file. Helps understand file relationships and dependencies.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "File to find co-edit partners for" },
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Maximum number of results (default: 10)" }
                    },
                    required = new[] { "filePath", "context" }
                }
            },
            new McpTool
            {
                Name = "get_file_clusters",
                Description = "Get clusters of files that are frequently edited together. Helps identify logical units/modules in the codebase.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_recent_sessions",
                Description = "Get recent sessions for a context. Shows what was discussed and edited in past sessions.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Maximum number of sessions (default: 10)" }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "detect_domains",
                Description = "Detect business domains (Auth, Billing, etc.) from file content for semantic organization",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the file" },
                        content = new { type = "string", description = "File content to analyze" }
                    },
                    required = new[] { "filePath", "content" }
                }
            },
            new McpTool
            {
                Name = "recalculate_importance",
                Description = "Recalculate importance scores for all files in a context (decays recency, updates rankings)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" }
                    },
                    required = new[] { "context" }
                }
            }
        };
    }

    public bool CanHandle(string toolName)
    {
        return toolName switch
        {
            "start_session" or "end_session" or "get_active_session" or
            "record_file_discussed" or "record_file_edited" or
            "store_qa" or "find_similar_questions" or
            "get_important_files" or "get_coedited_files" or "get_file_clusters" or
            "get_recent_sessions" or "detect_domains" or "recalculate_importance" => true,
            _ => false
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "start_session" => await StartSessionAsync(args, cancellationToken),
            "end_session" => await EndSessionAsync(args, cancellationToken),
            "get_active_session" => await GetActiveSessionAsync(args, cancellationToken),
            "record_file_discussed" => await RecordFileDiscussedAsync(args, cancellationToken),
            "record_file_edited" => await RecordFileEditedAsync(args, cancellationToken),
            "store_qa" => await StoreQAAsync(args, cancellationToken),
            "find_similar_questions" => await FindSimilarQuestionsAsync(args, cancellationToken),
            "get_important_files" => await GetImportantFilesAsync(args, cancellationToken),
            "get_coedited_files" => await GetCoEditedFilesAsync(args, cancellationToken),
            "get_file_clusters" => await GetFileClustersAsync(args, cancellationToken),
            "get_recent_sessions" => await GetRecentSessionsAsync(args, cancellationToken),
            "detect_domains" => await DetectDomainsAsync(args, cancellationToken),
            "recalculate_importance" => await RecalculateImportanceAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Session Management

    private async Task<McpToolResult> StartSessionAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var session = await _learningService.StartSessionAsync(context, cancellationToken);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"🆕 Session Started\n\nSession ID: {session.Id}\nContext: {session.Context}\nStarted: {session.StartedAt:u}\n\nUse this session ID to record file discussions and edits." }
            }
        };
    }

    private async Task<McpToolResult> EndSessionAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var sessionId = args?.GetValueOrDefault("sessionId")?.ToString();
        var summary = args?.GetValueOrDefault("summary")?.ToString();
        
        if (string.IsNullOrWhiteSpace(sessionId))
            return ErrorResult("sessionId is required");

        await _learningService.EndSessionAsync(sessionId, summary, cancellationToken);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✅ Session Ended\n\nSession ID: {sessionId}\nSummary: {summary ?? "(none)"}" }
            }
        };
    }

    private async Task<McpToolResult> GetActiveSessionAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var session = await _learningService.GetActiveSessionAsync(context, cancellationToken);
        
        if (session == null)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"No active session for context: {context}\n\nUse start_session to begin a new session." }
                }
            };
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"📋 Active Session\n\nSession ID: {session.Id}\nContext: {session.Context}\nStarted: {session.StartedAt:u}\nFiles Discussed: {session.FilesDiscussed.Count}\nFiles Edited: {session.FilesEdited.Count}\nQuestions Asked: {session.QuestionsAsked.Count}" }
            }
        };
    }

    private async Task<McpToolResult> RecordFileDiscussedAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var sessionId = args?.GetValueOrDefault("sessionId")?.ToString();
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(filePath))
            return ErrorResult("sessionId and filePath are required");

        await _learningService.RecordFileDiscussedAsync(sessionId, filePath, cancellationToken);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"📝 Recorded: {filePath} was discussed in session {sessionId}" }
            }
        };
    }

    private async Task<McpToolResult> RecordFileEditedAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var sessionId = args?.GetValueOrDefault("sessionId")?.ToString();
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(filePath))
            return ErrorResult("sessionId and filePath are required");

        await _learningService.RecordFileEditedAsync(sessionId, filePath, cancellationToken);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"✏️ Recorded: {filePath} was edited in session {sessionId}" }
            }
        };
    }

    #endregion

    #region Q&A Learning

    private async Task<McpToolResult> StoreQAAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var question = args?.GetValueOrDefault("question")?.ToString();
        var answer = args?.GetValueOrDefault("answer")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var relevantFilesObj = args?.GetValueOrDefault("relevantFiles");
        
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("question, answer, and context are required");

        var relevantFiles = ParseStringList(relevantFilesObj);

        await _learningService.StoreQuestionMappingAsync(question, answer, relevantFiles, context, cancellationToken);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"💾 Stored Q&A\n\nQuestion: {question}\nRelevant Files: {relevantFiles.Count}\n\nThe Memory Agent will remember this for instant recall!" }
            }
        };
    }

    private async Task<McpToolResult> FindSimilarQuestionsAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var question = args?.GetValueOrDefault("question")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = GetIntArg(args, "limit", 5);
        
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("question and context are required");

        var similar = await _learningService.FindSimilarQuestionsAsync(question, context, limit, cancellationToken);
        
        if (!similar.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"No similar questions found for: {question}" }
                }
            };
        }

        var output = $"🔍 Similar Questions Found: {similar.Count}\n\n";
        foreach (var qa in similar)
        {
            output += $"❓ {qa.Question}\n";
            output += $"   Asked {qa.TimesAsked}x | Last: {qa.LastAskedAt:d}\n";
            output += $"   Files: {string.Join(", ", qa.RelevantFiles.Take(3))}\n\n";
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Importance & Analytics

    private async Task<McpToolResult> GetImportantFilesAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = GetIntArg(args, "limit", 20);
        
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var metrics = await _learningService.GetMostImportantFilesAsync(context, limit, cancellationToken);
        
        if (!metrics.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"No importance metrics found for context: {context}\n\nStart using the Memory Agent to build importance data." }
                }
            };
        }

        var output = $"⭐ Most Important Files in {context}\n\n";
        var rank = 1;
        foreach (var m in metrics)
        {
            var stars = m.ImportanceScore switch
            {
                >= 0.8f => "⭐⭐⭐",
                >= 0.5f => "⭐⭐",
                _ => "⭐"
            };
            output += $"{rank}. {stars} {Path.GetFileName(m.FilePath)}\n";
            output += $"   Importance: {m.ImportanceScore:P0} | Access: {m.AccessCount} | Edits: {m.EditCount}\n";
            output += $"   Path: {m.FilePath}\n\n";
            rank++;
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> GetCoEditedFilesAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = GetIntArg(args, "limit", 10);
        
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("filePath and context are required");

        var coEdits = await _learningService.GetCoEditedFilesAsync(filePath, context, limit, cancellationToken);
        
        if (!coEdits.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"No co-edit patterns found for: {filePath}\n\nCo-edit tracking builds over time as files are edited together." }
                }
            };
        }

        var output = $"🔗 Files Often Edited With {Path.GetFileName(filePath)}\n\n";
        foreach (var co in coEdits)
        {
            var strength = co.CoEditStrength switch
            {
                >= 0.7f => "🔴 Strong",
                >= 0.4f => "🟡 Medium",
                _ => "🟢 Light"
            };
            output += $"• {Path.GetFileName(co.FilePath2)}\n";
            output += $"  {strength} ({co.CoEditCount} co-edits)\n";
            output += $"  Path: {co.FilePath2}\n\n";
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> GetFileClustersAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var clusters = await _learningService.GetFileClusterssAsync(context, cancellationToken);
        
        if (!clusters.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"No file clusters found for context: {context}\n\nClusters emerge as files are consistently edited together (3+ times)." }
                }
            };
        }

        var output = $"📦 File Clusters in {context}\n\n";
        var clusterNum = 1;
        foreach (var cluster in clusters)
        {
            output += $"Cluster #{clusterNum} ({cluster.Count} files):\n";
            foreach (var file in cluster)
            {
                output += $"  • {Path.GetFileName(file)}\n";
            }
            output += "\n";
            clusterNum++;
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> GetRecentSessionsAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = GetIntArg(args, "limit", 10);
        
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var sessions = await _learningService.GetRecentSessionsAsync(context, limit, cancellationToken);
        
        if (!sessions.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"No sessions found for context: {context}" }
                }
            };
        }

        var output = $"📜 Recent Sessions in {context}\n\n";
        foreach (var s in sessions)
        {
            var status = s.EndedAt.HasValue ? "✅ Completed" : "🟢 Active";
            output += $"{status} Session {s.Id}\n";
            output += $"  Started: {s.StartedAt:g}\n";
            if (s.EndedAt.HasValue)
                output += $"  Ended: {s.EndedAt:g}\n";
            output += $"  Files Discussed: {s.FilesDiscussed.Count} | Edited: {s.FilesEdited.Count}\n";
            if (!string.IsNullOrWhiteSpace(s.Summary))
                output += $"  Summary: {s.Summary}\n";
            output += "\n";
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> DetectDomainsAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        var content = args?.GetValueOrDefault("content")?.ToString();
        
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(content))
            return ErrorResult("filePath and content are required");

        var domains = await _learningService.DetectDomainsAsync(filePath, content, cancellationToken);
        
        if (!domains.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new() { Type = "text", Text = $"No business domains detected for: {filePath}" }
                }
            };
        }

        var output = $"🏷️ Detected Domains for {Path.GetFileName(filePath)}\n\n";
        foreach (var d in domains)
        {
            var confidence = d.Confidence switch
            {
                >= 0.7f => "High",
                >= 0.4f => "Medium",
                _ => "Low"
            };
            output += $"• {d.Name} ({confidence} confidence: {d.Confidence:P0})\n";
            output += $"  Keywords: {string.Join(", ", d.Keywords.Take(5))}\n\n";
        }
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    private async Task<McpToolResult> RecalculateImportanceAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        await _learningService.RecalculateImportanceScoresAsync(context, cancellationToken);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"📊 Importance scores recalculated for context: {context}\n\nRecency scores have been decayed based on time since last access." }
            }
        };
    }

    #endregion

    #region Helper Methods

    private static McpToolResult ErrorResult(string message)
    {
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = $"Error: {message}" }
            },
            IsError = true
        };
    }

    private static int GetIntArg(Dictionary<string, object>? args, string key, int defaultValue)
    {
        if (args?.TryGetValue(key, out var value) == true)
        {
            if (value is int i) return i;
            if (value is long l) return (int)l;
            if (value is JsonElement je && je.TryGetInt32(out var ji)) return ji;
            if (int.TryParse(value?.ToString(), out var pi)) return pi;
        }
        return defaultValue;
    }

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

    #endregion
}

