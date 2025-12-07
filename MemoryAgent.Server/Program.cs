using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Services;
using MemoryAgent.Server.Services.PatternValidation;
using MemoryAgent.Server.Services.Mcp;
using MemoryAgent.Server.Services.Mcp.Consolidated;
using MemoryAgent.Server.FileWatcher;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();

// Memory Cache for MCP tool results
builder.Services.AddMemoryCache();

// HTTP Clients
builder.Services.AddHttpClient("Ollama", client =>
{
    var ollamaUrl = builder.Configuration["Ollama:Url"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(ollamaUrl);
});

// DesignAgent is now a separate MCP server (port 5004)
// Configure Cursor to connect to design-mcp-wrapper.js

builder.Services.AddHttpClient<IVectorService, VectorService>(client =>
{
    var qdrantUrl = builder.Configuration["Qdrant:Url"] ?? "http://localhost:6333";
    // Ensure HTTP port (6333) not gRPC port (6334)
    if (qdrantUrl.Contains(":6334"))
    {
        qdrantUrl = qdrantUrl.Replace(":6334", ":6333");
    }
    client.BaseAddress = new Uri(qdrantUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Core Services
builder.Services.AddSingleton<IPathTranslationService, PathTranslationService>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IGraphService, GraphService>();
// Multi-language AST parser support (NO REGEX - Production Quality!)
builder.Services.AddSingleton<RoslynParser>();         // C# parser (Roslyn AST)
builder.Services.AddSingleton<TypeScriptASTParser>(); // JS/TS/React/Node.js parser (TS Compiler API)
builder.Services.AddSingleton<PythonASTParser>();      // Python parser (ast module via Python.NET)
builder.Services.AddSingleton<VBNetASTParser>();       // VB.NET parser (Roslyn AST)
builder.Services.AddSingleton<DartParser>();           // Dart/Flutter parser with pattern detection
builder.Services.AddSingleton<TerraformParser>();      // Terraform/HCL parser with IaC pattern detection
builder.Services.AddSingleton<BicepParser>();          // Azure Bicep parser
builder.Services.AddSingleton<ARMTemplateParser>();    // Azure ARM Template parser
builder.Services.AddSingleton<JsonParser>();           // Generic JSON parser (for non-ARM JSON files)
builder.Services.AddSingleton<ProjectFileParser>();    // .NET solution/project file parser (.sln, .csproj)
builder.Services.AddSingleton<ICodeParser, CompositeCodeParser>(); // Composite router
builder.Services.AddScoped<IIndexingService, IndexingService>();
builder.Services.AddSingleton<ISemgrepService, SemgrepService>();
builder.Services.AddScoped<IReindexService, ReindexService>();

// MCP Service - Orchestrator + 8 CONSOLIDATED Tool Handlers (25 tools total)
// Consolidated from 73 tools to 25 for better AI decision-making
builder.Services.AddScoped<IMcpService, McpService>();

// CONSOLIDATED TOOL HANDLERS (25 tools)
builder.Services.AddScoped<IMcpToolHandler, SearchToolHandler>();           // 1: smartsearch
builder.Services.AddScoped<IMcpToolHandler, IndexToolHandler>();            // 2: index
builder.Services.AddScoped<IMcpToolHandler, SessionToolHandler>();          // 3-7: session & learning tools
builder.Services.AddScoped<IMcpToolHandler, AnalysisToolHandler>();         // 8-11: analysis & validation
builder.Services.AddScoped<IMcpToolHandler, IntelligenceToolHandler>();     // 12-15: recommendations & insights
builder.Services.AddScoped<IMcpToolHandler, PlanningToolHandler>();         // 16-19: plans & todos
builder.Services.AddScoped<IMcpToolHandler, TransformToolHandler>();        // 20-21: transform & migration
builder.Services.AddScoped<IMcpToolHandler, EvolvingToolHandler>();         // 22-24: prompts, patterns, feedback
builder.Services.AddScoped<IMcpToolHandler, CodeUnderstandingToolHandler>();// 25: get_context, explain_code, find_examples
// Design tools are now in separate DesignAgent.Server MCP server (port 5004)

// Workspace handler (kept separate - auto-called by wrapper, not visible to AI)
builder.Services.AddScoped<IMcpToolHandler, WorkspaceToolHandler>();

builder.Services.AddScoped<ISmartSearchService, SmartSearchService>();
builder.Services.AddScoped<IPatternIndexingService, PatternIndexingService>();
builder.Services.AddScoped<IBestPracticeValidationService, BestPracticeValidationService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

// Learning Service (Agent Lightning) - Session tracking, Q&A learning, importance scoring
builder.Services.AddSingleton<ILearningService, LearningService>();
builder.Services.AddSingleton<MemoryAgent.Server.FileWatcher.AutoReindexService>();
builder.Services.AddSingleton<MemoryAgent.Server.FileWatcher.IAutoReindexService>(sp => sp.GetRequiredService<MemoryAgent.Server.FileWatcher.AutoReindexService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MemoryAgent.Server.FileWatcher.AutoReindexService>());

// Pattern Validators (refactored from single 2913-line file into 17 focused validators)
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.CachingPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.ResiliencePatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.ValidationPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.AgentFrameworkPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.AgentLightningPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.SemanticKernelPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.AutoGenPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.SecurityPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.ErrorHandlingPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.PluginArchitecturePatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.PublisherSubscriberPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.FlutterPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.DartPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.MicrosoftExtensionsAIPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.TerraformPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.BicepPatternValidator>();
builder.Services.AddSingleton<IPatternValidator, MemoryAgent.Server.Services.PatternValidation.ARMTemplatePatternValidator>();

// Pattern Validation Orchestrator
builder.Services.AddScoped<IPatternValidationService, PatternValidationService>();

// TODO and Plan Management
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<ITaskValidationService, TaskValidationService>();

// Evolving Prompt & Pattern System (Self-Improving)
// Note: Initialization happens automatically on startup (see auto-init section below)
builder.Services.AddSingleton<IPromptService, PromptService>();
builder.Services.AddSingleton<IEvolvingPatternCatalogService, EvolvingPatternCatalogService>();

// Intent Classification (LLM-powered)
builder.Services.AddScoped<IIntentClassificationService, IntentClassificationService>();

// Code Complexity Analysis
builder.Services.AddScoped<ICodeComplexityService, CodeComplexityService>();

// LLM Service (DeepSeek Coder via Ollama)
builder.Services.AddScoped<ILLMService, LLMService>();

// Blazor/Razor Transformation Services
builder.Services.AddScoped<RazorParser>();
builder.Services.AddScoped<IPageTransformationService, PageTransformationService>();
builder.Services.AddScoped<ICSSTransformationService, CSSTransformationService>();
builder.Services.AddScoped<IComponentExtractionService, ComponentExtractionService>();

// Transformation MCP Tools
builder.Services.AddScoped<MemoryAgent.Server.MCP.TransformationTools>();

// Background Services
builder.Services.AddHostedService<AutoReindexService>();

// CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Get logger
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Initialize databases on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        logger.LogInformation("Initializing databases...");

        var vectorService = scope.ServiceProvider.GetRequiredService<IVectorService>();
        var graphService = scope.ServiceProvider.GetRequiredService<IGraphService>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

        // Health checks
        var qdrantHealthy = await vectorService.HealthCheckAsync();
        var neo4jHealthy = await graphService.HealthCheckAsync();
        var ollamaHealthy = await embeddingService.HealthCheckAsync();

        logger.LogInformation("Health checks - Qdrant: {Qdrant}, Neo4j: {Neo4j}, Ollama: {Ollama}",
            qdrantHealthy ? "✓" : "✗",
            neo4jHealthy ? "✓" : "✗",
            ollamaHealthy ? "✓" : "✗");

        if (!qdrantHealthy)
        {
            logger.LogWarning("Qdrant is not healthy. Vector search may not work.");
        }

        if (!neo4jHealthy)
        {
            logger.LogWarning("Neo4j is not healthy. Graph queries may not work.");
        }

        if (!ollamaHealthy)
        {
            logger.LogWarning("Ollama is not healthy or model not found. Run: docker exec memory-agent-ollama ollama pull mxbai-embed-large:latest");
        }

        // Initialize
        // NOTE: Collections are ONLY created per-workspace via register_workspace MCP tool
        // Do NOT create default collections without a workspace context
        // await vectorService.InitializeCollectionsAsync(); // DISABLED - workspace-specific only
        await graphService.InitializeDatabaseAsync();

        // Pre-warm Ollama model (load into memory on startup)
        if (ollamaHealthy)
        {
            try
            {
                logger.LogInformation("Pre-loading Ollama embedding model...");
                await embeddingService.GenerateEmbeddingAsync("test", CancellationToken.None);
                logger.LogInformation("Ollama model pre-loaded successfully");
            }
            catch (Exception warmupEx)
            {
                logger.LogWarning(warmupEx, "Failed to pre-load Ollama model, it will load on first use");
            }
        }

        logger.LogInformation("Databases initialized successfully");
        
        // AUTO-INIT: Initialize evolving prompts and pattern catalog
        // This ensures prompts and patterns are ready without requiring explicit tool calls
        try
        {
            logger.LogInformation("Auto-initializing evolving systems...");
            
            // Initialize default prompts (idempotent - checks if exists first)
            var promptService = scope.ServiceProvider.GetRequiredService<IPromptService>();
            await promptService.InitializeDefaultPromptsAsync();
            logger.LogInformation("✅ Default prompts initialized");
            
            // Initialize evolving pattern catalog from static best practices (idempotent)
            var catalogService = scope.ServiceProvider.GetRequiredService<IEvolvingPatternCatalogService>();
            var isInitialized = await catalogService.IsInitializedAsync();
            if (!isInitialized)
            {
                await catalogService.InitializeFromStaticCatalogAsync();
                logger.LogInformation("✅ Evolving pattern catalog initialized from static patterns");
            }
            else
            {
                logger.LogInformation("✅ Evolving pattern catalog already initialized");
            }
        }
        catch (Exception initEx)
        {
            logger.LogWarning(initEx, "Failed to auto-initialize evolving systems. They will initialize on first use.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing databases");
    }
}

logger.LogInformation("Memory Code Agent started on http://localhost:5000");
logger.LogInformation("- Qdrant: {Qdrant}", builder.Configuration["Qdrant:Url"] ?? "http://qdrant:6333");
logger.LogInformation("- Neo4j: {Neo4j}", builder.Configuration["Neo4j:Url"] ?? "bolt://neo4j:7687");
logger.LogInformation("- Ollama: {Ollama}", builder.Configuration["Ollama:Url"] ?? "http://ollama:11434");

// GRACEFUL SHUTDOWN: Register handler to ensure proper cleanup
// This prevents database corruption when container stops or Windows restarts
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStopping.Register(() =>
{
    logger.LogWarning("⚠️ SHUTDOWN SIGNAL RECEIVED - Closing database connections...");
    
    // Dispose singleton services with Neo4j drivers
    // These MUST be disposed before Neo4j container stops to prevent corruption
    try
    {
        // Get and dispose services that hold Neo4j connections
        var graphService = app.Services.GetService<IGraphService>() as IDisposable;
        var learningService = app.Services.GetService<ILearningService>() as IDisposable;
        var promptService = app.Services.GetService<IPromptService>() as IDisposable;
        var patternCatalogService = app.Services.GetService<IEvolvingPatternCatalogService>() as IDisposable;
        
        graphService?.Dispose();
        learningService?.Dispose();
        promptService?.Dispose();
        patternCatalogService?.Dispose();
        
        logger.LogInformation("✅ All Neo4j connections closed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error disposing database connections during shutdown");
    }
});

lifetime.ApplicationStopped.Register(() =>
{
    logger.LogInformation("✅ Memory Agent shutdown complete - safe to stop databases");
});

app.Run();
