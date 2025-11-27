using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MemoryAgent.Server.Tests.Integration;

public class IndexingServiceWithSemgrepTests
{
    private readonly IndexingService _indexingService;
    private readonly Mock<IVectorService> _vectorServiceMock;
    private readonly Mock<IGraphService> _graphServiceMock;
    private readonly ISemgrepService _semgrepService;

    public IndexingServiceWithSemgrepTests()
    {
        var codeParser = new CodeParser(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<CodeParser>());
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        _vectorServiceMock = new Mock<IVectorService>();
        _graphServiceMock = new Mock<IGraphService>();
        var pathTranslationMock = new Mock<IPathTranslationService>();
        
        _semgrepService = new SemgrepService(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SemgrepService>());

        // Setup mocks
        embeddingServiceMock.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
                texts.Select(_ => new float[1024]).ToList());

        _vectorServiceMock.Setup(v => v.StoreCodeMemoriesAsync(It.IsAny<List<CodeMemory>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _graphServiceMock.Setup(g => g.StoreCodeNodesAsync(It.IsAny<List<CodeMemory>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _graphServiceMock.Setup(g => g.CreateRelationshipsAsync(It.IsAny<List<CodeRelationship>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _graphServiceMock.Setup(g => g.StorePatternNodeAsync(It.IsAny<CodePattern>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _graphServiceMock.Setup(g => g.DeleteByFilePathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _vectorServiceMock.Setup(v => v.DeleteByFilePathAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        pathTranslationMock.Setup(p => p.TranslateToContainerPath(It.IsAny<string>()))
            .Returns((string path) => path);

        _indexingService = new IndexingService(
            codeParser,
            embeddingServiceMock.Object,
            _vectorServiceMock.Object,
            _graphServiceMock.Object,
            pathTranslationMock.Object,
            _semgrepService,
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<IndexingService>());
    }

    [Fact]
    public async Task IndexFileAsync_ShouldIncludeSemgrepFindings()
    {
        // Arrange
        var testFilePath = "/tmp/test_with_vulnerability.cs";
        var vulnerableCode = @"
namespace Test
{
    public class VulnerableService
    {
        public void ExecuteQuery(string userId)
        {
            string query = ""SELECT * FROM Users WHERE Id = '"" + userId + ""'"";
            database.Execute(query);
        }
    }
}";
        await File.WriteAllTextAsync(testFilePath, vulnerableCode);

        try
        {
            // Act
            var result = await _indexingService.IndexFileAsync(testFilePath, "test-context");

            // Assert
            Assert.True(result.Success);
            Assert.True(result.PatternsDetected > 0, "Should have detected security patterns from Semgrep");

            // Verify security patterns were stored
            _graphServiceMock.Verify(
                g => g.StorePatternNodeAsync(
                    It.Is<CodePattern>(p => 
                        p.Type == PatternType.Security && 
                        p.Metadata.ContainsKey("is_semgrep_finding")),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task IndexFileAsync_ShouldNotFailIfSemgrepUnavailable()
    {
        // Arrange
        var testFilePath = "/tmp/test_no_semgrep.cs";
        var code = @"
namespace Test
{
    public class SimpleClass
    {
        public void Method() { }
    }
}";
        await File.WriteAllTextAsync(testFilePath, code);

        // Create indexing service with mock Semgrep that fails
        var mockSemgrep = new Mock<ISemgrepService>();
        mockSemgrep.Setup(s => s.ScanFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Semgrep not available"));

        var codeParser = new CodeParser(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<CodeParser>());
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]> { new float[1024] });

        var pathTranslationMock = new Mock<IPathTranslationService>();
        pathTranslationMock.Setup(p => p.TranslateToContainerPath(It.IsAny<string>()))
            .Returns((string path) => path);

        var indexingService = new IndexingService(
            codeParser,
            embeddingServiceMock.Object,
            _vectorServiceMock.Object,
            _graphServiceMock.Object,
            pathTranslationMock.Object,
            mockSemgrep.Object,
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<IndexingService>());

        try
        {
            // Act
            var result = await indexingService.IndexFileAsync(testFilePath, "test-context");

            // Assert - Should still succeed even though Semgrep failed
            Assert.True(result.Success, "Indexing should succeed even if Semgrep fails");
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task IndexFileAsync_ShouldStoreSemgrepMetadata()
    {
        // Arrange
        var testFilePath = "/tmp/test_semgrep_metadata.cs";
        var vulnerableCode = @"
namespace Test
{
    public class Test
    {
        public void WeakHash()
        {
            var md5 = System.Security.Cryptography.MD5.Create();
        }
    }
}";
        await File.WriteAllTextAsync(testFilePath, vulnerableCode);

        try
        {
            // Act
            var result = await _indexingService.IndexFileAsync(testFilePath, "test-context");

            // Assert
            Assert.True(result.Success);

            // Verify metadata was stored
            _graphServiceMock.Verify(
                g => g.StorePatternNodeAsync(
                    It.Is<CodePattern>(p =>
                        p.Metadata.ContainsKey("semgrep_rule") &&
                        p.Metadata.ContainsKey("cwe") &&
                        p.Metadata.ContainsKey("severity")),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }
}

