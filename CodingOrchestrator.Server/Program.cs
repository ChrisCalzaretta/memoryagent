using CodingOrchestrator.Server.Services;
using CodingOrchestrator.Server.Clients;
using Polly;
using Polly.Extensions.Http;
using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 📊 Configure OpenTelemetry for distributed tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "CodingOrchestrator",
            serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("CodingOrchestrator.TaskOrchestrator")
        .AddSource("CodingOrchestrator.ExecutionService")
        .AddOtlpExporter(options =>
        {
            // Configure OTLP endpoint (default: localhost:4317)
            var endpoint = builder.Configuration["OpenTelemetry:Endpoint"];
            if (!string.IsNullOrEmpty(endpoint))
            {
                options.Endpoint = new Uri(endpoint);
            }
        }));

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// 🔄 Configure retry policy with exponential backoff
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(
        3,
        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            var logger = builder.Services.BuildServiceProvider().GetService<ILogger<Program>>();
            logger?.LogWarning("🔄 Retry {RetryCount} after {Delay}s due to: {Error}",
                retryCount, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
        });

// 🔌 Configure circuit breaker policy
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (outcome, breakDelay) =>
        {
            var logger = builder.Services.BuildServiceProvider().GetService<ILogger<Program>>();
            logger?.LogError("🔴 Circuit breaker OPEN for {BreakDuration}s - service unavailable", breakDelay.TotalSeconds);
        },
        onReset: () =>
        {
            var logger = builder.Services.BuildServiceProvider().GetService<ILogger<Program>>();
            logger?.LogInformation("🟢 Circuit breaker RESET - service recovered");
        },
        onHalfOpen: () =>
        {
            var logger = builder.Services.BuildServiceProvider().GetService<ILogger<Program>>();
            logger?.LogWarning("🟡 Circuit breaker HALF-OPEN - testing service");
        });

// Combine retry + circuit breaker (retry first, then circuit breaker)
var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

// Configure HTTP clients for agents with combined resilience policies
builder.Services.AddHttpClient<IMemoryAgentClient, MemoryAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["MemoryAgent:BaseUrl"] ?? "http://localhost:5000");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddPolicyHandler(combinedPolicy);

builder.Services.AddHttpClient<ICodingAgentClient, CodingAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CodingAgent:BaseUrl"] ?? "http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(120); // Increased for code generation
}).AddPolicyHandler(combinedPolicy);

builder.Services.AddHttpClient<IValidationAgentClient, ValidationAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ValidationAgent:BaseUrl"] ?? "http://localhost:5002");
    client.Timeout = TimeSpan.FromSeconds(120); // Increased for validation
}).AddPolicyHandler(combinedPolicy);

// 🎨 Design Agent - Brand guidelines and design validation
builder.Services.AddHttpClient<IDesignAgentClient, DesignAgentClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["DesignAgent:BaseUrl"] ?? "http://localhost:5004");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddPolicyHandler(combinedPolicy);

// 🐳 Configure Docker Execution settings
builder.Services.Configure<DockerExecutionConfig>(
    builder.Configuration.GetSection("DockerExecution"));

// 🐳 Register ExecutionService for Docker-based code execution
builder.Services.AddSingleton<IExecutionService, ExecutionService>();

// 🔥 Register Docker Warmup background service (pre-pulls images)
builder.Services.AddHostedService<DockerWarmupService>();

// 💾 Register Job Persistence Service
builder.Services.AddSingleton<IJobPersistenceService, JobPersistenceService>();

// 🧩 Register split orchestrator services for better separation of concerns
builder.Services.AddScoped<IComplexityEstimationService, ComplexityEstimationService>();
builder.Services.AddScoped<IFileAccumulatorService, FileAccumulatorService>();
builder.Services.AddScoped<IResultPersistenceService, ResultPersistenceService>();

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

// 🏥 Add comprehensive health checks
builder.Services.AddHealthChecks()
    .AddCheck<DockerHealthCheck>("docker")
    .AddUrlGroup(
        new Uri(builder.Configuration["MemoryAgent:BaseUrl"] + "/health" ?? "http://localhost:5000/health"),
        name: "memory-agent",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
    .AddUrlGroup(
        new Uri(builder.Configuration["CodingAgent:BaseUrl"] + "/health" ?? "http://localhost:5001/health"),
        name: "coding-agent",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
    .AddUrlGroup(
        new Uri(builder.Configuration["ValidationAgent:BaseUrl"] + "/health" ?? "http://localhost:5002/health"),
        name: "validation-agent",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
    .AddUrlGroup(
        new Uri(builder.Configuration["DesignAgent:BaseUrl"] + "/health" ?? "http://localhost:5004/health"),
        name: "design-agent",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

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

