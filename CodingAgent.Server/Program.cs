using AgentContracts.Services;
using CodingAgent.Server.Clients;
using CodingAgent.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure HTTP client for MemoryAgent (Lightning)
builder.Services.AddHttpClient<IMemoryAgentClient, MemoryAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MemoryAgent:BaseUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure HTTP client for Ollama
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // LLM inference can take a while
});

// Configure HTTP client for ModelOrchestrator (needs plain HttpClient for multi-port)
builder.Services.AddHttpClient<ModelOrchestrator>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register ModelOrchestrator as singleton (caches model discovery)
builder.Services.AddSingleton<IModelOrchestrator>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<ModelOrchestrator>>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(ModelOrchestrator));
    var memoryAgent = sp.GetService<IMemoryAgentClient>(); // Optional - graceful degradation
    return new ModelOrchestrator(config, logger, httpClient, memoryAgent);
});

// 🧠 LLM Model Selector - uses LLM to confirm model selection based on task + historical rates
builder.Services.AddScoped<ILlmModelSelector, LlmModelSelector>();

// Register services (Scoped to ensure fresh instances per request)
builder.Services.AddScoped<ICodeGenerationService, CodeGenerationService>();
builder.Services.AddScoped<IPromptBuilder, PromptBuilder>();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

// Log startup
app.Logger.LogInformation("CodingAgent.Server starting on port {Port}", 
    app.Configuration["ASPNETCORE_URLS"] ?? "5001");

app.Run();

