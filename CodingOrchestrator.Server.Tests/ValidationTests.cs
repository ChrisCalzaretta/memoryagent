using System.ComponentModel.DataAnnotations;
using AgentContracts.Requests;
using Xunit;

namespace CodingOrchestrator.Server.Tests;

/// <summary>
/// Tests for DTO input validation
/// </summary>
public class ValidationTests
{
    #region OrchestrateTaskRequest Tests
    
    [Fact]
    public void OrchestrateTaskRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a simple hello world service",
            Context = "test-project",
            WorkspacePath = "E:/Projects/TestProject",
            Language = "csharp",
            MaxIterations = 5,
            MinValidationScore = 8
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void OrchestrateTaskRequest_EmptyTask_FailsValidation()
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Task"));
    }

    [Fact]
    public void OrchestrateTaskRequest_TaskTooShort_FailsValidation()
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Hi",  // Too short, min 10 chars
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Task"));
    }

    [Theory]
    [InlineData("python")]
    [InlineData("csharp")]
    [InlineData("typescript")]
    [InlineData("javascript")]
    [InlineData("go")]
    [InlineData("rust")]
    [InlineData("java")]
    [InlineData("dart")]
    [InlineData(null)]  // Null is valid (auto-detect)
    [InlineData("")]    // Empty is valid (auto-detect)
    public void OrchestrateTaskRequest_ValidLanguage_PassesValidation(string? language)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = "test",
            WorkspacePath = "/test",
            Language = language
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => r.MemberNames.Contains("Language"));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("c++")]
    [InlineData("CSHARP")]  // Case sensitive
    [InlineData("Python")]  // Case sensitive
    public void OrchestrateTaskRequest_InvalidLanguage_FailsValidation(string language)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = "test",
            WorkspacePath = "/test",
            Language = language
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Language"));
    }

    [Theory]
    [InlineData(0)]   // Too low
    [InlineData(-1)]  // Negative
    [InlineData(51)]  // Too high
    [InlineData(100)] // Way too high
    public void OrchestrateTaskRequest_InvalidMaxIterations_FailsValidation(int iterations)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = iterations
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("MaxIterations"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    public void OrchestrateTaskRequest_ValidMaxIterations_PassesValidation(int iterations)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = "test",
            WorkspacePath = "/test",
            MaxIterations = iterations
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => r.MemberNames.Contains("MaxIterations"));
    }

    [Theory]
    [InlineData(-1)]  // Too low
    [InlineData(11)]  // Too high
    public void OrchestrateTaskRequest_InvalidMinValidationScore_FailsValidation(int score)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = "test",
            WorkspacePath = "/test",
            MinValidationScore = score
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("MinValidationScore"));
    }

    [Fact]
    public void OrchestrateTaskRequest_PathTraversal_FailsValidation()
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = "test",
            WorkspacePath = "../../../etc/passwd"  // Path traversal attack
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("WorkspacePath"));
    }

    [Fact]
    public void OrchestrateTaskRequest_TildeInPath_FailsValidation()
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = "test",
            WorkspacePath = "~/secrets"  // Tilde expansion
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("WorkspacePath"));
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>")]
    [InlineData("vbscript:msgbox(1)")]
    public void OrchestrateTaskRequest_DangerousTaskContent_FailsValidation(string dangerousContent)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = $"Create a service with {dangerousContent}",
            Context = "test",
            WorkspacePath = "/test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Task"));
    }

    [Theory]
    [InlineData("valid-context")]
    [InlineData("valid_context")]
    [InlineData("valid.context")]
    [InlineData("ValidContext123")]
    public void OrchestrateTaskRequest_ValidContext_PassesValidation(string context)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = context,
            WorkspacePath = "/test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => r.MemberNames.Contains("Context"));
    }

    [Theory]
    [InlineData("invalid context")]  // Space
    [InlineData("invalid/context")]  // Slash
    [InlineData("invalid\\context")] // Backslash
    [InlineData("invalid:context")]  // Colon
    public void OrchestrateTaskRequest_InvalidContext_FailsValidation(string context)
    {
        // Arrange
        var request = new OrchestrateTaskRequest
        {
            Task = "Create a service that does something useful",
            Context = context,
            WorkspacePath = "/test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Context"));
    }

    #endregion

    #region GenerateCodeRequest Tests

    [Fact]
    public void GenerateCodeRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Create a simple service",
            WorkspacePath = "/test/project"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void GenerateCodeRequest_PathTraversalInWorkspace_FailsValidation()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Create a service",
            WorkspacePath = "../../etc"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("WorkspacePath"));
    }

    [Fact]
    public void GenerateCodeRequest_PathTraversalInTargetFiles_FailsValidation()
    {
        // Arrange
        var request = new GenerateCodeRequest
        {
            Task = "Create a service",
            WorkspacePath = "/test",
            TargetFiles = new List<string> { "Services/Test.cs", "../../../etc/passwd" }
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("TargetFiles"));
    }

    #endregion

    #region ValidateCodeRequest Tests

    [Fact]
    public void ValidateCodeRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Files = new List<CodeFile>
            {
                new CodeFile { Path = "Services/Test.cs", Content = "public class Test {}" }
            },
            Context = "test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ValidateCodeRequest_EmptyFiles_FailsValidation()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Files = new List<CodeFile>(),  // Empty list
            Context = "test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Files"));
    }

    [Fact]
    public void ValidateCodeRequest_TooManyFiles_FailsValidation()
    {
        // Arrange
        var files = Enumerable.Range(1, 51)
            .Select(i => new CodeFile { Path = $"File{i}.cs", Content = "content" })
            .ToList();

        var request = new ValidateCodeRequest
        {
            Files = files,  // 51 files, max is 50
            Context = "test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Files"));
    }

    [Fact]
    public void ValidateCodeRequest_ContentTooLarge_FailsValidation()
    {
        // Arrange
        var largeContent = new string('x', 11_000_000);  // 11MB, max is 10MB

        var request = new ValidateCodeRequest
        {
            Files = new List<CodeFile>
            {
                new CodeFile { Path = "LargeFile.cs", Content = largeContent }
            },
            Context = "test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Files"));
    }

    [Fact]
    public void ValidateCodeRequest_PathTraversalInFiles_FailsValidation()
    {
        // Arrange
        var request = new ValidateCodeRequest
        {
            Files = new List<CodeFile>
            {
                new CodeFile { Path = "../../../etc/passwd", Content = "content" }
            },
            Context = "test"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Files"));
    }

    #endregion

    #region EstimateComplexityRequest Tests

    [Fact]
    public void EstimateComplexityRequest_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new EstimateComplexityRequest
        {
            Task = "Create a complex microservice",
            Language = "csharp",
            Context = "test-project"
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void EstimateComplexityRequest_TaskTooShort_FailsValidation()
    {
        // Arrange
        var request = new EstimateComplexityRequest
        {
            Task = "Hi"  // Too short, min 5 chars
        };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains("Task"));
    }

    #endregion

    #region Helper Methods

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, validationResults, true);
        return validationResults;
    }

    #endregion
}

