using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using CodingOrchestrator.Server.Services;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Tests;

/// <summary>
/// Tests for FileAccumulatorService
/// </summary>
public class FileAccumulatorServiceTests
{
    private readonly Mock<ILogger<FileAccumulatorService>> _mockLogger;
    private readonly FileAccumulatorService _service;

    public FileAccumulatorServiceTests()
    {
        _mockLogger = new Mock<ILogger<FileAccumulatorService>>();
        _service = new FileAccumulatorService(_mockLogger.Object);
    }

    [Fact]
    public void AccumulateFiles_SingleFile_AddsToCollection()
    {
        // Arrange
        var files = new List<FileChange>
        {
            new FileChange 
            { 
                Path = "Services/Test.cs", 
                Content = "public class Test {}", 
                Type = FileChangeType.Created 
            }
        };

        // Act
        _service.AccumulateFiles(files);

        // Assert
        Assert.Equal(1, _service.Count);
        var accumulated = _service.GetAccumulatedFiles();
        Assert.True(accumulated.ContainsKey("Services/Test.cs"));
    }

    [Fact]
    public void AccumulateFiles_MultipleFiles_AddsAllToCollection()
    {
        // Arrange
        var files = new List<FileChange>
        {
            new FileChange { Path = "File1.cs", Content = "content1", Type = FileChangeType.Created },
            new FileChange { Path = "File2.cs", Content = "content2", Type = FileChangeType.Created },
            new FileChange { Path = "File3.cs", Content = "content3", Type = FileChangeType.Created }
        };

        // Act
        _service.AccumulateFiles(files);

        // Assert
        Assert.Equal(3, _service.Count);
    }

    [Fact]
    public void AccumulateFiles_DuplicatePath_UpdatesExisting()
    {
        // Arrange
        var firstBatch = new List<FileChange>
        {
            new FileChange { Path = "Services/Test.cs", Content = "version1", Type = FileChangeType.Created }
        };
        var secondBatch = new List<FileChange>
        {
            new FileChange { Path = "Services/Test.cs", Content = "version2", Type = FileChangeType.Modified }
        };

        // Act
        _service.AccumulateFiles(firstBatch);
        _service.AccumulateFiles(secondBatch);

        // Assert
        Assert.Equal(1, _service.Count);  // Still only 1 file
        var accumulated = _service.GetAccumulatedFiles();
        Assert.Equal("version2", accumulated["Services/Test.cs"].Content);
        Assert.Equal(FileChangeType.Modified, accumulated["Services/Test.cs"].Type);
    }

    [Fact]
    public void AccumulateFiles_MultipleBatches_AccumulatesAcrossIterations()
    {
        // Arrange - Simulating multiple coding iterations
        var iteration1 = new List<FileChange>
        {
            new FileChange { Path = "Models/User.cs", Content = "model1", Type = FileChangeType.Created }
        };
        var iteration2 = new List<FileChange>
        {
            new FileChange { Path = "Services/UserService.cs", Content = "service1", Type = FileChangeType.Created }
        };
        var iteration3 = new List<FileChange>
        {
            new FileChange { Path = "Controllers/UserController.cs", Content = "controller1", Type = FileChangeType.Created },
            new FileChange { Path = "Services/UserService.cs", Content = "service2-fixed", Type = FileChangeType.Modified }
        };

        // Act
        _service.AccumulateFiles(iteration1);
        _service.AccumulateFiles(iteration2);
        _service.AccumulateFiles(iteration3);

        // Assert
        Assert.Equal(3, _service.Count);
        var accumulated = _service.GetAccumulatedFiles();
        Assert.Equal("service2-fixed", accumulated["Services/UserService.cs"].Content);
    }

    [Fact]
    public void GetExecutionFiles_ReturnsCorrectFormat()
    {
        // Arrange
        var files = new List<FileChange>
        {
            new FileChange 
            { 
                Path = "main.py", 
                Content = "print('hello')", 
                Type = FileChangeType.Created,
                Reason = "Main file" 
            }
        };
        _service.AccumulateFiles(files);

        // Act
        var executionFiles = _service.GetExecutionFiles();

        // Assert
        Assert.Single(executionFiles);
        Assert.Equal("main.py", executionFiles[0].Path);
        Assert.Equal("print('hello')", executionFiles[0].Content);
        Assert.Equal((int)FileChangeType.Created, executionFiles[0].ChangeType);
        Assert.Equal("Main file", executionFiles[0].Reason);
    }

    [Fact]
    public void GetGeneratedFiles_ReturnsCorrectFormat()
    {
        // Arrange
        var files = new List<FileChange>
        {
            new FileChange 
            { 
                Path = "Services/Test.cs", 
                Content = "public class Test {}", 
                Type = FileChangeType.Created,
                Reason = "New service" 
            }
        };
        _service.AccumulateFiles(files);

        // Act
        var generatedFiles = _service.GetGeneratedFiles();

        // Assert
        Assert.Single(generatedFiles);
        Assert.Equal("Services/Test.cs", generatedFiles[0].Path);
        Assert.Equal("public class Test {}", generatedFiles[0].Content);
        Assert.Equal(FileChangeType.Created, generatedFiles[0].ChangeType);
        Assert.Equal("New service", generatedFiles[0].Reason);
    }

    [Fact]
    public void GetFileSummary_NoFiles_ReturnsNoFilesMessage()
    {
        // Act
        var summary = _service.GetFileSummary();

        // Assert
        Assert.Equal("No files generated", summary);
    }

    [Fact]
    public void GetFileSummary_WithFiles_ReturnsFormattedList()
    {
        // Arrange
        var files = new List<FileChange>
        {
            new FileChange { Path = "File1.cs", Content = "", Type = FileChangeType.Created },
            new FileChange { Path = "File2.cs", Content = "", Type = FileChangeType.Created }
        };
        _service.AccumulateFiles(files);

        // Act
        var summary = _service.GetFileSummary();

        // Assert
        Assert.Contains("FILES ALREADY GENERATED (2)", summary);
        Assert.Contains("- File1.cs", summary);
        Assert.Contains("- File2.cs", summary);
        Assert.Contains("Generate the MISSING files", summary);
    }

    [Fact]
    public void Clear_RemovesAllFiles()
    {
        // Arrange
        var files = new List<FileChange>
        {
            new FileChange { Path = "File1.cs", Content = "", Type = FileChangeType.Created },
            new FileChange { Path = "File2.cs", Content = "", Type = FileChangeType.Created }
        };
        _service.AccumulateFiles(files);
        Assert.Equal(2, _service.Count);

        // Act
        _service.Clear();

        // Assert
        Assert.Equal(0, _service.Count);
        Assert.Empty(_service.GetAccumulatedFiles());
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        // Arrange & Act
        Assert.Equal(0, _service.Count);

        _service.AccumulateFiles(new List<FileChange>
        {
            new FileChange { Path = "File1.cs", Content = "", Type = FileChangeType.Created }
        });
        Assert.Equal(1, _service.Count);

        _service.AccumulateFiles(new List<FileChange>
        {
            new FileChange { Path = "File2.cs", Content = "", Type = FileChangeType.Created },
            new FileChange { Path = "File3.cs", Content = "", Type = FileChangeType.Created }
        });
        Assert.Equal(3, _service.Count);
    }

    [Fact]
    public void GetAccumulatedFiles_ReturnsReadOnlyDictionary()
    {
        // Arrange
        var files = new List<FileChange>
        {
            new FileChange { Path = "File1.cs", Content = "", Type = FileChangeType.Created }
        };
        _service.AccumulateFiles(files);

        // Act
        var accumulated = _service.GetAccumulatedFiles();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, FileChange>>(accumulated);
    }
}

