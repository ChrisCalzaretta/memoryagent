using MemoryRouter.Server.Services;
using MemoryRouter.Server.Clients;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 🧠 Configure FunctionGemma client for Ollama (tool selection)
builder.Services.AddHttpClient<IFunctionGemmaClient, FunctionGemmaClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://ollama:11435");
    client.Timeout = TimeSpan.FromMinutes(2); // FunctionGemma can take time for complex planning
});

// 🤖 Configure DeepSeek client for AI complexity analysis
builder.Services.AddHttpClient<IAIComplexityAnalyzer, AIComplexityAnalyzer>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://ollama:11435");
    client.Timeout = TimeSpan.FromSeconds(30); // AI analysis should be fast
});

// 🎯 Configure agent clients
builder.Services.AddHttpClient<IMemoryAgentClient, MemoryAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MemoryAgent:BaseUrl"] ?? "http://memory-agent:5000");
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddHttpClient<ICodingOrchestratorClient, CodingOrchestratorClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CodingOrchestrator:BaseUrl"] ?? "http://coding-orchestrator:5003");
    client.Timeout = TimeSpan.FromSeconds(120);
});

// 📊 Register intelligence services (Statistical Learning + AI)
builder.Services.AddSingleton<IPerformanceTracker, PerformanceTracker>();
builder.Services.AddSingleton<IBackgroundJobManager, BackgroundJobManager>();
builder.Services.AddScoped<IHybridExecutionClassifier, HybridExecutionClassifier>();

// 📋 Register core services
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddScoped<IRouterService, RouterService>();
builder.Services.AddScoped<IMcpHandler, McpHandler>();

var app = builder.Build();

// Initialize tool registry on startup
var toolRegistry = app.Services.GetRequiredService<IToolRegistry>();
await toolRegistry.InitializeAsync();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.MapControllers();

// Log startup
app.Logger.LogInformation("🧠 MemoryRouter.Server starting on port {Port}", 
    app.Configuration["ASPNETCORE_URLS"] ?? "5010");
app.Logger.LogInformation("🤖 Hybrid AI + Statistical Intelligence enabled:");
app.Logger.LogInformation("   - FunctionGemma (tool selection) @ {Url}", 
    builder.Configuration["Ollama:BaseUrl"] ?? "http://ollama:11435");
app.Logger.LogInformation("   - DeepSeek AI (complexity analysis) @ {Url}", 
    builder.Configuration["Ollama:BaseUrl"] ?? "http://ollama:11435");
app.Logger.LogInformation("   - Statistical learning (performance tracking)");
app.Logger.LogInformation("   - Automatic async execution for long tasks (>15s)");

app.Run();

