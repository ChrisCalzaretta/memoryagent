using CodingOrchestrator.Server.Services;
using CodingOrchestrator.Server.Clients;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure retry policy
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Configure HTTP clients for agents
builder.Services.AddHttpClient<IMemoryAgentClient, MemoryAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MemoryAgent:BaseUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddPolicyHandler(retryPolicy);

builder.Services.AddHttpClient<ICodingAgentClient, CodingAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CodingAgent:BaseUrl"] ?? "http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(60);
}).AddPolicyHandler(retryPolicy);

builder.Services.AddHttpClient<IValidationAgentClient, ValidationAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ValidationAgent:BaseUrl"] ?? "http://localhost:5002");
    client.Timeout = TimeSpan.FromSeconds(60);
}).AddPolicyHandler(retryPolicy);

// 🐳 Register ExecutionService for Docker-based code execution
builder.Services.AddSingleton<IExecutionService, ExecutionService>();

// 💾 Register Job Persistence Service
builder.Services.AddSingleton<IJobPersistenceService, JobPersistenceService>();

// Register services (using factory to break circular dependency)
builder.Services.AddSingleton<ITaskOrchestrator, TaskOrchestrator>();
builder.Services.AddSingleton<IJobManager>(sp => 
{
    var orchestrator = (TaskOrchestrator)sp.GetRequiredService<ITaskOrchestrator>();
    var persistence = sp.GetRequiredService<IJobPersistenceService>();
    var logger = sp.GetRequiredService<ILogger<JobManager>>();
    var jobManager = new JobManager(orchestrator, persistence, logger);
    orchestrator.SetJobManager(jobManager);
    return jobManager;
});
builder.Services.AddSingleton<IMcpHandler, McpHandler>();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Initialize job persistence on startup (load persisted jobs)
var jobManager = app.Services.GetRequiredService<IJobManager>();
await jobManager.InitializeAsync();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

// Log startup
app.Logger.LogInformation("CodingOrchestrator.Server starting on port {Port}", 
    app.Configuration["ASPNETCORE_URLS"] ?? "5003");

app.Run();

