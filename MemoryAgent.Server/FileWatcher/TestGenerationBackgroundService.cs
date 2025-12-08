using System.Text.Json;

namespace MemoryAgent.Server.FileWatcher;

/// <summary>
/// Background service that processes test generation queue
/// Generates tests one at a time via CodingOrchestrator
/// Tests must pass validation (score >= 8)
/// </summary>
public class TestGenerationBackgroundService : BackgroundService
{
    private readonly ITestGenerationQueue _queue;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestGenerationBackgroundService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public TestGenerationBackgroundService(
        ITestGenerationQueue queue,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TestGenerationBackgroundService> logger)
    {
        _queue = queue;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("AutoTestGeneration:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("🧪 Auto test generation is DISABLED. Set AutoTestGeneration:Enabled=true to enable.");
            return;
        }

        _logger.LogInformation("🧪 Auto test generation service STARTED (1 at a time)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Don't process if already processing
                if (_queue.IsProcessing)
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                    continue;
                }

                // Try to get next item
                if (_queue.TryDequeue(out var request) && request != null)
                {
                    await ProcessTestGenerationAsync(request, stoppingToken);
                }
                else
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test generation service");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ProcessTestGenerationAsync(TestGenerationRequest request, CancellationToken ct)
    {
        _queue.StartProcessing();
        
        try
        {
            _logger.LogInformation("🧪 Generating tests for: {ClassName}", request.ClassName);

            var orchestratorUrl = _configuration["CodingOrchestrator:BaseUrl"] 
                ?? "http://localhost:5003";

            var client = _httpClientFactory.CreateClient("CodingOrchestrator");

            // Read source file content for context
            var sourceContent = "";
            if (File.Exists(request.SourceFilePath))
            {
                sourceContent = await File.ReadAllTextAsync(request.SourceFilePath, ct);
                // Truncate if too long
                if (sourceContent.Length > 5000)
                {
                    sourceContent = sourceContent[..5000] + "\n// ... truncated ...";
                }
            }

            // Create orchestration task
            var taskRequest = new
            {
                task = $@"Write comprehensive integration tests for the following C# class.

FILE: {Path.GetFileName(request.SourceFilePath)}
PROJECT: {request.ProjectName}

Requirements:
- Use xUnit test framework
- Write integration tests (NO MOCKS)
- Cover: happy path, error cases, edge cases
- Test file should be named: {request.ClassName}Tests.cs
- Namespace should be: {request.ProjectName}.Tests

SOURCE CODE:
```csharp
{sourceContent}
```

Generate ONLY the test file content. Tests MUST compile and pass.",
                language = "csharp",
                context = request.Context,
                workspacePath = Path.GetDirectoryName(request.SourceFilePath),
                background = false, // Wait for completion
                maxIterations = 5,
                minValidationScore = 8 // Tests must pass validation
            };

            var response = await client.PostAsJsonAsync(
                $"{orchestratorUrl}/api/orchestrator/task", 
                taskRequest, 
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("❌ Failed to start test generation for {ClassName}: {Error}",
                    request.ClassName, error);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<OrchestratorResponse>(ct);
            if (result == null)
            {
                _logger.LogWarning("❌ Empty response from orchestrator for {ClassName}", request.ClassName);
                return;
            }

            // Poll for completion (background=false should wait, but just in case)
            var jobId = result.JobId;
            var maxWait = TimeSpan.FromMinutes(5);
            var startTime = DateTime.UtcNow;
            
            while (DateTime.UtcNow - startTime < maxWait)
            {
                var statusResponse = await client.GetAsync(
                    $"{orchestratorUrl}/api/orchestrator/task/{jobId}", ct);
                
                if (!statusResponse.IsSuccessStatusCode)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }

                var status = await statusResponse.Content.ReadFromJsonAsync<TaskStatusResponse>(ct);
                
                if (status?.Status == 2) // Complete
                {
                    await HandleCompletedTaskAsync(request, status, ct);
                    return;
                }
                else if (status?.Status == 3) // Failed
                {
                    _logger.LogWarning("❌ Test generation failed for {ClassName}: {Error}",
                        request.ClassName, status.Error ?? "Unknown error");
                    return;
                }
                else if (status?.Status == 4) // Cancelled
                {
                    _logger.LogWarning("⚠️ Test generation cancelled for {ClassName}", request.ClassName);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }

            _logger.LogWarning("⏱️ Test generation timed out for {ClassName}", request.ClassName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating tests for {ClassName}", request.ClassName);
        }
        finally
        {
            _queue.CompleteProcessing();
        }
    }

    private async Task HandleCompletedTaskAsync(
        TestGenerationRequest request, 
        TaskStatusResponse status, 
        CancellationToken ct)
    {
        if (status.Result?.Files == null || !status.Result.Files.Any())
        {
            _logger.LogWarning("❌ No test files generated for {ClassName}", request.ClassName);
            return;
        }

        var score = status.Result.ValidationScore;
        if (score < 8)
        {
            _logger.LogWarning("⚠️ Test generation for {ClassName} scored {Score}/10 (below threshold)",
                request.ClassName, score);
            return;
        }

        // Ensure test directory exists
        var testDir = Path.GetDirectoryName(request.TestFilePath);
        if (!string.IsNullOrEmpty(testDir) && !Directory.Exists(testDir))
        {
            Directory.CreateDirectory(testDir);
            _logger.LogInformation("📁 Created test directory: {Dir}", testDir);
        }

        // Write test files
        foreach (var file in status.Result.Files)
        {
            var testPath = request.TestFilePath;
            
            // If orchestrator returned a different filename, use it
            if (!string.IsNullOrEmpty(file.Path) && file.Path.EndsWith("Tests.cs"))
            {
                testPath = Path.Combine(testDir ?? "", Path.GetFileName(file.Path));
            }

            await File.WriteAllTextAsync(testPath, file.Content, ct);
            
            _logger.LogInformation(
                "✅ Generated tests for {ClassName} → {TestFile} (Score: {Score}/10)",
                request.ClassName, Path.GetFileName(testPath), score);
        }
    }

    // Response DTOs
    private class OrchestratorResponse
    {
        public string JobId { get; set; } = "";
        public string Message { get; set; } = "";
    }

    private class TaskStatusResponse
    {
        public int Status { get; set; } // 0=Pending, 1=Running, 2=Complete, 3=Failed, 4=Cancelled
        public string? Error { get; set; }
        public TaskResult? Result { get; set; }
    }

    private class TaskResult
    {
        public int ValidationScore { get; set; }
        public List<GeneratedFile>? Files { get; set; }
    }

    private class GeneratedFile
    {
        public string Path { get; set; } = "";
        public string Content { get; set; } = "";
    }
}

