using System.Text;
using System.Text.Json;
using MemoryAgent.Server.Models;
using Neo4j.Driver;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// 🚀 SMART CODE GENERATION - MCP Tool Handler
/// Handles tools for intelligent code generation:
/// - generate_task_plan: Create execution plan before code generation
/// - get_project_symbols: Get all classes/methods/functions in context
/// - validate_imports: Check imports before Docker execution
/// - store_successful_task: Store working approaches
/// - query_similar_tasks: Find similar successful tasks
/// </summary>
public class SmartCodegenToolHandler : IMcpToolHandler
{
    private readonly IIndexingService _indexingService;
    private readonly IDriver _neo4j;
    private readonly IVectorService _vectorService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<SmartCodegenToolHandler> _logger;
    
    // Collection names for Neo4j node types
    private const string SuccessfulTaskNodeType = "SuccessfulTask";

    public SmartCodegenToolHandler(
        IIndexingService indexingService,
        IDriver neo4j,
        IVectorService vectorService,
        IEmbeddingService embeddingService,
        ILogger<SmartCodegenToolHandler> logger)
    {
        _indexingService = indexingService;
        _neo4j = neo4j;
        _vectorService = vectorService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "generate_task_plan",
                Description = "Generate an execution plan with checklist before code generation. Returns semantic breakdown, required classes/methods, and file dependency order.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        task = new { type = "string", description = "The coding task to plan" },
                        language = new { type = "string", description = "Target language (python, csharp, typescript, etc.)" },
                        context = new { type = "string", description = "Project context name" }
                    },
                    required = new[] { "task", "language", "context" }
                }
            },
            new McpTool
            {
                Name = "get_project_symbols",
                Description = "Get all indexed symbols (classes, methods, functions) in a project context. Includes signatures, descriptions, and import statements.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        context = new { type = "string", description = "Project context name" },
                        filter = new { type = "string", description = "Optional: filter by name (partial match)" },
                        includeDescriptions = new { type = "boolean", description = "Include detailed descriptions", @default = true }
                    },
                    required = new[] { "context" }
                }
            },
            new McpTool
            {
                Name = "validate_imports",
                Description = "Validate that all imports in code exist and are available. Returns list of valid/invalid imports with suggestions.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string", description = "The code to validate imports for" },
                        language = new { type = "string", description = "Language (python, csharp, typescript, etc.)" },
                        context = new { type = "string", description = "Project context for local imports" }
                    },
                    required = new[] { "code", "language" }
                }
            },
            new McpTool
            {
                Name = "store_successful_task",
                Description = "Store a successful task approach for future learning. Saved to both context-specific and shared collections.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        taskDescription = new { type = "string", description = "Description of the task" },
                        language = new { type = "string", description = "Programming language" },
                        context = new { type = "string", description = "Project context" },
                        approachUsed = new { type = "string", description = "Description of the approach that worked" },
                        patternsUsed = new { type = "array", items = new { type = "string" }, description = "Patterns used" },
                        filesGenerated = new { type = "array", items = new { type = "string" }, description = "Files that were generated" },
                        usefulSnippets = new { type = "array", items = new { type = "object" }, description = "Reusable code snippets" },
                        keywords = new { type = "array", items = new { type = "string" }, description = "Task keywords" },
                        iterationsNeeded = new { type = "integer", description = "How many iterations it took" },
                        finalScore = new { type = "integer", description = "Final validation score" },
                        modelUsed = new { type = "string", description = "Model that generated the code" },
                        semanticStructure = new { type = "string", description = "The class/method structure that worked" }
                    },
                    required = new[] { "taskDescription", "language", "context" }
                }
            },
            new McpTool
            {
                Name = "query_similar_tasks",
                Description = "Query similar successful tasks for guidance on how to approach a new task.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        task = new { type = "string", description = "The task to find similar tasks for" },
                        language = new { type = "string", description = "Target language" },
                        limit = new { type = "integer", description = "Max results", @default = 3 }
                    },
                    required = new[] { "task" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        return toolName switch
        {
            "generate_task_plan" => await GenerateTaskPlanAsync(args, cancellationToken),
            "get_project_symbols" => await GetProjectSymbolsAsync(args, cancellationToken),
            "validate_imports" => await ValidateImportsAsync(args, cancellationToken),
            "store_successful_task" => await StoreSuccessfulTaskAsync(args, cancellationToken),
            "query_similar_tasks" => await QuerySimilarTasksAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Generate Task Plan

    private async Task<McpToolResult> GenerateTaskPlanAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var task = args?.GetValueOrDefault("task")?.ToString() ?? "";
        var language = args?.GetValueOrDefault("language")?.ToString()?.ToLowerInvariant() ?? "python";
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";

        _logger.LogInformation("📋 Generating task plan for: {Task} ({Language})", task.Length > 50 ? task[..50] + "..." : task, language);

        // Get semantic breakdown based on task
        var semanticBreakdown = AnalyzeTaskSemantics(task, language);
        
        // Generate required classes and methods
        var requiredClasses = ExtractRequiredClasses(task, language);
        var requiredMethods = ExtractRequiredMethods(task, language);
        
        // Determine file dependency order
        var dependencyOrder = DetermineDependencyOrder(task, language, requiredClasses);
        
        // Generate plan steps
        var steps = GeneratePlanSteps(task, language, semanticBreakdown, requiredClasses, dependencyOrder);
        
        // Store plan in Neo4j for tracking
        var planId = await StorePlanInNeo4jAsync(task, language, context, steps, ct);

        var plan = new
        {
            planId,
            task,
            language,
            context,
            steps,
            semanticBreakdown,
            requiredClasses,
            requiredMethods,
            dependencyOrder,
            createdAt = DateTime.UtcNow
        };

        var output = new StringBuilder();
        output.AppendLine($"📋 TASK PLAN GENERATED");
        output.AppendLine($"Plan ID: {planId}");
        output.AppendLine($"Language: {language}");
        output.AppendLine();
        output.AppendLine("🎯 SEMANTIC BREAKDOWN:");
        output.AppendLine(semanticBreakdown);
        output.AppendLine();
        output.AppendLine("📦 REQUIRED COMPONENTS:");
        output.AppendLine($"  Classes: {string.Join(", ", requiredClasses)}");
        output.AppendLine($"  Methods: {string.Join(", ", requiredMethods)}");
        output.AppendLine();
        output.AppendLine("📁 FILE ORDER (generate in this order):");
        for (int i = 0; i < dependencyOrder.Count; i++)
        {
            output.AppendLine($"  {i + 1}. {dependencyOrder[i]}");
        }
        output.AppendLine();
        output.AppendLine("✅ CHECKLIST:");
        foreach (var step in steps)
        {
            output.AppendLine($"  [ ] {step.Order}. {step.Description}");
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(plan) }
            }
        };
    }

    private string AnalyzeTaskSemantics(string task, string language)
    {
        var breakdown = new StringBuilder();
        
        // Extract key verbs (create, add, implement, build, etc.)
        var actionVerbs = new[] { "create", "add", "implement", "build", "make", "develop", "write" };
        var foundActions = actionVerbs.Where(v => task.Contains(v, StringComparison.OrdinalIgnoreCase)).ToList();
        
        // Extract nouns (game, app, service, component, etc.)
        var commonNouns = new[] { "game", "app", "application", "service", "component", "api", "class", "module", "function" };
        var foundNouns = commonNouns.Where(n => task.Contains(n, StringComparison.OrdinalIgnoreCase)).ToList();
        
        // Detect UI requirements
        var hasUI = task.Contains("gui", StringComparison.OrdinalIgnoreCase) ||
                    task.Contains("ui", StringComparison.OrdinalIgnoreCase) ||
                    task.Contains("interface", StringComparison.OrdinalIgnoreCase) ||
                    task.Contains("window", StringComparison.OrdinalIgnoreCase);
        
        breakdown.AppendLine($"Actions: {string.Join(", ", foundActions)}");
        breakdown.AppendLine($"Components: {string.Join(", ", foundNouns)}");
        breakdown.AppendLine($"Has UI: {hasUI}");
        
        // Language-specific considerations
        breakdown.AppendLine($"Language patterns for {language}:");
        switch (language)
        {
            case "python":
                breakdown.AppendLine("  - Use snake_case for functions/variables");
                breakdown.AppendLine("  - Use PascalCase for classes");
                if (hasUI) breakdown.AppendLine("  - Consider tkinter or pygame for GUI");
                break;
            case "csharp":
            case "c#":
                breakdown.AppendLine("  - Use PascalCase for public members");
                breakdown.AppendLine("  - Use camelCase for private fields");
                breakdown.AppendLine("  - Include XML documentation");
                break;
            case "typescript":
            case "javascript":
                breakdown.AppendLine("  - Use camelCase for variables/functions");
                breakdown.AppendLine("  - Use PascalCase for classes/interfaces");
                if (hasUI) breakdown.AppendLine("  - Consider React components");
                break;
        }
        
        return breakdown.ToString();
    }

    private List<string> ExtractRequiredClasses(string task, string language)
    {
        var classes = new List<string>();
        
        // Common patterns for different task types
        if (task.Contains("blackjack", StringComparison.OrdinalIgnoreCase) ||
            task.Contains("card game", StringComparison.OrdinalIgnoreCase))
        {
            classes.AddRange(new[] { "Card", "Deck", "Hand", "Player", "Game" });
        }
        else if (task.Contains("todo", StringComparison.OrdinalIgnoreCase))
        {
            classes.AddRange(new[] { "TodoItem", "TodoList", "App" });
        }
        else if (task.Contains("chat", StringComparison.OrdinalIgnoreCase))
        {
            classes.AddRange(new[] { "Message", "User", "ChatRoom", "Client" });
        }
        else if (task.Contains("api", StringComparison.OrdinalIgnoreCase) ||
                 task.Contains("service", StringComparison.OrdinalIgnoreCase))
        {
            classes.AddRange(new[] { "Service", "Repository", "Controller", "Model" });
        }
        else
        {
            // Generic extraction based on task words
            classes.Add("Main");
            if (task.Contains("game", StringComparison.OrdinalIgnoreCase))
                classes.Add("Game");
            if (task.Contains("player", StringComparison.OrdinalIgnoreCase))
                classes.Add("Player");
        }
        
        return classes.Distinct().ToList();
    }

    private List<string> ExtractRequiredMethods(string task, string language)
    {
        var methods = new List<string>();
        
        // Common methods based on task type
        if (task.Contains("game", StringComparison.OrdinalIgnoreCase))
        {
            methods.AddRange(new[] { "start", "play", "update", "render", "handle_input" });
        }
        
        if (task.Contains("blackjack", StringComparison.OrdinalIgnoreCase))
        {
            methods.AddRange(new[] { "deal", "hit", "stand", "calculate_score", "check_winner" });
        }
        
        if (task.Contains("api", StringComparison.OrdinalIgnoreCase))
        {
            methods.AddRange(new[] { "get", "post", "put", "delete", "validate" });
        }
        
        // Format for language
        if (language == "python")
        {
            methods = methods.Select(m => m.ToLowerInvariant().Replace(" ", "_")).ToList();
        }
        else if (language is "csharp" or "c#" or "typescript" or "javascript")
        {
            methods = methods.Select(m => ToPascalCase(m)).ToList();
        }
        
        return methods.Distinct().ToList();
    }

    private List<string> DetermineDependencyOrder(string task, string language, List<string> classes)
    {
        var order = new List<string>();
        var ext = language switch
        {
            "python" => ".py",
            "csharp" or "c#" => ".cs",
            "typescript" => ".ts",
            "javascript" => ".js",
            "dart" or "flutter" => ".dart",
            _ => ".txt"
        };

        // Base/model classes first
        foreach (var cls in classes.Where(c => 
            c.EndsWith("Model") || c == "Card" || c == "TodoItem" || c == "Message"))
        {
            order.Add($"{cls.ToLowerInvariant()}{ext}");
        }
        
        // Container classes next
        foreach (var cls in classes.Where(c => 
            c == "Deck" || c == "Hand" || c == "TodoList" || c == "Repository"))
        {
            var fileName = language == "python" ? cls.ToLowerInvariant() : cls;
            if (!order.Contains($"{fileName.ToLowerInvariant()}{ext}"))
                order.Add($"{fileName.ToLowerInvariant()}{ext}");
        }
        
        // Entity classes
        foreach (var cls in classes.Where(c => 
            c == "Player" || c == "User" || c == "Client"))
        {
            var fileName = language == "python" ? cls.ToLowerInvariant() : cls;
            if (!order.Contains($"{fileName.ToLowerInvariant()}{ext}"))
                order.Add($"{fileName.ToLowerInvariant()}{ext}");
        }
        
        // Main/Game/Service classes last
        foreach (var cls in classes.Where(c => 
            c == "Game" || c == "Main" || c == "Service" || c == "App" || c == "Controller"))
        {
            var fileName = language == "python" ? cls.ToLowerInvariant() : cls;
            if (!order.Contains($"{fileName.ToLowerInvariant()}{ext}"))
                order.Add($"{fileName.ToLowerInvariant()}{ext}");
        }
        
        // Add main entry point
        if (language == "python" && !order.Contains("main.py"))
            order.Add("main.py");
        
        return order;
    }

    private List<PlanStepInfo> GeneratePlanSteps(string task, string language, string semanticBreakdown, List<string> classes, List<string> files)
    {
        var steps = new List<PlanStepInfo>();
        int order = 1;
        
        foreach (var file in files)
        {
            var className = Path.GetFileNameWithoutExtension(file);
            steps.Add(new PlanStepInfo
            {
                StepId = $"step_{order}",
                Order = order,
                Description = $"Create {file} with {ToPascalCase(className)} implementation",
                FileName = file,
                Status = "pending",
                SemanticSpec = $"Implement {className} class with required methods"
            });
            order++;
        }
        
        // Add integration step
        steps.Add(new PlanStepInfo
        {
            StepId = $"step_{order}",
            Order = order,
            Description = "Integrate all components and test",
            Status = "pending",
            SemanticSpec = "Ensure all imports work, main entry point runs correctly"
        });
        
        return steps;
    }

    private async Task<string> StorePlanInNeo4jAsync(string task, string language, string context, List<PlanStepInfo> steps, CancellationToken ct)
    {
        var planId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            await using var session = _neo4j.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                // Create plan node
                await tx.RunAsync(@"
                    CREATE (p:TaskPlan {
                        id: $planId,
                        task: $task,
                        language: $language,
                        context: $context,
                        status: 'pending',
                        createdAt: datetime()
                    })",
                    new { planId, task, language, context });
                
                // Create step nodes
                foreach (var step in steps)
                {
                    await tx.RunAsync(@"
                        MATCH (p:TaskPlan {id: $planId})
                        CREATE (s:PlanStep {
                            id: $stepId,
                            order: $order,
                            description: $description,
                            fileName: $fileName,
                            status: 'pending'
                        })
                        CREATE (p)-[:HAS_STEP]->(s)",
                        new { planId, stepId = step.StepId, order = step.Order, description = step.Description, fileName = step.FileName ?? "" });
                }
            });
            
            _logger.LogInformation("📋 Stored task plan {PlanId} with {StepCount} steps in Neo4j", planId, steps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store plan in Neo4j (non-critical)");
        }
        
        return planId;
    }

    private class PlanStepInfo
    {
        public string StepId { get; set; } = "";
        public int Order { get; set; }
        public string Description { get; set; } = "";
        public string FileName { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string SemanticSpec { get; set; } = "";
    }

    #endregion

    #region Get Project Symbols

    private async Task<McpToolResult> GetProjectSymbolsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";
        var filter = args?.GetValueOrDefault("filter")?.ToString();
        var includeDescriptions = SafeParseBool(args?.GetValueOrDefault("includeDescriptions"), true);

        _logger.LogInformation("🔍 Getting project symbols for context: {Context}", context);

        var symbols = new ProjectSymbolsResult
        {
            Context = context,
            Files = new List<string>(),
            Classes = new List<ClassSymbolInfo>(),
            Functions = new List<FunctionSymbolInfo>(),
            ImportPaths = new Dictionary<string, string>()
        };

        try
        {
            await using var session = _neo4j.AsyncSession();
            
            // Get classes
            var classResult = await session.ExecuteReadAsync(async tx =>
            {
                var query = @"
                    MATCH (c:Class)
                    WHERE c.context = $context
                    " + (string.IsNullOrEmpty(filter) ? "" : "AND toLower(c.name) CONTAINS toLower($filter)") + @"
                    OPTIONAL MATCH (c)-[:HAS_METHOD]->(m:Method)
                    OPTIONAL MATCH (c)-[:IN_FILE]->(f:File)
                    RETURN c.name as name, c.signature as signature, c.description as description,
                           f.path as file, collect(DISTINCT m.name) as methods
                    LIMIT 100";
                
                var result = await tx.RunAsync(query, new { context, filter = filter ?? "" });
                return await result.ToListAsync();
            });

            foreach (var record in classResult)
            {
                var classInfo = new ClassSymbolInfo
                {
                    Name = record["name"].As<string>(),
                    File = record["file"].As<string?>() ?? "",
                    Signature = record["signature"].As<string?>() ?? "",
                    Description = includeDescriptions ? (record["description"].As<string?>() ?? "") : "",
                    Methods = record["methods"].As<List<object>>()?.Select(m => m?.ToString() ?? "").ToList() ?? new()
                };
                
                // Generate import statement
                classInfo.ImportStatement = GenerateImportStatement(classInfo.Name, classInfo.File, context);
                
                symbols.Classes.Add(classInfo);
                symbols.ImportPaths[classInfo.Name] = classInfo.ImportStatement;
                
                if (!string.IsNullOrEmpty(classInfo.File) && !symbols.Files.Contains(classInfo.File))
                    symbols.Files.Add(classInfo.File);
            }

            // Get standalone functions
            var funcResult = await session.ExecuteReadAsync(async tx =>
            {
                var query = @"
                    MATCH (f:Function)
                    WHERE f.context = $context
                    " + (string.IsNullOrEmpty(filter) ? "" : "AND toLower(f.name) CONTAINS toLower($filter)") + @"
                    OPTIONAL MATCH (f)-[:IN_FILE]->(file:File)
                    RETURN f.name as name, f.signature as signature, f.description as description,
                           f.returnType as returnType, file.path as file
                    LIMIT 100";
                
                var result = await tx.RunAsync(query, new { context, filter = filter ?? "" });
                return await result.ToListAsync();
            });

            foreach (var record in funcResult)
            {
                var funcInfo = new FunctionSymbolInfo
                {
                    Name = record["name"].As<string>(),
                    File = record["file"].As<string?>() ?? "",
                    Signature = record["signature"].As<string?>() ?? "",
                    Description = includeDescriptions ? (record["description"].As<string?>() ?? "") : "",
                    ReturnType = record["returnType"].As<string?>() ?? ""
                };
                
                symbols.Functions.Add(funcInfo);
                
                if (!string.IsNullOrEmpty(funcInfo.File) && !symbols.Files.Contains(funcInfo.File))
                    symbols.Files.Add(funcInfo.File);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying Neo4j for symbols");
        }

        var output = new StringBuilder();
        output.AppendLine($"🔍 PROJECT SYMBOLS for {context}");
        output.AppendLine($"Files: {symbols.Files.Count}");
        output.AppendLine($"Classes: {symbols.Classes.Count}");
        output.AppendLine($"Functions: {symbols.Functions.Count}");
        output.AppendLine();
        
        if (symbols.Classes.Any())
        {
            output.AppendLine("📦 CLASSES:");
            foreach (var cls in symbols.Classes.Take(20))
            {
                output.AppendLine($"  • {cls.Name}");
                output.AppendLine($"    Import: {cls.ImportStatement}");
                if (cls.Methods.Any())
                    output.AppendLine($"    Methods: {string.Join(", ", cls.Methods.Take(5))}");
            }
        }
        
        if (symbols.Functions.Any())
        {
            output.AppendLine();
            output.AppendLine("⚡ FUNCTIONS:");
            foreach (var func in symbols.Functions.Take(20))
            {
                output.AppendLine($"  • {func.Name}");
                if (!string.IsNullOrEmpty(func.Signature))
                    output.AppendLine($"    Signature: {func.Signature}");
            }
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(symbols) }
            }
        };
    }

    private string GenerateImportStatement(string className, string filePath, string context)
    {
        if (string.IsNullOrEmpty(filePath)) return "";
        
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var moduleName = Path.GetFileNameWithoutExtension(filePath);
        
        return ext switch
        {
            ".py" => $"from {moduleName} import {className}",
            ".cs" => $"using {context}.{moduleName};",
            ".ts" or ".tsx" => $"import {{ {className} }} from './{moduleName}';",
            ".js" or ".jsx" => $"import {{ {className} }} from './{moduleName}';",
            ".dart" => $"import '{moduleName}.dart';",
            _ => $"// import {className} from {filePath}"
        };
    }

    private class ProjectSymbolsResult
    {
        public string Context { get; set; } = "";
        public List<string> Files { get; set; } = new();
        public List<ClassSymbolInfo> Classes { get; set; } = new();
        public List<FunctionSymbolInfo> Functions { get; set; } = new();
        public Dictionary<string, string> ImportPaths { get; set; } = new();
    }

    private class ClassSymbolInfo
    {
        public string Name { get; set; } = "";
        public string File { get; set; } = "";
        public string Signature { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Methods { get; set; } = new();
        public List<string> Properties { get; set; } = new();
        public string ImportStatement { get; set; } = "";
    }

    private class FunctionSymbolInfo
    {
        public string Name { get; set; } = "";
        public string File { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Signature { get; set; } = "";
        public string Description { get; set; } = "";
        public string ReturnType { get; set; } = "";
        public List<string> Parameters { get; set; } = new();
    }

    #endregion

    #region Validate Imports

    private async Task<McpToolResult> ValidateImportsAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var code = args?.GetValueOrDefault("code")?.ToString() ?? "";
        var language = args?.GetValueOrDefault("language")?.ToString()?.ToLowerInvariant() ?? "python";
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();

        _logger.LogInformation("✅ Validating imports for {Language} code", language);

        var result = new ImportValidationResultInfo
        {
            IsValid = true,
            Imports = new List<ImportCheckInfo>(),
            AvailableModules = new List<string>(),
            Summary = ""
        };

        // Extract imports from code
        var imports = ExtractImports(code, language);
        
        // Get standard library modules for the language
        var standardModules = GetStandardModules(language);
        result.AvailableModules.AddRange(standardModules);
        
        // 🔑 CRITICAL: Extract local module names from the code itself
        // If code contains "# File: card.py" or defines class Card in card.py, 
        // then "from card import Card" is valid!
        var localFilesInCode = ExtractLocalModulesFromCode(code, language);
        result.AvailableModules.AddRange(localFilesInCode);
        _logger.LogDebug("📁 Found {Count} local modules in generated code: {Modules}", 
            localFilesInCode.Count, string.Join(", ", localFilesInCode));
        
        // Get local modules from context (indexed files)
        if (!string.IsNullOrEmpty(context))
        {
            var localModules = await GetLocalModulesAsync(context, ct);
            result.AvailableModules.AddRange(localModules);
        }

        foreach (var import in imports)
        {
            var check = new ImportCheckInfo
            {
                ImportStatement = import.Statement,
                Module = import.Module,
                Symbol = import.Symbol,
                IsValid = false
            };

            // Check if it's a standard module
            if (standardModules.Contains(import.Module, StringComparer.OrdinalIgnoreCase))
            {
                check.IsValid = true;
                check.Reason = "Standard library module";
            }
            // Check if it's a common third-party module
            else if (IsCommonThirdPartyModule(import.Module, language))
            {
                check.IsValid = true;
                check.Reason = "Common third-party module (pip/npm install required)";
            }
            // Check if it's a local module
            else if (result.AvailableModules.Contains(import.Module, StringComparer.OrdinalIgnoreCase))
            {
                check.IsValid = true;
                check.Reason = "Local project module";
            }
            else
            {
                check.IsValid = false;
                check.Reason = "Module not found";
                check.Suggestion = SuggestModuleFix(import.Module, language, standardModules);
                result.IsValid = false;
            }

            result.Imports.Add(check);
        }

        var validCount = result.Imports.Count(i => i.IsValid);
        var invalidCount = result.Imports.Count(i => !i.IsValid);
        
        result.Summary = result.IsValid 
            ? $"✅ All {validCount} imports are valid"
            : $"⚠️ {invalidCount} invalid imports found out of {result.Imports.Count}";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result) }
            }
        };
    }

    /// <summary>
    /// Extract module names from file markers in the code itself
    /// E.g., "# File: card.py" or "// File: Card.cs" means 'card' is a local module
    /// </summary>
    private List<string> ExtractLocalModulesFromCode(string code, string language)
    {
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = code.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Match file markers like "# File: card.py" or "// File: card.py" or "// card.py"
            if (trimmed.StartsWith("# File:") || trimmed.StartsWith("// File:") || 
                trimmed.StartsWith("#") && trimmed.Contains(".py") ||
                trimmed.StartsWith("//") && (trimmed.Contains(".cs") || trimmed.Contains(".ts") || trimmed.Contains(".js")))
            {
                // Extract filename
                var colonIndex = trimmed.IndexOf(':');
                var filename = colonIndex >= 0 
                    ? trimmed[(colonIndex + 1)..].Trim()
                    : trimmed.TrimStart('#', '/', ' ').Trim();
                
                // Get module name from filename (without extension)
                var moduleName = Path.GetFileNameWithoutExtension(filename);
                if (!string.IsNullOrEmpty(moduleName) && moduleName.Length > 1)
                {
                    modules.Add(moduleName.ToLowerInvariant());
                }
            }
            
            // Also detect class definitions and use them as valid import targets
            // Python: class Card:
            if (language == "python" && trimmed.StartsWith("class "))
            {
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"class\s+(\w+)");
                if (match.Success)
                {
                    var className = match.Groups[1].Value;
                    // The module name is typically the lowercase version of the class
                    modules.Add(className.ToLowerInvariant());
                }
            }
        }
        
        return modules.ToList();
    }

    private List<ImportInfo> ExtractImports(string code, string language)
    {
        var imports = new List<ImportInfo>();
        var lines = code.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            switch (language)
            {
                case "python":
                    // import module
                    if (trimmed.StartsWith("import "))
                    {
                        var module = trimmed[7..].Split(' ')[0].Split('.')[0].Trim();
                        imports.Add(new ImportInfo { Statement = trimmed, Module = module });
                    }
                    // from module import ...
                    else if (trimmed.StartsWith("from "))
                    {
                        var parts = trimmed.Split(' ');
                        if (parts.Length >= 4)
                        {
                            var module = parts[1].Split('.')[0];
                            var symbol = parts[3];
                            imports.Add(new ImportInfo { Statement = trimmed, Module = module, Symbol = symbol });
                        }
                    }
                    break;
                    
                case "csharp":
                case "c#":
                    if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
                    {
                        var ns = trimmed[6..^1].Trim();
                        imports.Add(new ImportInfo { Statement = trimmed, Module = ns });
                    }
                    break;
                    
                case "typescript":
                case "javascript":
                    if (trimmed.StartsWith("import "))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"from ['""]([^'""]+)['""]");
                        if (match.Success)
                        {
                            var module = match.Groups[1].Value;
                            if (!module.StartsWith(".") && !module.StartsWith("/"))
                                imports.Add(new ImportInfo { Statement = trimmed, Module = module.Split('/')[0] });
                        }
                    }
                    break;
            }
        }

        return imports;
    }

    private List<string> GetStandardModules(string language)
    {
        return language switch
        {
            "python" => new List<string>
            {
                "os", "sys", "re", "json", "math", "random", "datetime", "time", "collections",
                "itertools", "functools", "typing", "pathlib", "logging", "unittest", "argparse",
                "dataclasses", "enum", "abc", "copy", "io", "threading", "multiprocessing",
                "asyncio", "socket", "http", "urllib", "email", "html", "xml", "sqlite3",
                "hashlib", "secrets", "uuid", "struct", "array", "queue", "heapq",
                "tkinter", "turtle"  // GUI modules
            },
            "csharp" or "c#" => new List<string>
            {
                "System", "System.Collections", "System.Collections.Generic", "System.Linq",
                "System.Text", "System.Text.Json", "System.IO", "System.Threading",
                "System.Threading.Tasks", "System.Net", "System.Net.Http", "Microsoft.Extensions"
            },
            "typescript" or "javascript" => new List<string>
            {
                "fs", "path", "http", "https", "url", "querystring", "crypto", "events",
                "stream", "util", "os", "child_process", "buffer"
            },
            _ => new List<string>()
        };
    }

    private bool IsCommonThirdPartyModule(string module, string language)
    {
        var commonModules = language switch
        {
            "python" => new[] { "numpy", "pandas", "requests", "flask", "django", "pygame", "pillow", "PIL", "matplotlib", "scipy", "sklearn" },
            "typescript" or "javascript" => new[] { "react", "vue", "angular", "express", "lodash", "axios", "moment", "dayjs" },
            "csharp" or "c#" => new[] { "Newtonsoft.Json", "Serilog", "AutoMapper", "Dapper", "MediatR" },
            _ => Array.Empty<string>()
        };
        
        return commonModules.Contains(module, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<string>> GetLocalModulesAsync(string context, CancellationToken ct)
    {
        var modules = new List<string>();
        
        try
        {
            await using var session = _neo4j.AsyncSession();
            var result = await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(@"
                    MATCH (f:File)
                    WHERE f.context = $context
                    RETURN f.path as path",
                    new { context });
                return await cursor.ToListAsync();
            });

            foreach (var record in result)
            {
                var path = record["path"].As<string>();
                var module = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(module))
                    modules.Add(module);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting local modules from Neo4j");
        }
        
        return modules;
    }

    private string SuggestModuleFix(string module, string language, List<string> standardModules)
    {
        // Find similar standard modules
        var similar = standardModules
            .Where(m => LevenshteinDistance(m.ToLower(), module.ToLower()) <= 2)
            .FirstOrDefault();
        
        if (similar != null)
            return $"Did you mean '{similar}'?";
        
        return language switch
        {
            "python" => $"Install with: pip install {module}",
            "typescript" or "javascript" => $"Install with: npm install {module}",
            "csharp" or "c#" => $"Install with: dotnet add package {module}",
            _ => "Check module name spelling"
        };
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        var m = s1.Length;
        var n = s2.Length;
        var d = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++) d[i, 0] = i;
        for (var j = 0; j <= n; j++) d[0, j] = j;

        for (var j = 1; j <= n; j++)
        for (var i = 1; i <= m; i++)
        {
            var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
        }

        return d[m, n];
    }

    private class ImportInfo
    {
        public string Statement { get; set; } = "";
        public string Module { get; set; } = "";
        public string Symbol { get; set; } = "";
    }

    private class ImportValidationResultInfo
    {
        public bool IsValid { get; set; }
        public List<ImportCheckInfo> Imports { get; set; } = new();
        public List<string> AvailableModules { get; set; } = new();
        public string Summary { get; set; } = "";
    }

    private class ImportCheckInfo
    {
        public string ImportStatement { get; set; } = "";
        public string Module { get; set; } = "";
        public string Symbol { get; set; } = "";
        public bool IsValid { get; set; }
        public string Reason { get; set; } = "";
        public string Suggestion { get; set; } = "";
    }

    #endregion

    #region Store Successful Task

    private async Task<McpToolResult> StoreSuccessfulTaskAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var taskDescription = args?.GetValueOrDefault("taskDescription")?.ToString() ?? "";
        var language = args?.GetValueOrDefault("language")?.ToString() ?? "";
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";
        var approachUsed = args?.GetValueOrDefault("approachUsed")?.ToString() ?? "";
        var patternsUsed = SafeParseStringList(args?.GetValueOrDefault("patternsUsed"));
        var filesGenerated = SafeParseStringList(args?.GetValueOrDefault("filesGenerated"));
        var keywords = SafeParseStringList(args?.GetValueOrDefault("keywords"));
        var iterationsNeeded = SafeParseInt(args?.GetValueOrDefault("iterationsNeeded"), 1);
        var finalScore = SafeParseInt(args?.GetValueOrDefault("finalScore"), 8);
        var modelUsed = args?.GetValueOrDefault("modelUsed")?.ToString() ?? "";
        var semanticStructure = args?.GetValueOrDefault("semanticStructure")?.ToString() ?? "";

        _logger.LogInformation("🎉 Storing successful task: {Task} ({Language})", 
            taskDescription.Length > 50 ? taskDescription[..50] + "..." : taskDescription, language);

        var taskId = Guid.NewGuid().ToString("N")[..12];
        
        // Store in Neo4j for relationship tracking and searchability
        try
        {
            await using var session = _neo4j.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                // Create task node with all metadata
                await tx.RunAsync(@"
                    CREATE (t:SuccessfulTask {
                        id: $id,
                        taskDescription: $taskDescription,
                        language: $language,
                        context: $context,
                        approachUsed: $approachUsed,
                        patternsUsed: $patternsUsed,
                        filesGenerated: $filesGenerated,
                        keywords: $keywords,
                        iterationsNeeded: $iterationsNeeded,
                        finalScore: $finalScore,
                        modelUsed: $modelUsed,
                        semanticStructure: $semanticStructure,
                        createdAt: datetime(),
                        isShared: true
                    })",
                    new { 
                        id = taskId,
                        taskDescription, language, context, approachUsed,
                        patternsUsed = string.Join(",", patternsUsed),
                        filesGenerated = string.Join(",", filesGenerated),
                        keywords = string.Join(",", keywords),
                        iterationsNeeded, finalScore, modelUsed, semanticStructure
                    });
                
                // Link to patterns used
                foreach (var pattern in patternsUsed)
                {
                    await tx.RunAsync(@"
                        MATCH (t:SuccessfulTask {id: $taskId})
                        MERGE (p:Pattern {name: $pattern})
                        CREATE (t)-[:USED_PATTERN]->(p)",
                        new { taskId, pattern });
                }
                
                // Link to context
                await tx.RunAsync(@"
                    MATCH (t:SuccessfulTask {id: $taskId})
                    MERGE (c:Context {name: $context})
                    CREATE (t)-[:IN_CONTEXT]->(c)",
                    new { taskId, context });
            });
            
            _logger.LogInformation("🎉 Stored successful task {TaskId} in Neo4j", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing in Neo4j (non-critical)");
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = $"🎉 Stored successful task!\n\nTask ID: {taskId}\nTask: {taskDescription}\nLanguage: {language}\nFiles: {filesGenerated.Count}\nScore: {finalScore}/10\nIterations: {iterationsNeeded}\n\n✅ Saved for future learning." }
            }
        };
    }

    #endregion

    #region Query Similar Tasks

    private async Task<McpToolResult> QuerySimilarTasksAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var task = args?.GetValueOrDefault("task")?.ToString() ?? "";
        var language = args?.GetValueOrDefault("language")?.ToString();
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 3);

        _logger.LogInformation("🔎 Querying similar successful tasks for: {Task}", 
            task.Length > 50 ? task[..50] + "..." : task);

        var result = new SimilarTasksResultInfo
        {
            FoundTasks = 0,
            Tasks = new List<SimilarTaskInfo>(),
            ReusableSnippets = new List<CodeSnippetInfo>(),
            SuggestedApproach = "",
            SuggestedStructure = ""
        };

        // Query Neo4j for similar tasks using keyword matching and full-text search
        try
        {
            await using var session = _neo4j.AsyncSession();
            var queryResults = await session.ExecuteReadAsync(async tx =>
            {
                // Build query based on keywords extracted from task
                var taskKeywords = ExtractTaskKeywords(task);
                var keywordsPattern = string.Join("|", taskKeywords.Select(k => $"(?i){k}"));
                
                var query = @"
                    MATCH (t:SuccessfulTask)
                    WHERE t.taskDescription =~ $pattern OR t.keywords =~ $pattern
                    " + (!string.IsNullOrEmpty(language) ? "AND toLower(t.language) = toLower($language)" : "") + @"
                    RETURN t.id as id, t.taskDescription as taskDescription, t.language as language,
                           t.approachUsed as approachUsed, t.semanticStructure as structure,
                           t.filesGenerated as filesGenerated, t.finalScore as score,
                           t.iterationsNeeded as iterations
                    ORDER BY t.finalScore DESC, t.createdAt DESC
                    LIMIT $limit";
                
                var cursor = await tx.RunAsync(query, new { 
                    pattern = $".*({keywordsPattern}).*",
                    language = language ?? "",
                    limit 
                });
                return await cursor.ToListAsync();
            });

            foreach (var record in queryResults)
            {
                var taskInfo = new SimilarTaskInfo
                {
                    TaskDescription = record["taskDescription"].As<string?>() ?? "",
                    Similarity = (record["score"].As<int?>() ?? 0) / 10.0f, // Convert score to similarity
                    ApproachUsed = record["approachUsed"].As<string?>() ?? "",
                    Structure = record["structure"].As<string?>() ?? "",
                    FilesGenerated = (record["filesGenerated"].As<string?>() ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .ToList()
                };
                result.Tasks.Add(taskInfo);

                // Use the best match for suggestions
                if (result.Tasks.Count == 1)
                {
                    result.SuggestedApproach = taskInfo.ApproachUsed;
                    result.SuggestedStructure = taskInfo.Structure;
                }
            }

            result.FoundTasks = result.Tasks.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying Neo4j for similar tasks");
        }

        var output = new StringBuilder();
        output.AppendLine($"🔎 SIMILAR SUCCESSFUL TASKS");
        output.AppendLine($"Found: {result.FoundTasks} similar tasks");
        output.AppendLine();
        
        if (result.Tasks.Any())
        {
            foreach (var t in result.Tasks)
            {
                var truncatedDesc = t.TaskDescription.Length > 60 ? t.TaskDescription[..60] + "..." : t.TaskDescription;
                output.AppendLine($"📋 {truncatedDesc}");
                output.AppendLine($"   Similarity: {t.Similarity:P0}");
                if (!string.IsNullOrEmpty(t.ApproachUsed))
                    output.AppendLine($"   Approach: {t.ApproachUsed}");
                if (t.FilesGenerated.Any())
                    output.AppendLine($"   Files: {string.Join(", ", t.FilesGenerated.Take(5))}");
                output.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(result.SuggestedApproach))
            {
                output.AppendLine("💡 SUGGESTED APPROACH:");
                output.AppendLine(result.SuggestedApproach);
            }
        }
        else
        {
            output.AppendLine("No similar tasks found. This will be the first!");
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = JsonSerializer.Serialize(result) }
            }
        };
    }

    private class SimilarTasksResultInfo
    {
        public int FoundTasks { get; set; }
        public List<SimilarTaskInfo> Tasks { get; set; } = new();
        public List<CodeSnippetInfo> ReusableSnippets { get; set; } = new();
        public string SuggestedApproach { get; set; } = "";
        public string SuggestedStructure { get; set; } = "";
    }

    private class SimilarTaskInfo
    {
        public string TaskDescription { get; set; } = "";
        public float Similarity { get; set; }
        public string ApproachUsed { get; set; } = "";
        public string Structure { get; set; } = "";
        public List<string> FilesGenerated { get; set; } = new();
    }

    private class CodeSnippetInfo
    {
        public string Name { get; set; } = "";
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Tags { get; set; } = new();
        public string Language { get; set; } = "";
    }

    #endregion

    #region Helpers

    private static List<string> ExtractTaskKeywords(string task)
    {
        var keywords = new List<string>();
        var techKeywords = new[] { "flutter", "blazor", "react", "maui", "wpf", "winforms", 
            "csharp", "c#", "python", "typescript", "javascript", "kotlin", "swift", "dart",
            "api", "rest", "grpc", "graphql", "websocket",
            "game", "blackjack", "todo", "chat", "calculator", "crud" };
        
        var lowerTask = task.ToLowerInvariant();
        foreach (var keyword in techKeywords)
        {
            if (lowerTask.Contains(keyword))
                keywords.Add(keyword);
        }
        return keywords;
    }

    private static List<string> SafeParseStringList(object? value)
    {
        if (value == null) return new List<string>();
        
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
                return je.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        
        if (value is IEnumerable<object> enumerable)
            return enumerable.Select(o => o?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
        
        return new List<string>();
    }

    private static List<string> ParseStringListFromJson(object? value)
    {
        if (value == null) return new List<string>();
        
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
                return je.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString();
                if (str?.StartsWith("[") == true)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<List<string>>(str) ?? new List<string>();
                    }
                    catch { return new List<string>(); }
                }
            }
        }
        
        return new List<string>();
    }

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

    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            _ => defaultValue
        };

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var words = input.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Select(w => char.ToUpper(w[0]) + w[1..].ToLower()));
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

