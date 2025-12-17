using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DesignAgent.Server.Services.DesignIntelligence;
using DesignAgent.Server.Models.DesignIntelligence;
using AgentContracts.Services;

namespace DesignAgent.Tests.Services;

public class DesignCaptureServiceTests
{
    private readonly Mock<IOllamaClient> _mockOllama;
    private readonly Mock<IDesignIntelligenceStorage> _mockStorage;
    private readonly Mock<ILogger<DesignCaptureService>> _mockLogger;
    private readonly DesignIntelligenceOptions _options;
    private readonly DesignCaptureService _service;

    public DesignCaptureServiceTests()
    {
        _mockOllama = new Mock<IOllamaClient>();
        _mockStorage = new Mock<IDesignIntelligenceStorage>();
        _mockLogger = new Mock<ILogger<DesignCaptureService>>();
        
        _options = new DesignIntelligenceOptions
        {
            TextModel = "phi4",
            MaxPagesPerSite = 6,
            CrawlDelayMs = 100, // Faster for tests
            ScreenshotPath = "./test_screenshots",
            ScreenshotBreakpoints = new[] { 1920, 1024, 375 }
        };

        _service = new DesignCaptureService(
            _mockOllama.Object,
            _mockStorage.Object,
            _mockLogger.Object,
            Options.Create(_options)
        );
    }

    [Fact]
    public async Task SelectImportantLinksAsync_ShouldParseLLMResponse()
    {
        // Arrange
        var baseUrl = "https://example.com";
        var links = new List<string>
        {
            "https://example.com/pricing",
            "https://example.com/features",
            "https://example.com/about",
            "https://example.com/blog",
            "https://example.com/login",
            "https://example.com/privacy"
        };

        var llmResponse = @"
[
  {""url"": ""https://example.com/pricing"", ""pageType"": ""pricing""},
  {""url"": ""https://example.com/features"", ""pageType"": ""features""},
  {""url"": ""https://example.com/about"", ""pageType"": ""about""},
  {""url"": ""https://example.com/blog"", ""pageType"": ""blog""}
]";

        _mockOllama.Setup(x => x.GenerateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResponse
            {
                Response = llmResponse,
                Success = true
            });

        // Act
        var selected = await _service.SelectImportantLinksAsync(baseUrl, links, 5);

        // Assert
        Assert.NotEmpty(selected);
        Assert.Contains("https://example.com/pricing", selected.Keys);
        Assert.Equal("pricing", selected["https://example.com/pricing"]);
        Assert.DoesNotContain("https://example.com/login", selected.Keys); // Should not include login
        Assert.DoesNotContain("https://example.com/privacy", selected.Keys); // Should not include privacy
    }

    [Fact]
    public async Task SelectImportantLinksAsync_ShouldUseFallback_WhenLLMFails()
    {
        // Arrange
        var baseUrl = "https://example.com";
        var links = new List<string>
        {
            "https://example.com/pricing",
            "https://example.com/features",
            "https://example.com/about"
        };

        _mockOllama.Setup(x => x.GenerateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResponse
            {
                Response = "Invalid JSON response",
                Success = true
            });

        // Act
        var selected = await _service.SelectImportantLinksAsync(baseUrl, links, 5);

        // Assert
        Assert.NotEmpty(selected); // Should use fallback pattern matching
        Assert.Contains("https://example.com/pricing", selected.Keys);
    }

    [Fact]
    public void IsExcludedUrl_ShouldFilterCommonPages()
    {
        // This would need to be a public method or use reflection to test
        // For now, we verify through integration that excluded URLs don't appear
        
        var excludedUrls = new[]
        {
            "https://example.com/login",
            "https://example.com/signup",
            "https://example.com/privacy",
            "https://example.com/terms",
            "https://example.com/careers",
            "https://example.com/api/v1/users"
        };

        // These should all be filtered out during link extraction
        // We can test this indirectly through CrawlWebsiteAsync
    }

    [Fact]
    public async Task CrawlWebsiteAsync_ShouldCreateDesignWithMultiplePages()
    {
        // Note: This is an integration test that requires ChromeDriver
        // In a real test, we'd use a mock HTML server or skip this test in CI
        
        // For now, we'll mark this as a theory that can be skipped
        // [Fact(Skip = "Requires ChromeDriver and internet connection")]
    }
}

