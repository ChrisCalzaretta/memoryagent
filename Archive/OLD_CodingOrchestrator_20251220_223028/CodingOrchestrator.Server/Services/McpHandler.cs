using System.Text.Json;
using AgentContracts.Requests;
using AgentContracts.Services;
using CodingOrchestrator.Server.Clients;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// Handles MCP tool calls for the orchestrator
/// Exposes: orchestration tools + design tools (external facing)
/// </summary>
public class McpHandler : IMcpHandler
{
    private readonly IJobManager _jobManager;
    private readonly IDesignAgentClient _designAgent;
    private readonly IPathTranslationService _pathTranslation;
    private readonly ILogger<McpHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public McpHandler(
        IJobManager jobManager, 
        IDesignAgentClient designAgent, 
        IPathTranslationService pathTranslation,
        ILogger<McpHandler> logger)
    {
        _jobManager = jobManager;
        _designAgent = designAgent;
        _pathTranslation = pathTranslation;
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
                        ["maxIterations"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Maximum coding/validation iterations. Default: 100", ["default"] = 100 },
                        ["minValidationScore"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Minimum score to pass validation (default: 8)", ["default"] = 8 },
                        ["validationMode"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Validation strictness: 'standard' (default, relaxed - only bugs/security) or 'enterprise' (strict - XML docs, CancellationToken, DI, etc.)", ["default"] = "standard", ["enum"] = new[] { "standard", "enterprise" } },
                        ["autoWriteFiles"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Automatically write generated files to workspace (default: true). Files sync incrementally with job storage backup. Set to false for manual-only extraction.", ["default"] = true }
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
            },
            new Dictionary<string, object>
            {
                ["name"] = "get_generated_files",
                ["description"] = "Extract and write generated files from a completed job to a specified directory. Files are automatically placed in a subdirectory named after the jobId (e.g., outputPath/job_xxx/). Works for both successful and failed jobs (gets all accumulated files).",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["jobId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The job ID to extract files from" },
                        ["outputPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Parent directory path. Files will be written to outputPath/jobId/ (e.g., 'E:\\GitHub\\MyProjects' creates 'E:\\GitHub\\MyProjects\\job_xxx\\')" }
                    },
                    ["required"] = new[] { "jobId", "outputPath" }
                }
            },
            
            // ═══════════════════════════════════════════════════════════
            // 🎨 DESIGN TOOLS - Brand guidelines and validation
            // ═══════════════════════════════════════════════════════════
            
            new Dictionary<string, object>
            {
                ["name"] = "design_questionnaire",
                ["description"] = "Get the brand builder questionnaire. Returns questions to answer for creating a complete brand system with colors, typography, components, and guidelines.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "design_create_brand",
                ["description"] = "Create a complete brand system from questionnaire answers. Returns design tokens, components, themes, voice guidelines, and accessibility requirements.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["brand_name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of the brand/product" },
                        ["tagline"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Optional tagline" },
                        ["description"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "1-2 sentence product description" },
                        ["target_audience"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Who is the target audience?" },
                        ["industry"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Industry: SaaS, E-commerce, Finance, Health, Education, Entertainment, Enterprise, Consumer, Other" },
                        ["personality_traits"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string" }, ["description"] = "3-5 traits: Professional, Playful, Trustworthy, Bold, Minimal, Luxurious, Friendly, Technical, Energetic, Calm, Innovative, Traditional, Warm, Cool, Serious, Fun" },
                        ["brand_voice"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Voice: Encouraging coach, Trusted advisor, Friendly helper, Expert authority, Playful friend, Calm guide" },
                        ["theme_preference"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Theme: Dark mode, Light mode, Both" },
                        ["color_preferences"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Colors you love (optional)" },
                        ["color_avoid"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Colors to avoid (optional)" },
                        ["visual_style"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Style: Minimal, Rich, Bold, Soft, Technical" },
                        ["corner_style"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Corners: Sharp, Slightly rounded, Rounded, Very rounded, Pill" },
                        ["font_preference"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Font: Sans-serif, Serif, Geometric, Humanist, Monospace accent" },
                        ["platforms"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string" }, ["description"] = "Platforms: Web, iOS, Android, Desktop" },
                        ["frameworks"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string" }, ["description"] = "Frameworks: Blazor, React, Vue, Angular, SwiftUI, Kotlin/Compose, Flutter, React Native, .NET MAUI, Plain HTML/CSS" },
                        ["css_framework"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "CSS: Tailwind CSS, Plain CSS, SCSS/Sass, CSS-in-JS" },
                        ["component_types"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string" }, ["description"] = "UI types: Dashboards, Forms, Lists, Cards, Navigation, Modals, Authentication, Settings, Landing pages, E-commerce, Chat, Data Visualization" },
                        ["motion_preference"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Animation: Minimal, Moderate, Rich, None" },
                        ["accessibility_level"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "WCAG level: AA (standard), AAA (strictest), Basic" }
                    },
                    ["required"] = new[] { "brand_name", "description", "industry", "personality_traits", "brand_voice", "visual_style", "platforms", "frameworks" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "design_get_brand",
                ["description"] = "Get an existing brand definition by context name. Returns full brand with tokens, components, themes, voice, and accessibility.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Brand context name (e.g., 'fittrack-pro')" }
                    },
                    ["required"] = new[] { "context" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "design_list_brands",
                ["description"] = "List all available brand definitions",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>()
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "design_validate",
                ["description"] = "Validate code against brand guidelines. Checks colors, typography, spacing, components, and accessibility. Returns score, grade, and issues with fixes.",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Brand context name" },
                        ["code"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Code to validate (HTML, CSS, Blazor, React, etc.)" }
                    },
                    ["required"] = new[] { "context", "code" }
                }
            },
            new Dictionary<string, object>
            {
                ["name"] = "design_update_brand",
                ["description"] = "Update an existing brand's settings (colors, fonts, etc.)",
                ["inputSchema"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["context"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Brand context name to update" },
                        ["primary_color"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "New primary color (hex)" },
                        ["font_family"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "New font family" },
                        ["theme_preference"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Theme: Dark mode, Light mode, Both" }
                    },
                    ["required"] = new[] { "context" }
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
            // Orchestration tools
            "orchestrate_task" => await HandleOrchestrateTaskAsync(arguments, cancellationToken),
            "get_task_status" => HandleGetTaskStatus(arguments),
            "cancel_task" => HandleCancelTask(arguments),
            "list_tasks" => HandleListTasks(),
            "get_generated_files" => await HandleGetGeneratedFilesAsync(arguments, cancellationToken),
            
            // Design tools
            "design_questionnaire" => await HandleDesignQuestionnaireAsync(cancellationToken),
            "design_create_brand" => await HandleDesignCreateBrandAsync(arguments, cancellationToken),
            "design_get_brand" => await HandleDesignGetBrandAsync(arguments, cancellationToken),
            "design_list_brands" => await HandleDesignListBrandsAsync(cancellationToken),
            "design_validate" => await HandleDesignValidateAsync(arguments, cancellationToken),
            "design_update_brand" => await HandleDesignUpdateBrandAsync(arguments, cancellationToken),
            
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
            MaxIterations = GetIntArg(arguments, "maxIterations", 100),  // Default 100
            MinValidationScore = GetIntArg(arguments, "minValidationScore", 8),
            ValidationMode = GetStringArg(arguments, "validationMode", "standard"), // "standard" or "enterprise"
            AutoWriteFiles = GetBoolArg(arguments, "autoWriteFiles", true)
        };

        var jobId = await _jobManager.StartJobAsync(request, cancellationToken);
        
        var autoWriteNote = request.AutoWriteFiles
            ? "✅ **Auto-write enabled** - Files sync to workspace incrementally (with job storage backup)"
            : "📋 **Manual mode** - Files stored in job persistence only, manual extraction needed";

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
        // 🗂️ Translate Windows path to container path for Docker environment
        var containerPath = _pathTranslation.TranslateToContainerPath(workspacePath);
        if (!string.IsNullOrEmpty(containerPath) && Directory.Exists(containerPath))
        {
            try
            {
                var files = Directory.GetFiles(containerPath, "*.*", SearchOption.TopDirectoryOnly);
                
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
        
        // Status header with emoji
        var statusEmoji = status.Status switch
        {
            AgentContracts.Responses.TaskState.Running => "🔄",
            AgentContracts.Responses.TaskState.Complete => "✅",
            AgentContracts.Responses.TaskState.Failed => "❌",
            AgentContracts.Responses.TaskState.Cancelled => "🚫",
            _ => "⏳"
        };
        
        result.AppendLine($"## {statusEmoji} Task Status: **{status.Status}**");
        result.AppendLine();
        result.AppendLine($"| Field | Value |");
        result.AppendLine($"|-------|-------|");
        result.AppendLine($"| **Job ID** | `{status.JobId}` |");
        result.AppendLine($"| **Progress** | {status.Progress}% |");
        result.AppendLine($"| **Current Phase** | {status.CurrentPhase} |");
        result.AppendLine($"| **Iteration** | {status.Iteration}/{status.MaxIterations} |");
        result.AppendLine();

        // 📋 Show execution plan with checklist
        if (status.Plan != null)
        {
            result.AppendLine("### 📋 Execution Plan");
            result.AppendLine();
            
            // Show required classes
            if (status.Plan.RequiredClasses.Any())
            {
                result.AppendLine($"**Required Components:** {string.Join(", ", status.Plan.RequiredClasses)}");
                result.AppendLine();
            }
            
            // Show dependency order
            if (status.Plan.DependencyOrder.Any())
            {
                result.AppendLine($"**Generation Order:** {string.Join(" → ", status.Plan.DependencyOrder)}");
                result.AppendLine();
            }
            
            // Show steps checklist
            if (status.Plan.Steps.Any())
            {
                result.AppendLine("**Checklist:**");
                result.AppendLine();
                foreach (var step in status.Plan.Steps.OrderBy(s => s.Order))
                {
                    var stepStatus = step.Status?.ToLower() switch
                    {
                        "completed" => "✅",
                        "in_progress" => "🔄",
                        "failed" => "❌",
                        _ => "⬜"
                    };
                    var fileName = !string.IsNullOrEmpty(step.FileName) ? $" (`{step.FileName}`)" : "";
                    result.AppendLine($"- {stepStatus} {step.Description}{fileName}");
                }
                result.AppendLine();
            }
        }

        // 📁 Show generated files
        if (status.GeneratedFiles.Any())
        {
            result.AppendLine($"### 📁 Files Generated ({status.GeneratedFiles.Count})");
            result.AppendLine();
            foreach (var file in status.GeneratedFiles.OrderBy(f => f))
            {
                result.AppendLine($"- ✅ `{file}`");
            }
            result.AppendLine();
        }

        // Analyze timeline for statistics
        if (status.Timeline.Any())
        {
            var buildAttempts = status.Timeline.Where(t => t.Name == "docker_execution").ToList();
            var validationAttempts = status.Timeline.Where(t => t.Name == "validation_agent").ToList();
            var codingAttempts = status.Timeline.Where(t => t.Name == "coding_agent").ToList();
            
            // Build statistics
            var buildSuccesses = buildAttempts.Count(t => t.Details?.TryGetValue("buildPassed", out var bp) == true && bp is bool b && b);
            var buildFailures = buildAttempts.Count - buildSuccesses;
            
            // Validation statistics 
            var validationScores = validationAttempts
                .Where(t => t.Details?.TryGetValue("score", out _) == true)
                .Select(t => {
                    if (t.Details!.TryGetValue("score", out var s) && s is int score) return score;
                    if (t.Details!.TryGetValue("score", out var s2) && s2 is long score2) return (int)score2;
                    return 0;
                })
                .ToList();
            
            result.AppendLine("### 📈 Progress Statistics");
            result.AppendLine();
            result.AppendLine($"| Metric | Value |");
            result.AppendLine($"|--------|-------|");
            result.AppendLine($"| **Coding Attempts** | {codingAttempts.Count} |");
            result.AppendLine($"| **Build Attempts** | {buildAttempts.Count} (✅ {buildSuccesses} / ❌ {buildFailures}) |");
            result.AppendLine($"| **Validation Attempts** | {validationAttempts.Count} |");
            if (validationScores.Any())
            {
                result.AppendLine($"| **Best Score** | {validationScores.Max()}/10 |");
                result.AppendLine($"| **Latest Score** | {validationScores.Last()}/10 |");
            }
            result.AppendLine();
            
            // Show recent errors/failures
            var recentFailures = status.Timeline
                .Where(t => t.Name == "docker_execution" && 
                           t.Details?.TryGetValue("buildPassed", out var bp) == true && 
                           bp is bool b && !b)
                .TakeLast(3)
                .ToList();
            
            if (recentFailures.Any())
            {
                result.AppendLine("### ❌ Recent Build Failures");
                result.AppendLine();
                foreach (var failure in recentFailures)
                {
                    var iter = failure.Iteration ?? 0;
                    result.AppendLine($"**Iteration {iter}:**");
                    if (failure.Details?.TryGetValue("error", out var error) == true && error != null)
                    {
                        var errorStr = error.ToString();
                        // Truncate long errors
                        if (errorStr?.Length > 500)
                            errorStr = errorStr.Substring(0, 500) + "...";
                        result.AppendLine($"```");
                        result.AppendLine(errorStr);
                        result.AppendLine($"```");
                    }
                    else
                    {
                        result.AppendLine("- Build failed (no error details captured)");
                    }
                }
                result.AppendLine();
            }
            
            // Show validation feedback if any
            var recentValidations = validationAttempts.TakeLast(2).ToList();
            if (recentValidations.Any())
            {
                result.AppendLine("### 🔍 Recent Validation Results");
                result.AppendLine();
                foreach (var validation in recentValidations)
                {
                    var iter = validation.Iteration ?? 0;
                    var score = 0;
                    if (validation.Details?.TryGetValue("score", out var s) == true)
                    {
                        if (s is int si) score = si;
                        else if (s is long sl) score = (int)sl;
                    }
                    var passed = validation.Details?.TryGetValue("passed", out var p) == true && p is bool pb && pb;
                    var icon = passed ? "✅" : "❌";
                    
                    result.AppendLine($"**Iteration {iter}:** {icon} Score {score}/10");
                    
                    if (validation.Details?.TryGetValue("feedback", out var feedback) == true && feedback != null)
                    {
                        var feedbackStr = feedback.ToString();
                        if (feedbackStr?.Length > 300)
                            feedbackStr = feedbackStr.Substring(0, 300) + "...";
                        result.AppendLine($"> {feedbackStr}");
                    }
                }
                result.AppendLine();
            }
            
            // Show last few timeline entries (condensed)
            result.AppendLine("### 📋 Recent Activity (last 10 phases)");
            result.AppendLine();
            var recentPhases = status.Timeline.TakeLast(10).ToList();
            foreach (var phase in recentPhases)
            {
                var duration = phase.DurationMs.HasValue ? $" ({phase.DurationMs}ms)" : "";
                var iterInfo = phase.Iteration.HasValue ? $" [iter {phase.Iteration}]" : "";
                
                // Add status indicator based on phase details
                var phaseIcon = "✅";
                if (phase.Name == "docker_execution" && phase.Details?.TryGetValue("buildPassed", out var bp) == true && bp is bool b && !b)
                    phaseIcon = "❌";
                else if (phase.Name == "validation_agent" && phase.Details?.TryGetValue("passed", out var vp) == true && vp is bool vb && !vb)
                    phaseIcon = "⚠️";
                    
                result.AppendLine($"- {phaseIcon} {phase.Name}{iterInfo}{duration}");
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

    private async Task<string> HandleGetGeneratedFilesAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var jobId = GetStringArg(arguments, "jobId");
        var outputPath = GetStringArg(arguments, "outputPath");
        
        if (string.IsNullOrEmpty(jobId))
            return "❌ Error: jobId is required";
        
        if (string.IsNullOrEmpty(outputPath))
            return "❌ Error: outputPath is required";
        
        var status = _jobManager.GetJobStatus(jobId);
        if (status == null)
            return $"❌ Error: Job '{jobId}' not found";
        
        // Get files from result (success) or partial result (failure)
        var files = status.Status == AgentContracts.Responses.TaskState.Complete && status.Result != null
            ? status.Result.Files
            : status.Error?.PartialResult?.Files;
        
        if (files == null || !files.Any())
            return $"❌ Error: No files found in job '{jobId}'";
        
        try
        {
            // Translate path from Windows to container if needed
            var translatedPath = _pathTranslation.TranslateToContainerPath(outputPath);
            
            // Append jobId as subdirectory name for better organization
            var finalPath = Path.Combine(translatedPath, jobId);
            
            // Create output directory
            Directory.CreateDirectory(finalPath);
            
            var filesWritten = 0;
            var errors = new List<string>();
            
            foreach (var file in files)
            {
                try
                {
                    var filePath = Path.Combine(finalPath, file.Path);
                    var fileDir = Path.GetDirectoryName(filePath);
                    
                    if (!string.IsNullOrEmpty(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }
                    
                    await File.WriteAllTextAsync(filePath, file.Content ?? "", cancellationToken);
                    filesWritten++;
                    _logger.LogInformation("✅ Wrote file: {Path}", filePath);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to write {file.Path}: {ex.Message}");
                    _logger.LogError(ex, "Failed to write file {Path}", file.Path);
                }
            }
            
            var result = new System.Text.StringBuilder();
            result.AppendLine($"# ✅ Files Extracted from Job: {jobId}");
            result.AppendLine();
            result.AppendLine($"**Output Path:** `{Path.Combine(outputPath, jobId)}`");
            result.AppendLine($"**Files Written:** {filesWritten}/{files.Count}");
            result.AppendLine();
            
            if (errors.Any())
            {
                result.AppendLine("## ⚠️ Errors:");
                foreach (var error in errors)
                {
                    result.AppendLine($"- {error}");
                }
                result.AppendLine();
            }
            
            result.AppendLine("## 📁 Files Written:");
            foreach (var file in files)
            {
                result.AppendLine($"- ✅ `{file.Path}`");
            }
            
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract files from job {JobId}", jobId);
            return $"❌ Error: Failed to write files - {ex.Message}";
        }
    }

    private static string GetStringArg(Dictionary<string, object> args, string key, string defaultValue = "")
    {
        if (args.TryGetValue(key, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.GetString() ?? defaultValue;
            }
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
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

    // ═══════════════════════════════════════════════════════════
    // 🎨 DESIGN TOOL HANDLERS
    // ═══════════════════════════════════════════════════════════

    private async Task<string> HandleDesignQuestionnaireAsync(CancellationToken cancellationToken)
    {
        try
        {
            var markdown = await _designAgent.GetQuestionnaireMarkdownAsync(cancellationToken);
            return markdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting questionnaire");
            return $"❌ Error: {ex.Message}";
        }
    }

    private async Task<string> HandleDesignCreateBrandAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        try
        {
            var brand = await _designAgent.CreateBrandAsync(arguments, cancellationToken);

            return $@"✅ **Brand Created: {brand.Name}**

**Context:** `{brand.Context}`
**Primary Color:** {brand.GetPrimaryColor()}
**Font:** {brand.GetFontFamily()}

**Next steps:**
- Use `design_get_brand context=""{brand.Context}""` to see full brand
- Use `design_validate context=""{brand.Context}"" code=""..."" ` to validate your code
- The orchestrator will automatically use this brand when generating UI code!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating brand");
            return $"❌ Error creating brand: {ex.Message}";
        }
    }

    private async Task<string> HandleDesignGetBrandAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var context = GetStringArg(arguments, "context");
        if (string.IsNullOrEmpty(context))
        {
            return "❌ Error: context is required";
        }

        try
        {
            var brand = await _designAgent.GetBrandAsync(context, cancellationToken);
            if (brand == null)
            {
                return $"❌ Brand '{context}' not found. Use `design_list_brands` to see available brands.";
            }

            return JsonSerializer.Serialize(brand, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting brand {Context}", context);
            return $"❌ Error: {ex.Message}";
        }
    }

    private async Task<string> HandleDesignListBrandsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var brands = await _designAgent.ListBrandsAsync(cancellationToken);

            if (brands.Count == 0)
            {
                return "No brands found. Use `design_questionnaire` then `design_create_brand` to create one.";
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine("📋 **Available Brands:**");
            result.AppendLine();
            foreach (var brand in brands)
            {
                result.AppendLine($"• **{brand.Name}** (context: `{brand.Context}`)");
            }
            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing brands");
            return $"❌ Error: {ex.Message}";
        }
    }

    private async Task<string> HandleDesignValidateAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var context = GetStringArg(arguments, "context");
        var code = GetStringArg(arguments, "code");

        if (string.IsNullOrEmpty(context))
        {
            return "❌ Error: context is required";
        }
        if (string.IsNullOrEmpty(code))
        {
            return "❌ Error: code is required";
        }

        try
        {
            var result = await _designAgent.ValidateAsync(context, code, cancellationToken);

            var output = new System.Text.StringBuilder();
            output.AppendLine($"🎨 **Design Validation:** {(result.IsCompliant ? "✅ PASS" : "❌ FAIL")}");
            output.AppendLine($"**Score:** {result.Score}/10 (Grade {result.Grade})");
            output.AppendLine();

            if (result.Issues.Count > 0)
            {
                output.AppendLine($"**Issues Found ({result.Issues.Count}):**");
                foreach (var issue in result.Issues)
                {
                    var icon = issue.Severity switch { 3 => "🔴", 2 => "🟠", 1 => "🟡", _ => "⚪" };
                    output.AppendLine($"  {icon} [{issue.Type}] {issue.Message}");
                    if (!string.IsNullOrEmpty(issue.Fix))
                    {
                        output.AppendLine($"     💡 Fix: {issue.Fix}");
                    }
                }
            }
            else
            {
                output.AppendLine("✨ No issues found!");
            }

            return output.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating design");
            return $"❌ Error: {ex.Message}";
        }
    }

    private async Task<string> HandleDesignUpdateBrandAsync(Dictionary<string, object> arguments, CancellationToken cancellationToken)
    {
        var context = GetStringArg(arguments, "context");
        if (string.IsNullOrEmpty(context))
        {
            return "❌ Error: context is required";
        }

        try
        {
            // Remove context from updates dict (it's the identifier, not an update)
            var updates = new Dictionary<string, object>(arguments);
            updates.Remove("context");

            var brand = await _designAgent.UpdateBrandAsync(context, updates, cancellationToken);

            return $@"✅ **Brand Updated: {brand.Name}**

**Context:** `{brand.Context}`
**Primary Color:** {brand.GetPrimaryColor()}
**Font:** {brand.GetFontFamily()}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating brand {Context}", context);
            return $"❌ Error: {ex.Message}";
        }
    }
}

