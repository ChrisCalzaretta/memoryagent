using AgentContracts.Services;
using ValidationAgent.Server.Clients;
using ValidationAgent.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for large requests (multi-file validation + 76+ accumulated files!)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2_000_000_000; // 2GB for accumulated files (handles any project size)
    options.Limits.MaxRequestBufferSize = 2_000_000_000;
    options.Limits.MaxRequestLineSize = 16384;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2); // Allow time for large uploads
});

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 64; // Support deeply nested structures
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true; // Accept both camelCase and PascalCase
    });

// Configure form options for large payloads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 2_000_000_000; // Match Kestrel limit (2GB)
});
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
builder.Services.AddScoped<ValidationService>(); // For direct injection in ensemble
builder.Services.AddScoped<IValidationPromptBuilder, ValidationPromptBuilder>();

// 🧠 Smart model selection - uses LLM + historical data to pick best model
builder.Services.AddScoped<IValidationModelSelector, ValidationModelSelector>();

// 🎯 Ensemble validation - model collaboration for higher quality
builder.Services.AddScoped<IValidationEnsembleService, ValidationEnsembleService>();

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

