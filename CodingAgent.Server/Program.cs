using AgentContracts.Services;
using CodingAgent.Server.Clients;
using CodingAgent.Server.Services;
using CodingAgent.Server.Templates;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for large requests (multi-file code generation + 76+ accumulated files!)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2_000_000_000; // 2GB for accumulated files (handles any project size)
    options.Limits.MaxRequestBufferSize = 2_000_000_000;
    options.Limits.MaxRequestLineSize = 16384;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2); // Allow time for large uploads
});

// 🌐 CORS - Allow browser access to API endpoints
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.MaxDepth = 64; // Support deeply nested structures
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true; // Accept both camelCase and PascalCase
    });

// 🌐 SIGNALR - Real-time bidirectional communication (WebSocket)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true; // Helpful for debugging
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB max message size
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
builder.Services.AddSingleton<ILlmModelSelector, LlmModelSelector>();

// 🌐 WEB SEARCH SERVICE - Augments LLMs with real-time web knowledge (official docs + general search)
builder.Services.AddSingleton<IWebSearchService, WebSearchService>();

// 🎨 DESIGN AGENT SERVICES - Brand system management and UI validation
builder.Services.AddHttpClient<IDesignAgentClient, DesignAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DesignAgent:BaseUrl"] ?? "http://design-agent:5004");
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddSingleton<LlmDesignQuestionnaireService>();
builder.Services.AddSingleton<DesignIntegrationService>();

// Register services (Singleton for job manager compatibility)
builder.Services.AddSingleton<ICodeGenerationService, CodeGenerationService>();
builder.Services.AddSingleton<IPromptBuilder, PromptBuilder>();

// 🎨 Template Service - project templates for C#, Flutter, etc.
builder.Services.AddSingleton<ITemplateService, TemplateService>();

// 🧠 Phi4 Thinking Service - project planning and failure analysis (Singleton for JobManager)
builder.Services.AddSingleton<IPhi4ThinkingService, Phi4ThinkingService>();

// 🧠🧠🧠 MULTI-MODEL SERVICES - Coordinates multiple models for thinking and coding
builder.Services.AddSingleton<IMultiModelThinkingService, MultiModelThinkingService>();
builder.Services.AddSingleton<IMultiModelCodingService, MultiModelCodingService>();

// 🔍 SELF-REVIEW SERVICE - LLM critiques its own code (like Claude!)
builder.Services.AddSingleton<ISelfReviewService, SelfReviewService>();

// 📜 HISTORY FORMATTER SERVICE - Formats complete history for LLM context
builder.Services.AddSingleton<IHistoryFormatterService, HistoryFormatterService>();

// 🌱 PROMPT SEED SERVICE - Seeds and manages prompts in MemoryAgent (Lightning)
builder.Services.AddSingleton<IPromptSeedService, PromptSeedService>();

// 🧠 LIGHTNING CONTEXT SERVICE - Full AI Lightning integration (sessions, Q&A, learning)
builder.Services.AddSingleton<ILightningContextService, LightningContextService>();

// 📊 HIERARCHICAL CONTEXT MANAGER - Solves large project context window issues
builder.Services.AddSingleton<IHierarchicalContextManager, HierarchicalContextManager>();

// 🕸️ PROJECT GRAPH SERVICE - Multi-file awareness, dependency analysis (Neo4j)
builder.Services.AddSingleton<IProjectGraphService, ProjectGraphService>();

// ⚠️ AMBIGUITY DETECTOR - Detects ambiguous terms, prevents wrong assumptions
builder.Services.AddSingleton<IAmbiguityDetector, AmbiguityDetector>();

// 💬 CONVERSATION SERVICE - Real-time WebSocket communication (SignalR)
builder.Services.AddSingleton<IConversationService, ConversationService>();

// 🎯 TOOL REASONING SERVICE - Intelligent tool selection (meta-reasoning)
builder.Services.AddSingleton<IToolReasoningService, ToolReasoningService>();

// 🤖 AGENTIC CODING SERVICE - Tool-augmented generation with file reading, codebase search
builder.Services.AddSingleton<IAgenticCodingService, AgenticCodingService>();

// 🛠️ Resilience Services - stub and failure report generation (Singleton for JobManager)
builder.Services.AddSingleton<IStubGenerator, StubGenerator>();
builder.Services.AddSingleton<IFailureReportGenerator, FailureReportGenerator>();
builder.Services.AddSingleton<IProjectScaffolder, ProjectScaffolder>();
builder.Services.AddSingleton<ICodebaseExplorer, CodebaseExplorer>();
builder.Services.AddSingleton<IDotnetScaffoldService, DotnetScaffoldService>();

// 📊 ValidationAgent Client - for code quality validation in retry loop
builder.Services.AddHttpClient<IValidationAgentClient, ValidationAgentClient>(client =>
{
    var validationHost = builder.Configuration["ValidationAgent:Host"] ?? "localhost";
    var validationPort = builder.Configuration["ValidationAgent:Port"] ?? "5003";
    client.BaseAddress = new Uri($"http://{validationHost}:{validationPort}");
    client.Timeout = TimeSpan.FromMinutes(2); // Validation can take time
});

// 🚀 NEW Project Orchestrator - multi-language project generation with templates, planning, and stubs
builder.Services.AddSingleton<IProjectOrchestrator, ProjectOrchestrator>();
builder.Services.AddSingleton<IJobManager, JobManager>();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// 🌱 SEED PROMPTS ON STARTUP
try
{
    var promptSeedService = app.Services.GetRequiredService<IPromptSeedService>();
    await promptSeedService.SeedPromptsAsync();
    app.Logger.LogInformation("✅ Prompt seeding completed");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "❌ Prompt seeding failed (non-fatal, continuing)");
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(); // 🌐 Enable CORS for browser requests
app.UseRouting();
app.UseStaticFiles(); // Serve wwwroot/conversation.html

app.MapControllers();
app.MapHealthChecks("/health");

// 🌐 SIGNALR HUB - Real-time conversation endpoint
app.MapHub<CodingAgent.Server.Hubs.CodingAgentHub>("/hubs/codingagent");

// Log startup
app.Logger.LogInformation("CodingAgent.Server starting on port {Port}", 
    app.Configuration["ASPNETCORE_URLS"] ?? "5001");

app.Run();

