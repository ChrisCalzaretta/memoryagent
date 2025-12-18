using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DesignAgent.Server.Services.DesignIntelligence;
using DesignAgent.Server.Models.DesignIntelligence;
using AgentContracts.Services;

namespace DesignAgent.Tests.Services;

public class DesignDiscoveryServiceTests
{
    private readonly Mock<IOllamaClient> _mockOllama;
    private readonly Mock<IDesignIntelligenceStorage> _mockStorage;
    private readonly Mock<ILogger<DesignDiscoveryService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpFactory;
    private readonly DesignIntelligenceOptions _options;
    private readonly DesignDiscoveryService _service;

    public DesignDiscoveryServiceTests()
    {
        _mockOllama = new Mock<IOllamaClient>();
        _mockStorage = new Mock<IDesignIntelligenceStorage>();
        _mockLogger = new Mock<ILogger<DesignDiscoveryService>>();
        _mockHttpFactory = new Mock<IHttpClientFactory>();
        
        _options = new DesignIntelligenceOptions
        {
            TextModel = "phi4",
            SearchProvider = "google",
            SearchQueriesPerRun = 5,
            SearchResultsPerQuery = 10
        };

        var quotaTracker = new SearchQuotaTracker(new Mock<ILogger<SearchQuotaTracker>>().Object);

        _service = new DesignDiscoveryService(
            _mockOllama.Object,
            _mockStorage.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockHttpFactory.Object,
            quotaTracker
        );
    }

    [Fact]
    public async Task GenerateSearchQueriesAsync_ShouldReturnQueries()
    {
        // Arrange
        var llmResponse = @"
best SaaS designs 2024
modern UI inspiration
Awwwards winners
minimal web design
gradient design trends
";
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
        var queries = await _service.GenerateSearchQueriesAsync(5);

        // Assert
        Assert.NotEmpty(queries);
        Assert.Contains(queries, q => q.Contains("SaaS"));
        Assert.Contains(queries, q => q.Contains("Awwwards"));
    }

    [Fact]
    public async Task EvaluateSearchResultAsync_ShouldReturnSource_WhenDesignWorthy()
    {
        // Arrange
        var url = "https://linear.app";
        var searchQuery = "best SaaS designs";
        
        var llmResponse = @"
{
  ""isDesignWorthy"": true,
  ""trustScore"": 10,
  ""category"": ""saas"",
  ""tags"": [""gradient"", ""minimal"", ""modern""],
  ""reason"": ""Well-known Y Combinator company with excellent design""
}";
        
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

        _mockStorage.Setup(x => x.GetSourceByUrlAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DesignSource?)null);

        // Act
        var source = await _service.EvaluateSearchResultAsync(url, searchQuery);

        // Assert
        Assert.NotNull(source);
        Assert.Equal("https://linear.app", source.Url);
        Assert.Equal(10, source.TrustScore);
        Assert.Equal("saas", source.Category);
        Assert.Contains("gradient", source.Tags);
    }

    [Fact]
    public async Task EvaluateSearchResultAsync_ShouldReturnNull_WhenNotDesignWorthy()
    {
        // Arrange
        var url = "https://example-blog.com/design-tips";
        var searchQuery = "design tips";
        
        var llmResponse = @"
{
  ""isDesignWorthy"": false,
  ""trustScore"": 3,
  ""category"": ""blog"",
  ""tags"": [],
  ""reason"": ""This is a blog post about design, not an actual designed site""
}";
        
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

        _mockStorage.Setup(x => x.GetSourceByUrlAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DesignSource?)null);

        // Act
        var source = await _service.EvaluateSearchResultAsync(url, searchQuery);

        // Assert
        Assert.Null(source);
    }

    [Fact]
    public async Task EvaluateSearchResultAsync_ShouldReturnNull_WhenAlreadyEvaluated()
    {
        // Arrange
        var url = "https://linear.app";
        var existingSource = new DesignSource
        {
            Url = url,
            AlreadyEvaluated = true
        };

        _mockStorage.Setup(x => x.GetSourceByUrlAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSource);

        // Act
        var source = await _service.EvaluateSearchResultAsync(url, "test query");

        // Assert
        Assert.Null(source);
        _mockOllama.Verify(x => x.GenerateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeedCuratedSourcesAsync_ShouldSkipDuplicates()
    {
        // Arrange
        var sources = new List<DesignSource>
        {
            new DesignSource { Url = "https://linear.app", TrustScore = 10 },
            new DesignSource { Url = "https://notion.so", TrustScore = 10 },
            new DesignSource { Url = "https://figma.com", TrustScore = 10 }
        };

        // Mock that linear.app already exists
        _mockStorage.Setup(x => x.GetSourceByUrlAsync("https://linear.app", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DesignSource { Url = "https://linear.app" });
        
        _mockStorage.Setup(x => x.GetSourceByUrlAsync("https://notion.so", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DesignSource?)null);
        
        _mockStorage.Setup(x => x.GetSourceByUrlAsync("https://figma.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DesignSource?)null);

        // Act
        var seeded = await _service.SeedCuratedSourcesAsync(sources);

        // Assert
        Assert.Equal(2, seeded); // Only 2 new sources
        _mockStorage.Verify(x => x.StoreSourceAsync(
            It.Is<DesignSource>(s => s.Url == "https://notion.so"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(x => x.StoreSourceAsync(
            It.Is<DesignSource>(s => s.Url == "https://figma.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

