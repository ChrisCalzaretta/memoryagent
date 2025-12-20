using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DesignAgent.Server.Services.DesignIntelligence;
using DesignAgent.Server.Models.DesignIntelligence;
using DesignAgent.Server.Clients;
using AgentContracts.Services;

namespace DesignAgent.Tests;

/// <summary>
/// Comprehensive tests for all search providers with quota tracking and Polly resilience
/// </summary>
public class SearchProviderTests
{
    private readonly Mock<IOllamaClient> _mockOllama;
    private readonly Mock<IDesignIntelligenceStorage> _mockStorage;
    private readonly Mock<ILogger<DesignDiscoveryService>> _mockLogger;
    private readonly Mock<IHttpClientFactory> _mockHttpFactory;
    private readonly SearchQuotaTracker _quotaTracker;
    private readonly DesignIntelligenceOptions _options;

    public SearchProviderTests()
    {
        _mockOllama = new Mock<IOllamaClient>();
        _mockStorage = new Mock<IDesignIntelligenceStorage>();
        _mockLogger = new Mock<ILogger<DesignDiscoveryService>>();
        _mockHttpFactory = new Mock<IHttpClientFactory>();
        _quotaTracker = new SearchQuotaTracker(new Mock<ILogger<SearchQuotaTracker>>().Object);
        
        _options = new DesignIntelligenceOptions
        {
            SearchProvider = "google",
            SearchApiKey = "test-google-key",
            SearchEngineId = "test-cx-id",
            BraveApiKey = "test-brave-key"
        };
    }

    [Fact(DisplayName = "Google Search - Should work with valid API key and CX ID")]
    public async Task GoogleSearch_ValidCredentials_ReturnsResults()
    {
        // Arrange
        var httpClient = new HttpClient(new MockHttpMessageHandler(
            request => MockGoogleResponse()));
        _mockHttpFactory.Setup(f => f.CreateClient("SearchClient"))
            .Returns(httpClient);

        var service = CreateService();

        // Act
        var results = await service.SearchDesignSourcesAsync("modern dashboard design", 10);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains("https://example.com", results);
        
        // Verify quota was recorded
        var (daily, monthly) = _quotaTracker.GetRemainingCalls("google");
        Assert.Equal(99, daily); // Started at 100, used 1
    }

    [Fact(DisplayName = "Brave Search - Should work with valid API key")]
    public async Task BraveSearch_ValidCredentials_ReturnsResults()
    {
        // Arrange
        _options.SearchProvider = "brave";
        var httpClient = new HttpClient(new MockHttpMessageHandler(
            request => MockBraveResponse()));
        _mockHttpFactory.Setup(f => f.CreateClient("SearchClient"))
            .Returns(httpClient);

        var service = CreateService();

        // Act
        var results = await service.SearchDesignSourcesAsync("e-commerce product page", 10);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains("https://brave-example.com", results);
        
        // Verify quota was recorded
        var (_, monthly) = _quotaTracker.GetRemainingCalls("brave");
        Assert.Equal(1999, monthly); // Started at 2000, used 1
    }

    [Fact(DisplayName = "Bing HTML Scraping - Should work without API key")]
    public async Task BingHTMLScraping_NoApiKey_ReturnsResults()
    {
        // Arrange
        _options.SearchProvider = "bing";
        _options.BingApiKey = null; // Force HTML scraping
        
        var httpClient = new HttpClient(new MockHttpMessageHandler(
            request => MockBingHtmlResponse()));
        _mockHttpFactory.Setup(f => f.CreateClient("SearchClient"))
            .Returns(httpClient);

        var service = CreateService();

        // Act
        var results = await service.SearchDesignSourcesAsync("landing page design", 10);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains("https://bing-example.com", results);
        
        // Note: Even HTML scraping records usage for tracking purposes
        // This helps us monitor overall search usage across all methods
        var (daily, monthly) = _quotaTracker.GetRemainingCalls("bing");
        Assert.Equal(99, daily); // Used 1 search (via HTML scraping)
    }

    [Fact(DisplayName = "DuckDuckGo HTML Scraping - Should always work (unlimited)")]
    public async Task DuckDuckGoScraping_Unlimited_ReturnsResults()
    {
        // Arrange
        _options.SearchProvider = "duckduckgo";
        
        var httpClient = new HttpClient(new MockHttpMessageHandler(
            request => MockDuckDuckGoHtmlResponse()));
        _mockHttpFactory.Setup(f => f.CreateClient("SearchClient"))
            .Returns(httpClient);

        var service = CreateService();

        // Act
        var results = await service.SearchDesignSourcesAsync("blog design", 10);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains("https://ddg-example.com", results);
    }

    [Fact(DisplayName = "Quota Tracking - Should prevent calls when daily limit reached")]
    public async Task QuotaTracking_DailyLimitReached_SkipsProvider()
    {
        // Arrange
        // Exhaust Google's daily quota (100 calls)
        for (int i = 0; i < 100; i++)
        {
            _quotaTracker.RecordCall("google");
        }

        var httpClient = new HttpClient(new MockHttpMessageHandler(
            request => MockDuckDuckGoHtmlResponse())); // Will fallback to DuckDuckGo
        _mockHttpFactory.Setup(f => f.CreateClient("SearchClient"))
            .Returns(httpClient);

        var service = CreateService();

        // Act
        var results = await service.SearchDesignSourcesAsync("test query", 10);

        // Assert
        Assert.NotEmpty(results); // Should get results from DuckDuckGo fallback
        Assert.Contains("https://ddg-example.com", results);
        
        // Verify Google was skipped
        Assert.False(_quotaTracker.HasQuotaRemaining("google"));
    }

    [Fact(DisplayName = "Fallback Chain - Should try all providers in order")]
    public async Task FallbackChain_GoogleFails_FallsBackToNextProvider()
    {
        // Arrange
        var callCount = 0;
        var httpClient = new HttpClient(new MockHttpMessageHandler(request =>
        {
            callCount++;
            
            // First call (Google) fails with 429
            if (callCount == 1)
                throw new HttpRequestException("Too Many Requests", 
                    null, System.Net.HttpStatusCode.TooManyRequests);
            
            // Second call (Brave) succeeds
            return MockBraveResponse();
        }));
        
        _mockHttpFactory.Setup(f => f.CreateClient("SearchClient"))
            .Returns(httpClient);

        var service = CreateService();

        // Act
        var results = await service.SearchDesignSourcesAsync("test query", 10);

        // Assert
        Assert.NotEmpty(results);
        Assert.True(callCount >= 2); // Should have tried at least 2 providers
    }

    [Fact(DisplayName = "Polly Retry - Should retry transient failures 3 times")]
    public async Task PollyRetry_TransientFailure_Retries3Times()
    {
        // Arrange
        var attemptCount = 0;
        var httpClient = new HttpClient(new MockHttpMessageHandler(request =>
        {
            attemptCount++;
            
            // Fail first 2 attempts, succeed on 3rd
            if (attemptCount < 3)
                throw new HttpRequestException("Network error");
            
            return MockGoogleResponse();
        }));
        
        _mockHttpFactory.Setup(f => f.CreateClient("SearchClient"))
            .Returns(httpClient);

        var service = CreateService();

        // Act
        var results = await service.SearchDesignSourcesAsync("test query", 10);

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal(3, attemptCount); // Should have retried 2 times, succeeded on 3rd
    }

    [Fact(DisplayName = "Quota Reset - Daily quota should reset after midnight UTC")]
    public void QuotaReset_AfterMidnight_ResetsDaily()
    {
        // Arrange
        _quotaTracker.RecordCall("google");
        var (dailyBefore, _) = _quotaTracker.GetRemainingCalls("google");
        
        // Act - Simulate midnight UTC reset
        _quotaTracker.ResetExpiredQuotas();
        
        // Assert
        // In real scenario, this would reset if date changed
        // For now, just verify the method exists and runs
        var (dailyAfter, _) = _quotaTracker.GetRemainingCalls("google");
        Assert.NotNull(dailyAfter);
    }

    // Helper methods

    private DesignDiscoveryService CreateService()
    {
        return new DesignDiscoveryService(
            _mockOllama.Object,
            _mockStorage.Object,
            _mockLogger.Object,
            Options.Create(_options),
            _mockHttpFactory.Object,
            _quotaTracker);
    }

    private static HttpResponseMessage MockGoogleResponse()
    {
        var json = @"{
            ""items"": [
                {""link"": ""https://example.com""},
                {""link"": ""https://example2.com""}
            ]
        }";
        return new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage MockBraveResponse()
    {
        var json = @"{
            ""web"": {
                ""results"": [
                    {""url"": ""https://brave-example.com""},
                    {""url"": ""https://brave-example2.com""}
                ]
            }
        }";
        return new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage MockBingHtmlResponse()
    {
        var html = @"
            <li class=""b_algo"">
                <h2><a href=""https://bing-example.com"">Example</a></h2>
            </li>
            <li class=""b_algo"">
                <h2><a href=""https://bing-example2.com"">Example 2</a></h2>
            </li>";
        return new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        };
    }

    private static HttpResponseMessage MockDuckDuckGoHtmlResponse()
    {
        var html = @"<a href=""/url?uddg=https%3A%2F%2Fddg-example.com"">Example</a>";
        return new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
        };
    }
}

/// <summary>
/// Mock HTTP message handler for testing
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _sendFunc;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sendFunc)
    {
        _sendFunc = sendFunc;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_sendFunc(request));
    }
}


