using System.Text.Json;
using MemoryAgent.Server.Models;
using TaskStatusModel = MemoryAgent.Server.Models.TaskStatus;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Implementation of MCP protocol for code memory
/// </summary>
public class McpService : IMcpService
{
    private readonly IIndexingService _indexingService;
    private readonly IReindexService _reindexService;
    private readonly IGraphService _graphService;
    private readonly ISmartSearchService _smartSearchService;
    private readonly ITodoService _todoService;
    private readonly IPlanService _planService;
    private readonly ITaskValidationService _validationService;
    private readonly IPatternIndexingService _patternService;
    private readonly IBestPracticeValidationService _bestPracticeValidation;
    private readonly IRecommendationService _recommendationService;
    private readonly IPatternValidationService _patternValidationService;
    private readonly ICodeComplexityService _complexityService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<McpService> _logger;

    public McpService(
        IIndexingService indexingService,
        IReindexService reindexService,
        IGraphService graphService,
        ISmartSearchService smartSearchService,
        ITodoService todoService,
        IPlanService planService,
        ITaskValidationService validationService,
        IPatternIndexingService patternService,
        IBestPracticeValidationService bestPracticeValidation,
        IRecommendationService recommendationService,
        IPatternValidationService patternValidationService,
        ICodeComplexityService complexityService,
        IServiceProvider serviceProvider,
        ILogger<McpService> logger)
    {
        _indexingService = indexingService;
        _reindexService = reindexService;
        _graphService = graphService;
        _smartSearchService = smartSearchService;
        _todoService = todoService;
        _planService = planService;
        _validationService = validationService;
        _patternService = patternService;
        _bestPracticeValidation = bestPracticeValidation;
        _recommendationService = recommendationService;
        _patternValidationService = patternValidationService;
        _complexityService = complexityService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<List<McpTool>> GetToolsAsync(CancellationToken cancellationToken = default)
    {
        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "index_file",
                Description = "Index a single code file into memory for semantic search and analysis",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to the file (use /workspace/... for mounted files)" },
                        context = new { type = "string", description = "Optional context name for grouping (e.g., 'ProjectName')" }
                    },
                    required = new[] { "path" }
                }
            },
            new McpTool
            {
                Name = "index_directory",
                Description = "Index an entire directory of code files recursively",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to directory (use /workspace/... for mounted files)" },
                        recursive = new { type = "boolean", description = "Whether to index subdirectories", @default = true },
                        context = new { type = "string", description = "Optional context name" }
                    },
                    required = new[] { "path" }
                }
            },
            new McpTool
            {
                Name = "query",
                Description = "Search code memory using semantic search. Ask natural language questions about code.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language question (e.g., 'How do we handle errors?')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results", @default = 5 },
                        minimumScore = new { type = "number", description = "Minimum similarity score 0-1", @default = 0.5 }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "search",
                Description = "Search code memory using semantic search (alias for query). Ask natural language questions about code.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language question (e.g., 'How do we handle errors?')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results", @default = 5 },
                        minimumScore = new { type = "number", description = "Minimum similarity score 0-1", @default = 0.5 }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "reindex",
                Description = "Reindex code to update memory after changes. Detects new, modified, and deleted files.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        path = new { type = "string", description = "Path to directory to reindex" },
                        context = new { type = "string", description = "Context name" },
                        removeStale = new { type = "boolean", description = "Remove deleted files from memory", @default = true }
                    },
                    required = new[] { "path" }
                }
            },
            new McpTool
            {
                Name = "impact_analysis",
                Description = "Analyze what code would be impacted if a class changes",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name" }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "dependency_chain",
                Description = "Get the dependency chain for a class",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string", description = "Fully qualified class name" },
                        maxDepth = new { type = "number", description = "Maximum depth", @default = 5 }
                    },
                    required = new[] { "className" }
                }
            },
            new McpTool
            {
                Name = "find_circular_dependencies",
                Description = "Find circular dependencies in code",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Optional context to search within" }
                    }
                }
            },
            new McpTool
            {
                Name = "smartsearch",
                Description = "Intelligent search that auto-detects whether to use graph-first (for structural queries) or semantic-first (for conceptual queries) strategy. Returns enriched results with relationships and scores.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Search query (e.g., 'classes that implement IService' or 'how is authentication handled?')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results per page", @default = 20 },
                        offset = new { type = "number", description = "Offset for pagination", @default = 0 },
                        includeRelationships = new { type = "boolean", description = "Include relationship data", @default = true },
                        relationshipDepth = new { type = "number", description = "Max relationship depth", @default = 2 }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "add_todo",
                Description = "Add a TODO item to track technical debt, bugs, or improvements",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context" },
                        title = new { type = "string", description = "TODO title" },
                        description = new { type = "string", description = "Detailed description" },
                        priority = new { type = "string", description = "Priority: Low, Medium, High, Critical", @default = "Medium" },
                        filePath = new { type = "string", description = "Optional file path" },
                        lineNumber = new { type = "number", description = "Optional line number" },
                        assignedTo = new { type = "string", description = "Optional assignee email" }
                    },
                    required = new[] { "context", "title" }
                }
            },
            new McpTool
            {
                Name = "search_todos",
                Description = "Search and filter TODO items",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Filter by context" },
                        status = new { type = "string", description = "Filter by status: Pending, InProgress, Completed, Cancelled" },
                        priority = new { type = "string", description = "Filter by priority: Low, Medium, High, Critical" },
                        assignedTo = new { type = "string", description = "Filter by assignee" }
                    }
                }
            },
            new McpTool
            {
                Name = "update_todo_status",
                Description = "Update the status of a TODO item",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        todoId = new { type = "string", description = "TODO ID" },
                        status = new { type = "string", description = "New status: Pending, InProgress, Completed, Cancelled" }
                    },
                    required = new[] { "todoId", "status" }
                }
            },
            new McpTool
            {
                Name = "create_plan",
                Description = "Create a development plan with tasks and dependencies",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context" },
                        name = new { type = "string", description = "Plan name" },
                        description = new { type = "string", description = "Plan description" },
                        tasks = new
                        {
                            type = "array",
                            description = "Array of tasks",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    title = new { type = "string" },
                                    description = new { type = "string" },
                                    orderIndex = new { type = "number" }
                                }
                            }
                        }
                    },
                    required = new[] { "context", "name", "tasks" }
                }
            },
            new McpTool
            {
                Name = "get_plan_status",
                Description = "Get the status and progress of a development plan",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" }
                    },
                    required = new[] { "planId" }
                }
            },
            new McpTool
            {
                Name = "update_task_status",
                Description = "Update the status of a task in a plan",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" },
                        taskId = new { type = "string", description = "Task ID" },
                        status = new { type = "string", description = "New status: Pending, InProgress, Blocked, Completed, Cancelled" }
                    },
                    required = new[] { "planId", "taskId", "status" }
                }
            },
            new McpTool
            {
                Name = "complete_plan",
                Description = "Mark a development plan as completed",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" }
                    },
                    required = new[] { "planId" }
                }
            },
            new McpTool
            {
                Name = "search_plans",
                Description = "Search and filter development plans",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Filter by context" },
                        status = new { type = "string", description = "Filter by status: Draft, Active, Completed, Cancelled, OnHold" }
                    }
                }
            },
            new McpTool
            {
                Name = "validate_task",
                Description = "Validate a task against its rules before completion. Checks for required tests, files, code quality, etc. Can auto-fix validation failures if enabled.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        planId = new { type = "string", description = "Plan ID" },
                        taskId = new { type = "string", description = "Task ID to validate" },
                        autoFix = new { type = "boolean", description = "Automatically fix validation failures (e.g., create missing tests)" }
                    },
                    required = new[] { "planId", "taskId" }
                }
            },
            
            // PATTERN DETECTION TOOLS
            new McpTool
            {
                Name = "search_patterns",
                Description = "Search for code patterns (caching, retry logic, validation, etc.) using semantic search. Returns detected patterns with confidence scores and Azure best practice links.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Pattern search query (e.g., 'caching patterns', 'retry logic', 'validation')" },
                        context = new { type = "string", description = "Optional context to search within" },
                        limit = new { type = "number", description = "Maximum results", @default = 20 }
                    },
                    required = new[] { "query" }
                }
            },
            new McpTool
            {
                Name = "validate_best_practices",
                Description = "Validate a project against Azure best practices. Returns compliance score, which practices are implemented, and which are missing with recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to validate" },
                        bestPractices = new { type = "array", description = "Specific practices to check (optional, defaults to all 21 practices)", items = new { type = "string" } },
                        includeExamples = new { type = "boolean", description = "Include code examples in results", @default = true },
                        maxExamplesPerPractice = new { type = "number", description = "Maximum examples per practice", @default = 5 }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_recommendations",
                Description = "Analyze a project and get prioritized recommendations for missing or weak patterns. Returns health score and actionable recommendations with code examples.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to analyze" },
                        categories = new { type = "array", description = "Focus on specific categories (optional)", items = new { type = "string" } },
                        includeLowPriority = new { type = "boolean", description = "Include low-priority recommendations", @default = false },
                        maxRecommendations = new { type = "number", description = "Maximum recommendations to return", @default = 10 }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_available_best_practices",
                Description = "Get list of all available Azure best practices that can be validated.",
                InputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            
            // PATTERN VALIDATION TOOLS (NEW)
            new McpTool
            {
                Name = "validate_pattern_quality",
                Description = "Deep validation of a specific pattern's implementation quality. Returns quality score (1-10), grade (A-F), issues found, and auto-fix code if available.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern_id = new { type = "string", description = "Pattern ID to validate" },
                        context = new { type = "string", description = "Project context (optional)" },
                        include_auto_fix = new { type = "boolean", description = "Include auto-fix code", @default = true },
                        min_severity = new { type = "string", description = "Minimum severity to report (low|medium|high|critical)", @default = "low" }
                    },
                    required = new[] { "pattern_id" }
                }
            },
            new McpTool
            {
                Name = "find_anti_patterns",
                Description = "Find all anti-patterns and badly implemented patterns in a project. Returns patterns with issues, security vulnerabilities, and overall security score.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to search" },
                        min_severity = new { type = "string", description = "Minimum severity (low|medium|high|critical)", @default = "medium" },
                        include_legacy = new { type = "boolean", description = "Include legacy/deprecated patterns", @default = true }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "validate_security",
                Description = "Security audit of detected patterns. Returns overall security score, vulnerabilities found, and remediation steps.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to validate" },
                        pattern_types = new { type = "array", description = "Specific pattern types to check (optional)", items = new { type = "string" } }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "get_migration_path",
                Description = "Get step-by-step migration path for legacy/deprecated patterns. Returns detailed migration instructions, code examples, and effort estimate.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        pattern_id = new { type = "string", description = "Pattern ID to get migration path for" },
                        include_code_example = new { type = "boolean", description = "Include before/after code example", @default = true }
                    },
                    required = new[] { "pattern_id" }
                }
            },
            new McpTool
            {
                Name = "validate_project",
                Description = "Comprehensive project validation. Returns overall quality/security scores, all pattern validations, vulnerabilities, legacy patterns, and top recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context to validate" }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "analyze_code_complexity",
                Description = "Analyze code complexity metrics (cyclomatic, cognitive, LOC, nesting, code smells) for a file or specific method. Returns detailed complexity scores with grades and recommendations.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the file to analyze" },
                        methodName = new { type = "string", description = "Optional: specific method name to analyze (if omitted, analyzes all methods in file)" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new McpTool
            {
                Name = "register_workspace",
                Description = "Register a workspace directory for file watching (auto-reindex). Called automatically by wrapper on startup.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workspacePath = new { type = "string", description = "Full path to workspace directory" },
                        context = new { type = "string", description = "Context name for this workspace" }
                    },
                    required = new[] { "workspacePath", "context" }
                }
            },
            new McpTool
            {
                Name = "unregister_workspace",
                Description = "Unregister a workspace directory from file watching. Called automatically by wrapper on shutdown.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        workspacePath = new { type = "string", description = "Full path to workspace directory" }
                    },
                    required = new[] { "workspacePath" }
                }
            },
            new McpTool
            {
                Name = "transform_page",
                Description = "Transform a Blazor/Razor page to modern architecture with clean code and CSS",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Path to the .razor/.cshtml file to transform" },
                        extractComponents = new { type = "boolean", description = "Extract reusable components", @default = true },
                        modernizeCSS = new { type = "boolean", description = "Modernize CSS (extract inline styles, use CSS variables)", @default = true },
                        addErrorHandling = new { type = "boolean", description = "Add error handling", @default = true },
                        addLoadingStates = new { type = "boolean", description = "Add loading states", @default = true },
                        addAccessibility = new { type = "boolean", description = "Add accessibility features", @default = true },
                        outputDirectory = new { type = "string", description = "Optional output directory for generated files" }
                    },
                    required = new[] { "sourcePath" }
                }
            },
            new McpTool
            {
                Name = "learn_transformation",
                Description = "Learn transformation pattern from example (old → new page)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        exampleOldPath = new { type = "string", description = "Path to old/legacy version of the page" },
                        exampleNewPath = new { type = "string", description = "Path to new/modernized version" },
                        patternName = new { type = "string", description = "Name for this transformation pattern" }
                    },
                    required = new[] { "exampleOldPath", "exampleNewPath", "patternName" }
                }
            },
            new McpTool
            {
                Name = "apply_transformation",
                Description = "Apply learned transformation pattern to a new page",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        patternId = new { type = "string", description = "ID of the learned pattern to apply" },
                        targetPath = new { type = "string", description = "Path to the page to transform" }
                    },
                    required = new[] { "patternId", "targetPath" }
                }
            },
            new McpTool
            {
                Name = "list_transformation_patterns",
                Description = "Get all learned transformation patterns",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Optional context to filter patterns" }
                    }
                }
            },
            new McpTool
            {
                Name = "detect_reusable_components",
                Description = "Scan project for reusable component patterns (repeated UI blocks)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        projectPath = new { type = "string", description = "Path to project directory to scan" },
                        minOccurrences = new { type = "number", description = "Minimum occurrences to be a candidate", @default = 2 },
                        minSimilarity = new { type = "number", description = "Minimum similarity score (0-1)", @default = 0.7 }
                    },
                    required = new[] { "projectPath" }
                }
            },
            new McpTool
            {
                Name = "extract_component",
                Description = "Extract a detected component candidate into a reusable component",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        componentCandidateJson = new { type = "string", description = "JSON string of ComponentCandidate object" },
                        outputPath = new { type = "string", description = "Path where to save the extracted component" }
                    },
                    required = new[] { "componentCandidateJson", "outputPath" }
                }
            },
            new McpTool
            {
                Name = "transform_css",
                Description = "Transform CSS - extract inline styles, modernize, add variables",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Path to file with CSS/inline styles" },
                        generateVariables = new { type = "boolean", description = "Generate CSS variables", @default = true },
                        modernizeLayout = new { type = "boolean", description = "Modernize layout (Grid/Flexbox)", @default = true },
                        addResponsive = new { type = "boolean", description = "Add responsive design", @default = true },
                        addAccessibility = new { type = "boolean", description = "Add accessibility improvements", @default = true },
                        outputPath = new { type = "string", description = "Optional output CSS file path" }
                    },
                    required = new[] { "sourcePath" }
                }
            },
            new McpTool
            {
                Name = "analyze_css",
                Description = "Analyze CSS quality and get recommendations",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourcePath = new { type = "string", description = "Path to file to analyze for CSS quality" }
                    },
                    required = new[] { "sourcePath" }
                }
            }
        };

        return Task.FromResult(tools);
    }

    public async Task<McpToolResult> CallToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling tool: {Tool} with args: {Args}", 
                toolCall.Name, JsonSerializer.Serialize(toolCall.Arguments));

            var result = toolCall.Name switch
            {
                "index_file" => await IndexFileToolAsync(toolCall.Arguments, cancellationToken),
                "index_directory" => await IndexDirectoryToolAsync(toolCall.Arguments, cancellationToken),
                "query" => await QueryToolAsync(toolCall.Arguments, cancellationToken),
                "search" => await QueryToolAsync(toolCall.Arguments, cancellationToken), // Alias for query
                "reindex" => await ReindexToolAsync(toolCall.Arguments, cancellationToken),
                "smartsearch" => await SmartSearchToolAsync(toolCall.Arguments, cancellationToken),
                "impact_analysis" => await ImpactAnalysisToolAsync(toolCall.Arguments, cancellationToken),
                "dependency_chain" => await DependencyChainToolAsync(toolCall.Arguments, cancellationToken),
                "find_circular_dependencies" => await CircularDependenciesToolAsync(toolCall.Arguments, cancellationToken),
                "add_todo" => await AddTodoToolAsync(toolCall.Arguments, cancellationToken),
                "search_todos" => await SearchTodosToolAsync(toolCall.Arguments, cancellationToken),
                "update_todo_status" => await UpdateTodoStatusToolAsync(toolCall.Arguments, cancellationToken),
                "create_plan" => await CreatePlanToolAsync(toolCall.Arguments, cancellationToken),
                "get_plan_status" => await GetPlanStatusToolAsync(toolCall.Arguments, cancellationToken),
                "update_task_status" => await UpdateTaskStatusToolAsync(toolCall.Arguments, cancellationToken),
                "complete_plan" => await CompletePlanToolAsync(toolCall.Arguments, cancellationToken),
                "search_plans" => await SearchPlansToolAsync(toolCall.Arguments, cancellationToken),
                "validate_task" => await ValidateTaskToolAsync(toolCall.Arguments, cancellationToken),
                "search_patterns" => await SearchPatternsToolAsync(toolCall.Arguments, cancellationToken),
                "validate_best_practices" => await ValidateBestPracticesToolAsync(toolCall.Arguments, cancellationToken),
                "get_recommendations" => await GetRecommendationsToolAsync(toolCall.Arguments, cancellationToken),
                "get_available_best_practices" => await GetAvailableBestPracticesToolAsync(toolCall.Arguments, cancellationToken),
                "validate_pattern_quality" => await ValidatePatternQualityToolAsync(toolCall.Arguments, cancellationToken),
                "find_anti_patterns" => await FindAntiPatternsToolAsync(toolCall.Arguments, cancellationToken),
                "validate_security" => await ValidateSecurityToolAsync(toolCall.Arguments, cancellationToken),
                "get_migration_path" => await GetMigrationPathToolAsync(toolCall.Arguments, cancellationToken),
                "validate_project" => await ValidateProjectToolAsync(toolCall.Arguments, cancellationToken),
                "analyze_code_complexity" => await AnalyzeCodeComplexityToolAsync(toolCall.Arguments, cancellationToken),
                "register_workspace" => await RegisterWorkspaceToolAsync(toolCall.Arguments, cancellationToken),
                "unregister_workspace" => await UnregisterWorkspaceToolAsync(toolCall.Arguments, cancellationToken),
                "transform_page" => await TransformPageToolAsync(toolCall.Arguments, cancellationToken),
                "learn_transformation" => await LearnTransformationToolAsync(toolCall.Arguments, cancellationToken),
                "apply_transformation" => await ApplyTransformationToolAsync(toolCall.Arguments, cancellationToken),
                "list_transformation_patterns" => await ListTransformationPatternsToolAsync(toolCall.Arguments, cancellationToken),
                "detect_reusable_components" => await DetectReusableComponentsToolAsync(toolCall.Arguments, cancellationToken),
                "extract_component" => await ExtractComponentToolAsync(toolCall.Arguments, cancellationToken),
                "transform_css" => await TransformCSSToolAsync(toolCall.Arguments, cancellationToken),
                "analyze_css" => await AnalyzeCSSToolAsync(toolCall.Arguments, cancellationToken),
                _ => new McpToolResult
                {
                    IsError = true,
                    Content = new List<McpContent>
                    {
                        new McpContent { Type = "text", Text = $"Unknown tool: {toolCall.Name}" }
                    }
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling tool: {Tool}", toolCall.Name);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Error: {ex.Message}" }
                }
            };
        }
    }

    public async Task<McpResponse?> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Handling MCP request: {Method}", request.Method);

            // Notifications don't get responses (MCP protocol spec)
            if (request.Method.StartsWith("notifications/"))
            {
                _logger.LogInformation("Notification received: {Method} (no response will be sent)", request.Method);
                return null;
            }

            var result = request.Method switch
            {
                "tools/list" => new McpResponse
                {
                    Id = request.Id,
                    Result = new { tools = await GetToolsAsync(cancellationToken) }
                },
                "tools/call" => await HandleToolCallAsync(request, cancellationToken),
                "initialize" => new McpResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "memory-code-agent",
                            version = "1.0.0"
                        }
                    }
                },
                _ => new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32601,
                        Message = $"Method not found: {request.Method}"
                    }
                }
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = $"Internal error: {ex.Message}"
                }
            };
        }
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request, CancellationToken cancellationToken)
    {
        var paramsJson = JsonSerializer.Serialize(request.Params);
        var toolCall = JsonSerializer.Deserialize<McpToolCall>(paramsJson);
        
        if (toolCall == null)
        {
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32602, Message = "Invalid params" }
            };
        }

        var result = await CallToolAsync(toolCall, cancellationToken);
        
        return new McpResponse
        {
            Id = request.Id,
            Result = result
        };
    }

    // Tool implementations
    private async Task<McpToolResult> IndexFileToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();

        if (string.IsNullOrWhiteSpace(path))
        {
            return ErrorResult("Path is required");
        }

        var result = await _indexingService.IndexFileAsync(path, context, ct);
        
        return new McpToolResult
        {
            IsError = !result.Success,
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> IndexDirectoryToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var recursive = args?.GetValueOrDefault("recursive") as bool? ?? true;

        if (string.IsNullOrWhiteSpace(path))
        {
            return ErrorResult("Path is required");
        }

        var result = await _indexingService.IndexDirectoryAsync(path, recursive, context, ct);
        
        return new McpToolResult
        {
            IsError = !result.Success,
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> QueryToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var query = args?.GetValueOrDefault("query")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var limit = args?.GetValueOrDefault("limit") as int? ?? 5;
        var minimumScore = args?.GetValueOrDefault("minimumScore") as float? ?? 0.7f;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResult("Query is required");
        }

        var result = await _indexingService.QueryAsync(query, context, limit, minimumScore, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ReindexToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var path = args?.GetValueOrDefault("path")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var removeStale = args?.GetValueOrDefault("removeStale") as bool? ?? true;

        var result = await _reindexService.ReindexAsync(context, path, removeStale, ct);
        
        return new McpToolResult
        {
            IsError = !result.Success,
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> SmartSearchToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var query = args?.GetValueOrDefault("query")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();
        var limit = args?.GetValueOrDefault("limit") as int? ?? 20;
        var offset = args?.GetValueOrDefault("offset") as int? ?? 0;
        var includeRelationships = args?.GetValueOrDefault("includeRelationships") as bool? ?? true;
        var relationshipDepth = args?.GetValueOrDefault("relationshipDepth") as int? ?? 2;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResult("Query is required");
        }

        var request = new SmartSearchRequest
        {
            Query = query,
            Context = context,
            Limit = limit,
            Offset = offset,
            IncludeRelationships = includeRelationships,
            RelationshipDepth = relationshipDepth
        };

        var result = await _smartSearchService.SearchAsync(request, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ImpactAnalysisToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();

        if (string.IsNullOrWhiteSpace(className))
        {
            return ErrorResult("className is required");
        }

        var impacted = await _graphService.GetImpactAnalysisAsync(className, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Classes impacted by changes to {className}:\n" + 
                           string.Join("\n", impacted.Select(c => $"- {c}"))
                }
            }
        };
    }

    private async Task<McpToolResult> DependencyChainToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var className = args?.GetValueOrDefault("className")?.ToString();
        var maxDepth = args?.GetValueOrDefault("maxDepth") as int? ?? 5;

        if (string.IsNullOrWhiteSpace(className))
        {
            return ErrorResult("className is required");
        }

        var dependencies = await _graphService.GetDependencyChainAsync(className, maxDepth, ct);
        
        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Dependencies for {className}:\n" + 
                           string.Join("\n", dependencies.Select(d => $"- {d}"))
                }
            }
        };
    }

    private async Task<McpToolResult> CircularDependenciesToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString();

        var cycles = await _graphService.FindCircularDependenciesAsync(context, ct);
        
        if (!cycles.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = "No circular dependencies found!" }
                }
            };
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"Found {cycles.Count} circular dependency cycles:\n" +
                           string.Join("\n\n", cycles.Select((cycle, i) => 
                               $"Cycle {i + 1}: {string.Join(" → ", cycle)}"))
                }
            }
        };
    }

    private async Task<McpToolResult> AddTodoToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString() ?? "default";
        var title = args?.GetValueOrDefault("title")?.ToString() ?? "";
        var description = args?.GetValueOrDefault("description")?.ToString() ?? "";
        var priority = Enum.TryParse<TodoPriority>(args?.GetValueOrDefault("priority")?.ToString(), out var parsedpriority) ? parsedpriority : TodoPriority.Medium;
        var filePath = args?.GetValueOrDefault("filePath")?.ToString() ?? "";
        var lineNumber = (args?.GetValueOrDefault("lineNumber") as int?) ?? 0;
        var assignedTo = args?.GetValueOrDefault("assignedTo")?.ToString() ?? "";

        var request = new AddTodoRequest
        {
            Context = context,
            Title = title,
            Description = description,
            Priority = priority,
            FilePath = filePath,
            LineNumber = lineNumber,
            AssignedTo = assignedTo
        };

        var todo = await _todoService.AddTodoAsync(request, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ TODO added successfully!\n\n" +
                           $"ID: {todo.Id}\n" +
                           $"Title: {todo.Title}\n" +
                           $"Priority: {todo.Priority}\n" +
                           $"Status: {todo.Status}\n" +
                           $"Created: {todo.CreatedAt:yyyy-MM-dd HH:mm}"
                }
            }
        };
    }

    private async Task<McpToolResult> SearchTodosToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.TryGetValue("context", out var ctx) == true ? ctx?.ToString() : null;
        var statusStr = args?.TryGetValue("status", out var stat) == true ? stat?.ToString() : null;
        var priorityStr = args?.TryGetValue("priority", out var prio) == true ? prio?.ToString() : null;
        var assignedTo = args?.TryGetValue("assignedTo", out var assigned) == true ? assigned?.ToString() : null;

        TodoStatus? status = statusStr != null ? Enum.Parse<TodoStatus>(statusStr) : null;
        
        var todos = await _todoService.GetTodosAsync(context, status, cancellationToken);

        // Filter by priority and assignedTo
        if (priorityStr != null)
        {
            var priority = Enum.Parse<TodoPriority>(priorityStr);
            todos = todos.Where(t => t.Priority == priority).ToList();
        }
        if (assignedTo != null)
        {
            todos = todos.Where(t => t.AssignedTo.Contains(assignedTo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var text = todos.Any()
            ? $"Found {todos.Count} TODO(s):\n\n" +
              string.Join("\n\n", todos.Select(t =>
                  $"📌 {t.Title}\n" +
                  $"   ID: {t.Id}\n" +
                  $"   Priority: {t.Priority}\n" +
                  $"   Status: {t.Status}\n" +
                  $"   {(string.IsNullOrEmpty(t.AssignedTo) ? "" : $"Assigned: {t.AssignedTo}\n   ")}" +
                  $"   Created: {t.CreatedAt:yyyy-MM-dd}"))
            : "No TODOs found.";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> UpdateTodoStatusToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var todoId = args?.GetValueOrDefault("todoId")?.ToString() ?? "";
        var statusStr = args?.GetValueOrDefault("status")?.ToString() ?? "";
        var status = Enum.Parse<TodoStatus>(statusStr);

        var todo = await _todoService.UpdateTodoAsync(todoId, status, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ TODO status updated!\n\n" +
                           $"Title: {todo.Title}\n" +
                           $"Status: {todo.Status}\n" +
                           $"{(todo.CompletedAt.HasValue ? $"Completed: {todo.CompletedAt:yyyy-MM-dd HH:mm}" : "")}"
                }
            }
        };
    }

    private async Task<McpToolResult> CreatePlanToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString() ?? "default";
        var name = args?.GetValueOrDefault("name")?.ToString() ?? "";
        var description = args?.GetValueOrDefault("description")?.ToString() ?? "";
        
        var tasks = new List<PlanTaskRequest>();
        if (args?.TryGetValue("tasks", out var tasksArr) == true && tasksArr is IEnumerable<object> tasksList)
        {
            int index = 0;
            foreach (var taskElem in tasksList)
            {
                if (taskElem is Dictionary<string, object> taskDict)
                {
                    tasks.Add(new PlanTaskRequest
                    {
                        Title = taskDict.GetValueOrDefault("title")?.ToString() ?? "",
                        Description = taskDict.GetValueOrDefault("description")?.ToString() ?? "",
                        OrderIndex = (taskDict.GetValueOrDefault("orderIndex") as int?) ?? index++,
                        Dependencies = new List<string>()
                    });
                }
            }
        }

        var request = new AddPlanRequest
        {
            Context = context,
            Name = name,
            Description = description,
            Tasks = tasks
        };

        var plan = await _planService.AddPlanAsync(request, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Development Plan created!\n\n" +
                           $"ID: {plan.Id}\n" +
                           $"Name: {plan.Name}\n" +
                           $"Status: {plan.Status}\n" +
                           $"Tasks: {plan.Tasks.Count}\n" +
                           $"Created: {plan.CreatedAt:yyyy-MM-dd HH:mm}"
                }
            }
        };
    }

    private async Task<McpToolResult> GetPlanStatusToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString() ?? "";
        var plan = await _planService.GetPlanAsync(planId, cancellationToken);

        if (plan == null)
        {
            return ErrorResult($"Plan not found: {planId}");
        }

        var total = plan.Tasks.Count;
        var completed = plan.Tasks.Count(t => t.Status == TaskStatusModel.Completed);
        var inProgress = plan.Tasks.Count(t => t.Status == TaskStatusModel.InProgress);
        var pending = plan.Tasks.Count(t => t.Status == TaskStatusModel.Pending);
        var progress = total > 0 ? (double)completed / total * 100 : 0;

        var text = $"📋 {plan.Name}\n\n" +
                   $"Status: {plan.Status}\n" +
                   $"Progress: {progress:F1}% ({completed}/{total} tasks completed)\n\n" +
                   $"Tasks:\n" +
                   string.Join("\n", plan.Tasks.OrderBy(t => t.OrderIndex).Select(t =>
                       $"  {(t.Status == TaskStatusModel.Completed ? "✅" : t.Status == TaskStatusModel.InProgress ? "🔄" : t.Status == TaskStatusModel.Blocked ? "🚫" : "⏳")} {t.Title} ({t.Status})"));

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> UpdateTaskStatusToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString() ?? "";
        var taskId = args?.GetValueOrDefault("taskId")?.ToString() ?? "";
        var statusStr = args?.GetValueOrDefault("status")?.ToString() ?? "";
        var status = Enum.Parse<TaskStatusModel>(statusStr);

        var plan = await _planService.UpdateTaskStatusAsync(planId, taskId, status, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Task status updated!\n\n" +
                           $"Plan: {plan.Name}\n" +
                           $"Progress: {(double)plan.Tasks.Count(t => t.Status == TaskStatusModel.Completed) / plan.Tasks.Count * 100:F1}%"
                }
            }
        };
    }

    private async Task<McpToolResult> CompletePlanToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString() ?? "";
        var plan = await _planService.CompletePlanAsync(planId, cancellationToken);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Plan completed!\n\n" +
                           $"Name: {plan.Name}\n" +
                           $"Completed: {plan.CompletedAt:yyyy-MM-dd HH:mm}\n" +
                           $"Total tasks: {plan.Tasks.Count}"
                }
            }
        };
    }

    private async Task<McpToolResult> SearchPlansToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.TryGetValue("context", out var ctx) == true ? ctx?.ToString() : null;
        var statusStr = args?.TryGetValue("status", out var stat) == true ? stat?.ToString() : null;
        PlanStatus? status = statusStr != null ? Enum.Parse<PlanStatus>(statusStr) : null;

        var plans = await _planService.GetPlansAsync(context, status, cancellationToken);

        var text = plans.Any()
            ? $"Found {plans.Count} plan(s):\n\n" +
              string.Join("\n\n", plans.Select(p =>
              {
                  var total = p.Tasks.Count;
                  var completed = p.Tasks.Count(t => t.Status == TaskStatusModel.Completed);
                  var progress = total > 0 ? (double)completed / total * 100 : 0;
                  return $"📋 {p.Name}\n" +
                         $"   ID: {p.Id}\n" +
                         $"   Status: {p.Status}\n" +
                         $"   Progress: {progress:F1}% ({completed}/{total})\n" +
                         $"   Created: {p.CreatedAt:yyyy-MM-dd}";
              }))
            : "No plans found.";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateTaskToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var planId = args?.GetValueOrDefault("planId")?.ToString()!;
        var taskId = args?.GetValueOrDefault("taskId")?.ToString()!;
        var autoFix = args?.TryGetValue("autoFix", out var fix) == true && (fix as bool?) == true;

        // Get the plan and task
        var plan = await _planService.GetPlanAsync(planId, cancellationToken);
        if (plan == null)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Plan not found: {planId}" }
                }
            };
        }

        var task = plan.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"Task not found: {taskId}" }
                }
            };
        }

        // Validate the task
        var validationResult = await _validationService.ValidateTaskAsync(task, plan.Context, cancellationToken);

        if (validationResult.IsValid)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"✅ Task '{task.Title}' passed all validation rules!\n\n" +
                               "Task is ready to be marked as completed."
                    }
                }
            };
        }

        // Validation failed
        var failureText = $"❌ Task '{task.Title}' failed validation:\n\n";
        foreach (var failure in validationResult.Failures)
        {
            failureText += $"• {failure.RuleType}: {failure.Message}\n";
            if (failure.CanAutoFix)
            {
                failureText += $"  💡 Auto-fix available: {failure.FixDescription}\n";
            }
            
            // Add actionable context for AI agents (Cursor)
            if (failure.ActionableContext.Any())
            {
                failureText += $"\n  📋 Details:\n";
                
                if (failure.ActionableContext.ContainsKey("suggestion"))
                {
                    failureText += $"     {failure.ActionableContext["suggestion"]}\n";
                }
                
                if (failure.ActionableContext.ContainsKey("methods_to_test"))
                {
                    var methods = failure.ActionableContext["methods_to_test"];
                    failureText += $"\n     Methods needing tests:\n";
                    
                    // Show first few methods
                    if (methods is IEnumerable<object> methodList)
                    {
                        var methodArray = methodList.Take(5).ToArray();
                        for (int i = 0; i < methodArray.Length && i < 5; i++)
                        {
                            var method = methodArray[i];
                            var nameProperty = method.GetType().GetProperty("Name");
                            if (nameProperty != null)
                            {
                                var methodName = nameProperty.GetValue(method)?.ToString();
                                failureText += $"       - {methodName}\n";
                            }
                        }
                        
                        var totalCount = failure.ActionableContext.ContainsKey("method_count") 
                            ? SafeParseInt(failure.ActionableContext["method_count"], 0) 
                            : 0;
                        if (totalCount > 5)
                        {
                            failureText += $"       ... and {totalCount - 5} more\n";
                        }
                    }
                }
                
                if (failure.ActionableContext.ContainsKey("example_test_names"))
                {
                    var examples = failure.ActionableContext["example_test_names"];
                    if (examples is IEnumerable<object> exampleList)
                    {
                        failureText += $"\n     Example test names:\n";
                        foreach (var example in exampleList.Take(3))
                        {
                            failureText += $"       - {example}\n";
                        }
                    }
                }
            }
            
            failureText += "\n";
        }

        // Try auto-fix if requested
        if (autoFix)
        {
            failureText += "\n🔧 Attempting auto-fix...\n";
            var wasFixed = await _validationService.AutoFixValidationFailuresAsync(task, validationResult, plan.Context, cancellationToken);
            
            if (wasFixed)
            {
                failureText += "✅ Auto-fix completed! Please re-validate to confirm.\n";
            }
            else
            {
                failureText += "❌ Auto-fix failed. Manual intervention required.\n";
            }
        }
        else if (validationResult.Suggestions.Any())
        {
            failureText += "\n💡 Suggestions:\n";
            foreach (var suggestion in validationResult.Suggestions)
            {
                failureText += $"• {suggestion}\n";
            }
            failureText += "\nRun with autoFix: true to automatically fix these issues.\n";
        }

        return new McpToolResult
        {
            IsError = !autoFix, // Only error if not auto-fixing
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = failureText }
            }
        };
    }

    private static McpToolResult ErrorResult(string message)
    {
        return new McpToolResult
        {
            IsError = true,
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = $"Error: {message}" }
            }
        };
    }

    // PATTERN DETECTION TOOL IMPLEMENTATIONS

    private async Task<McpToolResult> SearchPatternsToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var query = args?.GetValueOrDefault("query")?.ToString() ?? "";
        var context = args?.GetValueOrDefault("context")?.ToString();
        
        // Safely parse limit from args (handles JsonElement)
        var limit = 20;
        if (args?.TryGetValue("limit", out var limitObj) == true && limitObj != null)
        {
            var limitStr = limitObj.ToString();
            if (int.TryParse(limitStr, out var parsedLimit))
            {
                limit = parsedLimit;
            }
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResult("Query is required");
        }

        var patterns = await _patternService.SearchPatternsAsync(query, context, limit, cancellationToken);

        if (!patterns.Any())
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"No patterns found for query: '{query}'" }
                }
            };
        }

        var text = $"🔍 Found {patterns.Count} pattern(s) for '{query}':\n\n";
        
        foreach (var pattern in patterns)
        {
            text += $"📊 {pattern.Name}\n";
            text += $"   Type: {pattern.Type} ({pattern.Category})\n";
            text += $"   Implementation: {pattern.Implementation}\n";
            text += $"   Language: {pattern.Language}\n";
            text += $"   File: {pattern.FilePath}:{pattern.LineNumber}\n";
            text += $"   Confidence: {pattern.Confidence:P0}\n";
            text += $"   Best Practice: {pattern.BestPractice}\n";
            if (!string.IsNullOrEmpty(pattern.AzureBestPracticeUrl))
            {
                text += $"   📚 Azure Docs: {pattern.AzureBestPracticeUrl}\n";
            }
            text += $"\n   Code:\n   {TruncateCode(pattern.Content, 200)}\n\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateBestPracticesToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString();
        var includeExamples = args?.TryGetValue("includeExamples", out var includeEx) == true ? SafeParseBool(includeEx, true) : true;
        var maxExamples = args?.TryGetValue("maxExamplesPerPractice", out var maxEx) == true ? SafeParseInt(maxEx, 5) : 5;

        if (string.IsNullOrWhiteSpace(context))
        {
            return ErrorResult("Context is required");
        }

        var bestPractices = new List<string>();
        if (args?.TryGetValue("bestPractices", out var bpObj) == true && bpObj is IEnumerable<object> bpList)
        {
            bestPractices = bpList.Select(bp => bp.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        var request = new BestPracticeValidationRequest
        {
            Context = context,
            BestPractices = bestPractices.Any() ? bestPractices : null,
            IncludeExamples = includeExamples,
            MaxExamplesPerPractice = maxExamples
        };

        var result = await _bestPracticeValidation.ValidateBestPracticesAsync(request, cancellationToken);

        var text = $"📋 Best Practice Validation for '{context}'\n\n";
        text += $"Overall Score: {result.OverallScore:P0} ({result.PracticesImplemented}/{result.TotalPracticesChecked} practices)\n";
        text += $"✅ Implemented: {result.PracticesImplemented}\n";
        text += $"❌ Missing: {result.PracticesMissing}\n\n";

        text += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

        // Group by implemented/missing
        var implemented = result.Results.Where(r => r.Implemented).ToList();
        var missing = result.Results.Where(r => !r.Implemented).ToList();

        if (implemented.Any())
        {
            text += "✅ IMPLEMENTED PRACTICES:\n\n";
            foreach (var practice in implemented.OrderByDescending(p => p.Count))
            {
                text += $"• {practice.Practice} ({practice.PatternType})\n";
                text += $"  Count: {practice.Count} instances\n";
                text += $"  Avg Confidence: {practice.AverageConfidence:P0}\n";

                if (includeExamples && practice.Examples.Any())
                {
                    text += "  Examples:\n";
                    foreach (var example in practice.Examples.Take(3))
                    {
                        text += $"    - {example.FilePath}:{example.LineNumber} ({example.Implementation})\n";
                    }
                }
                text += "\n";
            }
        }

        if (missing.Any())
        {
            text += "\n❌ MISSING PRACTICES:\n\n";
            foreach (var practice in missing)
            {
                text += $"• {practice.Practice} ({practice.PatternType})\n";
                text += $"  Recommendation: {practice.Recommendation}\n";
                if (!string.IsNullOrEmpty(practice.AzureUrl))
                {
                    text += $"  📚 Learn more: {practice.AzureUrl}\n";
                }
                text += "\n";
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> GetRecommendationsToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString();
        var includeLowPriority = args?.TryGetValue("includeLowPriority", out var incLow) == true ? SafeParseBool(incLow, false) : false;
        var maxRecommendations = args?.TryGetValue("maxRecommendations", out var maxRec) == true ? SafeParseInt(maxRec, 10) : 10;

        if (string.IsNullOrWhiteSpace(context))
        {
            return ErrorResult("Context is required");
        }

        var categories = new List<PatternCategory>();
        if (args?.TryGetValue("categories", out var catObj) == true && catObj is IEnumerable<object> catList)
        {
            foreach (var cat in catList)
            {
                if (Enum.TryParse<PatternCategory>(cat.ToString(), out var category))
                {
                    categories.Add(category);
                }
            }
        }

        var request = new RecommendationRequest
        {
            Context = context,
            Categories = categories.Any() ? categories : null,
            IncludeLowPriority = includeLowPriority,
            MaxRecommendations = maxRecommendations
        };

        var result = await _recommendationService.AnalyzeAndRecommendAsync(request, cancellationToken);

        var text = $"🎯 Architecture Recommendations for '{context}'\n\n";
        text += $"Overall Health: {result.OverallHealth:P0}\n";
        text += $"Patterns Detected: {result.TotalPatternsDetected}\n";
        text += $"Recommendations: {result.Recommendations.Count}\n\n";

        text += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

        if (!result.Recommendations.Any())
        {
            text += "✅ No critical recommendations! Your project looks good.\n";
        }
        else
        {
            // Group by priority
            var critical = result.Recommendations.Where(r => r.Priority == "CRITICAL").ToList();
            var high = result.Recommendations.Where(r => r.Priority == "HIGH").ToList();
            var medium = result.Recommendations.Where(r => r.Priority == "MEDIUM").ToList();
            var low = result.Recommendations.Where(r => r.Priority == "LOW").ToList();

            if (critical.Any())
            {
                text += "🚨 CRITICAL PRIORITY:\n\n";
                foreach (var rec in critical)
                {
                    text += FormatRecommendation(rec);
                }
            }

            if (high.Any())
            {
                text += "\n⚠️  HIGH PRIORITY:\n\n";
                foreach (var rec in high)
                {
                    text += FormatRecommendation(rec);
                }
            }

            if (medium.Any())
            {
                text += "\n📌 MEDIUM PRIORITY:\n\n";
                foreach (var rec in medium)
                {
                    text += FormatRecommendation(rec);
                }
            }

            if (low.Any() && includeLowPriority)
            {
                text += "\n💡 LOW PRIORITY:\n\n";
                foreach (var rec in low)
                {
                    text += FormatRecommendation(rec);
                }
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> GetAvailableBestPracticesToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var practices = await _bestPracticeValidation.GetAvailableBestPracticesAsync(cancellationToken);

        var text = "📚 Available Azure Best Practices (21 total):\n\n";
        
        var grouped = practices.GroupBy(p => GetCategoryFromPracticeName(p));
        
        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            text += $"{group.Key}:\n";
            foreach (var practice in group.OrderBy(p => p))
            {
                text += $"  • {practice}\n";
            }
            text += "\n";
        }

        text += "\nUsage: Call validate_best_practices with specific practice names,\n";
        text += "or omit to check all 21 practices.\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidatePatternQualityToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var patternId = args?.GetValueOrDefault("pattern_id")?.ToString() ?? "";
        var context = args?.GetValueOrDefault("context")?.ToString();
        var includeAutoFix = args?.GetValueOrDefault("include_auto_fix") as bool? ?? true;
        var minSeverityStr = args?.GetValueOrDefault("min_severity")?.ToString() ?? "low";
        
        var minSeverity = minSeverityStr.ToLower() switch
        {
            "critical" => IssueSeverity.Critical,
            "high" => IssueSeverity.High,
            "medium" => IssueSeverity.Medium,
            _ => IssueSeverity.Low
        };

        var result = await _patternValidationService.ValidatePatternQualityAsync(patternId, context, includeAutoFix, cancellationToken);

        var text = $"🔍 Pattern Quality Validation\n\n";
        text += $"Pattern: {result.Pattern.Name}\n";
        text += $"Quality Score: {result.Score}/10 (Grade: {result.Grade})\n";
        text += $"Security Score: {result.SecurityScore}/10\n\n";

        if (result.Issues.Any())
        {
            text += "❌ Issues Found:\n\n";
            foreach (var issue in result.Issues.Where(i => i.Severity >= minSeverity))
            {
                var icon = issue.Severity switch
                {
                    IssueSeverity.Critical => "🚨",
                    IssueSeverity.High => "❌",
                    IssueSeverity.Medium => "⚠️",
                    _ => "ℹ️"
                };
                text += $"{icon} {issue.Severity}: {issue.Message}\n";
                if (issue.FixGuidance != null)
                    text += $"   💡 Fix: {issue.FixGuidance}\n";
                text += "\n";
            }
        }

        if (result.Recommendations.Any())
        {
            text += "📋 Recommendations:\n";
            foreach (var rec in result.Recommendations)
            {
                text += $"• {rec}\n";
            }
            text += "\n";
        }

        if (!string.IsNullOrEmpty(result.AutoFixCode))
        {
            text += "🔧 Auto-Fix Code:\n\n```\n" + result.AutoFixCode + "\n```\n\n";
        }

        text += $"Summary: {result.Summary}\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> FindAntiPatternsToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString() ?? "";
        var minSeverityStr = args?.GetValueOrDefault("min_severity")?.ToString() ?? "medium";
        var includeLegacy = args?.GetValueOrDefault("include_legacy") as bool? ?? true;

        var minSeverity = minSeverityStr.ToLower() switch
        {
            "critical" => IssueSeverity.Critical,
            "high" => IssueSeverity.High,
            "low" => IssueSeverity.Low,
            _ => IssueSeverity.Medium
        };

        var result = await _patternValidationService.FindAntiPatternsAsync(context, minSeverity, includeLegacy, cancellationToken);

        var text = $"🚨 Anti-Pattern Analysis for {context}\n\n";
        text += $"Total Anti-Patterns Found: {result.TotalCount}\n";
        text += $"Critical Issues: {result.CriticalCount}\n";
        text += $"Overall Security Score: {result.OverallSecurityScore}/10\n\n";

        if (result.AntiPatterns.Any())
        {
            text += "📋 Anti-Patterns Detected:\n\n";
            foreach (var antiPattern in result.AntiPatterns.Take(10))
            {
                text += $"• {antiPattern.Pattern.Name} (Score: {antiPattern.Score}/10)\n";
                text += $"  File: {antiPattern.Pattern.FilePath}\n";
                if (antiPattern.Issues.Any())
                {
                    var topIssue = antiPattern.Issues.OrderByDescending(i => i.Severity).First();
                    text += $"  🚨 {topIssue.Severity}: {topIssue.Message}\n";
                }
                text += "\n";
            }

            if (result.AntiPatterns.Count > 10)
            {
                text += $"... and {result.AntiPatterns.Count - 10} more\n\n";
            }
        }

        text += $"Summary: {result.Summary}\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateSecurityToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString() ?? "";

        var result = await _patternValidationService.ValidateSecurityAsync(context, null, cancellationToken);

        var text = $"🔒 Security Validation for {context}\n\n";
        text += $"Security Score: {result.SecurityScore}/10 ({result.Grade})\n";
        text += $"Vulnerabilities Found: {result.Vulnerabilities.Count}\n\n";

        if (result.Vulnerabilities.Any())
        {
            text += "🚨 Security Vulnerabilities:\n\n";
            foreach (var vuln in result.Vulnerabilities.Take(10))
            {
                var icon = vuln.Severity switch
                {
                    IssueSeverity.Critical => "🚨",
                    IssueSeverity.High => "❗",
                    IssueSeverity.Medium => "⚠️",
                    _ => "ℹ️"
                };
                text += $"{icon} {vuln.Severity} - {vuln.PatternName}\n";
                text += $"  Description: {vuln.Description}\n";
                text += $"  File: {vuln.FilePath}\n";
                if (!string.IsNullOrEmpty(vuln.Reference))
                    text += $"  Reference: {vuln.Reference}\n";
                text += $"  🔧 Remediation: {vuln.Remediation}\n\n";
            }
        }

        if (result.RemediationSteps.Any())
        {
            text += "📋 Priority Remediation Steps:\n";
            foreach (var step in result.RemediationSteps.Take(5))
            {
                text += $"• {step}\n";
            }
            text += "\n";
        }

        text += $"Summary: {result.Summary}\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> GetMigrationPathToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var patternId = args?.GetValueOrDefault("pattern_id")?.ToString() ?? "";
        var includeCodeExample = args?.GetValueOrDefault("include_code_example") as bool? ?? true;

        var result = await _patternValidationService.GetMigrationPathAsync(patternId, includeCodeExample, cancellationToken);

        if (result == null)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent { Type = "text", Text = $"No migration path available for pattern {patternId}" }
                }
            };
        }

        var text = $"🔄 Migration Path\n\n";
        text += $"Current Pattern: {result.CurrentPattern}\n";
        text += $"Target Pattern: {result.TargetPattern}\n";
        text += $"Status: {result.Status}\n";
        text += $"Effort Estimate: {result.EffortEstimate}\n";
        text += $"Complexity: {result.Complexity}\n\n";

        text += "📋 Migration Steps:\n\n";
        foreach (var step in result.Steps)
        {
            text += $"{step.StepNumber}. {step.Title}\n";
            text += $"   {step.Instructions}\n";
            if (step.FilesToModify.Any())
                text += $"   Files: {string.Join(", ", step.FilesToModify)}\n";
            text += "\n";
        }

        if (result.CodeExample != null)
        {
            text += "💡 Code Example:\n\n";
            text += $"{result.CodeExample.Description}\n\n";
            text += "Before:\n```\n" + result.CodeExample.Before + "\n```\n\n";
            text += "After:\n```\n" + result.CodeExample.After + "\n```\n\n";
        }

        if (result.Benefits.Any())
        {
            text += "✅ Benefits:\n";
            foreach (var benefit in result.Benefits)
            {
                text += $"• {benefit}\n";
            }
            text += "\n";
        }

        if (result.Risks.Any())
        {
            text += "⚠️ Risks of NOT Migrating:\n";
            foreach (var risk in result.Risks)
            {
                text += $"• {risk}\n";
            }
            text += "\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> ValidateProjectToolAsync(Dictionary<string, object>? args, CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString() ?? "";

        var result = await _patternValidationService.ValidateProjectAsync(context, cancellationToken);

        var text = $"📊 Project Validation Report - {context}\n\n";
        text += $"Overall Quality Score: {result.OverallScore}/10\n";
        text += $"Security Score: {result.SecurityScore}/10\n";
        text += $"Total Patterns: {result.TotalPatterns}\n\n";

        text += "📈 Patterns by Grade:\n";
        foreach (var grade in result.PatternsByGrade.OrderBy(g => g.Key))
        {
            text += $"  Grade {grade.Key}: {grade.Value} patterns\n";
        }
        text += "\n";

        if (result.CriticalIssues.Any())
        {
            text += $"🚨 Critical Issues ({result.CriticalIssues.Count}):\n";
            foreach (var issue in result.CriticalIssues.Take(5))
            {
                text += $"  • {issue.Message}\n";
            }
            if (result.CriticalIssues.Count > 5)
                text += $"  ... and {result.CriticalIssues.Count - 5} more\n";
            text += "\n";
        }

        if (result.SecurityVulnerabilities.Any())
        {
            text += $"🔒 Security Vulnerabilities ({result.SecurityVulnerabilities.Count}):\n";
            foreach (var vuln in result.SecurityVulnerabilities.Take(5))
            {
                text += $"  {vuln.Severity}: {vuln.Description}\n";
            }
            if (result.SecurityVulnerabilities.Count > 5)
                text += $"  ... and {result.SecurityVulnerabilities.Count - 5} more\n";
            text += "\n";
        }

        if (result.LegacyPatterns.Any())
        {
            text += $"⚠️ Legacy Patterns Needing Migration ({result.LegacyPatterns.Count}):\n";
            foreach (var legacy in result.LegacyPatterns.Take(5))
            {
                text += $"  • {legacy.CurrentPattern} → {legacy.TargetPattern} ({legacy.EffortEstimate})\n";
            }
            if (result.LegacyPatterns.Count > 5)
                text += $"  ... and {result.LegacyPatterns.Count - 5} more\n";
            text += "\n";
        }

        if (result.TopRecommendations.Any())
        {
            text += "📋 Top Recommendations:\n";
            foreach (var rec in result.TopRecommendations)
            {
                text += $"  {rec}\n";
            }
            text += "\n";
        }

        text += $"Summary: {result.Summary}\n";
        text += $"Generated: {result.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    private async Task<McpToolResult> AnalyzeCodeComplexityToolAsync(
        Dictionary<string, object> args,
        CancellationToken cancellationToken)
    {
        var filePath = args.GetValueOrDefault("filePath")?.ToString() ?? "";
        var methodName = args.GetValueOrDefault("methodName")?.ToString();

        var result = await _complexityService.AnalyzeFileAsync(filePath, methodName, cancellationToken);

        if (!result.Success)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"Error analyzing code complexity:\n{string.Join("\n", result.Errors)}"
                    }
                }
            };
        }

        var text = $"📊 Code Complexity Analysis\n";
        text += $"File: {result.FilePath}\n";
        if (!string.IsNullOrEmpty(result.MethodName))
        {
            text += $"Method: {result.MethodName}\n";
        }
        text += $"\n";

        // Summary
        text += $"📈 Summary (Overall Grade: {result.Summary.OverallGrade})\n";
        text += $"  Total Methods: {result.Summary.TotalMethods}\n";
        text += $"  Avg Cyclomatic Complexity: {result.Summary.AverageCyclomaticComplexity}\n";
        text += $"  Avg Cognitive Complexity: {result.Summary.AverageCognitiveComplexity}\n";
        text += $"  Avg Lines of Code: {result.Summary.AverageLinesOfCode}\n";
        text += $"  Max Cyclomatic Complexity: {result.Summary.MaxCyclomaticComplexity}\n";
        text += $"  Max Cognitive Complexity: {result.Summary.MaxCognitiveComplexity}\n";
        text += $"  Methods with High Complexity: {result.Summary.MethodsWithHighComplexity}\n";
        text += $"  Methods with Code Smells: {result.Summary.MethodsWithCodeSmells}\n";
        text += $"\n";

        if (result.Summary.FileRecommendations.Any())
        {
            text += $"📋 File-Level Recommendations:\n";
            foreach (var rec in result.Summary.FileRecommendations)
            {
                text += $"  {rec}\n";
            }
            text += $"\n";
        }

        // Method details
        if (result.Methods.Any())
        {
            text += $"🔍 Method Details:\n\n";
            
            // Sort by grade (worst first) then by complexity
            var sortedMethods = result.Methods
                .OrderBy(m => m.Grade switch { "F" => 1, "D" => 2, "C" => 3, "B" => 4, "A" => 5, _ => 6 })
                .ThenByDescending(m => m.CyclomaticComplexity)
                .ToList();

            foreach (var method in sortedMethods)
            {
                var gradeEmoji = method.Grade switch
                {
                    "A" => "✅",
                    "B" => "✅",
                    "C" => "⚠️",
                    "D" => "❌",
                    "F" => "🔴",
                    _ => "❓"
                };

                text += $"{gradeEmoji} {method.ClassName}.{method.MethodName} (Grade: {method.Grade})\n";
                text += $"  Lines: {method.StartLine}-{method.EndLine} ({method.LinesOfCode} LOC)\n";
                text += $"  Cyclomatic Complexity: {method.CyclomaticComplexity}\n";
                text += $"  Cognitive Complexity: {method.CognitiveComplexity}\n";
                text += $"  Max Nesting Depth: {method.MaxNestingDepth}\n";
                text += $"  Parameters: {method.ParameterCount}\n";
                
                if (method.DatabaseCalls > 0)
                {
                    text += $"  Database Calls: {method.DatabaseCalls}\n";
                }
                
                if (method.HasHttpCalls)
                {
                    text += $"  Has HTTP Calls: Yes\n";
                }
                
                if (method.IsPublic)
                {
                    text += $"  Visibility: Public API\n";
                }
                
                if (method.CodeSmells.Any())
                {
                    text += $"  Code Smells: {string.Join(", ", method.CodeSmells)}\n";
                }
                
                if (method.ExceptionTypes.Any())
                {
                    text += $"  Exception Types: {string.Join(", ", method.ExceptionTypes)}\n";
                }

                if (method.Recommendations.Any())
                {
                    text += $"  Recommendations:\n";
                    foreach (var rec in method.Recommendations)
                    {
                        text += $"    {rec}\n";
                    }
                }
                
                text += $"\n";
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = text }
            }
        };
    }

    // Helper methods

    private string TruncateCode(string code, int maxLength)
    {
        if (code.Length <= maxLength)
        {
            return code.Replace("\n", "\n   ");
        }

        return code.Substring(0, maxLength - 3).Replace("\n", "\n   ") + "...";
    }

    private string FormatRecommendation(PatternRecommendation rec)
    {
        var text = $"• {rec.Issue}\n";
        text += $"  Category: {rec.Category} ({rec.PatternType})\n";
        text += $"  Recommendation: {rec.Recommendation}\n";
        text += $"  Impact: {rec.Impact}\n";

        if (rec.AffectedFiles.Any())
        {
            text += $"  Affected Files: {string.Join(", ", rec.AffectedFiles.Take(3))}{(rec.AffectedFiles.Count > 3 ? "..." : "")}\n";
        }

        if (!string.IsNullOrEmpty(rec.AzureUrl))
        {
            text += $"  📚 Learn more: {rec.AzureUrl}\n";
        }

        if (!string.IsNullOrEmpty(rec.CodeExample))
        {
            text += $"  Example:\n{IndentCode(rec.CodeExample, 4)}\n";
        }

        text += "\n";
        return text;
    }

    private string IndentCode(string code, int spaces)
    {
        var indent = new string(' ', spaces);
        return indent + code.Replace("\n", "\n" + indent);
    }

    private string GetCategoryFromPracticeName(string practice)
    {
        if (practice.Contains("cache")) return "Performance (Caching)";
        if (practice.Contains("retry") || practice.Contains("circuit") || practice.Contains("timeout")) return "Reliability (Resilience)";
        if (practice.Contains("validation")) return "Security (Validation)";
        if (practice.Contains("auth") || practice.Contains("encryption")) return "Security";
        if (practice.Contains("pagination") || practice.Contains("versioning") || practice.Contains("rate")) return "API Design";
        if (practice.Contains("health") || practice.Contains("logging") || practice.Contains("metrics")) return "Observability";
        if (practice.Contains("background") || practice.Contains("message")) return "Performance (Background Processing)";
        if (practice.Contains("configuration") || practice.Contains("feature")) return "Configuration";
        return "General";
    }

    // Helper methods to safely parse arguments (handles JsonElement)
    private int SafeParseInt(object? value, int defaultValue)
    {
        if (value == null) return defaultValue;
        
        var str = value.ToString();
        if (int.TryParse(str, out var result))
        {
            return result;
        }
        
        return defaultValue;
    }

    private bool SafeParseBool(object? value, bool defaultValue)
    {
        if (value == null) return defaultValue;
        
        var str = value.ToString();
        if (bool.TryParse(str, out var result))
        {
            return result;
        }
        
        return defaultValue;
    }

    private async Task<McpToolResult> RegisterWorkspaceToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var workspacePath = args?.GetValueOrDefault("workspacePath")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString();

        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(context))
        {
            return ErrorResult("workspacePath and context are required");
        }

        _logger.LogInformation("🔧 Registering workspace: {Path} → {Context}", workspacePath, context);

        try
        {
            // Step 1: Create isolated Qdrant collections for this workspace
            var vectorService = _serviceProvider.GetRequiredService<IVectorService>();
            await vectorService.InitializeCollectionsForContextAsync(context, cancellationToken);
            _logger.LogInformation("  ✅ Qdrant collections created for: {Context}", context);

            // Step 2: Create isolated Neo4j database for this workspace
            await _graphService.CreateDatabaseAsync(context, cancellationToken);
            _logger.LogInformation("  ✅ Neo4j database created for: {Context}", context);

            // Step 3: Register file watcher for auto-reindex
            var autoReindexService = _serviceProvider.GetService<FileWatcher.IAutoReindexService>();
            if (autoReindexService != null)
            {
                await autoReindexService.RegisterWorkspaceAsync(workspacePath, context);
                _logger.LogInformation("  ✅ File watcher started for: {Context}", context);
            }
            else
            {
                _logger.LogWarning("  ⚠️ AutoReindexService not available - file watching disabled");
            }

            // Step 4: Check if workspace is empty - if so, trigger initial full reindex
            var filePaths = await vectorService.GetFilePathsForContextAsync(context, cancellationToken);
            if (!filePaths.Any())
            {
                _logger.LogInformation("  🔍 Collections empty, triggering initial full reindex...");
                
                // Trigger background reindex (don't wait for it - can take a while)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var indexingService = _serviceProvider.GetRequiredService<IIndexingService>();
                        await indexingService.IndexDirectoryAsync(
                            workspacePath,
                            true, // recursive
                            context,
                            CancellationToken.None
                        );
                        _logger.LogInformation("  ✅ Initial reindex completed for: {Context}", context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "  ❌ Initial reindex failed for: {Context}", context);
                    }
                }, CancellationToken.None);

                return new McpToolResult
                {
                    Content = new List<McpContent>
                    {
                        new McpContent
                        {
                            Type = "text",
                            Text = $"✅ Workspace registered with isolated storage:\n" +
                                   $"  Path: {workspacePath}\n" +
                                   $"  Context: {context}\n" +
                                   $"  Qdrant Collections: {context.ToLower()}_files, {context.ToLower()}_classes, {context.ToLower()}_methods, {context.ToLower()}_patterns\n" +
                                   $"  Neo4j Database: {context.ToLower()}\n" +
                                   $"  File Watcher: {(autoReindexService != null ? "Active" : "Disabled")}\n" +
                                   $"\n🔄 Initial indexing started in background... This may take a few minutes."
                        }
                    }
                };
            }

            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"✅ Workspace registered with isolated storage:\n" +
                               $"  Path: {workspacePath}\n" +
                               $"  Context: {context}\n" +
                               $"  Qdrant Collections: {context.ToLower()}_files, {context.ToLower()}_classes, {context.ToLower()}_methods, {context.ToLower()}_patterns\n" +
                               $"  Neo4j Database: {context.ToLower()}\n" +
                               $"  File Watcher: {(autoReindexService != null ? "Active" : "Disabled")}\n" +
                               $"  Indexed Files: {filePaths.Count}"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering workspace: {Context}", context);
            return new McpToolResult
            {
                IsError = true,
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = $"❌ Error registering workspace: {ex.Message}"
                    }
                }
            };
        }
    }

    private async Task<McpToolResult> UnregisterWorkspaceToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var workspacePath = args?.GetValueOrDefault("workspacePath")?.ToString();

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return ErrorResult("workspacePath is required");
        }

        var autoReindexService = _serviceProvider.GetService<FileWatcher.IAutoReindexService>();
        if (autoReindexService == null)
        {
            return new McpToolResult
            {
                Content = new List<McpContent>
                {
                    new McpContent
                    {
                        Type = "text",
                        Text = "⚠️ AutoReindexService not available"
                    }
                }
            };
        }

        await autoReindexService.UnregisterWorkspaceAsync(workspacePath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = $"✅ Workspace unregistered:\n  Path: {workspacePath}"
                }
            }
        };
    }

    // ===========================
    // TRANSFORMATION TOOL HANDLERS
    // ===========================

    private async Task<McpToolResult> TransformPageToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return ErrorResult("sourcePath is required");
        }

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.TransformPage(
            sourcePath,
            SafeParseBool(args?.GetValueOrDefault("extractComponents"), true),
            SafeParseBool(args?.GetValueOrDefault("modernizeCSS"), true),
            SafeParseBool(args?.GetValueOrDefault("addErrorHandling"), true),
            SafeParseBool(args?.GetValueOrDefault("addLoadingStates"), true),
            SafeParseBool(args?.GetValueOrDefault("addAccessibility"), true),
            args?.GetValueOrDefault("outputDirectory")?.ToString()
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> LearnTransformationToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var exampleOldPath = args?.GetValueOrDefault("exampleOldPath")?.ToString();
        var exampleNewPath = args?.GetValueOrDefault("exampleNewPath")?.ToString();
        var patternName = args?.GetValueOrDefault("patternName")?.ToString();

        if (string.IsNullOrWhiteSpace(exampleOldPath) || string.IsNullOrWhiteSpace(exampleNewPath) || string.IsNullOrWhiteSpace(patternName))
        {
            return ErrorResult("exampleOldPath, exampleNewPath, and patternName are required");
        }

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.LearnTransformation(exampleOldPath, exampleNewPath, patternName);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ApplyTransformationToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var patternId = args?.GetValueOrDefault("patternId")?.ToString();
        var targetPath = args?.GetValueOrDefault("targetPath")?.ToString();

        if (string.IsNullOrWhiteSpace(patternId) || string.IsNullOrWhiteSpace(targetPath))
        {
            return ErrorResult("patternId and targetPath are required");
        }

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.ApplyTransformation(patternId, targetPath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ListTransformationPatternsToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var context = args?.GetValueOrDefault("context")?.ToString();

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.ListTransformationPatterns(context);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> DetectReusableComponentsToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var projectPath = args?.GetValueOrDefault("projectPath")?.ToString();
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return ErrorResult("projectPath is required");
        }

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.DetectReusableComponents(
            projectPath,
            SafeParseInt(args?.GetValueOrDefault("minOccurrences"), 2),
            (float)SafeParseDouble(args?.GetValueOrDefault("minSimilarity"), 0.7)
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> ExtractComponentToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        // Handle componentCandidateJson - it can be passed as a JSON string OR as a JSON object
        string? componentCandidateJson = null;
        var candidateArg = args?.GetValueOrDefault("componentCandidateJson");
        if (candidateArg != null)
        {
            if (candidateArg is JsonElement jsonElement)
            {
                // If it's a JsonElement, serialize it to a JSON string
                componentCandidateJson = jsonElement.GetRawText();
            }
            else if (candidateArg is string str)
            {
                componentCandidateJson = str;
            }
            else
            {
                // Fallback: try to serialize the object to JSON
                componentCandidateJson = JsonSerializer.Serialize(candidateArg);
            }
        }
        
        var outputPath = args?.GetValueOrDefault("outputPath")?.ToString();

        if (string.IsNullOrWhiteSpace(componentCandidateJson) || string.IsNullOrWhiteSpace(outputPath))
        {
            return ErrorResult("componentCandidateJson and outputPath are required");
        }

        _logger.LogDebug("ExtractComponent - JSON received (first 200 chars): {Json}", 
            componentCandidateJson.Length > 200 ? componentCandidateJson[..200] + "..." : componentCandidateJson);

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.ExtractComponent(componentCandidateJson, outputPath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> TransformCSSToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return ErrorResult("sourcePath is required");
        }

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.TransformCSS(
            sourcePath,
            SafeParseBool(args?.GetValueOrDefault("generateVariables"), true),
            SafeParseBool(args?.GetValueOrDefault("modernizeLayout"), true),
            SafeParseBool(args?.GetValueOrDefault("addResponsive"), true),
            SafeParseBool(args?.GetValueOrDefault("addAccessibility"), true),
            args?.GetValueOrDefault("outputPath")?.ToString()
        );

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private async Task<McpToolResult> AnalyzeCSSToolAsync(
        Dictionary<string, object>? args,
        CancellationToken cancellationToken)
    {
        var sourcePath = args?.GetValueOrDefault("sourcePath")?.ToString();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return ErrorResult("sourcePath is required");
        }

        var transformationTools = _serviceProvider.GetRequiredService<MCP.TransformationTools>();
        var result = await transformationTools.AnalyzeCSS(sourcePath);

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent
                {
                    Type = "text",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                }
            }
        };
    }

    private double SafeParseDouble(object? value, double defaultValue)
    {
        if (value == null) return defaultValue;
        
        var str = value.ToString();
        if (double.TryParse(str, out var result))
        {
            return result;
        }
        
        return defaultValue;
    }
}





