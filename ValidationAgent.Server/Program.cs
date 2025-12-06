using AgentContracts.Services;
using ValidationAgent.Server.Clients;
using ValidationAgent.Server.Services;

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

// Configure HTTP client for Ollama (LLM validation)
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // LLM inference can take a while
});

// Register services (Scoped to ensure fresh instances per request)
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<IValidationPromptBuilder, ValidationPromptBuilder>();

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
app.Logger.LogInformation("ValidationAgent.Server starting on port {Port}", 
    app.Configuration["ASPNETCORE_URLS"] ?? "5002");

app.Run();

