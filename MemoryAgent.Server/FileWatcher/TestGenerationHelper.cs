namespace MemoryAgent.Server.FileWatcher;

/// <summary>
/// Helper to determine if a file needs tests and queue generation
/// </summary>
public class TestGenerationHelper
{
    private readonly ITestGenerationQueue _queue;
    private readonly ILogger<TestGenerationHelper> _logger;

    // Files to skip test generation for
    private static readonly HashSet<string> SkipFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Program.cs",
        "Startup.cs",
        "AssemblyInfo.cs",
        "GlobalUsings.cs",
        "Usings.cs"
    };

    // Patterns to skip
    private static readonly string[] SkipPatterns = new[]
    {
        "Tests.cs",      // Already a test
        "Test.cs",       // Already a test
        ".Designer.cs",  // Generated
        ".g.cs",         // Generated
        ".generated.cs", // Generated
        ".xaml.cs",      // XAML code-behind
    };

    // Directories to skip
    private static readonly string[] SkipDirectories = new[]
    {
        "/bin/",
        "/obj/",
        "/node_modules/",
        "/.git/",
        "/Migrations/",  // EF migrations
        "/Properties/",  // Assembly properties
    };

    public TestGenerationHelper(
        ITestGenerationQueue queue,
        ILogger<TestGenerationHelper> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    /// <summary>
    /// Check files after reindex and queue test generation for those without tests
    /// </summary>
    public void ProcessChangedFiles(IEnumerable<string> filePaths, string context, string workspacePath)
    {
        foreach (var filePath in filePaths)
        {
            ProcessFile(filePath, context, workspacePath);
        }
    }

    /// <summary>
    /// Check single file and queue test generation if needed
    /// </summary>
    public void ProcessFile(string filePath, string context, string workspacePath)
    {
        // Only process C# files for now
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return;

        // Normalize path
        filePath = filePath.Replace("\\", "/");

        // Check if should skip
        if (ShouldSkipFile(filePath))
        {
            _logger.LogDebug("Skipping test generation for: {File}", Path.GetFileName(filePath));
            return;
        }

        // Check if already queued
        if (_queue.IsQueued(filePath))
        {
            return;
        }

        // Determine project name and test path
        var (projectName, testFilePath) = GetTestFilePath(filePath, workspacePath);
        
        if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(testFilePath))
        {
            _logger.LogDebug("Could not determine test path for: {File}", filePath);
            return;
        }

        // Check if test file already exists
        if (File.Exists(testFilePath))
        {
            _logger.LogDebug("Tests already exist: {TestFile}", Path.GetFileName(testFilePath));
            return;
        }

        // Get class name from file
        var className = Path.GetFileNameWithoutExtension(filePath);

        // Queue test generation
        _queue.Enqueue(new TestGenerationRequest
        {
            SourceFilePath = filePath,
            Context = context,
            TestFilePath = testFilePath,
            ProjectName = projectName,
            ClassName = className,
            QueuedAt = DateTime.UtcNow
        });
    }

    private bool ShouldSkipFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        // Skip specific file names
        if (SkipFileNames.Contains(fileName))
            return true;

        // Skip patterns
        foreach (var pattern in SkipPatterns)
        {
            if (fileName.EndsWith(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Skip interfaces (I*.cs where second char is uppercase)
        if (fileName.StartsWith("I") && fileName.Length > 2 && char.IsUpper(fileName[1]))
            return true;

        // Skip directories
        foreach (var dir in SkipDirectories)
        {
            if (filePath.Contains(dir, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Skip if already in a .Tests project
        if (filePath.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains(".Tests\\", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Determine the test file path based on source file path
    /// </summary>
    /// <returns>(ProjectName, TestFilePath)</returns>
    private (string ProjectName, string TestFilePath) GetTestFilePath(string sourceFilePath, string workspacePath)
    {
        // sourceFilePath: /workspace/MyProject/Services/UserService.cs
        // workspacePath: /workspace/MyProject
        // testFilePath: /workspace/MyProject.Tests/UserServiceTests.cs

        try
        {
            // Get relative path from workspace
            var relativePath = sourceFilePath;
            if (sourceFilePath.StartsWith(workspacePath))
            {
                relativePath = sourceFilePath[workspacePath.Length..].TrimStart('/');
            }

            // Get project name from workspace path
            var projectName = Path.GetFileName(workspacePath.TrimEnd('/'));
            
            // Get class name
            var className = Path.GetFileNameWithoutExtension(sourceFilePath);
            
            // Build test file path
            // /workspace/MyProject → /workspace/MyProject.Tests/UserServiceTests.cs
            var projectDir = Path.GetDirectoryName(workspacePath.TrimEnd('/')) ?? workspacePath;
            var testProjectPath = Path.Combine(projectDir, $"{projectName}.Tests");
            var testFilePath = Path.Combine(testProjectPath, $"{className}Tests.cs");

            // Normalize
            testFilePath = testFilePath.Replace("\\", "/");

            return (projectName, testFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error determining test path for {File}", sourceFilePath);
            return ("", "");
        }
    }
}

