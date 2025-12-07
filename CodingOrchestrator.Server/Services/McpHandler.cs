using System.Text.Json;
using AgentContracts.Requests;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Handles MCP tool calls for the orchestrator
/// </summary>
public class McpHandler : IMcpHandler
{
    private readonly IJobManager _jobManager;
    private readonly ILogger<McpHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public McpHandler(IJobManager jobManager, ILogger<McpHandler> logger)
    {
        _jobManager = jobManager;
        _logger = logger;
    }

    public IEnumerable<object> GetToolDefinitions()
    {
        var tools = new List<object>
        {
            new Dictionary<string, object>
            {
                ["name"] = "orchestrate_task",
                ["description"] = "Start a multi-agent coding task. The coding agent generates code and the validation agent reviews it until quality standards are met. Supports ANY programming language! Uses 'Search Before Write' to avoid duplicating existing code.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["task"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The coding task to perform (e.g., 'Create a hello world in Python', 'Add caching to UserService')" },
                        ["language"] = new Dictionary<string, object> { 
                            ["type"] = "string", 
                            ["description"] = "Target programming language: python, csharp, typescript, javascript, go, rust, java, ruby, php, swift, kotlin, dart, sql, html, css, shell, or auto (detect from workspace/task). Default: auto",
                            ["enum"] = new[] { "auto", "python", "csharp", "typescript", "javascript", "go", "rust", "java", "ruby", "php", "swift", "kotlin", "dart", "sql", "html", "css", "shell" },
                            ["default"] = "auto"
                        },
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Project context name for Lightning memory" },
                        ["workspacePath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Path to the workspace root" },
                        ["background"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Run as background job (default: true)", ["default"] = true },
                        ["maxIterations"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Maximum coding/validation iterations. No limit - set as high as needed (e.g., 100 for complex, 1000+ for huge projects). Default: 50", ["default"] = 50 },
                        ["minValidationScore"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Minimum score to pass validation (default: 8)", ["default"] = 8 },
                        ["autoWriteFiles"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Automatically write generated files to workspace (default: false). When false, files are returned for manual review.", ["default"] = false }
                    },
                    ["required"] = new[] { "task" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "get_task_status",
                ["description"] = "Get the status of a running or completed coding task",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["jobId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The job ID returned by orchestrate_task" }
                    },
                    ["required"] = new[] { "jobId" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "cancel_task",
                ["description"] = "Cancel a running coding task",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["jobId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The job ID to cancel" }
                    },
                    ["required"] = new[] { "jobId" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "list_tasks",
                ["description"] = "List all active and recent coding tasks",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                }
            }
        };
        return tools;
    }

    public async Task<string> HandleToolCallAsync(string toolName, Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling MCP tool call: {Tool}", toolName);

        return toolName switch
        {
            "orchestrate_task" => await HandleOrchestrateTaskAsync(arguments, cancellationToken),
            "get_task_status" => HandleGetTaskStatus(arguments),
            "cancel_task" => HandleCancelTask(arguments),
            "list_tasks" => HandleListTasks(),
            _ => $"Unknown tool: {toolName}"
        };
    }

    private async Task<string> HandleOrchestrateTaskAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var task = GetStringArg(arguments, "task");
        var language = GetStringArg(arguments, "language");
        var context = GetStringArg(arguments, "context");
        var workspacePath = GetStringArg(arguments, "workspacePath");
        
        // Auto-detect language if not specified or set to "auto"
        if (string.IsNullOrEmpty(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            language = DetectLanguageFromTaskOrWorkspace(task, workspacePath);
            _logger.LogInformation("Auto-detected language: {Language}", language);
        }
        
        // Set defaults for optional context/workspacePath
        if (string.IsNullOrEmpty(context))
        {
            context = "default";
        }
        if (string.IsNullOrEmpty(workspacePath))
        {
            workspacePath = ".";
        }
        
        var request = new OrchestrateTaskRequest
        {
            Task = task,
            Language = language,
            Context = context,
            WorkspacePath = workspacePath,
            Background = GetBoolArg(arguments, "background", true),
            MaxIterations = GetIntArg(arguments, "maxIterations", 50),  // Default 50, no cap - user can set 100000 if needed
            MinValidationScore = GetIntArg(arguments, "minValidationScore", 8),
            AutoWriteFiles = GetBoolArg(arguments, "autoWriteFiles", false)
        };

        var jobId = await _jobManager.StartJobAsync(request, cancellationToken);
        
        var autoWriteNote = request.AutoWriteFiles 
            ? "✅ **Auto-write enabled** - Files will be written to workspace automatically"
            : "📋 **Manual mode** - Files will be returned for you to review and create";

        var languageDisplay = GetLanguageDisplayName(request.Language);

        return $@"🚀 **Multi-Agent Coding Task Started**

**Job ID:** `{jobId}`
**Task:** {request.Task}
**Language:** {languageDisplay}
**Context:** {request.Context}
{autoWriteNote}

🔍 **Search Before Write** is enabled - existing code will be reused, not duplicated.

**To check status:** Call `get_task_status` with jobId: `{jobId}`

**Progress will include:**
- 🔍 Search for existing code (avoid duplication)
- Context gathering from Lightning memory
- Code generation iterations ({languageDisplay})
- Validation passes with scores
- Final result with generated files";
    }

    /// <summary>
    /// Auto-detect programming language from task description or workspace
    /// </summary>
    private string DetectLanguageFromTaskOrWorkspace(string task, string workspacePath)
    {
        var taskLower = task.ToLowerInvariant();
        
        // Explicit language mentions in task
        var languageKeywords = new Dictionary<string, string[]>
        {
            ["python"] = new[] { "python", "py ", ".py", "django", "flask", "fastapi", "pandas", "numpy" },
            ["typescript"] = new[] { "typescript", " ts ", ".ts", "angular", "nest.js", "nestjs" },
            ["javascript"] = new[] { "javascript", " js ", ".js", "node", "react", "vue", "express", "next.js", "nextjs" },
            ["csharp"] = new[] { "c#", "csharp", ".cs", "dotnet", ".net", "blazor", "asp.net", "aspnet" },
            ["go"] = new[] { " go ", "golang", ".go" },
            ["rust"] = new[] { "rust", ".rs", "cargo" },
            ["java"] = new[] { " java ", ".java", "spring", "maven", "gradle" },
            ["ruby"] = new[] { "ruby", ".rb", "rails" },
            ["php"] = new[] { " php", ".php", "laravel", "symfony" },
            ["swift"] = new[] { "swift", ".swift", "swiftui", "ios app" },
            ["kotlin"] = new[] { "kotlin", ".kt", "android" },
            ["flutter"] = new[] { "flutter", "flutter app", "flutter widget" },
            ["dart"] = new[] { "dart", ".dart" },
            ["sql"] = new[] { " sql", ".sql", "database query", "stored procedure" },
            ["html"] = new[] { "html", ".html", "webpage" },
            ["css"] = new[] { " css", ".css", "stylesheet" },
            ["shell"] = new[] { "bash", "shell", ".sh", "script", "powershell", ".ps1" }
        };
        
        foreach (var (language, keywords) in languageKeywords)
        {
            if (keywords.Any(k => taskLower.Contains(k)))
            {
                return language;
            }
        }
        
        // Check workspace for common files
        if (!string.IsNullOrEmpty(workspacePath) && Directory.Exists(workspacePath))
        {
            try
            {
                var files = Directory.GetFiles(workspacePath, "*.*", SearchOption.TopDirectoryOnly);
                
                // Check for language-specific project files
                if (files.Any(f => f.EndsWith(".csproj") || f.EndsWith(".sln"))) return "csharp";
                if (files.Any(f => f.EndsWith("package.json")))
                {
                    // Check if TypeScript
                    if (files.Any(f => f.EndsWith("tsconfig.json"))) return "typescript";
                    return "javascript";
                }
                if (files.Any(f => f.EndsWith("requirements.txt") || f.EndsWith("pyproject.toml") || f.EndsWith("setup.py"))) return "python";
                if (files.Any(f => f.EndsWith("go.mod"))) return "go";
                if (files.Any(f => f.EndsWith("Cargo.toml"))) return "rust";
                if (files.Any(f => f.EndsWith("pom.xml") || f.EndsWith("build.gradle"))) return "java";
                if (files.Any(f => f.EndsWith("Gemfile"))) return "ruby";
                if (files.Any(f => f.EndsWith("composer.json"))) return "php";
                if (files.Any(f => f.EndsWith("pubspec.yaml"))) return "dart";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not scan workspace for language detection");
            }
        }
        
        // Default to csharp for this codebase context
        return "csharp";
    }

    /// <summary>
    /// Get display name for a language
    /// </summary>
    private static string GetLanguageDisplayName(string? language)
    {
        return language?.ToLowerInvariant() switch
        {
            "python" => "🐍 Python",
            "csharp" => "💜 C#",
            "typescript" => "💙 TypeScript",
            "javascript" => "💛 JavaScript",
            "go" => "🐹 Go",
            "rust" => "🦀 Rust",
            "java" => "☕ Java",
            "ruby" => "💎 Ruby",
            "php" => "🐘 PHP",
            "swift" => "🍎 Swift",
            "kotlin" => "🟣 Kotlin",
            "dart" => "🎯 Dart/Flutter",
            "sql" => "🗃️ SQL",
            "html" => "🌐 HTML",
            "css" => "🎨 CSS",
            "shell" => "🐚 Shell/Bash",
            _ => language ?? "Unknown"
        };
    }

    private string HandleGetTaskStatus(Dictionary<string, object> arguments)
    {
        var jobId = GetStringArg(arguments, "jobId");
        var status = _jobManager.GetJobStatus(jobId);

        if (status == null)
        {
            return $"❌ Job `{jobId}` not found";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine($"📊 **Task Status: {status.Status}**");
        result.AppendLine();
        result.AppendLine($"**Job ID:** `{status.JobId}`");
        result.AppendLine($"**Progress:** {status.Progress}%");
        result.AppendLine($"**Current Phase:** {status.CurrentPhase}");
        result.AppendLine($"**Iteration:** {status.Iteration}/{status.MaxIterations}");
        result.AppendLine();

        if (status.Timeline.Any())
        {
            result.AppendLine("**Timeline:**");
            foreach (var phase in status.Timeline)
            {
                var duration = phase.DurationMs.HasValue ? $" ({phase.DurationMs}ms)" : "";
                var iterInfo = phase.Iteration.HasValue ? $" [iter {phase.Iteration}]" : "";
                result.AppendLine($"- ✅ {phase.Name}{iterInfo}{duration}");
            }
            result.AppendLine();
        }

        // DEBUG: Log what we're checking
        _logger.LogInformation("DEBUG: status.Status = {Status} (int: {StatusInt}), Result is null: {IsNull}", 
            status.Status, (int)status.Status, status.Result == null);
        
        if (status.Status == AgentContracts.Responses.TaskState.Complete && status.Result != null)
        {
            result.AppendLine("**✅ COMPLETED**");
            result.AppendLine($"- Validation Score: {status.Result.ValidationScore}/10");
            result.AppendLine($"- Total Iterations: {status.Result.TotalIterations}");
            result.AppendLine($"- Duration: {status.Result.TotalDurationMs}ms");
            result.AppendLine($"- Files Generated: {status.Result.Files.Count}");
            result.AppendLine($"- Summary: {status.Result.Summary}");
            result.AppendLine();
            
            _logger.LogInformation("Returning {FileCount} files in get_task_status response", status.Result.Files.Count);
            
            if (status.Result.Files.Count == 0)
            {
                result.AppendLine("⚠️ **No files in result** - Check orchestrator logs for parsing errors");
            }
            
            foreach (var file in status.Result.Files)
            {
                _logger.LogInformation("Including file: {Path}, ContentLength: {Len}", file.Path, file.Content?.Length ?? 0);
                
                result.AppendLine($"---");
                result.AppendLine($"### 📄 {file.Path}");
                result.AppendLine($"**Change Type:** {file.ChangeType}");
                if (!string.IsNullOrEmpty(file.Reason))
                {
                    result.AppendLine($"**Reason:** {file.Reason}");
                }
                result.AppendLine();
                
                // Detect language from file extension
                var ext = System.IO.Path.GetExtension(file.Path)?.ToLowerInvariant();
                var lang = ext switch
                {
                    ".cs" => "csharp",
                    ".ts" => "typescript",
                    ".js" => "javascript",
                    ".py" => "python",
                    ".sql" => "sql",
                    ".json" => "json",
                    ".yaml" or ".yml" => "yaml",
                    _ => ""
                };
                
                result.AppendLine($"```{lang}");
                result.AppendLine(file.Content ?? "// Empty content");
                result.AppendLine("```");
                result.AppendLine();
            }
        }
        else if (status.Status == AgentContracts.Responses.TaskState.Failed && status.Error != null)
        {
            result.AppendLine("**❌ FAILED**");
            result.AppendLine($"- Error: {status.Error.Message}");
            result.AppendLine($"- Type: {status.Error.Type}");
            if (status.Error.CanRetry)
            {
                result.AppendLine("- Can retry: Yes");
            }
        }

        return result.ToString();
    }

    private string HandleCancelTask(Dictionary<string, object> arguments)
    {
        var jobId = GetStringArg(arguments, "jobId");
        var cancelled = _jobManager.CancelJob(jobId);

        return cancelled 
            ? $"✅ Job `{jobId}` has been cancelled" 
            : $"❌ Could not cancel job `{jobId}` - not found or already completed";
    }

    private string HandleListTasks()
    {
        var tasks = _jobManager.GetAllJobs().ToList();

        if (!tasks.Any())
        {
            return "No active tasks";
        }

        var result = new System.Text.StringBuilder();
        result.AppendLine("**Active Tasks:**");
        result.AppendLine();

        foreach (var task in tasks)
        {
            var statusIcon = task.Status switch
            {
                AgentContracts.Responses.TaskState.Queued => "⏳",
                AgentContracts.Responses.TaskState.Running => "🔄",
                AgentContracts.Responses.TaskState.Complete => "✅",
                AgentContracts.Responses.TaskState.Failed => "❌",
                AgentContracts.Responses.TaskState.Cancelled => "🚫",
                _ => "❓"
            };

            result.AppendLine($"{statusIcon} `{task.JobId}` - {task.Status} ({task.Progress}%) - {task.CurrentPhase}");
        }

        return result.ToString();
    }

    private static string GetStringArg(Dictionary<string, object> args, string key)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.GetString() ?? "";
            }
            return value?.ToString() ?? "";
        }
        return "";
    }

    private static bool GetBoolArg(Dictionary<string, object> args, string key, bool defaultValue)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind == JsonValueKind.True;
            }
            if (value is bool b)
            {
                return b;
            }
        }
        return defaultValue;
    }

    private static int GetIntArg(Dictionary<string, object> args, string key, int defaultValue)
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement && jsonElement.TryGetInt32(out var intValue))
            {
                return intValue;
            }
            if (value is int i)
            {
                return i;
            }
        }
        return defaultValue;
    }
}

