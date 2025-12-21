using CodingAgent.Server.Services;
using CodingAgent.Server.Templates;
using AgentContracts.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace CodingAgent.Server.Tests;

public class ProjectOrchestratorTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<ProjectOrchestrator> _logger;
    private readonly ILogger<TemplateService> _templateLogger;

    public ProjectOrchestratorTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new XUnitLogger<ProjectOrchestrator>(output);
        _templateLogger = new XUnitLogger<TemplateService>(output);
    }

    [Fact]
    public async Task GenerateProjectAsync_CSharpConsoleApp_ReturnsTemplateFiles()
    {
        // Arrange
        var mockCodeGen = new Mock<ICodeGenerationService>();
        var templateService = new TemplateService(_templateLogger);
        var mockStubGen = new Mock<IStubGenerator>();
        var mockFailureGen = new Mock<IFailureReportGenerator>();
        
        var orchestrator = new ProjectOrchestrator(
            mockCodeGen.Object,
            templateService,
            mockStubGen.Object,
            mockFailureGen.Object,
            _logger);

        // Act
        var result = await orchestrator.GenerateProjectAsync(
            "Create a C# console app",
            language: "csharp");

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.FileChanges);
        Assert.Contains(result.FileChanges, f => f.Path.EndsWith(".cs"));
        Assert.Contains("template:", result.ModelUsed);
        
        _output.WriteLine($"✅ Generated {result.FileChanges.Count} files");
        foreach (var file in result.FileChanges)
        {
            _output.WriteLine($"  📄 {file.Path} ({file.Content.Length} chars)");
        }
    }

    [Fact]
    public async Task GenerateProjectAsync_FlutterIosApp_ReturnsFlutterFiles()
    {
        // Arrange
        var mockCodeGen = new Mock<ICodeGenerationService>();
        var templateService = new TemplateService(_templateLogger);
        var mockStubGen = new Mock<IStubGenerator>();
        var mockFailureGen = new Mock<IFailureReportGenerator>();
        
        var orchestrator = new ProjectOrchestrator(
            mockCodeGen.Object,
            templateService,
            mockStubGen.Object,
            mockFailureGen.Object,
            _logger);

        // Act
        var result = await orchestrator.GenerateProjectAsync(
            "Create a Flutter iOS app",
            language: "flutter");

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.FileChanges);
        Assert.Contains(result.FileChanges, f => f.Path.EndsWith(".dart"));
        Assert.Contains(result.FileChanges, f => f.Path == "pubspec.yaml");
        
        _output.WriteLine($"✅ Generated {result.FileChanges.Count} files");
        foreach (var file in result.FileChanges)
        {
            _output.WriteLine($"  📄 {file.Path}");
            if (file.Path.EndsWith(".dart"))
            {
                _output.WriteLine($"     Preview: {file.Content.Substring(0, Math.Min(100, file.Content.Length))}...");
            }
        }
    }

    [Fact]
    public async Task GenerateProjectAsync_WebApi_ReturnsApiFiles()
    {
        // Arrange
        var mockCodeGen = new Mock<ICodeGenerationService>();
        var templateService = new TemplateService(_templateLogger);
        var mockStubGen = new Mock<IStubGenerator>();
        var mockFailureGen = new Mock<IFailureReportGenerator>();
        
        var orchestrator = new ProjectOrchestrator(
            mockCodeGen.Object,
            templateService,
            mockStubGen.Object,
            mockFailureGen.Object,
            _logger);

        // Act
        var result = await orchestrator.GenerateProjectAsync(
            "Create a REST API for user management",
            language: "csharp"); // FIXED: specify language

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.FileChanges);
        Assert.Contains(result.FileChanges, f => f.Path == "Program.cs");
        Assert.Contains("webapi", result.ModelUsed.ToLower());
        
        _output.WriteLine($"✅ Generated {result.FileChanges.Count} files");
    }
}

// Helper class for logging test output
public class XUnitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public XUnitLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        if (exception != null)
        {
            _output.WriteLine($"  Exception: {exception.Message}");
        }
    }
}

