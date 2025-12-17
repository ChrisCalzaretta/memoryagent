using DesignAgent.Server.Clients;
using DesignAgent.Server.Services;
using DesignAgent.Server.Services.DesignIntelligence;
using DesignAgent.Server.Models.DesignIntelligence;
using AgentContracts.Services;
using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure Design Intelligence options
builder.Services.Configure<DesignIntelligenceOptions>(
    builder.Configuration.GetSection(DesignIntelligenceOptions.SectionName));

// Neo4j Driver
builder.Services.AddSingleton<IDriver>(sp =>
{
    var neo4jUri = builder.Configuration.GetConnectionString("Neo4j") ?? "bolt://localhost:7687";
    var username = builder.Configuration["Neo4jCredentials:Username"] ?? "neo4j";
    var password = builder.Configuration["Neo4jCredentials:Password"] ?? "password";
    
    return GraphDatabase.Driver(neo4jUri, AuthTokens.Basic(username, password));
});

// Register Design Agent services (existing)
builder.Services.AddSingleton<IBrandService, BrandService>();
builder.Services.AddSingleton<ITokenGeneratorService, TokenGeneratorService>();
builder.Services.AddSingleton<IComponentSpecService, ComponentSpecService>();
builder.Services.AddSingleton<IDesignValidationService, DesignValidationService>();
builder.Services.AddSingleton<IQuestionnaireService, QuestionnaireService>();
builder.Services.AddSingleton<IAccessibilityService, AccessibilityService>();

// 🧠 LLM Infrastructure
builder.Services.AddSingleton<IOllamaClient, OllamaClient>();
builder.Services.AddSingleton<IMemoryAgentClient, MemoryAgentClient>();
builder.Services.AddSingleton<IDesignModelSelector, DesignModelSelector>();
builder.Services.AddSingleton<ILlmDesignService, LlmDesignService>();

// 🎨 Design Intelligence Services
builder.Services.AddSingleton<IDesignIntelligenceStorage, DesignIntelligenceStorage>();
builder.Services.AddSingleton<IDesignDiscoveryService, DesignDiscoveryService>();
builder.Services.AddSingleton<IDesignCaptureService, DesignCaptureService>();
builder.Services.AddSingleton<IDesignAnalysisService, DesignAnalysisService>();
builder.Services.AddSingleton<IDesignLearningService, DesignLearningService>();
builder.Services.AddSingleton<IA2uiGeneratorService, A2uiGeneratorService>();
builder.Services.AddHostedService<DesignIntelligenceBackgroundService>();

// Configure HTTP client for Ollama
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    var ollamaUrl = builder.Configuration["Ollama:Url"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(5); // LLM calls can be slow
});

// Configure HTTP client for Memory Agent (Lightning prompts + model learning)
builder.Services.AddHttpClient("MemoryAgent", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MemoryAgent:BaseUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure HTTP client for Search APIs
builder.Services.AddHttpClient("SearchClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "DesignAgent" }));

// Log startup
app.Logger.LogInformation("DesignAgent.Server starting on port {Port}",
    app.Configuration["ASPNETCORE_URLS"] ?? "5004");

app.Run();

