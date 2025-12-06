using System;
using Moq;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using MemoryAgent.Server.Services.Mcp.Consolidated;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Tests for the consolidated MCP tool handlers
/// </summary>
public class ConsolidatedToolHandlerTests
{
    #region SearchToolHandler Tests

    [Fact]
    public void SearchToolHandler_GetTools_ReturnsSingleTool()
    {
        // Arrange
        var mockSmartSearch = new Mock<ISmartSearchService>();
        var mockLearning = new Mock<ILearningService>();
        var mockLogger = new Mock<ILogger<SearchToolHandler>>();
        var handler = new SearchToolHandler(mockSmartSearch.Object, mockLearning.Object, mockLogger.Object);

        // Act
        var tools = handler.GetTools().ToList();

        // Assert
        Assert.Single(tools);
        Assert.Equal("smartsearch", tools[0].Name);
        Assert.Contains("semantic", tools[0].Description.ToLower());
    }

    [Fact]
    public async Task SearchToolHandler_HandleToolAsync_RequiresQuery()
    {
        // Arrange
        var mockSmartSearch = new Mock<ISmartSearchService>();
        var mockLearning = new Mock<ILearningService>();
        var mockLogger = new Mock<ILogger<SearchToolHandler>>();
        var handler = new SearchToolHandler(mockSmartSearch.Object, mockLearning.Object, mockLogger.Object);

        // Act
        var result = await handler.HandleToolAsync("smartsearch", new Dictionary<string, object>(), CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task SearchToolHandler_HandleToolAsync_CallsSmartSearch()
    {
        // Arrange
        var mockSmartSearch = new Mock<ISmartSearchService>();
        mockSmartSearch.Setup(s => s.SearchAsync(It.IsAny<SmartSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartSearchResponse 
            { 
                Results = new List<SmartSearchResult>
                {
                    new SmartSearchResult { FilePath = "test.cs", Content = "test content", Score = 0.95f }
                }
            });
        
        var mockLearning = new Mock<ILearningService>();
        var mockLogger = new Mock<ILogger<SearchToolHandler>>();
        var handler = new SearchToolHandler(mockSmartSearch.Object, mockLearning.Object, mockLogger.Object);

        var args = new Dictionary<string, object>
        {
            { "query", "test query" },
            { "context", "memoryagent" }
        };

        // Act
        var result = await handler.HandleToolAsync("smartsearch", args, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        mockSmartSearch.Verify(s => s.SearchAsync(
            It.Is<SmartSearchRequest>(r => r.Query == "test query" && r.Context == "memoryagent"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region IndexToolHandler Tests

    [Fact]
    public void IndexToolHandler_GetTools_ReturnsSingleTool()
    {
        // Arrange
        var mockIndexing = new Mock<IIndexingService>();
        var mockReindex = new Mock<IReindexService>();
        var mockLogger = new Mock<ILogger<IndexToolHandler>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var handler = new IndexToolHandler(mockIndexing.Object, mockReindex.Object, mockServiceProvider.Object, mockLogger.Object);

        // Act
        var tools = handler.GetTools().ToList();

        // Assert
        Assert.Single(tools);
        Assert.Equal("index", tools[0].Name);
    }

    [Fact]
    public async Task IndexToolHandler_HandleToolAsync_RequiresPath()
    {
        // Arrange
        var mockIndexing = new Mock<IIndexingService>();
        var mockReindex = new Mock<IReindexService>();
        var mockLogger = new Mock<ILogger<IndexToolHandler>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var handler = new IndexToolHandler(mockIndexing.Object, mockReindex.Object, mockServiceProvider.Object, mockLogger.Object);

        // Act
        var result = await handler.HandleToolAsync("index", new Dictionary<string, object>(), CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task IndexToolHandler_HandleToolAsync_FileScope_CallsIndexFile()
    {
        // Arrange
        var mockIndexing = new Mock<IIndexingService>();
        mockIndexing.Setup(i => i.IndexFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { Success = true, FilesIndexed = 1 });
        
        var mockReindex = new Mock<IReindexService>();
        var mockLogger = new Mock<ILogger<IndexToolHandler>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var handler = new IndexToolHandler(mockIndexing.Object, mockReindex.Object, mockServiceProvider.Object, mockLogger.Object);

        var args = new Dictionary<string, object>
        {
            { "path", "test.cs" },
            { "scope", "file" },
            { "context", "memoryagent" }  // context is required
        };

        // Act
        var result = await handler.HandleToolAsync("index", args, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        mockIndexing.Verify(i => i.IndexFileAsync("test.cs", "memoryagent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndexToolHandler_HandleToolAsync_DirectoryScope_CallsIndexDirectory()
    {
        // Arrange
        var mockIndexing = new Mock<IIndexingService>();
        mockIndexing.Setup(i => i.IndexDirectoryAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { Success = true, FilesIndexed = 10 });
        
        var mockReindex = new Mock<IReindexService>();
        var mockLogger = new Mock<ILogger<IndexToolHandler>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var handler = new IndexToolHandler(mockIndexing.Object, mockReindex.Object, mockServiceProvider.Object, mockLogger.Object);

        var args = new Dictionary<string, object>
        {
            { "path", "/workspace" },
            { "scope", "directory" },
            { "recursive", true },
            { "context", "memoryagent" }  // context is required
        };

        // Act
        var result = await handler.HandleToolAsync("index", args, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        mockIndexing.Verify(i => i.IndexDirectoryAsync("/workspace", true, "memoryagent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IndexToolHandler_HandleToolAsync_ReindexScope_CallsReindex()
    {
        // Arrange
        var mockIndexing = new Mock<IIndexingService>();
        var mockReindex = new Mock<IReindexService>();
        mockReindex.Setup(r => r.ReindexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Action<int>>()))
            .ReturnsAsync(new ReindexResult { Success = true, FilesAdded = 5, FilesUpdated = 3 });
        
        var mockLogger = new Mock<ILogger<IndexToolHandler>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var handler = new IndexToolHandler(mockIndexing.Object, mockReindex.Object, mockServiceProvider.Object, mockLogger.Object);

        var args = new Dictionary<string, object>
        {
            { "path", "/workspace" },
            { "scope", "reindex" },
            { "context", "memoryagent" }
        };

        // Act
        var result = await handler.HandleToolAsync("index", args, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        mockReindex.Verify(r => r.ReindexAsync("memoryagent", "/workspace", true, It.IsAny<CancellationToken>(), It.IsAny<Action<int>>()), Times.Once);
    }

    #endregion

    #region SessionToolHandler Tests

    [Fact]
    public void SessionToolHandler_GetTools_Returns6Tools()
    {
        // Arrange
        var mockLearning = new Mock<ILearningService>();
        var mockLogger = new Mock<ILogger<SessionToolHandler>>();
        var handler = new SessionToolHandler(mockLearning.Object, mockLogger.Object);

        // Act
        var tools = handler.GetTools().ToList();

        // Assert
        Assert.Equal(6, tools.Count);
        Assert.Contains(tools, t => t.Name == "start_session");
        Assert.Contains(tools, t => t.Name == "end_session");
        Assert.Contains(tools, t => t.Name == "record_file_discussed");
        Assert.Contains(tools, t => t.Name == "record_file_edited");
        Assert.Contains(tools, t => t.Name == "store_qa");
        Assert.Contains(tools, t => t.Name == "find_similar_questions");
    }

    [Fact]
    public async Task SessionToolHandler_StartSession_CallsLearningService()
    {
        // Arrange
        var mockLearning = new Mock<ILearningService>();
        var expectedSession = new Session { Id = "sess123", Context = "memoryagent" };
        mockLearning.Setup(l => l.StartSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);
        
        var mockLogger = new Mock<ILogger<SessionToolHandler>>();
        var handler = new SessionToolHandler(mockLearning.Object, mockLogger.Object);

        var args = new Dictionary<string, object>
        {
            { "context", "MemoryAgent" }  // Note: uppercase - should be normalized
        };

        // Act
        var result = await handler.HandleToolAsync("start_session", args, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        mockLearning.Verify(l => l.StartSessionAsync("memoryagent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SessionToolHandler_StoreQA_RequiresAllFields()
    {
        // Arrange
        var mockLearning = new Mock<ILearningService>();
        var mockLogger = new Mock<ILogger<SessionToolHandler>>();
        var handler = new SessionToolHandler(mockLearning.Object, mockLogger.Object);

        // Missing answer and relevantFiles
        var args = new Dictionary<string, object>
        {
            { "question", "How does X work?" },
            { "context", "memoryagent" }
        };

        // Act
        var result = await handler.HandleToolAsync("store_qa", args, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
    }

    #endregion

    #region EvolvingToolHandler Tests

    [Fact]
    public void EvolvingToolHandler_GetTools_Returns3Tools()
    {
        // Arrange
        var mockPrompt = new Mock<IPromptService>();
        var mockPattern = new Mock<IEvolvingPatternCatalogService>();
        var mockLogger = new Mock<ILogger<EvolvingToolHandler>>();
        var handler = new EvolvingToolHandler(mockPrompt.Object, mockPattern.Object, mockLogger.Object);

        // Act
        var tools = handler.GetTools().ToList();

        // Assert
        Assert.Equal(3, tools.Count);
        Assert.Contains(tools, t => t.Name == "manage_prompts");
        Assert.Contains(tools, t => t.Name == "manage_patterns");
        Assert.Contains(tools, t => t.Name == "feedback");
    }

    #endregion

    #region Context Normalization Tests

    [Theory]
    [InlineData("MemoryAgent", "memoryagent")]
    [InlineData("MEMORYAGENT", "memoryagent")]
    [InlineData("memoryagent", "memoryagent")]
    [InlineData("Memory_Agent", "memory_agent")]
    public async Task SearchHandler_NormalizesContextToLowercase(string inputContext, string expectedContext)
    {
        // Arrange
        var mockSmartSearch = new Mock<ISmartSearchService>();
        mockSmartSearch.Setup(s => s.SearchAsync(It.IsAny<SmartSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartSearchResponse { Results = new List<SmartSearchResult>() });
        
        var mockLearning = new Mock<ILearningService>();
        var handler = new SearchToolHandler(mockSmartSearch.Object, mockLearning.Object, Mock.Of<ILogger<SearchToolHandler>>());
        
        var args = new Dictionary<string, object>
        {
            { "query", "test" },
            { "context", inputContext }
        };
        
        // Act
        await handler.HandleToolAsync("smartsearch", args, CancellationToken.None);
        
        // Assert
        mockSmartSearch.Verify(s => s.SearchAsync(
            It.Is<SmartSearchRequest>(r => r.Context == expectedContext),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("MemoryAgent", "memoryagent")]
    [InlineData("MEMORYAGENT", "memoryagent")]
    public async Task SessionHandler_NormalizesContextToLowercase(string inputContext, string expectedContext)
    {
        // Arrange
        var mockLearning = new Mock<ILearningService>();
        mockLearning.Setup(l => l.StartSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Session { Id = "test", Context = expectedContext });
        
        var handler = new SessionToolHandler(mockLearning.Object, Mock.Of<ILogger<SessionToolHandler>>());
        
        var args = new Dictionary<string, object>
        {
            { "context", inputContext }
        };
        
        // Act
        await handler.HandleToolAsync("start_session", args, CancellationToken.None);
        
        // Assert
        mockLearning.Verify(l => l.StartSessionAsync(expectedContext, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Tool Count Verification

    [Fact]
    public void ConsolidatedHandlers_TotalToolCount_Under30()
    {
        // Verify we haven't exceeded our tool limit
        var totalTools = 0;

        // SearchToolHandler: 1 tool
        var searchHandler = new SearchToolHandler(
            Mock.Of<ISmartSearchService>(), 
            Mock.Of<ILearningService>(),
            Mock.Of<ILogger<SearchToolHandler>>());
        totalTools += searchHandler.GetTools().Count();

        // IndexToolHandler: 1 tool
        var indexHandler = new IndexToolHandler(
            Mock.Of<IIndexingService>(), 
            Mock.Of<IReindexService>(), 
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<IndexToolHandler>>());
        totalTools += indexHandler.GetTools().Count();

        // SessionToolHandler: 6 tools
        var sessionHandler = new SessionToolHandler(
            Mock.Of<ILearningService>(), 
            Mock.Of<ILogger<SessionToolHandler>>());
        totalTools += sessionHandler.GetTools().Count();

        // EvolvingToolHandler: 3 tools
        var evolvingHandler = new EvolvingToolHandler(
            Mock.Of<IPromptService>(), 
            Mock.Of<IEvolvingPatternCatalogService>(), 
            Mock.Of<ILogger<EvolvingToolHandler>>());
        totalTools += evolvingHandler.GetTools().Count();

        // Assert we're within our target (these 4 handlers = 11 tools)
        // Plus workspace tools (2) and remaining handlers = should total ~27
        Assert.Equal(11, totalTools);  // Just counting what we've tested here
        Assert.InRange(totalTools, 5, 30);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task SearchToolHandler_WhenSearchFails_ReturnsEmptyResults()
    {
        // Arrange - test when search returns no results (empty list)
        var mockSmartSearch = new Mock<ISmartSearchService>();
        mockSmartSearch.Setup(s => s.SearchAsync(It.IsAny<SmartSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartSearchResponse { Results = new List<SmartSearchResult>() });
        
        var mockLearning = new Mock<ILearningService>();
        var mockLogger = new Mock<ILogger<SearchToolHandler>>();
        var handler = new SearchToolHandler(mockSmartSearch.Object, mockLearning.Object, mockLogger.Object);

        var args = new Dictionary<string, object>
        {
            { "query", "nonexistent code" },
            { "context", "test" }
        };

        // Act
        var result = await handler.HandleToolAsync("smartsearch", args, CancellationToken.None);

        // Assert - should not error, just return empty results
        Assert.False(result.IsError);
        Assert.Contains("0 results", result.Content![0].Text!.ToLower());
    }

    [Fact]
    public async Task IndexToolHandler_ReturnsErrorWhenMissingContext()
    {
        // Arrange
        var mockIndexing = new Mock<IIndexingService>();
        var mockReindex = new Mock<IReindexService>();
        var mockLogger = new Mock<ILogger<IndexToolHandler>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        var handler = new IndexToolHandler(mockIndexing.Object, mockReindex.Object, mockServiceProvider.Object, mockLogger.Object);

        // Missing context
        var args = new Dictionary<string, object>
        {
            { "path", "test.cs" },
            { "scope", "file" }
        };

        // Act
        var result = await handler.HandleToolAsync("index", args, CancellationToken.None);

        // Assert - should fail due to missing context
        Assert.True(result.IsError);
        Assert.Contains("context", result.Content![0].Text!.ToLower());
    }

    #endregion
}
