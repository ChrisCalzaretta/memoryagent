using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MemoryRouter.Server.Services;
using MemoryRouter.Server.Clients;

namespace MemoryRouter.Server.Tests.Integration;

/// <summary>
/// Base class for integration tests with real service connections
/// </summary>
public class IntegrationTestBase : IDisposable
{
    protected readonly HttpClient MemoryAgentHttpClient;
    protected readonly HttpClient CodingOrchestratorHttpClient;
    protected readonly HttpClient OllamaHttpClient;
    
    protected readonly IMemoryAgentClient MemoryAgentClient;
    protected readonly ICodingOrchestratorClient CodingOrchestratorClient;
    protected readonly IFunctionGemmaClient FunctionGemmaClient;
    
    protected readonly IToolRegistry ToolRegistry;
    protected readonly IPerformanceTracker PerformanceTracker;
    protected readonly IBackgroundJobManager JobManager;
    protected readonly IHybridExecutionClassifier ExecutionClassifier;
    protected readonly IRouterService RouterService;

    public IntegrationTestBase()
    {
        // Configure HTTP clients for local services
        MemoryAgentHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000"),
            Timeout = TimeSpan.FromSeconds(120)
        };

        CodingOrchestratorHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5003"),
            Timeout = TimeSpan.FromSeconds(120)
        };

        OllamaHttpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(2)
        };

        // Create loggers
        var memoryAgentLogger = NullLogger<MemoryAgentClient>.Instance;
        var codingOrchestratorLogger = NullLogger<CodingOrchestratorClient>.Instance;
        var gemmaLogger = NullLogger<FunctionGemmaClient>.Instance;
        var registryLogger = NullLogger<ToolRegistry>.Instance;
        var routerLogger = NullLogger<RouterService>.Instance;
        var classifierLogger = NullLogger<HybridExecutionClassifier>.Instance;

        // Create service clients
        MemoryAgentClient = new MemoryAgentClient(MemoryAgentHttpClient, memoryAgentLogger);
        CodingOrchestratorClient = new CodingOrchestratorClient(CodingOrchestratorHttpClient, codingOrchestratorLogger);
        FunctionGemmaClient = new FunctionGemmaClient(OllamaHttpClient, gemmaLogger);

        // Create tool registry
        ToolRegistry = new ToolRegistry(registryLogger, MemoryAgentClient, CodingOrchestratorClient);
        ToolRegistry.InitializeAsync().Wait();

        // Create intelligence services
        PerformanceTracker = new PerformanceTracker(NullLogger<PerformanceTracker>.Instance);
        JobManager = new BackgroundJobManager(NullLogger<BackgroundJobManager>.Instance);
        
        var aiAnalyzer = new AIComplexityAnalyzer(OllamaHttpClient, NullLogger<AIComplexityAnalyzer>.Instance);
        ExecutionClassifier = new HybridExecutionClassifier(
            PerformanceTracker,
            aiAnalyzer,
            classifierLogger
        );

        // Create router service
        RouterService = new RouterService(
            FunctionGemmaClient,
            ToolRegistry,
            MemoryAgentClient,
            CodingOrchestratorClient,
            ExecutionClassifier,
            JobManager,
            PerformanceTracker,
            routerLogger
        );
    }

    public void Dispose()
    {
        MemoryAgentHttpClient?.Dispose();
        CodingOrchestratorHttpClient?.Dispose();
        OllamaHttpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
