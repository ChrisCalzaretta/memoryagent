using AgentContracts.Services;
using CodingAgent.Server.Clients;
using CodingAgent.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for large requests (multi-file code generation + 76+ accumulated files!)
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

// Configure HTTP client for Ollama
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // LLM inference can take a while
});

// 📊 Claude Rate Limit Tracker - monitors token usage per minute
builder.Services.AddSingleton<IClaudeRateLimitTracker, ClaudeRateLimitTracker>();

// ☁️ Configure HTTP client for Anthropic Claude (optional - for high-quality code generation)
builder.Services.AddHttpClient<IAnthropicClient, AnthropicClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Allow extra time for rate limit waits
});
builder.Services.AddSingleton<IAnthropicClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(AnthropicClient));
    var logger = sp.GetRequiredService<ILogger<AnthropicClient>>();
    var config = sp.GetRequiredService<IConfiguration>();
    var rateLimitTracker = sp.GetService<IClaudeRateLimitTracker>(); // Optional
    return new AnthropicClient(httpClient, logger, config, rateLimitTracker);
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

