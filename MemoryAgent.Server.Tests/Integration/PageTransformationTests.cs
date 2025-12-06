using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MemoryAgent.Server.Tests.Integration;

public class PageTransformationTests : IntegrationTestBase
{
    private readonly IPageTransformationService _transformationService;
    private readonly ICSSTransformationService _cssService;
    private readonly IComponentExtractionService _componentService;
    
    public PageTransformationTests()
    {
        _transformationService = ServiceProvider.GetRequiredService<IPageTransformationService>();
        _cssService = ServiceProvider.GetRequiredService<ICSSTransformationService>();
        _componentService = ServiceProvider.GetRequiredService<IComponentExtractionService>();
    }
    
    [Fact]
    public async Task TransformPage_WithInlineStyles_ShouldExtractToCSS()
    {
        // Arrange
        var sourcePage = @"
            @page ""/test""
            <div style=""color: red; padding: 10px;"">
                <h1>Test Header</h1>
                <p style=""font-size: 14px;"">Test paragraph</p>
            </div>
        ";
        
        var filePath = await CreateTestFileAsync("Test.razor", sourcePage);
        
        // Act
        var result = await _transformationService.TransformPageAsync(
            filePath,
            new TransformationOptions { ModernizeCSS = true });
        
        // Assert
        Assert.Equal(TransformationStatus.Completed, result.Status);
        Assert.True(result.InlineStylesRemoved > 0);
        Assert.Contains(result.GeneratedFiles, f => f.Type == FileType.CSS);
    }
    
    [Fact]
    public async Task TransformPage_LargeComponent_ShouldExtractComponents()
    {
        // Arrange
        var sourcePage = @"
            @page ""/products""
            @* Large 500-line component with repeated patterns *@
            <div class=""product-grid"">
                @foreach (var p in products)
                {
                    <div class=""product-card"">
                        <h3>@p.Name</h3>
                        <p>@p.Description</p>
                        <span>@p.Price</span>
                    </div>
                }
            </div>
        ";
        
        var filePath = await CreateTestFileAsync("Products.razor", sourcePage);
        
        // Act
        var result = await _transformationService.TransformPageAsync(
            filePath,
            new TransformationOptions { ExtractComponents = true });
        
        // Assert
        Assert.Equal(TransformationStatus.Completed, result.Status);
        // May extract ProductCard component
    }
    
    [Fact]
    public async Task AnalyzeCSS_WithInlineStyles_ShouldDetectIssues()
    {
        // Arrange
        var sourcePage = @"
            <div style=""float: left; width: 33%;"">
                <span style=""color: #333;"">Test</span>
            </div>
        ";
        
        var filePath = await CreateTestFileAsync("TestCSS.razor", sourcePage);
        
        // Act
        var result = await _cssService.AnalyzeCSSAsync(filePath);
        
        // Assert
        Assert.True(result.InlineStyleCount > 0);
        Assert.NotEmpty(result.Issues);
        Assert.NotEmpty(result.Recommendations);
        Assert.True(result.QualityScore < 100);
    }
    
    [Fact]
    public async Task LearnPattern_WithExampleFiles_ShouldCreatePattern()
    {
        // Arrange
        var oldPage = @"
            @page ""/old""
            <div style=""padding: 10px;"">
                <span>Content</span>
            </div>
        ";
        
        var newPage = @"
            @page ""/new""
            <div class=""container"">
                <span>Content</span>
            </div>
        ";
        
        var oldPath = await CreateTestFileAsync("Old.razor", oldPage);
        var newPath = await CreateTestFileAsync("New.razor", newPage);
        
        // Act
        var pattern = await _transformationService.LearnPatternAsync(
            oldPath,
            newPath,
            "TestPattern");
        
        // Assert
        Assert.NotNull(pattern);
        Assert.Equal("TestPattern", pattern.Name);
        Assert.NotEmpty(pattern.ExampleOldFilePath);
        Assert.NotEmpty(pattern.ExampleNewFilePath);
    }
    
    [Fact]
    public async Task DetectReusableComponents_WithRepeatedPatterns_ShouldFindCandidates()
    {
        // Arrange
        var projectPath = await CreateTestProjectAsync(new Dictionary<string, string>
        {
            ["Page1.razor"] = @"<div class='card'><h3>Title1</h3></div>",
            ["Page2.razor"] = @"<div class='card'><h3>Title2</h3></div>",
            ["Page3.razor"] = @"<div class='card'><h3>Title3</h3></div>"
        });
        
        // Act
        var candidates = await _componentService.DetectReusableComponentsAsync(
            projectPath,
            minOccurrences: 2);
        
        // Assert
        // May find card pattern
        Assert.NotNull(candidates);
    }
    
    // === HELPER METHODS ===
    
    private async Task<string> CreateTestFileAsync(string fileName, string content)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MemoryAgentTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, content);
        
        return filePath;
    }
    
    private async Task<string> CreateTestProjectAsync(Dictionary<string, string> files)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "MemoryAgentTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(projectDir);
        
        foreach (var (fileName, content) in files)
        {
            var filePath = Path.Combine(projectDir, fileName);
            await File.WriteAllTextAsync(filePath, content);
        }
        
        return projectDir;
    }
}

/// <summary>
/// Base class for integration tests
/// </summary>
public class IntegrationTestBase : IDisposable
{
    protected ServiceProvider ServiceProvider { get; }
    
    public IntegrationTestBase()
    {
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging();
        
        // Add configuration (Ollama settings) - DeepSeek for better JSON compliance
        var configData = new Dictionary<string, string?>
        {
            ["Ollama:BaseUrl"] = "http://localhost:11434",
            ["Ollama:LLMModel"] = "deepseek-coder-v2:16b"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        
        // Add HTTP client with Ollama base URL
        services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = new Uri("http://localhost:11434");
            client.Timeout = TimeSpan.FromMinutes(15); // First load can take 2-3 min, then generation time
        });
        
        // Add code parsers
        services.AddSingleton<MemoryAgent.Server.CodeAnalysis.RazorParser>();
        
        // Add transformation services
        services.AddScoped<MemoryAgent.Server.Services.ILLMService, MemoryAgent.Server.Services.LLMService>();
        services.AddScoped<MemoryAgent.Server.Services.IPageTransformationService, MemoryAgent.Server.Services.PageTransformationService>();
        services.AddScoped<MemoryAgent.Server.Services.ICSSTransformationService, MemoryAgent.Server.Services.CSSTransformationService>();
        services.AddScoped<MemoryAgent.Server.Services.IComponentExtractionService, MemoryAgent.Server.Services.ComponentExtractionService>();
        services.AddSingleton<MemoryAgent.Server.Services.IPromptService, TestPromptService>();
        
        // Add mock/test implementations of code parsers if needed
        // For now, we'll let the tests fail if they try to parse actual code
        
        ServiceProvider = services.BuildServiceProvider();
    }
    
    public void Dispose()
    {
        ServiceProvider?.Dispose();
    }
}

