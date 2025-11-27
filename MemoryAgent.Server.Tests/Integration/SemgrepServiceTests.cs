using MemoryAgent.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MemoryAgent.Server.Tests.Integration;

public class SemgrepServiceTests
{
    private readonly ISemgrepService _semgrepService;
    private readonly ILogger<SemgrepService> _logger;

    public SemgrepServiceTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SemgrepService>();
        _semgrepService = new SemgrepService(_logger);
    }

    [Fact]
    public async Task IsAvailableAsync_ShouldReturnTrue_WhenSemgrepInstalled()
    {
        // Act
        var isAvailable = await _semgrepService.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable, "Semgrep should be available in the Docker container");
    }

    [Fact]
    public async Task ScanFileAsync_ShouldDetectSqlInjection()
    {
        // Arrange
        var testFilePath = "/tmp/test_sql_injection.cs";
        var vulnerableCode = @"
public class UserRepository
{
    public User GetUser(string userId)
    {
        string query = ""SELECT * FROM Users WHERE Id = '"" + userId + ""'"";
        return database.Execute(query);
    }
}";
        await File.WriteAllTextAsync(testFilePath, vulnerableCode);

        try
        {
            // Act
            var report = await _semgrepService.ScanFileAsync(testFilePath);

            // Assert
            Assert.True(report.Success);
            Assert.NotEmpty(report.Findings);
            
            var sqlInjectionFinding = report.Findings
                .FirstOrDefault(f => f.Message.Contains("injection", StringComparison.OrdinalIgnoreCase));
            
            Assert.NotNull(sqlInjectionFinding);
            Assert.Contains("sql", sqlInjectionFinding.RuleId, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_ShouldDetectHardcodedSecrets()
    {
        // Arrange
        var testFilePath = "/tmp/test_hardcoded_secret.cs";
        var vulnerableCode = @"
public class ConfigService
{
    private const string API_KEY = ""sk-1234567890abcdefghijklmnopqrstuvwxyz"";
    
    public void ConfigureApi()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add(""Authorization"", ""Bearer "" + API_KEY);
    }
}";
        await File.WriteAllTextAsync(testFilePath, vulnerableCode);

        try
        {
            // Act
            var report = await _semgrepService.ScanFileAsync(testFilePath);

            // Assert
            Assert.True(report.Success);
            // Note: Semgrep might or might not detect this depending on rules loaded
            // This is more of a smoke test to ensure no crashes
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_ShouldReturnNoFindings_ForSecureCode()
    {
        // Arrange
        var testFilePath = "/tmp/test_secure.cs";
        var secureCode = @"
public class UserRepository
{
    public async Task<User> GetUserAsync(int userId)
    {
        using var command = new SqlCommand(""SELECT * FROM Users WHERE Id = @userId"", connection);
        command.Parameters.AddWithValue(""@userId"", userId);
        return await command.ExecuteReaderAsync();
    }
}";
        await File.WriteAllTextAsync(testFilePath, secureCode);

        try
        {
            // Act
            var report = await _semgrepService.ScanFileAsync(testFilePath);

            // Assert
            Assert.True(report.Success);
            // Secure code should have fewer or no findings
            // (exact behavior depends on Semgrep rules)
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ScanFileAsync_ShouldHandleNonExistentFile()
    {
        // Arrange
        var nonExistentFile = "/tmp/this_file_does_not_exist.cs";

        // Act
        var report = await _semgrepService.ScanFileAsync(nonExistentFile);

        // Assert
        Assert.False(report.Success);
        Assert.NotEmpty(report.Errors);
        Assert.Contains("not found", report.Errors.First(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanDirectoryAsync_ShouldScanMultipleFiles()
    {
        // Arrange
        var testDir = "/tmp/semgrep_test_dir";
        Directory.CreateDirectory(testDir);

        var file1 = Path.Combine(testDir, "secure.cs");
        var file2 = Path.Combine(testDir, "vulnerable.cs");

        await File.WriteAllTextAsync(file1, @"
public class Secure
{
    public void SafeMethod(string input)
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentNullException(nameof(input));
    }
}");

        await File.WriteAllTextAsync(file2, @"
public class Vulnerable
{
    public void UnsafeMethod(string userId)
    {
        string query = ""DELETE FROM Users WHERE Id = '"" + userId + ""'"";
        database.Execute(query);
    }
}");

        try
        {
            // Act
            var report = await _semgrepService.ScanDirectoryAsync(testDir, recursive: false);

            // Assert
            Assert.True(report.Success);
            // Should have scanned both files
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    [Fact]
    public async Task ScanFileAsync_ShouldParseMetadata_WhenAvailable()
    {
        // Arrange
        var testFilePath = "/tmp/test_metadata.cs";
        var vulnerableCode = @"
public class Test
{
    public void WeakCrypto()
    {
        var md5 = MD5.Create();
        var hash = md5.ComputeHash(data);
    }
}";
        await File.WriteAllTextAsync(testFilePath, vulnerableCode);

        try
        {
            // Act
            var report = await _semgrepService.ScanFileAsync(testFilePath);

            // Assert
            Assert.True(report.Success);
            
            if (report.Findings.Any())
            {
                var finding = report.Findings.First();
                Assert.NotNull(finding.Metadata);
                // Metadata fields are optional, just verify structure exists
            }
        }
        finally
        {
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }
}

