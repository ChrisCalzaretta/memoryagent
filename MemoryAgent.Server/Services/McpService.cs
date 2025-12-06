using System.Diagnostics;
using System.Text.Json;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services.Mcp;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Orchestrator for all MCP tools - delegates to specialized handlers
/// Refactored from 2979 lines to ~150 lines using Strategy Pattern
/// Now includes Agent Lightning tool usage tracking
/// </summary>
public class McpService : IMcpService
{
    private readonly IEnumerable<IMcpToolHandler> _handlers;
    private readonly ILearningService _learningService;
    private readonly ILogger<McpService> _logger;
    private readonly Dictionary<string, IMcpToolHandler> _toolHandlerMap;
    
    // Tools that should NOT be tracked to avoid infinite loops
    private static readonly HashSet<string> _excludedFromTracking = new()
    {
        "get_tool_usage",
        "get_popular_tools", 
        "get_recent_tool_invocations",
        "get_tool_patterns",
        "get_insights" // Can call itself via category='tools'
    };
    
    // Tools that manage sessions themselves - don't auto-start session for these
    private static readonly HashSet<string> _sessionManagementTools = new()
    {
        "workspace_status",
        "register_workspace",
        "unregister_workspace"
    };
    
    // Cache of active sessions per context to avoid repeated lookups
    private readonly Dictionary<string, string> _activeSessionCache = new();

    public McpService(
        IEnumerable<IMcpToolHandler> handlers,
        ILearningService learningService,
        ILogger<McpService> logger)
    {
        _handlers = handlers;
        _learningService = learningService;
        _logger = logger;

        // Build tool→handler mapping for O(1) lookup
        _toolHandlerMap = new Dictionary<string, IMcpToolHandler>();
        foreach (var handler in handlers)
        {
            foreach (var tool in handler.GetTools())
            {
                _toolHandlerMap[tool.Name] = handler;
            }
        }

        _logger.LogInformation("🎯 MCP Service initialized with {HandlerCount} handlers and {ToolCount} tools (with Agent Lightning tracking)",
            handlers.Count(), _toolHandlerMap.Count);
    }

    public async Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔧 MCP Tool Call: {ToolName}", toolCall.Name);
        var stopwatch = Stopwatch.StartNew();

        if (!_toolHandlerMap.TryGetValue(toolCall.Name, out var handler))
        {
            _logger.LogWarning("❌ Unknown MCP tool: {ToolName}", toolCall.Name);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error: Unknown tool '{toolCall.Name}'\n\n" +
                               $"Available tools:\n{string.Join("\n", _toolHandlerMap.Keys.OrderBy(k => k).Select(k => $"  • {k}"))}"
                    }
                }
            };
        }

        try
        {
            // AUTO-SESSION: Ensure a session exists for tools that need context
            var sessionId = await EnsureSessionExistsAsync(toolCall, cancellationToken);
            
            // AUTO-RECORD: Track files mentioned in tool arguments
            await AutoRecordFilesFromArgumentsAsync(sessionId, toolCall.Arguments, cancellationToken);
            
            var result = await handler.HandleToolAsync(toolCall.Name, toolCall.Arguments, cancellationToken);
            stopwatch.Stop();
            
            _logger.LogInformation("✅ MCP Tool '{ToolName}' completed {Status} in {Duration}ms",
                toolCall.Name, result.IsError ? "with errors" : "successfully", stopwatch.ElapsedMilliseconds);
            
            // Track tool usage with Agent Lightning (except for tracking tools themselves)
            if (!_excludedFromTracking.Contains(toolCall.Name))
            {
                await TrackToolInvocationAsync(toolCall, result, stopwatch.ElapsedMilliseconds, cancellationToken);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "❌ MCP Tool '{ToolName}' failed with exception in {Duration}ms", 
                toolCall.Name, stopwatch.ElapsedMilliseconds);
            
            // Track failed invocation
            if (!_excludedFromTracking.Contains(toolCall.Name))
            {
                await TrackToolInvocationAsync(toolCall, null, stopwatch.ElapsedMilliseconds, cancellationToken, ex.Message);
            }
            
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error executing tool '{toolCall.Name}': {ex.Message}\n\n{ex.StackTrace}"
                    }
                }
            };
        }
    }
    
    private async Task TrackToolInvocationAsync(
        McpToolCall toolCall, 
        McpToolResult? result, 
        long durationMs, 
        CancellationToken cancellationToken,
        string? errorMessage = null)
    {
        try
        {
            // Extract context and query from arguments
            var context = toolCall.Arguments?.GetValueOrDefault("context")?.ToString();
            var query = ExtractQueryFromArguments(toolCall.Name, toolCall.Arguments);
            var sessionId = toolCall.Arguments?.GetValueOrDefault("sessionId")?.ToString();
            
            // Extract result summary and count
            string? resultSummary = null;
            int? resultCount = null;
            
            if (result != null && !result.IsError && result.Content?.Any() == true)
            {
                var textContent = result.Content.FirstOrDefault(c => c.Type == "text")?.Text;
                if (textContent != null)
                {
                    resultSummary = textContent.Length > 200 ? textContent[..200] + "..." : textContent;
                    
                    // Try to extract result count from common patterns
                    if (textContent.Contains(" results") || textContent.Contains(" found"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(textContent, @"(\d+)\s+(results?|found|items?|files?|patterns?)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
                        {
                            resultCount = count;
                        }
                    }
                }
            }
            
            await _learningService.RecordToolInvocationAsync(
                toolCall.Name,
                context,
                sessionId,
                query,
                toolCall.Arguments,
                errorMessage == null && (result?.IsError != true),
                errorMessage ?? (result?.IsError == true ? "Tool returned error" : null),
                durationMs,
                resultSummary,
                resultCount,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Don't let tracking failures break the tool call
            _logger.LogWarning(ex, "Failed to track tool invocation for {ToolName}", toolCall.Name);
        }
    }
    
    private static string? ExtractQueryFromArguments(string toolName, Dictionary<string, object>? args)
    {
        if (args == null) return null;
        
        // Different tools use different parameter names for the query/question
        var queryKeys = new[] { "query", "question", "search", "filePath", "path", "className", "pattern_id" };
        
        foreach (var key in queryKeys)
        {
            if (args.TryGetValue(key, out var value) && value != null)
            {
                var strValue = value.ToString();
                if (!string.IsNullOrWhiteSpace(strValue))
                    return strValue;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// AUTO-SESSION: Automatically starts a session if one doesn't exist for the context.
    /// This ensures every tool call has session context for learning.
    /// </summary>
    private async Task<string?> EnsureSessionExistsAsync(McpToolCall toolCall, CancellationToken cancellationToken)
    {
        // Skip session management for tools that handle sessions themselves
        if (_sessionManagementTools.Contains(toolCall.Name))
            return null;
            
        // Extract context from arguments
        var context = toolCall.Arguments?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        
        // If no context provided, try to extract from other arguments or use default
        if (string.IsNullOrWhiteSpace(context))
        {
            // Some tools might not have context - use a default
            context = "default";
        }
        
        // Check cache first
        if (_activeSessionCache.TryGetValue(context, out var cachedSessionId))
        {
            // Verify session is still active
            var existingSession = await _learningService.GetActiveSessionAsync(context, cancellationToken);
            if (existingSession != null && existingSession.Id == cachedSessionId)
            {
                return cachedSessionId;
            }
            // Cache is stale, remove it
            _activeSessionCache.Remove(context);
        }
        
        // Check for existing active session
        var activeSession = await _learningService.GetActiveSessionAsync(context, cancellationToken);
        if (activeSession != null)
        {
            _activeSessionCache[context] = activeSession.Id;
            return activeSession.Id;
        }
        
        // AUTO-START: No active session, create one automatically
        try
        {
            _logger.LogInformation("🚀 Auto-starting session for context: {Context}", context);
            var newSession = await _learningService.StartSessionAsync(context, cancellationToken);
            _activeSessionCache[context] = newSession.Id;
            _logger.LogInformation("✅ Auto-session started: {SessionId} for context: {Context}", newSession.Id, context);
            return newSession.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-start session for context: {Context}", context);
            return null;
        }
    }
    
    /// <summary>
    /// AUTO-RECORD: Automatically records files mentioned in tool arguments as "discussed".
    /// This helps the learning system understand which files are being worked on.
    /// </summary>
    private async Task AutoRecordFilesFromArgumentsAsync(string? sessionId, Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || args == null)
            return;
            
        try
        {
            // Extract file paths from various argument names
            var fileArgNames = new[] { "filePath", "path", "sourcePath", "targetPath", "outputPath", "exampleOldPath", "exampleNewPath" };
            var filePaths = new HashSet<string>();
            
            foreach (var argName in fileArgNames)
            {
                if (args.TryGetValue(argName, out var value) && value != null)
                {
                    var path = value.ToString();
                    if (!string.IsNullOrWhiteSpace(path) && (path.Contains('/') || path.Contains('\\') || path.Contains('.')))
                    {
                        filePaths.Add(path);
                    }
                }
            }
            
            // Also check for relevantFiles array
            if (args.TryGetValue("relevantFiles", out var filesObj) && filesObj != null)
            {
                IEnumerable<string>? filesList = filesObj switch
                {
                    IEnumerable<string> list => list,
                    System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.Array 
                        => je.EnumerateArray().Select(e => e.GetString()).Where(s => s != null).Cast<string>(),
                    _ => null
                };
                
                if (filesList != null)
                {
                    foreach (var file in filesList)
                    {
                        if (!string.IsNullOrWhiteSpace(file))
                            filePaths.Add(file);
                    }
                }
            }
            
            // Record each file as discussed
            foreach (var filePath in filePaths)
            {
                await _learningService.RecordFileDiscussedAsync(sessionId, filePath, cancellationToken);
                _logger.LogDebug("📁 Auto-recorded file as discussed: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            // Don't let auto-recording failures break the tool call
            _logger.LogWarning(ex, "Failed to auto-record files from arguments");
        }
    }

    public async Task<List<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var allTools = _handlers
            .SelectMany(handler => handler.GetTools())
            .OrderBy(tool => tool.Name)
            .ToList();

        _logger.LogInformation("📋 Listing {Count} MCP tools", allTools.Count);
        return allTools;
    }

    public async Task<McpResponse?> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📥 MCP Request: {Method}", request.Method);

        return request.Method switch
        {
            "initialize" => HandleInitializeAsync(request),
            "notifications/initialized" => null, // Client notification, no response needed
            "tools/list" => await HandleListToolsAsync(request, cancellationToken),
            "tools/call" => await HandleCallToolAsync(request, cancellationToken),
            _ => new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}"
                }
            }
        };
    }

    private McpResponse HandleInitializeAsync(McpRequest request)
    {
        _logger.LogInformation("🤝 MCP Initialize handshake");
        
        return new McpResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { listChanged = true }
                },
                serverInfo = new
                {
                    name = "memory-code-agent",
                    version = "1.0.0"
                }
            }
        };
    }

    private async Task<McpResponse> HandleListToolsAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var tools = await GetToolsAsync(cancellationToken);

        return new McpResponse
        {
            JsonRpc = "2.0",
            Id = request.Id,
            Result = new
            {
                tools = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = t.InputSchema
                })
            }
        };
    }

    private async Task<McpResponse> HandleCallToolAsync(McpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Parse tool call params from request (case-insensitive for JSON property names)
            var paramsJson = JsonSerializer.Serialize(request.Params);
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var callParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, jsonOptions);

            if (callParams == null || string.IsNullOrWhiteSpace(callParams.Name))
            {
                return new McpResponse
                {
                    JsonRpc = "2.0",
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32602,
                        Message = "Invalid params: 'name' is required"
                    }
                };
            }

            var result = await CallToolAsync(new McpToolCall { Name = callParams.Name, Arguments = callParams.Arguments }, cancellationToken);

            return new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Result = new
                {
                    content = result.Content,
                    isError = result.IsError
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling tool call");
            return new McpResponse
            {
                JsonRpc = "2.0",
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                }
            };
        }
    }

    private class ToolCallParams
    {
        public string Name { get; set; } = "";
        public Dictionary<string, object>? Arguments { get; set; }
    }
}
