using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CodingOrchestrator.Server.Services;
using CodingOrchestrator.Server.Clients;
using AgentContracts.Requests;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Tests;

/// <summary>
/// Tests for ResultPersistenceService
/// </summary>
public class ResultPersistenceServiceTests
{
    private readonly Mock<IMemoryAgentClient> _mockMemoryAgent;
    private readonly Mock<ILogger<ResultPersistenceService>> _mockLogger;
    private readonly ResultPersistenceService _service;

    public ResultPersistenceServiceTests()
    {
        _mockMemoryAgent = new Mock<IMemoryAgentClient>();
        _mockLogger = new Mock<ILogger<ResultPersistenceService>>();
        _service = new ResultPersistenceService(_mockMemoryAgent.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StoreSuccessAsync_CallsMemoryAgent()
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test"
        };

        var generatedCode = new GenerateCodeResponse
        {
            Success = true,
            Explanation = "Created a new service",
            FileChanges = new List<FileChange>
            {
                new FileChange { Path = "Services/Test.cs", Content = "code", Type = FileChangeType.Created }
            }
        };

        // Act
        await _service.StoreSuccessAsync(request, generatedCode, 9, CancellationToken.None);

        // Assert
        _mockMemoryAgent.Verify(x => x.StoreQaAsync(
            It.Is<string>(t => t == "Create a service"),
            It.IsAny<string>(),
            It.Is<List<string>>(f => f.Contains("Services/Test.cs")),
            It.Is<string>(c => c == "test"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockMemoryAgent.Verify(x => x.RecordPromptFeedbackAsync(
            "coding_agent_system",
            true,
            9,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StoreSuccessAsync_MemoryAgentFails_DoesNotThrow()
    {
        // Arrange
        _mockMemoryAgent.Setup(x => x.StoreQaAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), 
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service",
            Context = "test",
            WorkspacePath = "/test"
        };

        var generatedCode = new GenerateCodeResponse
        {
            Success = true,
            FileChanges = new List<FileChange>
            {
                new FileChange { Path = "Test.cs", Content = "code", Type = FileChangeType.Created }
            }
        };

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => 
            _service.StoreSuccessAsync(request, generatedCode, 9, CancellationToken.None));
        
        Assert.Null(exception);
    }

    [Fact]
    public async Task RecordFailureAsync_CallsMemoryAgent()
    {
        // Arrange & Act
        await _service.RecordFailureAsync(5, CancellationToken.None);

        // Assert
        _mockMemoryAgent.Verify(x => x.RecordPromptFeedbackAsync(
            "coding_agent_system",
            false,
            5,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordFailureAsync_MemoryAgentFails_DoesNotThrow()
    {
        // Arrange
        _mockMemoryAgent.Setup(x => x.RecordPromptFeedbackAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act & Assert - Should not throw
        var exception = await Record.ExceptionAsync(() => 
            _service.RecordFailureAsync(5, CancellationToken.None));
        
        Assert.Null(exception);
    }

    [Fact]
    public async Task WriteFilesToWorkspaceAsync_WritesFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var files = new List<GeneratedFile>
            {
                new GeneratedFile 
                { 
                    Path = "test.txt", 
                    Content = "Hello World", 
                    ChangeType = FileChangeType.Created 
                }
            };

            // Act
            var result = await _service.WriteFilesToWorkspaceAsync(files, tempDir, CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.Equal("test.txt", result[0]);
            Assert.True(File.Exists(Path.Combine(tempDir, "test.txt")));
            Assert.Equal("Hello World", await File.ReadAllTextAsync(Path.Combine(tempDir, "test.txt")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFilesToWorkspaceAsync_CreatesSubdirectories()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var files = new List<GeneratedFile>
            {
                new GeneratedFile 
                { 
                    Path = "Services/SubDir/Test.cs", 
                    Content = "public class Test {}", 
                    ChangeType = FileChangeType.Created 
                }
            };

            // Act
            var result = await _service.WriteFilesToWorkspaceAsync(files, tempDir, CancellationToken.None);

            // Assert
            Assert.Single(result);
            var expectedPath = Path.Combine(tempDir, "Services", "SubDir", "Test.cs");
            Assert.True(File.Exists(expectedPath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFilesToWorkspaceAsync_CreatesBackupForExistingFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create existing file
            var existingFile = Path.Combine(tempDir, "existing.txt");
            await File.WriteAllTextAsync(existingFile, "Original content");

            var files = new List<GeneratedFile>
            {
                new GeneratedFile 
                { 
                    Path = "existing.txt", 
                    Content = "New content", 
                    ChangeType = FileChangeType.Created  // Trying to create over existing
                }
            };

            // Act
            await _service.WriteFilesToWorkspaceAsync(files, tempDir, CancellationToken.None);

            // Assert
            Assert.True(File.Exists(existingFile + ".backup"));
            Assert.Equal("Original content", await File.ReadAllTextAsync(existingFile + ".backup"));
            Assert.Equal("New content", await File.ReadAllTextAsync(existingFile));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFilesToWorkspaceAsync_HandlesCancellation()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var files = new List<GeneratedFile>
            {
                new GeneratedFile { Path = "test.txt", Content = "content", ChangeType = FileChangeType.Created }
            };

            // Act - The service catches cancellation per-file to allow partial writes
            // When cancelled before any file is processed, it should return empty list
            var result = await _service.WriteFilesToWorkspaceAsync(files, tempDir, cts.Token);

            // Assert - Either throws or returns empty (graceful degradation)
            // Since the service catches per-file, verify no files were written
            Assert.Empty(result);
        }
        catch (OperationCanceledException)
        {
            // This is also acceptable behavior
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFilesToWorkspaceAsync_HandlesAbsolutePaths()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}");
        var absolutePath = Path.Combine(tempDir, "absolute.txt");
        Directory.CreateDirectory(tempDir);

        try
        {
            var files = new List<GeneratedFile>
            {
                new GeneratedFile 
                { 
                    Path = absolutePath,  // Absolute path
                    Content = "Absolute content", 
                    ChangeType = FileChangeType.Created 
                }
            };

            // Act
            var result = await _service.WriteFilesToWorkspaceAsync(files, tempDir, CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.True(File.Exists(absolutePath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFilesToWorkspaceAsync_ContinuesOnError()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a readonly directory that will cause write failures
            // We'll use a valid second file to verify we continue after error
            var files = new List<GeneratedFile>
            {
                new GeneratedFile 
                { 
                    // Invalid path characters will cause failure on Windows
                    Path = "invalid\0path.txt", 
                    Content = "content", 
                    ChangeType = FileChangeType.Created 
                },
                new GeneratedFile 
                { 
                    Path = "valid.txt", 
                    Content = "valid content", 
                    ChangeType = FileChangeType.Created 
                }
            };

            // Act
            var result = await _service.WriteFilesToWorkspaceAsync(files, tempDir, CancellationToken.None);

            // Assert - Should have written at least the valid file
            Assert.Contains("valid.txt", result);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}

