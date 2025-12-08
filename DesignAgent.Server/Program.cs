using DesignAgent.Server.Clients;
using DesignAgent.Server.Services;
using AgentContracts.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register Design Agent services
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

