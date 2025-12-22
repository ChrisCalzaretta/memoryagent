using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AgentContracts.Requests;
using AgentContracts.Responses;
using CodingAgent.Server.Clients;
using CodingAgent.Server.Templates;

namespace CodingAgent.Server.Services;

/// <summary>
/// Manages background code generation jobs with persistence
/// </summary>
public interface IJobManager
{
    Task<string> StartJobAsync(string task, string? language, int maxIterations, string? workspacePath, CancellationToken ct);
    JobStatus? GetJobStatus(string jobId);
    Task CancelJobAsync(string jobId);
    List<JobStatus> ListJobs();
}

public class JobManager : IJobManager
{
    private readonly ConcurrentDictionary<string, JobState> _jobs = new();
    private readonly ICodeGenerationService _codeGeneration;
    private readonly IMultiModelThinkingService? _multiThinking;
    private readonly IMultiModelCodingService? _multiCoding;
    private readonly IAgenticCodingService? _agenticCoding;
    private readonly IValidationAgentClient _validation;
    private readonly IMemoryAgentClient? _memoryAgent;
    private readonly ILightningContextService? _lightning;
    private readonly IStubGenerator _stubGenerator;
    private readonly IProjectScaffolder _scaffolder;
    private readonly ICodebaseExplorer _codebaseExplorer;
    private readonly ITemplateService _templates;
    private readonly IDotnetScaffoldService _dotnetScaffold;
    private readonly DesignIntegrationService? _designIntegration;
    private readonly IPhi4ThinkingService? _phi4Thinking;
    private readonly string _jobsPath;
    private readonly ILogger<JobManager> _logger;

    public JobManager(
        ICodeGenerationService codeGeneration,
        IValidationAgentClient validation,
        IStubGenerator stubGenerator,
        IProjectScaffolder scaffolder,
        ICodebaseExplorer codebaseExplorer,
        ITemplateService templates,
        IDotnetScaffoldService dotnetScaffold,
        IConfiguration configuration,
        ILogger<JobManager> logger,
        IMultiModelThinkingService? multiThinking = null,
        IMultiModelCodingService? multiCoding = null,
        IAgenticCodingService? agenticCoding = null,
        IMemoryAgentClient? memoryAgent = null,
        ILightningContextService? lightning = null,
        DesignIntegrationService? designIntegration = null,
        IPhi4ThinkingService? phi4Thinking = null)
    {
        _codeGeneration = codeGeneration;
        _multiThinking = multiThinking;
        _multiCoding = multiCoding;
        _agenticCoding = agenticCoding;
        _validation = validation;
        _memoryAgent = memoryAgent;
        _lightning = lightning;
        _designIntegration = designIntegration;
        _phi4Thinking = phi4Thinking;
        _stubGenerator = stubGenerator;
        _scaffolder = scaffolder;
        _codebaseExplorer = codebaseExplorer;
        _templates = templates;
        _dotnetScaffold = dotnetScaffold;
        _jobsPath = configuration["JobPersistence:Path"] ?? "/data/jobs";
        _logger = logger;

        // Ensure directory exists
        Directory.CreateDirectory(_jobsPath);
        _logger.LogInformation("💾 Job persistence at {Path}", _jobsPath);
        
        // Clean up interrupted jobs from previous container restart
        CleanupInterruptedJobs();
        
        if (_agenticCoding != null)
        {
            _logger.LogInformation("🤖 Agentic coding enabled - LLMs can read files and search codebase!");
        }
        
        if (_memoryAgent != null)
        {
            _logger.LogInformation("🧠 MemoryAgent integration enabled - automatic indexing to Qdrant & Neo4j!");
        }
        
        if (_lightning != null)
        {
            _logger.LogInformation("⚡ AI Lightning enabled - full learning system active!");
        }
        
        if (_phi4Thinking != null)
        {
            _logger.LogInformation("🧠 Phi4 planning enabled - LLM will generate intelligent file-by-file plans!");
        }
    }

    /// <summary>
    /// On startup, mark any "running" jobs as "stopped" since they were interrupted by container restart
    /// </summary>
    private void CleanupInterruptedJobs()
    {
        try
        {
            var jobFiles = Directory.GetFiles(_jobsPath, "*.json");
            var stoppedCount = 0;
            
            foreach (var filePath in jobFiles)
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var jobStatus = JsonSerializer.Deserialize<JobStatus>(json);
                    
                    if (jobStatus != null && jobStatus.Status != null && 
                        (jobStatus.Status.Contains("running", StringComparison.OrdinalIgnoreCase) || 
                         jobStatus.Status.Contains("queued", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Mark as stopped
                        jobStatus.Status = "stopped (container restart)";
                        jobStatus.CompletedAt = DateTime.UtcNow;
                        jobStatus.Error = "Job was interrupted by container restart and did not complete";
                        
                        // Save updated status
                        var updatedJson = JsonSerializer.Serialize(jobStatus, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        });
                        File.WriteAllText(filePath, updatedJson);
                        
                        stoppedCount++;
                        _logger.LogInformation("🛑 Marked interrupted job as stopped: {JobId}", jobStatus.JobId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to process job file: {File}", Path.GetFileName(filePath));
                }
            }
            
            if (stoppedCount > 0)
            {
                _logger.LogInformation("✅ Cleaned up {Count} interrupted job(s)", stoppedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to cleanup interrupted jobs");
        }
    }

    public async Task<string> StartJobAsync(string task, string? language, int maxIterations, string? workspacePath, CancellationToken ct)
    {
        var jobId = $"job_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var jobState = new JobState
        {
            JobId = jobId,
            Task = task,
            Language = language,
            MaxIterations = maxIterations,
            Status = "running",
            Progress = 0,
            StartedAt = DateTime.UtcNow,
            CancellationTokenSource = cts
        };

        _jobs[jobId] = jobState;
        await PersistJobAsync(jobState);

        // Start background task with 10-ATTEMPT RETRY LOOP!
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("🚀 Job {JobId} started: {Task} (max {MaxIterations} attempts)", 
                    jobId, task, maxIterations);
                
                var jobWorkspacePath = Path.Combine(_jobsPath, jobId);
                Directory.CreateDirectory(jobWorkspacePath);
                
                // 🔍 STEP 0A: EXPLORE EXISTING CODEBASE (Like Claude!)
                // Convert USER workspace path (from parameter) to container path
                string? actualWorkspacePath = null;
                if (!string.IsNullOrEmpty(workspacePath))
                {
                    if (workspacePath.StartsWith("E:\\GitHub\\", StringComparison.OrdinalIgnoreCase) ||
                        workspacePath.StartsWith("E:/GitHub/", StringComparison.OrdinalIgnoreCase))
                    {
                        // E:\GitHub\testagent → /workspace/testagent
                        var normalized = workspacePath.Replace("\\", "/");
                        var relativePath = normalized.Substring("E:/GitHub/".Length);
                        actualWorkspacePath = $"/workspace/{relativePath}";
                        _logger.LogInformation("📁 Converted workspace path: {Original} → {Container}",
                            workspacePath, actualWorkspacePath);
                    }
                    else if (workspacePath.StartsWith("/workspace/"))
                    {
                        actualWorkspacePath = workspacePath;
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Unknown workspace path format: {Path}", workspacePath);
                    }
                }
                
                // ⚡ STEP 0A1: INITIALIZE AI LIGHTNING SESSION
                // Initialize variables that will be used throughout the method
                ValidationFeedback? feedback = null;
                BrandSystem? brandGuidelines = null;
                
                LightningContext? lightningContext = null;
                if (_lightning != null && !string.IsNullOrEmpty(actualWorkspacePath))
                {
                    try
                    {
                        _logger.LogInformation("⚡ Initializing AI Lightning session...");
                        lightningContext = await _lightning.InitializeSessionAsync(actualWorkspacePath, cts.Token);
                        _logger.LogInformation("✅ Lightning session active: {Context}", lightningContext.ContextName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Lightning initialization failed (non-fatal)");
                    }
                }
                
                // ⚡ STEP 0A2: CHECK FOR SIMILAR TASKS IN LIGHTNING
                List<QAPair>? similarTasks = null;
                if (_lightning != null)
                {
                    try
                    {
                        _logger.LogInformation("🔍 Checking Lightning for similar tasks...");
                        similarTasks = await _lightning.CheckSimilarTasksAsync(task, cts.Token);
                        
                        if (similarTasks.Any())
                        {
                            _logger.LogInformation("💡 Found {Count} similar tasks! Scores: {Scores}",
                                similarTasks.Count,
                                string.Join(", ", similarTasks.Select(t => t.Score)));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Similar task check failed (non-fatal)");
                    }
                }
                
                CodebaseContext? codebaseContext = null;
                if (!string.IsNullOrEmpty(actualWorkspacePath) && Directory.Exists(actualWorkspacePath))
                {
                    try
                    {
                        _logger.LogInformation("🔍 Exploring codebase at: {Path}", actualWorkspacePath);
                        codebaseContext = await _codebaseExplorer.ExploreAsync(actualWorkspacePath, cts.Token);
                        
                        if (!codebaseContext.IsEmpty)
                        {
                            _logger.LogInformation("✅ Codebase explored: {Files} files, {Dirs} dirs, {Namespaces} namespaces",
                                codebaseContext.Files.Count, codebaseContext.Directories.Count, codebaseContext.Namespaces.Count);
                        }
                        else
                        {
                            _logger.LogInformation("📁 Empty workspace - will create new project");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Failed to explore codebase, continuing without context");
                    }
                }
                
                // 🏗️ STEP 0B: AUTO-SCAFFOLD PROJECT STRUCTURE (Using Templates!)
                // Use pre-built templates (no SDK needed!)
                // Only for NEW projects (not modifications)
                ScaffoldResult? scaffoldResult = null;
                var taskLower = task.ToLowerInvariant();
                var isNewProject = taskLower.Contains("create") || taskLower.Contains("new") || 
                                  taskLower.Contains("complete") || taskLower.Contains("project");
                var isModification = taskLower.Contains("add") || taskLower.Contains("modify") || 
                                    taskLower.Contains("update") || taskLower.Contains("fix") ||
                                    taskLower.Contains("change");
                
                // 🔍 FIX: Allow scaffolding for "Create" tasks even if workspace has files
                // If task explicitly says "create", prioritize that over existing files
                var forceScaffold = taskLower.StartsWith("create") || 
                                   (taskLower.Contains("create new") || taskLower.Contains("create a"));
                
                if (isNewProject && !isModification && (forceScaffold || codebaseContext == null || codebaseContext.IsEmpty))
                {
                    try
                    {
                        _logger.LogInformation("🏗️ Detected new project request - using Docker-based scaffolding...");
                        
                        // Detect project type from task
                        var projectType = _dotnetScaffold.DetectProjectType(task);
                        var projectName = "GeneratedApp";
                        
                        // Run dotnet new inside Docker container with SDK
                        var dotnetResult = await _dotnetScaffold.ScaffoldProjectAsync(projectType, projectName, cts.Token);
                        
                        if (dotnetResult.Success && dotnetResult.Files.Count > 0)
                        {
                            _logger.LogInformation("✨ Scaffolded using Docker: {Template} ({FileCount} files)",
                                dotnetResult.TemplateName, dotnetResult.Files.Count);
                            
                            // Convert to ScaffoldResult format
                            scaffoldResult = new ScaffoldResult
                            {
                                Success = true,
                                ProjectType = dotnetResult.ProjectType,
                                ProjectPath = "",
                                Command = dotnetResult.Command,
                                Files = dotnetResult.Files
                            };
                        }
                        
                        if (scaffoldResult != null && scaffoldResult.Success && scaffoldResult.Files.Count > 0)
                        {
                            _logger.LogInformation("✅ Scaffolded {ProjectType} project with {FileCount} files using: {Command}",
                                scaffoldResult.ProjectType, scaffoldResult.Files.Count, scaffoldResult.Command);
                            
                            // Create SMART SUMMARY for LLM
                            // Show FULL content of key modifiable files, list others
                            var scaffoldSummary = new System.Text.StringBuilder();
                            scaffoldSummary.AppendLine($"✅ Scaffolded {scaffoldResult.ProjectType} project with {scaffoldResult.Files.Count} files");
                            scaffoldSummary.AppendLine();
                            
                            // Key files that LLM might need to modify (show full content)
                            var keyFiles = new[] { "Program.cs", "Startup.cs", "_Imports.razor", "appsettings.json", "App.razor" };
                            scaffoldSummary.AppendLine("📄 KEY FILES (full content - you CAN modify these if needed):");
                            scaffoldSummary.AppendLine();
                            
                            foreach (var keyFile in keyFiles)
                            {
                                var file = scaffoldResult.Files.FirstOrDefault(f => f.Path.EndsWith(keyFile, StringComparison.OrdinalIgnoreCase));
                                if (file != null)
                                {
                                    scaffoldSummary.AppendLine($"--- {file.Path} ---");
                                    // Truncate if too long (max 100 lines)
                                    var lines = file.Content.Split('\n');
                                    var preview = string.Join("\n", lines.Take(100));
                                    if (lines.Length > 100) preview += $"\n... ({lines.Length - 100} more lines)";
                                    scaffoldSummary.AppendLine(preview);
                                    scaffoldSummary.AppendLine($"--- END {file.Path} ---");
                                    scaffoldSummary.AppendLine();
                                }
                            }
                            
                            // List other files (don't show content)
                            var otherFiles = scaffoldResult.Files.Where(f => 
                                !keyFiles.Any(kf => f.Path.EndsWith(kf, StringComparison.OrdinalIgnoreCase))).ToList();
                            
                            if (otherFiles.Any())
                            {
                                scaffoldSummary.AppendLine($"📁 OTHER SCAFFOLDED FILES ({otherFiles.Count} files - don't regenerate unless needed):");
                                foreach (var file in otherFiles.Take(20))
                                {
                                    scaffoldSummary.AppendLine($"  - {file.Path}");
                                }
                                if (otherFiles.Count > 20)
                                {
                                    scaffoldSummary.AppendLine($"  ... and {otherFiles.Count - 20} more files");
                                }
                                scaffoldSummary.AppendLine();
                            }
                            
                            scaffoldSummary.AppendLine("🎯 YOUR TASK: " + task);
                            scaffoldSummary.AppendLine();
                            scaffoldSummary.AppendLine("✅ YOU CAN:");
                            scaffoldSummary.AppendLine("1. Generate NEW files (game logic, UI components, styling)");
                            scaffoldSummary.AppendLine("2. MODIFY key files above (e.g., add services to Program.cs, update _Imports.razor)");
                            scaffoldSummary.AppendLine("3. OVERRIDE any scaffolded file by generating it with the same path");
                            scaffoldSummary.AppendLine();
                            scaffoldSummary.AppendLine("❌ DON'T:");
                            scaffoldSummary.AppendLine("- Regenerate unchanged boilerplate files");
                            scaffoldSummary.AppendLine("- Copy/paste scaffolded files without modifications");
                            
                            feedback = new ValidationFeedback
                            {
                                Score = 5, // Partial credit for scaffolding
                                Issues = new List<ValidationIssue>
                                {
                                    new ValidationIssue
                                    {
                                        Severity = "info",
                                        Message = "Project scaffolded successfully",
                                        Suggestion = scaffoldSummary.ToString(),
                                        Rule = "scaffolding"
                                    }
                                }
                            };
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️ No scaffolding applied - LLM will generate complete project");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Scaffolding failed, falling back to full LLM generation");
                    }
                }
                else
                {
                    _logger.LogInformation("ℹ️ Modification request detected - skipping scaffolding");
                }
                
                // ⚡ INJECT SIMILAR TASK INFO FROM LIGHTNING
                if (similarTasks != null && similarTasks.Any())
                {
                    feedback ??= new ValidationFeedback { Score = 0, Issues = new List<ValidationIssue>() };
                    
                    var similarTaskInfo = new System.Text.StringBuilder();
                    similarTaskInfo.AppendLine("⚡ LIGHTNING FOUND SIMILAR TASKS!");
                    similarTaskInfo.AppendLine();
                    similarTaskInfo.AppendLine("AI Lightning has seen similar tasks before. Learn from these:");
                    similarTaskInfo.AppendLine();
                    
                    foreach (var similar in similarTasks.Take(3)) // Top 3 similar
                    {
                        similarTaskInfo.AppendLine($"## Similar Task (Score: {similar.Score}/10)");
                        similarTaskInfo.AppendLine($"Question: {similar.Question}");
                        similarTaskInfo.AppendLine($"Timestamp: {similar.Timestamp:yyyy-MM-dd}");
                        similarTaskInfo.AppendLine();
                        
                        if (similar.Score >= 8)
                        {
                            similarTaskInfo.AppendLine("✅ This worked well! Consider reusing patterns.");
                        }
                        else
                        {
                            similarTaskInfo.AppendLine("⚠️ This had issues. Avoid similar approaches.");
                        }
                        similarTaskInfo.AppendLine();
                    }
                    
                    feedback.Issues.Insert(0, new ValidationIssue
                    {
                        Severity = "info",
                        Message = "💡 Lightning Learning: Similar tasks found",
                        Suggestion = similarTaskInfo.ToString(),
                        Rule = "lightning_similar_tasks"
                    });
                    
                    _logger.LogInformation("💡 Added {Count} similar tasks to LLM context", similarTasks.Count);
                }
                
                // 📋 STEP 1: CREATE PROJECT PLAN USING PHI4 (LLM-generated, not hardcoded!)
                List<ProjectStep>? projectPlan = null;
                CodingAgent.Server.Services.ProjectPlan? phi4Plan = null;
                
                try
                {
                    _logger.LogInformation("📋 Creating project plan for job {JobId}...", jobId);
                    
                    // 🧠 STEP 1A: Use Phi4 to generate SMART file-by-file plan (if available)
                    if (_phi4Thinking != null && _templates != null)
                    {
                        try
                        {
                            _logger.LogInformation("🧠 Phi4 generating initial project plan...");
                            
                            // Detect template based on task
                            var templateType = DetectTemplateType(task, language ?? "csharp");
                            var template = _templates.GetTemplateById(templateType);
                            
                            if (template != null)
                            {
                                phi4Plan = await _phi4Thinking.PlanProjectAsync(
                                    task, 
                                    template, 
                                    codebaseContext?.KeyFileContents ?? new Dictionary<string, string>(),
                                    cts.Token);
                                
                                _logger.LogInformation("✅ Phi4 generated plan: {FileCount} files, complexity {Complexity}/10",
                                    phi4Plan.TotalFiles, phi4Plan.EstimatedComplexity);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Phi4 planning failed, falling back to generic estimator");
                        }
                    }
                    
                    // 🤝 STEP 1B: MULTI-MODEL DEBATE ON THE PLAN (Strategic collaboration!)
                    if (phi4Plan != null && _multiThinking != null)
                    {
                        try
                        {
                            _logger.LogInformation("🤝 Multi-model strategic debate on project plan...");
                            
                            var planSummary = $"Project: {phi4Plan.ProjectName}\n" +
                                             $"Files: {string.Join(", ", phi4Plan.Files.Select(f => f.FileName))}\n" +
                                             $"Complexity: {phi4Plan.EstimatedComplexity}/10\n" +
                                             $"Risks: {string.Join(", ", phi4Plan.Risks ?? new List<string>())}";
                            
                            var strategicThinkingContext = new ThinkingContext
                            {
                                TaskDescription = $"Review and improve this project plan:\n\n{planSummary}\n\nOriginal task: {task}",
                                FilePath = "ProjectPlan",
                                Language = language ?? "csharp",
                                ExistingFiles = codebaseContext?.KeyFileContents ?? new Dictionary<string, string>(),
                                PreviousAttempts = new List<AttemptSummary>(),
                                BrandGuidelines = brandGuidelines
                            };
                            
                            var strategicThinking = await _multiThinking.ThinkSmartAsync(strategicThinkingContext, 1, cts.Token);
                            
                            _logger.LogInformation("✅ Strategic planning complete: {Strategy}, approach: {Approach}",
                                strategicThinking.Strategy, strategicThinking.Approach?.Substring(0, Math.Min(100, strategicThinking.Approach?.Length ?? 0)));
                            
                            // TODO: Parse strategicThinking.FinalRecommendation to refine plan
                            // For now, we use Phi4's plan as-is
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Multi-model strategic planning failed (using Phi4 plan as-is)");
                        }
                    }
                    
                    // 📊 STEP 1C: Convert to ProjectStep format
                    if (phi4Plan != null)
                    {
                        // Convert Phi4's file-by-file plan to todos
                        projectPlan = phi4Plan.Files.Select((file, idx) => new ProjectStep
                        {
                            Description = $"{file.FileName} - {file.Purpose}",
                            Priority = file.Priority,
                            Complexity = file.EstimatedComplexity,
                            Type = DetermineStepType(file.FileName),
                            Status = "pending"
                        }).ToList();
                    }
                    else
                    {
                        // Fallback to generic estimator if Phi4 unavailable
                        _logger.LogInformation("📊 Using generic project estimator (Phi4 not available)");
                        projectPlan = EstimateProjectSteps(task, language ?? "csharp", scaffoldResult);
                    }
                    
                    // Store in job state (primary source of truth)
                    jobState.ProjectPlan = projectPlan;
                    await PersistJobAsync(jobState);
                    
                    _logger.LogInformation("✅ Created plan with {StepCount} steps", projectPlan.Count);
                    
                    // Also send to MemoryAgent for learning (optional, non-blocking)
                    if (_memoryAgent != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _memoryAgent.CallMcpToolAsync("manage_plan", new Dictionary<string, object>
                                {
                                    ["action"] = "create",
                                    ["context"] = jobId,
                                    ["plan"] = new
                                    {
                                        title = task,
                                        language = language ?? "csharp",
                                        steps = projectPlan.Select((step, idx) => new
                                        {
                                            id = $"step_{idx}",
                                            description = step.Description,
                                            priority = step.Priority,
                                            complexity = step.Complexity,
                                            status = "pending",
                                            type = step.Type
                                        }).ToList()
                                    }
                                }, cts.Token);
                                
                                _logger.LogInformation("✅ Plan synced to MemoryAgent");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "⚠️ Failed to sync plan to MemoryAgent (non-fatal)");
                            }
                        }, cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to create project plan (non-fatal)");
                }
                
                // 🎨 STEP 0.5: DESIGN INTEGRATION (for UI tasks)
                if (_designIntegration != null)
                {
                    var isUITask = _designIntegration.IsUICode(task, language ?? "csharp");
                    if (isUITask)
                    {
                        _logger.LogInformation("🎨 UI task detected - creating brand guidelines using LLM...");
                        var workspaceContext = workspacePath != null 
                            ? Path.GetFileName(workspacePath) 
                            : "default";
                        brandGuidelines = await _designIntegration.GetOrCreateBrandAsync(
                            task, 
                            language ?? "csharp",
                            workspaceContext,
                            cts.Token);
                        
                        if (brandGuidelines != null)
                        {
                            _logger.LogInformation("✅ Brand guidelines ready: {Name} ({Primary}/{Secondary})", 
                                brandGuidelines.Name, 
                                brandGuidelines.Colors.Primary,
                                brandGuidelines.Colors.Secondary);
                        }
                    }
                }
                
                // 🔄 FILE-BY-FILE GENERATION LOOP (Following Phi4's plan!)
                // If we have a plan, generate files one at a time in order
                // Otherwise, fall back to all-at-once retry loop
                bool useFileByFileMode = (projectPlan != null && projectPlan.Count > 0 && projectPlan.Count <= 20);
                GenerateCodeResponse? lastResult = null;
                
                if (useFileByFileMode)
                {
                    _logger.LogInformation("📋 FILE-BY-FILE MODE: Generating {Count} files in order", projectPlan.Count);
                    lastResult = await GenerateFilesOneByOneAsync(
                        jobId, jobState, projectPlan, task, language, 
                        actualWorkspacePath, jobWorkspacePath, codebaseContext, 
                        scaffoldResult, brandGuidelines, maxIterations, cts.Token);
                }
                else
                {
                    _logger.LogInformation("🔄 ALL-AT-ONCE MODE: Generating all files together (fallback)");
                }
                
                // 🔄 FALLBACK: ALL-AT-ONCE RETRY LOOP (for when plan is unavailable or too large)
                if (!useFileByFileMode)
                {
                    for (int iteration = 1; iteration <= maxIterations; iteration++)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    
                    jobState.Progress = (iteration * 90) / maxIterations;
                    jobState.Status = $"running (attempt {iteration}/{maxIterations})";
                    await PersistJobAsync(jobState);
                    
                    _logger.LogInformation("🔄 Job {JobId} - Attempt {Iteration}/{MaxIterations}", 
                        jobId, iteration, maxIterations);
                    
                    try
                    {
                        // 🔍 INJECT CODEBASE CONTEXT INTO FEEDBACK (Critical for Claude-like generation!)
                        if (codebaseContext != null && !codebaseContext.IsEmpty)
                        {
                            if (feedback == null)
                            {
                                feedback = new ValidationFeedback { Score = 0, Issues = new List<ValidationIssue>() };
                            }
                            
                            // Add codebase context as first issue so LLMs see it
                            var contextIssue = new ValidationIssue
                            {
                                Severity = "info",
                                Message = "📁 EXISTING CODEBASE CONTEXT (Read this carefully before generating code!)",
                                Suggestion = codebaseContext.ToLLMSummary(),
                                Rule = "codebase_context"
                            };
                            
                            // Only add if not already present
                            if (!feedback.Issues.Any(i => i.Rule == "codebase_context"))
                            {
                                feedback.Issues.Insert(0, contextIssue);
                                _logger.LogInformation("📋 Injected codebase context: {Files} files, {Namespaces} namespaces",
                                    codebaseContext.Files.Count, codebaseContext.Namespaces.Count);
                            }
                        }
                        
                        // Brand guidelines already initialized at the start (before mode branching)
                        
                        // 🧠 STEP 1: MULTI-MODEL THINKING (if available and attempt <= 8)
                        if (_multiThinking != null && iteration <= 8)
                        {
                            _logger.LogInformation("🧠 Multi-model thinking for attempt {Iteration}...", iteration);
                            
                            var thinkingContext = new ThinkingContext
                            {
                                TaskDescription = task,
                                FilePath = "Generated",
                                Language = language ?? "csharp",
                                ExistingFiles = new Dictionary<string, string>(),
                                PreviousAttempts = feedback?.History?
                                    .Select(h => new AttemptSummary
                                    {
                                        AttemptNumber = h.AttemptNumber,
                                        Model = h.Model,
                                        Score = h.Score,
                                        Issues = h.Issues.Select(i => $"{i.Severity}: {i.Message}").ToArray()
                                    })
                                    .ToList() ?? new(),
                                LatestBuildErrors = feedback?.BuildErrors,
                                LatestValidationScore = feedback?.Score,
                                LatestValidationIssues = feedback?.Issues
                                    .Select(i => $"{i.Severity}: {i.Message}")
                                    .ToArray() ?? Array.Empty<string>(),
                                LatestValidationSummary = feedback?.Summary,
                                BrandGuidelines = brandGuidelines // 🎨 NEW: Inject brand guidelines!
                            };
                            
                            var thinking = await _multiThinking.ThinkSmartAsync(thinkingContext, iteration, cts.Token);
                            
                            _logger.LogInformation("✅ Thinking complete: {Strategy} strategy, {Models} model(s), confidence {Confidence:P0}",
                                thinking.Strategy, thinking.ParticipatingModels.Count, thinking.Confidence);
                            
                            jobState.Status = $"running (attempt {iteration}/{maxIterations}) - {thinking.Strategy} thinking";
                            await PersistJobAsync(jobState);
                        }
                        
                        // 💻 STEP 2: CODE GENERATION (Priority: Agentic → Multi-Model → Single-Model)
                        GenerateCodeResponse? codeResult = null;
                        bool useMultiModel = false;
                        
                        // 🤖 OPTION 1: AGENTIC CODING (BEST - LLM can explore codebase with tools!)
                        if (_agenticCoding != null && iteration <= 5) // Use agentic for early attempts
                        {
                            _logger.LogInformation("🤖 AGENTIC coding for attempt {Iteration} - LLM can read files & search!", iteration);
                            
                            jobState.Status = $"running (attempt {iteration}/{maxIterations}) - agentic (tool-augmented)";
                            await PersistJobAsync(jobState);
                            
                            var agenticResult = await _agenticCoding.GenerateWithToolsAsync(
                                task: task,
                                language: language,
                                workspacePath: actualWorkspacePath,
                                jobWorkspacePath: jobWorkspacePath,
                                codebaseContext: codebaseContext,
                                previousFeedback: feedback, // 📜 Pass full history!
                                cancellationToken: cts.Token);
                            
                            if (agenticResult.Success)
                            {
                                codeResult = new GenerateCodeResponse
                                {
                                    FileChanges = agenticResult.GeneratedFiles,
                                    ModelUsed = "agentic-qwen2.5-coder:14b",
                                    Success = true
                                };
                                
                                _logger.LogInformation("✅ Agentic coding complete: {Iterations} iterations, {ToolCalls} tool calls, {Files} files",
                                    agenticResult.Iterations, agenticResult.ToolCallsExecuted, agenticResult.GeneratedFiles.Count);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ Agentic coding failed: {Error}, falling back to multi-model", agenticResult.Error);
                                useMultiModel = true; // Fall through to next option
                            }
                        }
                        
                        // 🧠 OPTION 2: MULTI-MODEL CODING (GOOD - parallel/collaborative models)
                        if ((codeResult == null && _multiCoding != null) || useMultiModel)
                        {
                            _logger.LogInformation("💻 Multi-model coding for attempt {Iteration}...", iteration);
                            
                            var multiRequest = new GenerateCodeRequest
                            {
                                Task = task,
                                Language = language,
                                WorkspacePath = workspacePath,
                                PreviousFeedback = feedback
                            };
                            
                            var multiResult = await _multiCoding.GenerateSmartAsync(multiRequest, iteration, "", cts.Token);
                            
                            codeResult = multiResult.ToResponse(multiResult.ParticipatingModels.FirstOrDefault() ?? "multi-model");
                            
                            _logger.LogInformation("✅ Coding complete: {Strategy} strategy, {Models} model(s), {Files} files",
                                multiResult.Strategy, multiResult.ParticipatingModels.Count, multiResult.FileChanges.Count);
                            
                            jobState.Status = $"running (attempt {iteration}/{maxIterations}) - {multiResult.Strategy} coding";
                        }
                        
                        // 🔄 OPTION 3: SINGLE-MODEL FALLBACK (last resort)
                        if (codeResult == null)
                        {
                            _logger.LogWarning("⚠️ No advanced coding services available, using single-model fallback");
                            var request = new GenerateCodeRequest
                            {
                                Task = task,
                                Language = language,
                                WorkspacePath = workspacePath,
                                PreviousFeedback = feedback
                            };
                            
                            codeResult = await _codeGeneration.GenerateAsync(request, cts.Token);
                        }
                        
                        lastResult = codeResult;
                        
                        // 🔀 MERGE: Scaffolded files + LLM-generated files
                        var allFiles = new List<CodeFile>();
                        
                        // Add scaffolded files first (baseline project structure)
                        if (scaffoldResult?.Files != null)
                        {
                            allFiles.AddRange(scaffoldResult.Files.Select(f => new CodeFile
                            {
                                Path = f.Path,
                                Content = f.Content
                            }));
                            _logger.LogInformation("📦 Including {Count} scaffolded files", scaffoldResult.Files.Count);
                        }
                        
                        // Add/override with LLM-generated files (task-specific code)
                        foreach (var llmFile in lastResult.FileChanges)
                        {
                            var existing = allFiles.FirstOrDefault(f => f.Path == llmFile.Path);
                            if (existing != null)
                            {
                                // Override scaffolded file with LLM version
                                existing.Content = llmFile.Content;
                                _logger.LogDebug("🔄 Overriding scaffolded file: {Path}", llmFile.Path);
                            }
                            else
                            {
                                // New file from LLM
                                allFiles.Add(new CodeFile
                                {
                                    Path = llmFile.Path,
                                    Content = llmFile.Content
                                });
                            }
                        }
                        
                        _logger.LogInformation("📊 Total files for validation: {Total} (scaffolded: {Scaffolded}, LLM: {LLM})",
                            allFiles.Count, scaffoldResult?.Files.Count ?? 0, lastResult.FileChanges.Count);
                        
                        // 📋 UPDATE PLAN: Mark code generation step as in-progress
                        if (iteration == 1 && jobState.ProjectPlan != null && jobState.ProjectPlan.Count > 1)
                        {
                            jobState.ProjectPlan[1].Status = "in_progress"; // Core logic step
                            await PersistJobAsync(jobState);
                        }
                        
                        // ✅ STEP 3: VALIDATE the generated code (with task alignment check!)
                        var validation = await _validation.ValidateAsync(new ValidateCodeRequest
                        {
                            Files = allFiles,
                            Context = "codegen",
                            Language = language ?? "csharp",
                            WorkspacePath = workspacePath,
                            OriginalTask = task // 🎯 CRITICAL: Pass task for alignment validation!
                        }, cts.Token);
                    
                        _logger.LogInformation("📊 Job {JobId} - Validation score: {Score}/10 ({IssueCount} issues)", 
                            jobId, validation.Score, validation.Issues.Count);
                        
                        // 💾 STEP 3.5: WRITE FILES TO JOB WORKSPACE & INDEX IN MEMORY AGENT
                        // This allows future iterations to find these files via search_codebase()
                        try
                        {
                            // Write all files to job workspace
                            foreach (var file in allFiles)
                            {
                                var filePath = Path.Combine(jobWorkspacePath, file.Path);
                                var fileDir = Path.GetDirectoryName(filePath);
                                if (!string.IsNullOrEmpty(fileDir))
                                {
                                    Directory.CreateDirectory(fileDir);
                                }
                                await File.WriteAllTextAsync(filePath, file.Content, cts.Token);
                            }
                            
                            _logger.LogInformation("💾 Wrote {Count} files to job workspace: {Path}", 
                                allFiles.Count, jobWorkspacePath);
                            
                            // 🧠 INDEX IN MEMORY AGENT (Qdrant + Neo4j)
                            if (_memoryAgent != null)
                            {
                                await IndexGeneratedFilesAsync(allFiles, jobWorkspacePath, cts.Token);
                            }
                            else
                            {
                                _logger.LogWarning("⚠️ MemoryAgent not available - skipping indexing (files won't be searchable!)");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "⚠️ Failed to write/index files (non-fatal, continuing)");
                        }
                        
                           // 🎯 SMART BREAK LOGIC - Your exact specs!
                           // Break at 8+ (excellent)
                           if (validation.Score >= 8)
                           {
                               _logger.LogInformation("✅ Job {JobId} - EXCELLENT score {Score}/10 on attempt {Iteration}!",
                                   jobId, validation.Score, iteration);
                               
                               // 📁 AUTO-WRITE: Copy files to workspace/generated/{timestamp}_{task-name}/ folder
                               try
                               {
                                   var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                                   var sanitizedTask = SanitizeTaskName(task);
                                   var generatedFolderName = $"{timestamp}_{sanitizedTask}";
                                   
                                   // Convert Windows path to container path: E:\GitHub\testagent → /workspace/testagent
                                   string generatedBasePath;
                                   if (!string.IsNullOrEmpty(workspacePath))
                                   {
                                       // Convert Windows path to Linux container path
                                       // E:\GitHub\testagent → /workspace/testagent
                                       var normalizedPath = workspacePath.Replace("\\", "/");
                                       if (normalizedPath.StartsWith("E:/GitHub/", StringComparison.OrdinalIgnoreCase))
                                       {
                                           var relativePath = normalizedPath.Substring("E:/GitHub/".Length);
                                           generatedBasePath = $"/workspace/{relativePath}/generated";
                                       }
                                       else if (normalizedPath.StartsWith("/workspace/"))
                                       {
                                           generatedBasePath = $"{normalizedPath}/generated";
                                       }
                                       else
                                       {
                                           // Fallback: assume it's under /workspace
                                           generatedBasePath = $"/workspace/{Path.GetFileName(workspacePath)}/generated";
                                       }
                                   }
                                   else
                                   {
                                       // Default: current working directory or /workspace/MemoryAgent
                                       generatedBasePath = "/workspace/MemoryAgent/generated";
                                       _logger.LogWarning("⚠️ No workspacePath provided, defaulting to {Path}", generatedBasePath);
                                   }
                                   
                                   var generatedPath = Path.Combine(generatedBasePath, generatedFolderName);
                                   
                                   Directory.CreateDirectory(generatedPath);
                                   
                                   // Write ALL files (scaffolded + LLM-generated)
                                   foreach (var file in allFiles)
                                   {
                                       var filePath = Path.Combine(generatedPath, file.Path);
                                       var fileDir = Path.GetDirectoryName(filePath);
                                       if (!string.IsNullOrEmpty(fileDir))
                                       {
                                           Directory.CreateDirectory(fileDir);
                                       }
                                       await File.WriteAllTextAsync(filePath, file.Content, cts.Token);
                                   }
                                   
                                   _logger.LogInformation("📁 Auto-wrote {FileCount} files to: {Path} (workspace: {Workspace}, scaffolded: {Scaffolded}, LLM: {LLM})",
                                       allFiles.Count, generatedPath, workspacePath ?? "default", scaffoldResult?.Files.Count ?? 0, lastResult.FileChanges.Count);
                               }
                               catch (Exception ex)
                               {
                                   _logger.LogError(ex, "⚠️ Failed to auto-write files to generated folder (non-fatal)");
                               }
                               
                               // 📋 UPDATE PLAN: Mark all steps as completed
                               if (jobState.ProjectPlan != null)
                               {
                                   try
                                   {
                                       _logger.LogInformation("📋 Marking all plan steps as completed...");
                                       
                                       foreach (var step in jobState.ProjectPlan)
                                       {
                                           step.Status = "completed";
                                       }
                                       
                                       await PersistJobAsync(jobState);
                                       _logger.LogInformation("✅ All {Count} plan steps marked as completed", jobState.ProjectPlan.Count);
                                   }
                                   catch (Exception ex)
                                   {
                                       _logger.LogWarning(ex, "⚠️ Failed to update plan completion (non-fatal)");
                                   }
                               }
                               
                               // ⚡ RECORD SUCCESSFUL GENERATION IN LIGHTNING
                               if (_lightning != null)
                               {
                                   try
                                   {
                                       _logger.LogInformation("⚡ Recording successful generation in Lightning...");
                                       
                                       await _lightning.RecordSuccessfulGenerationAsync(
                                           task: task,
                                           files: allFiles.Select(f => new AgentContracts.Responses.FileChange 
                                           { 
                                               Path = f.Path, 
                                               Content = f.Content,
                                               Type = FileChangeType.Created
                                           }).ToList(),
                                           score: validation.Score,
                                           language: language ?? "csharp",
                                           metadata: new Dictionary<string, object>
                                           {
                                               ["iterations"] = iteration,
                                               ["model"] = lastResult.ModelUsed,
                                               ["scaffolded_files"] = scaffoldResult?.Files.Count ?? 0,
                                               ["llm_files"] = lastResult.FileChanges.Count,
                                               ["total_files"] = allFiles.Count,
                                               ["validation_score"] = validation.Score,
                                               ["timestamp"] = DateTime.UtcNow
                                           },
                                           cancellationToken: cts.Token
                                       );
                                       
                                       _logger.LogInformation("✅ Lightning learned from this success!");
                                   }
                                   catch (Exception ex)
                                   {
                                       _logger.LogError(ex, "❌ Failed to record in Lightning (this is BAD - no learning!)");
                                   }
                               }
                               
                               break;
                           }
                        
                        // Break at 6.5+ after attempt 3 (good enough)
                        if (validation.Score >= 6.5 && iteration >= 3)
                        {
                            _logger.LogWarning("⚠️ Job {JobId} - ACCEPTABLE score {Score}/10 on attempt {Iteration} - stopping", 
                                jobId, validation.Score, iteration);
                            break;
                        }
                        
                        // Break after 10 attempts (something is wrong)
                        if (iteration >= maxIterations)
                        {
                            _logger.LogError("🚨 Job {JobId} - CRITICAL: Score {Score}/10 after {Iterations} attempts!", 
                                jobId, validation.Score, iteration);
                            break;
                        }
                        
                        // Prepare feedback for next iteration with COMPLETE HISTORY tracking
                        var attemptHistory = new AttemptHistory
                        {
                            AttemptNumber = iteration,
                            Model = lastResult.ModelUsed,
                            Score = validation.Score,
                            Issues = validation.Issues,
                            BuildErrors = validation.BuildErrors,
                            Summary = validation.Summary,
                            Timestamp = DateTime.UtcNow,
                            // 🔥 NEW: Store FULL generated files (not just snippet)
                            GeneratedFiles = allFiles.Select(f => new CodeFile { Path = f.Path, Content = f.Content }).ToList(),
                            // 🔥 NEW: Store compilation output
                            CompilationOutput = validation.BuildErrors
                        };
                        
                        feedback = validation.ToFeedback();
                        feedback.TriedModels.Add(lastResult.ModelUsed);
                        feedback.History ??= new List<AttemptHistory>();
                        feedback.History.Add(attemptHistory);
                        
                        _logger.LogInformation("⚠️ Job {JobId} - Score {Score}/10 on attempt {Iteration}, retrying...", 
                            jobId, validation.Score, iteration);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Job {JobId} - Error during attempt {Iteration}", jobId, iteration);
                        
                        // Continue trying unless all attempts exhausted
                        if (iteration >= maxIterations)
                        {
                            lastResult = new GenerateCodeResponse
                            {
                                Success = false,
                                Error = $"Failed after {maxIterations} attempts: {ex.Message}",
                                FileChanges = new List<FileChange>()
                            };
                            break;
                        }
                    }
                }
                } // End of fallback all-at-once mode

                jobState.Status = lastResult?.Success == true ? "completed" : "failed";
                jobState.Progress = 100;
                
                // 🔍 FIX: Include ALL files (scaffolded + LLM) in result, not just LLM files
                if (lastResult != null && scaffoldResult?.Files != null && scaffoldResult.Files.Count > 0)
                {
                    // Merge scaffolded files with LLM files for complete result
                    var allResultFiles = new List<FileChange>();
                    
                    // Add scaffolded files first
                    allResultFiles.AddRange(scaffoldResult.Files.Select(f => new FileChange
                    {
                        Path = f.Path,
                        Content = f.Content,
                        Type = FileChangeType.Created
                    }));
                    
                    // Override/add LLM-generated files
                    foreach (var llmFile in lastResult.FileChanges)
                    {
                        var existing = allResultFiles.FirstOrDefault(f => f.Path.Equals(llmFile.Path, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.Content = llmFile.Content;
                        }
                        else
                        {
                            allResultFiles.Add(llmFile);
                        }
                    }
                    
                    // Update result with ALL files
                    lastResult = new GenerateCodeResponse
                    {
                        Success = lastResult.Success,
                        FileChanges = allResultFiles,
                        Explanation = lastResult.Explanation,
                        Error = lastResult.Error,
                        ModelUsed = lastResult.ModelUsed
                    };
                    
                    _logger.LogInformation("📦 Result includes {Total} files ({Scaffolded} scaffolded + {LLM} generated)",
                        allResultFiles.Count, scaffoldResult.Files.Count, lastResult.FileChanges.Count - scaffoldResult.Files.Count);
                }
                
                jobState.Result = lastResult;
                jobState.CompletedAt = DateTime.UtcNow;
                jobState.Error = lastResult?.Error;

                await PersistJobAsync(jobState);
                _logger.LogInformation("✅ Job {JobId} completed: {Status}", jobId, jobState.Status);
            }
            catch (OperationCanceledException)
            {
                jobState.Status = "cancelled";
                jobState.CompletedAt = DateTime.UtcNow;
                await PersistJobAsync(jobState);
                _logger.LogInformation("🛑 Job {JobId} cancelled", jobId);
            }
            catch (Exception ex)
            {
                jobState.Status = "failed";
                jobState.Error = ex.Message;
                jobState.CompletedAt = DateTime.UtcNow;
                await PersistJobAsync(jobState);
                _logger.LogError(ex, "❌ Job {JobId} failed", jobId);
            }
        }, cts.Token);

        return jobId;
    }

    public JobStatus? GetJobStatus(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            return new JobStatus
            {
                JobId = state.JobId,
                Task = state.Task,
                Status = state.Status,
                Progress = state.Progress,
                StartedAt = state.StartedAt,
                CompletedAt = state.CompletedAt,
                Error = state.Error,
                Result = state.Result,
                Todos = state.ProjectPlan?.Select((step, idx) => new ProjectTodo
                {
                    Id = $"step_{idx}",
                    Description = step.Description,
                    Priority = step.Priority,
                    Complexity = step.Complexity,
                    Type = step.Type,
                    Status = step.Status
                }).ToList()
            };
        }

        // Try to load from disk
        var filePath = Path.Combine(_jobsPath, $"{jobId}.json");
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<JobStatus>(json);
            }
            catch
            {
                // Ignore
            }
        }

        return null;
    }

    public async Task CancelJobAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var state))
        {
            state.CancellationTokenSource.Cancel();
            state.Status = "cancelled";
            state.CompletedAt = DateTime.UtcNow;
            await PersistJobAsync(state);
        }
    }

    public List<JobStatus> ListJobs()
    {
        return _jobs.Values.Select(s => new JobStatus
        {
            JobId = s.JobId,
            Task = s.Task,
            Status = s.Status,
            Progress = s.Progress,
            StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt,
            Error = s.Error,
            Result = s.Result
        }).ToList();
    }

    private async Task PersistJobAsync(JobState state)
    {
        try
        {
            var status = new JobStatus
            {
                JobId = state.JobId,
                Task = state.Task,
                Status = state.Status,
                Progress = state.Progress,
                StartedAt = state.StartedAt,
                CompletedAt = state.CompletedAt,
                Error = state.Error,
                Result = state.Result
            };

            var json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
            var filePath = Path.Combine(_jobsPath, $"{state.JobId}.json");
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist job {JobId}", state.JobId);
        }
    }

    /// <summary>
    /// Index generated files in MemoryAgent (Qdrant + Neo4j) so they're searchable
    /// </summary>
    private async Task IndexGeneratedFilesAsync(List<CodeFile> files, string workspacePath, CancellationToken cancellationToken)
    {
        if (_memoryAgent == null)
        {
            return;
        }
        
        try
        {
            _logger.LogInformation("🧠 Indexing {Count} files in MemoryAgent (Qdrant + Neo4j)...", files.Count);
            
            var startTime = DateTime.UtcNow;
            var successCount = 0;
            var failCount = 0;
            
            // Index each file individually
            foreach (var file in files)
            {
                try
                {
                    var filePath = Path.Combine(workspacePath, file.Path);
                    
                    // Call MemoryAgent's index tool
                    // This will:
                    // 1. Create vector embeddings in Qdrant
                    // 2. Update file relationships in Neo4j
                    // 3. Make files searchable via search_codebase()
                    
                    await IndexFileInMemoryAgentAsync(filePath, file.Content, cancellationToken);
                    
                    successCount++;
                    _logger.LogDebug("✅ Indexed: {Path}", file.Path);
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogWarning(ex, "⚠️ Failed to index {Path}", file.Path);
                }
            }
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("✅ Indexing complete: {Success} succeeded, {Fail} failed, took {Duration}ms",
                successCount, failCount, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Bulk indexing failed");
        }
    }
    
    /// <summary>
    /// Index a single file in MemoryAgent
    /// </summary>
    private async Task IndexFileInMemoryAgentAsync(string filePath, string content, CancellationToken cancellationToken)
    {
        if (_memoryAgent == null)
        {
            return;
        }
        
        try
        {
            // 🧠 Explicit indexing in MemoryAgent
            // This creates:
            // - Vector embeddings in Qdrant for semantic search
            // - File relationships in Neo4j for dependency tracking
            await _memoryAgent.IndexFileAsync(filePath, content, "codegen", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index file {Path}", filePath);
            // Don't throw - indexing is optional, generation should continue
        }
    }
    
    /// <summary>
    /// Sanitize task name for use in folder names
    /// </summary>
    private string SanitizeTaskName(string task)
    {
        // Take first 50 chars, remove invalid filename chars, replace spaces with hyphens
        var sanitized = task.Length > 50 ? task.Substring(0, 50) : task;
        var invalidChars = Path.GetInvalidFileNameChars();
        sanitized = string.Join("", sanitized.Split(invalidChars));
        sanitized = sanitized.Replace(" ", "-").Replace(".", "").ToLowerInvariant();
        return string.IsNullOrWhiteSpace(sanitized) ? "generated-code" : sanitized;
    }
    
    /// <summary>
    /// Generate files one by one following the plan (FILE-BY-FILE MODE)
    /// </summary>
    private async Task<GenerateCodeResponse> GenerateFilesOneByOneAsync(
        string jobId,
        JobState jobState,
        List<ProjectStep> projectPlan,
        string task,
        string? language,
        string? actualWorkspacePath,
        string jobWorkspacePath,
        CodebaseContext? codebaseContext,
        ScaffoldResult? scaffoldResult,
        BrandSystem? brandGuidelines,
        int maxIterations,
        CancellationToken ct)
    {
        var allGeneratedFiles = new List<CodeFile>();
        ValidationFeedback? projectFeedback = null;
        ValidateCodeResponse? lastValidation = null;
        
        _logger.LogInformation("📋 Starting file-by-file generation with project-level retry: {Total} files, max {Max} project iterations", 
            projectPlan.Count, maxIterations);
        
        // Add scaffolded files first (if any)
        if (scaffoldResult?.Files != null)
        {
            allGeneratedFiles.AddRange(scaffoldResult.Files.Select(f => new CodeFile
            {
                Path = f.Path,
                Content = f.Content
            }));
            _logger.LogInformation("📦 Including {Count} scaffolded files", scaffoldResult.Files.Count);
        }
        
        // 🔄 PROJECT-LEVEL RETRY LOOP (like all-at-once mode!)
        for (int projectIteration = 1; projectIteration <= maxIterations; projectIteration++)
        {
            ct.ThrowIfCancellationRequested();
            
            _logger.LogInformation("🔄 PROJECT ITERATION {Iteration}/{Max} - Generating/regenerating files...", 
                projectIteration, maxIterations);
            
            // Calculate base progress from project iterations (each iteration is worth 90% / maxIterations)
            var baseProgressFromIterations = ((projectIteration - 1) * 90) / maxIterations;
            var progressRangeForThisIteration = 90 / maxIterations; // How much progress this iteration can add
            
            jobState.Status = $"project iteration {projectIteration}/{maxIterations}";
            await PersistJobAsync(jobState);
            
            var totalFiles = projectPlan.Count;
            var filesCompleted = 0;
            
            // Determine which files to generate this iteration
            var filesToGenerate = projectIteration == 1 
                ? projectPlan.OrderBy(t => t.Priority).ToList() // First iteration: all files
                : projectPlan.Where(t => t.Status != "completed").OrderBy(t => t.Priority).ToList(); // Later: only failed/pending
            
            if (filesToGenerate.Count == 0)
            {
                _logger.LogInformation("✅ All files completed, validating...");
                filesToGenerate = projectPlan.OrderBy(t => t.Priority).ToList(); // Regenerate all for another attempt
            }
            
            _logger.LogInformation("📝 Iteration {Iteration}: Generating {Count}/{Total} files", 
                projectIteration, filesToGenerate.Count, totalFiles);
            
            // Process each file in the plan (ordered by priority)
            foreach (var todo in filesToGenerate)
        {
            ct.ThrowIfCancellationRequested();
            
            filesCompleted++;
            var fileName = ExtractFileName(todo.Description);
            
            _logger.LogInformation("📝 [{Current}/{Total}] Generating: {FileName}", 
                filesCompleted, totalFiles, fileName);
            
            // Mark todo as in-progress
            todo.Status = "in_progress";
            // Calculate cumulative progress: base from iterations + progress within this iteration
            var progressInThisIteration = (filesCompleted * progressRangeForThisIteration) / totalFiles;
            jobState.Progress = baseProgressFromIterations + progressInThisIteration;
            jobState.Status = $"iteration {projectIteration}/{maxIterations}: file {filesCompleted}/{totalFiles}: {fileName}";
            await PersistJobAsync(jobState);
            
            // Try to generate this specific file (up to 3 attempts per file)
            bool fileSuccess = false;
            CodeFile? generatedFile = null;
            
            for (int attempt = 1; attempt <= 3 && !fileSuccess; attempt++)
            {
                try
                {
                    _logger.LogInformation("🤖 Attempt {Attempt}/3 for {FileName}", attempt, fileName);
                    
                    // 🤝 TACTICAL: Multi-model debate for THIS specific file
                    if (_multiThinking != null && attempt == 1)
                    {
                        try
                        {
                            // 🔍 DEBUG: Log file paths to identify duplicates
                            var filePaths = allGeneratedFiles.Select(f => f.Path).ToList();
                            var duplicates = filePaths.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => $"{g.Key} (x{g.Count()})").ToList();
                            if (duplicates.Any())
                            {
                                _logger.LogWarning("⚠️ Duplicate file paths detected: {Duplicates}", string.Join(", ", duplicates));
                            }
                            
                            var tacticalThinkingContext = new ThinkingContext
                            {
                                TaskDescription = $"Generate ONLY this file: {todo.Description}\n\nOriginal task context: {task}",
                                FilePath = fileName,
                                Language = language ?? "csharp",
                                // Remove duplicates by taking the last occurrence of each path
                                // Filter out any files with invalid paths (null, empty, or just a language name)
                                ExistingFiles = allGeneratedFiles
                                    .Where(f => !string.IsNullOrWhiteSpace(f.Path) && 
                                               f.Path.Contains('/') || f.Path.Contains('\\') || f.Path.Contains('.'))
                                    .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                                    .ToDictionary(g => g.Key, g => g.Last().Content, StringComparer.OrdinalIgnoreCase),
                                PreviousAttempts = new List<AttemptSummary>(),
                                BrandGuidelines = brandGuidelines
                            };
                            
                            var tacticalThinking = await _multiThinking.ThinkSmartAsync(tacticalThinkingContext, attempt, ct);
                            
                            _logger.LogInformation("✅ Tactical thinking for {FileName}: {Strategy}", 
                                fileName, tacticalThinking.Strategy);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚠️ Tactical thinking failed for {FileName} (non-fatal)", fileName);
                        }
                    }
                    
                    // 🤖 Generate THIS specific file
                    AgenticCodingResult? fileResult = null;
                    
                    if (_agenticCoding != null)
                    {
                        fileResult = await _agenticCoding.GenerateWithToolsAsync(
                            task: $"Generate ONLY this specific file: {todo.Description}\n\n" +
                                  $"File name: {fileName}\n" +
                                  $"Purpose: {todo.Description}\n" +
                                  $"Type: {todo.Type}\n\n" +
                                  $"IMPORTANT: Generate ONLY {fileName}, not multiple files!\n\n" +
                                  $"Original task context: {task}",
                            language: language,
                            workspacePath: actualWorkspacePath,
                            jobWorkspacePath: jobWorkspacePath,
                            codebaseContext: codebaseContext,
                            previousFeedback: null,
                            cancellationToken: ct);
                    }
                    
                    if (fileResult != null && fileResult.Success && fileResult.GeneratedFiles.Count > 0)
                    {
                        // Take the first file (or find matching file), convert FileChange to CodeFile
                        var fileChange = fileResult.GeneratedFiles.FirstOrDefault(f => 
                            f.Path.Contains(fileName, StringComparison.OrdinalIgnoreCase)) 
                            ?? fileResult.GeneratedFiles[0];
                        
                        generatedFile = new CodeFile
                        {
                            Path = fileChange.Path,
                            Content = fileChange.Content
                        };
                        
                        // Quick validation: does it have content?
                        if (string.IsNullOrWhiteSpace(generatedFile.Content))
                        {
                            _logger.LogWarning("⚠️ {FileName} generated but empty, retrying...", fileName);
                            continue;
                        }
                        
                        // ✅ Basic validation passed
                        fileSuccess = true;
                        
                        // Remove old version if it exists (for retries)
                        var existingFile = allGeneratedFiles.FirstOrDefault(f => 
                            f.Path.Equals(generatedFile.Path, StringComparison.OrdinalIgnoreCase));
                        if (existingFile != null)
                        {
                            allGeneratedFiles.Remove(existingFile);
                            _logger.LogInformation("🔄 Replaced existing {FileName}", fileName);
                        }
                        
                        allGeneratedFiles.Add(generatedFile);
                        
                        _logger.LogInformation("✅ {FileName} generated successfully ({Bytes} bytes)", 
                            fileName, generatedFile.Content.Length);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ Attempt {Attempt} failed for {FileName}", attempt, fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error generating {FileName} (attempt {Attempt})", fileName, attempt);
                }
            }
            
            if (fileSuccess && generatedFile != null)
            {
                // Mark todo as completed
                todo.Status = "completed";
                _logger.LogInformation("✅ [{Current}/{Total}] {FileName} COMPLETED", 
                    filesCompleted, totalFiles, fileName);
            }
            else
            {
                // Mark as failed but continue with other files
                todo.Status = "failed";
                _logger.LogError("❌ [{Current}/{Total}] {FileName} FAILED after 3 attempts", 
                    filesCompleted, totalFiles, fileName);
            }
            
            await PersistJobAsync(jobState);
        }
        
        // 🎯 END OF FILE GENERATION LOOP - Now validate the complete project
        _logger.LogInformation("🎯 Iteration {Iteration}: All {Total} files processed, running validation...", 
            projectIteration, totalFiles);
        
        // Write all files to job workspace
        try
        {
            foreach (var file in allGeneratedFiles)
            {
                var filePath = Path.Combine(jobWorkspacePath, file.Path);
                var fileDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }
                await File.WriteAllTextAsync(filePath, file.Content, ct);
            }
            
            _logger.LogInformation("💾 Wrote {Count} files to job workspace", allGeneratedFiles.Count);
            
            // 🧠 INDEX IN MEMORY AGENT
            if (_memoryAgent != null)
            {
                await IndexGeneratedFilesAsync(allGeneratedFiles, jobWorkspacePath, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Failed to write/index files (non-fatal)");
        }
        
        // ✅ VALIDATE COMPLETE PROJECT
        lastValidation = await _validation.ValidateAsync(new ValidateCodeRequest
        {
            Files = allGeneratedFiles,
            Context = "codegen",
            Language = language ?? "csharp",
            WorkspacePath = actualWorkspacePath,
            OriginalTask = task
        }, ct);
        
        _logger.LogInformation("📊 Project iteration {Iteration}: Validation score {Score}/10 ({Issues} issues)", 
            projectIteration, lastValidation.Score, lastValidation.Issues.Count);
        
        // 🎉 EXCELLENT SCORE - BREAK AND SAVE!
        if (lastValidation.Score >= 8)
        {
            _logger.LogInformation("✅ EXCELLENT score {Score}/10 on iteration {Iteration}!", 
                lastValidation.Score, projectIteration);
            
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                var sanitizedTask = SanitizeTaskName(task);
                var generatedFolderName = $"{timestamp}_{sanitizedTask}";
                
                string generatedBasePath;
                if (!string.IsNullOrEmpty(actualWorkspacePath?.Replace("/workspace/", "")))
                {
                    var normalizedPath = actualWorkspacePath.Replace("\\", "/");
                    generatedBasePath = $"{normalizedPath}/generated";
                }
                else
                {
                    generatedBasePath = "/workspace/MemoryAgent/generated";
                }
                
                var generatedPath = Path.Combine(generatedBasePath, generatedFolderName);
                Directory.CreateDirectory(generatedPath);
                
                foreach (var file in allGeneratedFiles)
                {
                    var filePath = Path.Combine(generatedPath, file.Path);
                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }
                    await File.WriteAllTextAsync(filePath, file.Content, ct);
                }
                
                _logger.LogInformation("📁 Auto-wrote {FileCount} files to: {Path}", 
                    allGeneratedFiles.Count, generatedPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "⚠️ Failed to auto-write files to generated folder (non-fatal)");
            }
            
            // ⚡ RECORD IN LIGHTNING
            if (_lightning != null)
            {
                try
                {
                    await _lightning.RecordSuccessfulGenerationAsync(
                        task: task,
                        files: allGeneratedFiles.Select(f => new AgentContracts.Responses.FileChange 
                        { 
                            Path = f.Path, 
                            Content = f.Content,
                            Type = FileChangeType.Created
                        }).ToList(),
                        score: lastValidation.Score,
                        language: language ?? "csharp",
                        metadata: new Dictionary<string, object>
                        {
                            ["mode"] = "file-by-file-retry",
                            ["project_iterations"] = projectIteration,
                            ["total_files"] = allGeneratedFiles.Count,
                            ["validation_score"] = lastValidation.Score,
                            ["timestamp"] = DateTime.UtcNow
                        },
                        cancellationToken: ct
                    );
                    
                    _logger.LogInformation("✅ Lightning learned from this success!");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Failed to record in Lightning");
                }
            }
            
            // SUCCESS! Break out of project iteration loop
            break;
        }
        
        // 🎯 GOOD ENOUGH SCORE (6.5-7.9) after iteration 3+
        if (lastValidation.Score >= 6.5 && projectIteration >= 3)
        {
            _logger.LogWarning("⚠️ ACCEPTABLE score {Score}/10 on iteration {Iteration} - stopping", 
                lastValidation.Score, projectIteration);
            break;
        }
        
        // 🚨 MAX ITERATIONS REACHED
        if (projectIteration >= maxIterations)
        {
            _logger.LogError("🚨 CRITICAL: Score {Score}/10 after {Iterations} project iterations!", 
                lastValidation.Score, projectIteration);
            break;
        }
        
        // ⚠️ SCORE TOO LOW - ANALYZE AND DECIDE: Fix files OR regenerate plan
        _logger.LogWarning("⚠️ Score {Score}/10 on iteration {Iteration}, analyzing issues...", 
            lastValidation.Score, projectIteration);
        
        // 🧠 CHECK IF THE PLAN ITSELF IS INSUFFICIENT
        // Look for critical issues about "incomplete", "missing features", "doesn't address task"
        var planInsufficientKeywords = new[] 
        { 
            "does not address", "missing", "incomplete", "not implemented", 
            "only includes", "lacks", "doesn't include", "missing features",
            "doesn't solve", "insufficient", "basic implementation"
        };
        
        var planIssues = lastValidation.Issues
            .Where(i => i.Severity == "CRITICAL" && 
                       planInsufficientKeywords.Any(k => i.Message.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        
        // 🔄 OPTION 1: PLAN IS INSUFFICIENT - Regenerate plan with more detail!
        if (planIssues.Any() && projectIteration <= 3 && _phi4Thinking != null && _templates != null)
        {
            _logger.LogWarning("🧠 PLAN INSUFFICIENT! Validation says: {Issue}", planIssues.First().Message);
            _logger.LogInformation("🔄 Asking Phi4 to create a MORE DETAILED plan based on validation feedback...");
            
            try
            {
                // Build feedback context for Phi4
                var feedbackForPhi4 = new StringBuilder();
                feedbackForPhi4.AppendLine("PREVIOUS PLAN WAS INSUFFICIENT!");
                feedbackForPhi4.AppendLine($"Score: {lastValidation.Score}/10");
                feedbackForPhi4.AppendLine();
                feedbackForPhi4.AppendLine("Critical Issues:");
                foreach (var issue in planIssues.Take(3))
                {
                    feedbackForPhi4.AppendLine($"- {issue.Message}");
                }
                feedbackForPhi4.AppendLine();
                feedbackForPhi4.AppendLine("Files generated so far:");
                foreach (var file in allGeneratedFiles.Take(10))
                {
                    feedbackForPhi4.AppendLine($"- {file.Path}");
                }
                feedbackForPhi4.AppendLine();
                feedbackForPhi4.AppendLine("CREATE A MORE COMPREHENSIVE PLAN with additional files to address ALL requirements!");
                
                // Get template
                var templateType = DetectTemplateType(task, language ?? "csharp");
                var template = _templates.GetTemplateById(templateType);
                
                if (template != null)
                {
                    // Ask Phi4 for a NEW, more detailed plan
                    var newPlan = await _phi4Thinking.PlanProjectAsync(
                        userRequest: task + "\n\nIMPORTANT: " + feedbackForPhi4.ToString(),
                        selectedTemplate: template,
                        existingContext: allGeneratedFiles
                            .Where(f => !string.IsNullOrWhiteSpace(f.Path) && 
                                       (f.Path.Contains('/') || f.Path.Contains('\\') || f.Path.Contains('.')))
                            .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.Last().Content, StringComparer.OrdinalIgnoreCase),
                        ct: ct
                    );
                    
                    if (newPlan != null && newPlan.Files.Count > projectPlan.Count)
                    {
                        _logger.LogInformation("✅ Phi4 generated NEW plan with {NewCount} files (was {OldCount})", 
                            newPlan.Files.Count, projectPlan.Count);
                        
                        // Clear old plan and replace with new detailed plan
                        projectPlan.Clear();
                        projectPlan.AddRange(newPlan.Files.Select((file, idx) => new ProjectStep
                        {
                            Description = $"{file.FileName} - {file.Purpose}",
                            Priority = file.Priority,
                            Complexity = file.EstimatedComplexity,
                            Type = DetermineStepType(file.FileName),
                            Status = "pending"
                        }));
                        
                        // Clear generated files to start fresh with new plan
                        allGeneratedFiles.Clear();
                        
                        // Re-add scaffolded files
                        if (scaffoldResult?.Files != null)
                        {
                            allGeneratedFiles.AddRange(scaffoldResult.Files.Select(f => new CodeFile
                            {
                                Path = f.Path,
                                Content = f.Content
                            }));
                        }
                        
                        // Update job state
                        jobState.ProjectPlan = projectPlan;
                        await PersistJobAsync(jobState);
                        
                        _logger.LogInformation("🔄 NEW PLAN LOADED - Continuing to next iteration with expanded plan!");
                        continue; // Skip to next iteration with new plan
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to regenerate plan, will try fixing files instead");
            }
        }
        
        // 🔄 OPTION 2: PLAN IS OK - Just fix specific files
        _logger.LogInformation("🔧 Plan is OK, identifying files to fix...");
        
        // Identify which files/areas caused issues
        var issuesByFile = lastValidation.Issues
            .Where(i => !string.IsNullOrEmpty(i.File))
            .GroupBy(i => i.File)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        if (issuesByFile.Any())
        {
            _logger.LogInformation("📋 Issues found in {FileCount} files:", issuesByFile.Count);
            foreach (var fileGroup in issuesByFile.Take(5))
            {
                _logger.LogInformation("  - {File}: {IssueCount} issues", fileGroup.Key, fileGroup.Count());
                
                // Mark these files as "failed" so they get regenerated
                var matchingTodo = projectPlan.FirstOrDefault(t => 
                    ExtractFileName(t.Description).Equals(fileGroup.Key, StringComparison.OrdinalIgnoreCase));
                
                if (matchingTodo != null)
                {
                    matchingTodo.Status = "failed";
                    _logger.LogInformation("    → Marked {File} for regeneration", fileGroup.Key);
                }
            }
        }
        else
        {
            // No specific files identified - mark all as pending to regenerate
            _logger.LogWarning("⚠️ No specific files identified, will regenerate all files");
            foreach (var todo in projectPlan)
            {
                todo.Status = "pending";
            }
        }
        
        // Prepare feedback for next iteration
        projectFeedback = lastValidation.ToFeedback();
        projectFeedback.Summary = $"Project iteration {projectIteration}: Score {lastValidation.Score}/10. {issuesByFile.Count} files with issues. Regenerating failed files.";
        
        _logger.LogInformation("🔄 Continuing to iteration {Next}...", projectIteration + 1);
        
        } // End of project iteration loop
        
        // 🎯 FINAL RESULT (after all project iterations)
        _logger.LogInformation("🏁 File-by-file generation complete after {Iterations} project iterations. Final score: {Score}/10", 
            maxIterations, lastValidation?.Score ?? 0);
        
        var result = new GenerateCodeResponse
        {
            FileChanges = allGeneratedFiles.Select(f => new FileChange
            {
                Path = f.Path,
                Content = f.Content,
                Type = FileChangeType.Created
            }).ToList(),
            ModelUsed = $"file-by-file-retry-{maxIterations}iter",
            Success = (lastValidation?.Score ?? 0) >= 6,
            Explanation = lastValidation?.Summary
        };
        
        return result;
    }
    
    /// <summary>
    /// Extract file name from todo description (e.g., "Calculator.cs - Core logic" -> "Calculator.cs")
    /// </summary>
    private string ExtractFileName(string description)
    {
        // Format: "FileName.ext - Purpose"
        var parts = description.Split(new[] { " - ", " – " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            var fileName = parts[0].Trim();
            // Remove any leading/trailing quotes or special chars
            fileName = fileName.Trim('"', '\'', ' ');
            return fileName;
        }
        
        return description; // Fallback
    }
    
    /// <summary>
    /// Detect template type from task description
    /// </summary>
    private string DetectTemplateType(string task, string language)
    {
        var taskLower = task.ToLowerInvariant();
        
        if (language == "csharp")
        {
            if (taskLower.Contains("blazor") || taskLower.Contains("razor")) return "blazor-server";
            if (taskLower.Contains("web api") || taskLower.Contains("rest")) return "webapi";
            if (taskLower.Contains("console")) return "console";
            if (taskLower.Contains("class library") || taskLower.Contains("library")) return "classlib";
        }
        
        return "console"; // Default
    }
    
    /// <summary>
    /// Determine step type from file name
    /// </summary>
    private string DetermineStepType(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        
        if (lower.Contains("test")) return "tests";
        if (lower.Contains(".razor") || lower.Contains(".cshtml")) return "ui";
        if (lower.Contains("program.cs") || lower.Contains("startup.cs")) return "scaffold";
        if (lower.Contains("validator") || lower.Contains("validation")) return "validation";
        
        return "code"; // Default
    }
    
    /// <summary>
    /// Estimate project steps from task description (FALLBACK when Phi4 unavailable)
    /// </summary>
    private List<ProjectStep> EstimateProjectSteps(string task, string language, ScaffoldResult? scaffoldResult)
    {
        var steps = new List<ProjectStep>();
        
        // Step 1: Scaffolding (if applicable)
        if (scaffoldResult != null && scaffoldResult.Success)
        {
            steps.Add(new ProjectStep
            {
                Description = $"Scaffold {scaffoldResult.ProjectType} project structure",
                Priority = 1,
                Complexity = 2,
                Type = "scaffold"
            });
        }
        
        // Step 2: Core logic
        steps.Add(new ProjectStep
        {
            Description = "Generate core business logic and models",
            Priority = 2,
            Complexity = 5,
            Type = "code"
        });
        
        // Step 3: UI (if applicable)
        if (task.ToLowerInvariant().Contains("ui") || 
            task.ToLowerInvariant().Contains("page") || 
            task.ToLowerInvariant().Contains("component") ||
            task.ToLowerInvariant().Contains("blazor") ||
            task.ToLowerInvariant().Contains("razor"))
        {
            steps.Add(new ProjectStep
            {
                Description = "Generate UI components and pages",
                Priority = 3,
                Complexity = 4,
                Type = "ui"
            });
        }
        
        // Step 4: Testing (always)
        steps.Add(new ProjectStep
        {
            Description = "Add unit tests and integration tests",
            Priority = 4,
            Complexity = 3,
            Type = "tests"
        });
        
        // Step 5: Validation
        steps.Add(new ProjectStep
        {
            Description = "Validate code quality and compile",
            Priority = 5,
            Complexity = 2,
            Type = "validation"
        });
        
        return steps;
    }

    private class ProjectStep
    {
        public string Description { get; set; } = "";
        public int Priority { get; set; }
        public int Complexity { get; set; }
        public string Type { get; set; } = "";
        public string Status { get; set; } = "pending"; // pending, in_progress, completed
    }

    private class JobState
    {
        public string JobId { get; set; } = "";
        public string Task { get; set; } = "";
        public string? Language { get; set; }
        public int MaxIterations { get; set; }
        public string Status { get; set; } = "running";
        public int Progress { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? Error { get; set; }
        public GenerateCodeResponse? Result { get; set; }
        public List<ProjectStep>? ProjectPlan { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
    }
}

public class JobStatus
{
    public string JobId { get; set; } = "";
    public string Task { get; set; } = "";
    public string Status { get; set; } = "";
    public int Progress { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
    public GenerateCodeResponse? Result { get; set; }
    public List<ProjectTodo>? Todos { get; set; }
}

public class ProjectTodo
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public int Priority { get; set; }
    public int Complexity { get; set; }
    public string Type { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending, in_progress, completed
}

