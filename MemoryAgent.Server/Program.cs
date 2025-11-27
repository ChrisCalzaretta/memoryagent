using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Services;
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
    client.Timeout = TimeSpan.FromHours(2); // Indexing large projects can take a long time
});

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
builder.Services.AddSingleton<ICodeParser, RoslynParser>();
builder.Services.AddScoped<IIndexingService, IndexingService>();
builder.Services.AddSingleton<ISemgrepService, SemgrepService>();
builder.Services.AddScoped<IReindexService, ReindexService>();
builder.Services.AddScoped<IMcpService, McpService>();
builder.Services.AddScoped<ISmartSearchService, SmartSearchService>();
builder.Services.AddScoped<IPatternIndexingService, PatternIndexingService>();
builder.Services.AddScoped<IBestPracticeValidationService, BestPracticeValidationService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddSingleton<MemoryAgent.Server.FileWatcher.AutoReindexService>();
builder.Services.AddSingleton<MemoryAgent.Server.FileWatcher.IAutoReindexService>(sp => sp.GetRequiredService<MemoryAgent.Server.FileWatcher.AutoReindexService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MemoryAgent.Server.FileWatcher.AutoReindexService>());
builder.Services.AddScoped<IPatternValidationService, PatternValidationService>();

// TODO and Plan Management
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<ITaskValidationService, TaskValidationService>();

// Code Complexity Analysis
builder.Services.AddScoped<ICodeComplexityService, CodeComplexityService>();

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
        await vectorService.InitializeCollectionsAsync();
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

app.Run();
