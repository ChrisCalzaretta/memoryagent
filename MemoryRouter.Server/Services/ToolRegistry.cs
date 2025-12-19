using System.Collections.Concurrent;
using MemoryRouter.Server.Models;
using MemoryRouter.Server.Clients;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Dynamically discovers and registers all tools from MemoryAgent and CodingOrchestrator
/// Augments them with orchestration metadata (keywords, use cases) for better AI routing
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly ILogger<ToolRegistry> _logger;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ICodingOrchestratorClient _codingOrchestrator;
    private readonly ConcurrentDictionary<string, ToolDefinition> _tools = new();
    private bool _initialized = false;

    public ToolRegistry(
        ILogger<ToolRegistry> logger, 
        IMemoryAgentClient memoryAgent,
        ICodingOrchestratorClient codingOrchestrator)
    {
        _logger = logger;
        _memoryAgent = memoryAgent;
        _codingOrchestrator = codingOrchestrator;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            _logger.LogInformation("ToolRegistry already initialized");
            return;
        }

        _logger.LogInformation("🔧 Initializing ToolRegistry - dynamically discovering all tools...");

        try
        {
            // Dynamically discover tools from both services
            await DiscoverMemoryAgentToolsAsync(cancellationToken);
            await DiscoverCodingOrchestratorToolsAsync(cancellationToken);

            _initialized = true;
            
            _logger.LogInformation("✅ ToolRegistry initialized with {ToolCount} tools", _tools.Count);
            _logger.LogInformation("   📦 MemoryAgent tools: {Count}", _tools.Count(t => t.Value.Service == "memory-agent"));
            _logger.LogInformation("   🎯 CodingOrchestrator tools: {Count}", _tools.Count(t => t.Value.Service == "coding-orchestrator"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize ToolRegistry");
            throw;
        }
    }

    public IEnumerable<ToolDefinition> GetAllTools()
    {
        return _tools.Values.OrderBy(t => t.Service).ThenBy(t => t.Name);
    }

    public ToolDefinition? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IEnumerable<ToolDefinition> SearchTools(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        
        return _tools.Values.Where(t =>
            t.Name.ToLowerInvariant().Contains(lowerQuery) ||
            t.Description.ToLowerInvariant().Contains(lowerQuery) ||
            t.Keywords.Any(k => k.ToLowerInvariant().Contains(lowerQuery)) ||
            t.UseCases.Any(u => u.ToLowerInvariant().Contains(lowerQuery))
        );
    }

    /// <summary>
    /// Dynamically discover tools from MemoryAgent MCP server
    /// </summary>
    private async Task DiscoverMemoryAgentToolsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔍 Discovering MemoryAgent tools...");
        
        var mcpTools = await _memoryAgent.GetToolsAsync(cancellationToken);
        
        foreach (var mcpTool in mcpTools)
        {
            var tool = new ToolDefinition
            {
                Name = mcpTool.Name,
                Description = mcpTool.Description ?? string.Empty,
                Service = "memory-agent",
                InputSchema = mcpTool.InputSchema ?? new Dictionary<string, object>()
            };
            
            // Augment with orchestration metadata for better AI routing
            AugmentToolMetadata(tool);
            RegisterTool(tool);
        }
        
        _logger.LogInformation("✅ Discovered {Count} MemoryAgent tools", _tools.Count(t => t.Value.Service == "memory-agent"));
    }

    /// <summary>
    /// Dynamically discover tools from CodingOrchestrator MCP server
    /// </summary>
    private async Task DiscoverCodingOrchestratorToolsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔍 Discovering CodingOrchestrator tools...");
        
        var mcpTools = await _codingOrchestrator.GetToolsAsync(cancellationToken);
        
        foreach (var mcpTool in mcpTools)
        {
            var tool = new ToolDefinition
            {
                Name = mcpTool.Name,
                Description = mcpTool.Description ?? string.Empty,
                Service = "coding-orchestrator",
                InputSchema = mcpTool.InputSchema ?? new Dictionary<string, object>()
            };
            
            // Augment with orchestration metadata for better AI routing
            AugmentToolMetadata(tool);
            RegisterTool(tool);
        }
        
        _logger.LogInformation("✅ Discovered {Count} CodingOrchestrator tools", _tools.Count(t => t.Value.Service == "coding-orchestrator"));
    }

    /// <summary>
    /// Augment discovered tools with keywords and use cases for better FunctionGemma orchestration
    /// This metadata helps the AI understand WHEN to use each tool
    /// </summary>
    private void AugmentToolMetadata(ToolDefinition tool)
    {
        // Extract keywords from tool name and description
        var keywords = new List<string>();
        var useCases = new List<string>();
        
        var lowerName = tool.Name.ToLowerInvariant();
        var lowerDesc = tool.Description.ToLowerInvariant();
        
        // 🔍 SEARCH TOOLS - Finding existing code, documentation, patterns
        if (lowerName.Contains("search") || lowerName.Contains("find") || lowerName == "smartsearch")
        {
            keywords.AddRange(new[] { "search", "find", "query", "lookup", "discover", "locate", "where is" });
            useCases.AddRange(new[] { 
                "Find existing code or files", 
                "Search for patterns or examples",
                "Locate specific functionality",
                "Discover how something works",
                "Find authentication/API/database code"
            });
            
            // Enhanced description
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Search codebase for existing code, patterns, functions, or files using semantic search";
            }
        }
        
        // 📦 INDEXING TOOLS - Making workspace searchable
        if (lowerName.Contains("index") || lowerDesc.Contains("index"))
        {
            keywords.AddRange(new[] { "index", "workspace", "setup", "initialize", "prepare", "scan" });
            useCases.AddRange(new[] { 
                "First-time project setup", 
                "Make codebase searchable", 
                "Enable semantic search",
                "Scan new project",
                "Prepare workspace for AI"
            });
            
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Index workspace files to enable semantic search and code understanding";
            }
        }
        
        // 🔬 ANALYSIS TOOLS - Understanding code structure
        if (lowerName.Contains("analyze") || lowerName.Contains("explain") || lowerName.Contains("dependency"))
        {
            keywords.AddRange(new[] { "analyze", "understand", "explain", "dependencies", "relationships", "how does", "what is" });
            useCases.AddRange(new[] { 
                "Understand code structure", 
                "Learn how feature works", 
                "Find dependencies between files",
                "Explain complex code",
                "Map system architecture"
            });
            
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Analyze code to understand structure, dependencies, and relationships";
            }
        }
        
        // ✅ VALIDATION TOOLS - Code review and quality checks
        if (lowerName.Contains("validate") || lowerName.Contains("check") || lowerName.Contains("review"))
        {
            keywords.AddRange(new[] { "validate", "check", "review", "quality", "security", "compliance", "best practices" });
            useCases.AddRange(new[] { 
                "Review code quality", 
                "Check for security issues", 
                "Validate best practices",
                "Security audit",
                "Code standards compliance"
            });
            
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Validate code for quality, security, and best practices compliance";
            }
        }
        
        // 📋 PLANNING TOOLS - Breaking down work into tasks
        if (lowerName.Contains("plan") || lowerName.Contains("manage_plan"))
        {
            keywords.AddRange(new[] { "plan", "strategy", "breakdown", "roadmap", "execution plan", "task list" });
            useCases.AddRange(new[] { 
                "Create execution plan for feature", 
                "Break down complex project", 
                "Generate implementation roadmap",
                "Plan development strategy",
                "Create step-by-step guide"
            });
            
            if (string.IsNullOrEmpty(tool.Description) || tool.Description.Length < 50)
            {
                tool.Description = "Create detailed execution plans with task breakdowns, timelines, and implementation strategies for projects or features";
            }
        }
        
        // 📝 TODO/TASK MANAGEMENT - Tracking work items
        if (lowerName.Contains("todo") && !lowerName.Contains("plan"))
        {
            keywords.AddRange(new[] { "todo", "task", "reminder", "track", "manage", "list" });
            useCases.AddRange(new[] { 
                "Add TODO reminders", 
                "Track work items", 
                "Manage task list",
                "Create reminders",
                "List pending tasks"
            });
            
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Manage TODO items and task tracking within the codebase";
            }
        }
        
        // 🚀 CODE GENERATION/ORCHESTRATION - Creating new code
        if (lowerName.Contains("orchestrate") || (lowerName.Contains("task") && !lowerName.Contains("todo")))
        {
            keywords.AddRange(new[] { "code", "generate", "create", "build", "implement", "develop", "write", "make" });
            useCases.AddRange(new[] { 
                "Generate new code from scratch", 
                "Create complete features or apps", 
                "Build REST APIs or services",
                "Implement new functionality",
                "Write full applications",
                "Develop microservices"
            });
            
            if (string.IsNullOrEmpty(tool.Description) || tool.Description.Length < 50)
            {
                tool.Description = "Generate complete applications, features, or code files from natural language descriptions. Use for creating new code (not searching/analyzing existing code)";
            }
        }
        
        // 🎨 DESIGN/BRAND TOOLS - UI/UX and brand management
        if (lowerName.Contains("design") || lowerName.Contains("brand"))
        {
            keywords.AddRange(new[] { "design", "brand", "UI", "UX", "style", "guidelines", "theme", "colors" });
            useCases.AddRange(new[] { 
                "Create design system", 
                "Manage brand guidelines", 
                "Validate UI consistency",
                "Generate style guides",
                "Design questionnaire"
            });
            
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Manage design systems, brand guidelines, and UI/UX validation";
            }
        }
        
        // 🧠 LEARNING/KNOWLEDGE TOOLS - Storing and retrieving facts
        if (lowerName.Contains("learn") || lowerName.Contains("knowledge") || lowerName.Contains("qa") || lowerName.Contains("question"))
        {
            keywords.AddRange(new[] { "learn", "knowledge", "remember", "store", "fact", "qa", "question", "answer" });
            useCases.AddRange(new[] { 
                "Remember project decisions", 
                "Store team knowledge", 
                "Learn from conversations",
                "Answer questions about project",
                "Retrieve historical context"
            });
            
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Store and retrieve project knowledge, decisions, and conversational context";
            }
        }
        
        // 📊 STATUS/MONITORING TOOLS - Checking progress and state
        if (lowerName.Contains("status") || (lowerName.Contains("list") && !lowerName.Contains("plan")) || lowerName.Contains("get_"))
        {
            keywords.AddRange(new[] { "status", "list", "check", "monitor", "view", "show", "get", "progress" });
            useCases.AddRange(new[] { 
                "Check task/job status", 
                "View progress", 
                "Monitor running operations",
                "List completed work",
                "Get task results"
            });
            
            if (string.IsNullOrEmpty(tool.Description))
            {
                tool.Description = "Check status, view progress, and monitor ongoing operations";
            }
        }
        
        // 🛑 CANCEL/STOP TOOLS - Canceling operations
        if (lowerName.Contains("cancel") || lowerName.Contains("stop") || lowerName.Contains("abort"))
        {
            keywords.AddRange(new[] { "cancel", "stop", "abort", "terminate", "kill", "end" });
            useCases.AddRange(new[] { 
                "Cancel running operations", 
                "Stop long-running tasks", 
                "Abort failed jobs",
                "Terminate processes",
                "End background work"
            });
            
            if (string.IsNullOrEmpty(tool.Description) || tool.Description.Length < 30)
            {
                tool.Description = "Cancel, stop, or abort running operations and background tasks";
            }
        }
        
        // Add the tool name itself as a keyword
        keywords.Add(lowerName.Replace("_", " "));
        
        // Remove duplicates and update tool
        tool.Keywords = keywords.Distinct().ToList();
        tool.UseCases = useCases.Distinct().ToList();
    }


    private void RegisterTool(ToolDefinition tool)
    {
        if (_tools.TryAdd(tool.Name, tool))
        {
            _logger.LogDebug("   ➕ Registered tool: {Service}/{Tool}", tool.Service, tool.Name);
        }
        else
        {
            _logger.LogWarning("   ⚠️ Tool already exists: {Tool}", tool.Name);
        }
    }
}

