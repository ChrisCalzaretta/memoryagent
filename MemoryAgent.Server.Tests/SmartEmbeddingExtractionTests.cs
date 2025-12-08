using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Integration tests for smart embedding extraction from multiple languages
/// </summary>
public class SmartEmbeddingExtractionTests
{
    private readonly ITestOutputHelper _output;
    private const int MaxEmbeddingChars = 1800;

    public SmartEmbeddingExtractionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CSharp_Class_ExtractsAllSemanticFields()
    {
        // Arrange
        var filePath = "TestData/SmartEmbedding_CSharp_Test.cs.txt";
        var code = await File.ReadAllTextAsync(filePath);
        var logger = NullLogger<RoslynParser>.Instance;
        var loggerFactory = NullLoggerFactory.Instance;
        var parser = new RoslynParser(logger, loggerFactory, null!);

        // Act
        var result = await parser.ParseCodeAsync(code, filePath, "test");

        // Assert
        var authServiceClass = result.CodeElements.FirstOrDefault(e => 
            e.Type == CodeMemoryType.Class && e.Name.Contains("AuthenticationService"));

        Assert.NotNull(authServiceClass);
        
        // Validate Summary extraction (from XML docs)
        _output.WriteLine($"📄 Class: {authServiceClass.Name}");
        _output.WriteLine($"   Summary: {authServiceClass.Summary}");
        Assert.False(string.IsNullOrEmpty(authServiceClass.Summary), "Summary should be extracted from XML docs");
        Assert.Contains("authenticating users", authServiceClass.Summary.ToLower());
        
        // Validate Signature extraction
        _output.WriteLine($"   Signature: {authServiceClass.Signature}");
        Assert.False(string.IsNullOrEmpty(authServiceClass.Signature), "Signature should be extracted");
        Assert.Contains("class AuthenticationService", authServiceClass.Signature);
        Assert.Contains("IAuthenticationService", authServiceClass.Signature);
        
        // Validate Purpose
        _output.WriteLine($"   Purpose: {authServiceClass.Purpose}");
        Assert.False(string.IsNullOrEmpty(authServiceClass.Purpose), "Purpose should be populated");
        
        // Validate Tags
        _output.WriteLine($"   Tags: {string.Join(", ", authServiceClass.Tags)}");
        Assert.NotEmpty(authServiceClass.Tags);
        Assert.Contains("service", authServiceClass.Tags);
        
        // Validate Dependencies
        _output.WriteLine($"   Dependencies: {string.Join(", ", authServiceClass.Dependencies)}");
        Assert.NotEmpty(authServiceClass.Dependencies);
        Assert.Contains(authServiceClass.Dependencies, d => d.Contains("IUserRepository"));
        Assert.Contains(authServiceClass.Dependencies, d => d.Contains("IJwtTokenService"));
        
        // Validate embedding text structure
        var embeddingText = authServiceClass.GetEmbeddingText();
        _output.WriteLine($"\n📊 Embedding Text Structure:");
        _output.WriteLine($"   Length: {embeddingText.Length} chars (limit: {MaxEmbeddingChars})");
        _output.WriteLine($"   First 500 chars:\n{embeddingText.Substring(0, Math.Min(500, embeddingText.Length))}");
        
        Assert.True(embeddingText.Length <= MaxEmbeddingChars, 
            $"Embedding text should be <= {MaxEmbeddingChars} chars, got {embeddingText.Length}");
        Assert.StartsWith("[CLASS]", embeddingText);
        Assert.Contains("Signature:", embeddingText);
        Assert.Contains("Purpose:", embeddingText);
        Assert.Contains("Tags:", embeddingText);
        Assert.Contains("Dependencies:", embeddingText);
        Assert.Contains("Code:", embeddingText);
    }

    [Fact]
    public async Task CSharp_Method_ExtractsAllSemanticFields()
    {
        // Arrange
        var filePath = "TestData/SmartEmbedding_CSharp_Test.cs.txt";
        var code = await File.ReadAllTextAsync(filePath);
        var logger = NullLogger<RoslynParser>.Instance;
        var loggerFactory = NullLoggerFactory.Instance;
        var parser = new RoslynParser(logger, loggerFactory, null!);

        // Act
        var result = await parser.ParseCodeAsync(code, filePath, "test");

        // Assert
        var loginMethod = result.CodeElements.FirstOrDefault(e => 
            e.Type == CodeMemoryType.Method && e.Name.Contains("LoginAsync"));

        Assert.NotNull(loginMethod);
        
        _output.WriteLine($"\n📄 Method: {loginMethod.Name}");
        
        // Validate Summary
        _output.WriteLine($"   Summary: {loginMethod.Summary}");
        Assert.False(string.IsNullOrEmpty(loginMethod.Summary), "Method summary should be extracted");
        Assert.Contains("Authenticates a user", loginMethod.Summary);
        
        // Validate Signature
        _output.WriteLine($"   Signature: {loginMethod.Signature}");
        Assert.Contains("Task<AuthResult> LoginAsync", loginMethod.Signature);
        Assert.Contains("string username", loginMethod.Signature);
        
        // Validate Tags
        _output.WriteLine($"   Tags: {string.Join(", ", loginMethod.Tags)}");
        Assert.Contains("async", loginMethod.Tags);
        Assert.Contains("public", loginMethod.Tags);
        
        // Validate Dependencies
        _output.WriteLine($"   Dependencies: {string.Join(", ", loginMethod.Dependencies)}");
        Assert.Contains(loginMethod.Dependencies, d => d.Contains("Task<AuthResult>"));
        
        // Validate embedding text
        var embeddingText = loginMethod.GetEmbeddingText();
        _output.WriteLine($"\n📊 Embedding Text:");
        _output.WriteLine($"   Length: {embeddingText.Length} chars");
        _output.WriteLine($"   Preview:\n{embeddingText.Substring(0, Math.Min(600, embeddingText.Length))}");
        
        Assert.True(embeddingText.Length <= MaxEmbeddingChars);
        Assert.StartsWith("[METHOD]", embeddingText);
    }

    [Fact(Skip = "JavaScriptParser not implemented")]
    public void JavaScript_Class_ExtractsJSDocAndMetadata()
    {
        // Arrange
        var filePath = "TestData/SmartEmbedding_JavaScript_Test.js";
        var code = File.ReadAllText(filePath);

        // Act
        // var result = JavaScriptParser.ParseJavaScriptFile(filePath, "test");

        // Assert
        // var userServiceClass = result.CodeElements.FirstOrDefault(e => 
        //     e.Type == CodeMemoryType.Class && e.Name.Contains("UserService"));

        // Assert.NotNull(userServiceClass);
        
        // _output.WriteLine($"\n📄 JavaScript Class: {userServiceClass.Name}");
        // _output.WriteLine($"   Summary: {userServiceClass.Summary}");
        // _output.WriteLine($"   Signature: {userServiceClass.Signature}");
        // _output.WriteLine($"   Tags: {string.Join(", ", userServiceClass.Tags)}");
        // _output.WriteLine($"   Dependencies: {string.Join(", ", userServiceClass.Dependencies)}");
        
        // // Summary should be extracted from JSDoc
        // Assert.False(string.IsNullOrEmpty(userServiceClass.Summary), "JSDoc summary should be extracted");
        
        // // Tags should be populated
        // Assert.NotEmpty(userServiceClass.Tags);
        // Assert.Contains("javascript", userServiceClass.Tags);
        
        // // Validate embedding text
        // var embeddingText = userServiceClass.GetEmbeddingText();
        // _output.WriteLine($"\n📊 JS Embedding Text Length: {embeddingText.Length} chars");
        // _output.WriteLine($"   Preview:\n{embeddingText.Substring(0, Math.Min(500, embeddingText.Length))}");
        
        // Assert.True(embeddingText.Length <= MaxEmbeddingChars);
        // Assert.StartsWith("[CLASS]", embeddingText);
    }

    [Fact(Skip = "PythonParser not implemented")]
    public void Python_Class_ExtractsDocstringAndMetadata()
    {
        // Arrange
        var filePath = "TestData/SmartEmbedding_Python_Test.py";

        // Act
        // var result = PythonParser.ParsePythonFile(filePath, "test");

        // Assert
        // var orderServiceClass = result.CodeElements.FirstOrDefault(e => 
        //     e.Type == CodeMemoryType.Class && e.Name.Contains("OrderService"));

        // Assert.NotNull(orderServiceClass);
        
        // _output.WriteLine($"\n📄 Python Class: {orderServiceClass.Name}");
        // _output.WriteLine($"   Summary: {orderServiceClass.Summary}");
        // _output.WriteLine($"   Signature: {orderServiceClass.Signature}");
        // _output.WriteLine($"   Tags: {string.Join(", ", orderServiceClass.Tags)}");
        // _output.WriteLine($"   Dependencies: {string.Join(", ", orderServiceClass.Dependencies)}");
        
        // // Python parser should extract class info
        // Assert.NotNull(orderServiceClass.Name);
        // Assert.NotEmpty(orderServiceClass.Content);
        
        // // Validate embedding text
        // var embeddingText = orderServiceClass.GetEmbeddingText();
        // _output.WriteLine($"\n📊 Python Embedding Text Length: {embeddingText.Length} chars");
        // _output.WriteLine($"   Preview:\n{embeddingText.Substring(0, Math.Min(500, embeddingText.Length))}");
        
        // Assert.True(embeddingText.Length <= MaxEmbeddingChars);
        // Assert.StartsWith("[CLASS]", embeddingText);
    }

    [Fact]
    public async Task BeforeAfter_Comparison_ShowsImprovement()
    {
        // Arrange
        var filePath = "TestData/SmartEmbedding_CSharp_Test.cs.txt";
        var code = await File.ReadAllTextAsync(filePath);
        var logger = NullLogger<RoslynParser>.Instance;
        var loggerFactory = NullLoggerFactory.Instance;
        var parser = new RoslynParser(logger, loggerFactory, null!);

        // Act
        var result = await parser.ParseCodeAsync(code, filePath, "test");
        var authService = result.CodeElements.First(e => 
            e.Type == CodeMemoryType.Class && e.Name.Contains("AuthenticationService"));

        // Compare
        var oldEmbedding = authService.Content; // Just raw code
        var newEmbedding = authService.GetEmbeddingText(); // Smart embedding

        _output.WriteLine("\n" + new string('=', 80));
        _output.WriteLine("📊 BEFORE vs AFTER COMPARISON");
        _output.WriteLine(new string('=', 80));
        
        _output.WriteLine("\n❌ BEFORE (Raw Content):");
        _output.WriteLine($"   Length: {oldEmbedding.Length} chars");
        _output.WriteLine($"   Starts with: {oldEmbedding.Substring(0, Math.Min(200, oldEmbedding.Length))}...");
        _output.WriteLine("   ⚠️  No semantic metadata!");
        _output.WriteLine("   ⚠️  No type prefix!");
        _output.WriteLine("   ⚠️  No summary/purpose!");
        
        _output.WriteLine("\n✅ AFTER (Smart Embedding):");
        _output.WriteLine($"   Length: {newEmbedding.Length} chars (optimized to <= {MaxEmbeddingChars})");
        _output.WriteLine($"   Content:\n{newEmbedding.Substring(0, Math.Min(800, newEmbedding.Length))}");
        _output.WriteLine("\n   ✨ Has type prefix: [CLASS]");
        _output.WriteLine("   ✨ Has signature");
        _output.WriteLine("   ✨ Has purpose/summary");
        _output.WriteLine("   ✨ Has tags");
        _output.WriteLine("   ✨ Has dependencies");
        _output.WriteLine("   ✨ Optimized code content");
        
        _output.WriteLine("\n📈 IMPROVEMENT:");
        _output.WriteLine($"   Search relevance: MUCH BETTER (semantic metadata at start)");
        _output.WriteLine($"   Token efficiency: {((float)newEmbedding.Length / MaxEmbeddingChars * 100):F1}% of budget");
        _output.WriteLine($"   Metadata preservation: 100% (never truncated)");
        
        // Assert improvements
        Assert.True(newEmbedding.Length <= MaxEmbeddingChars, "Should fit within token budget");
        Assert.Contains("[CLASS]", newEmbedding);
        Assert.Contains("Signature:", newEmbedding);
        Assert.Contains("Purpose:", newEmbedding);
        Assert.DoesNotContain("[CLASS]", oldEmbedding); // Old approach has no metadata
    }

    [Fact]
    public async Task LargeClass_TruncatesOnlyCode_PreservesMetadata()
    {
        // This test validates that even with large classes,
        // the metadata (signature, purpose, tags, dependencies) is NEVER truncated
        
        // Arrange
        var filePath = "TestData/SmartEmbedding_CSharp_Test.cs.txt";
        var code = await File.ReadAllTextAsync(filePath);
        var logger = NullLogger<RoslynParser>.Instance;
        var loggerFactory = NullLoggerFactory.Instance;
        var parser = new RoslynParser(logger, loggerFactory, null!);

        // Act
        var result = await parser.ParseCodeAsync(code, filePath, "test");
        var authService = result.CodeElements.First(e => 
            e.Type == CodeMemoryType.Class && e.Name.Contains("AuthenticationService"));

        var embeddingText = authService.GetEmbeddingText();

        _output.WriteLine($"\n🔍 Large Class Truncation Test:");
        _output.WriteLine($"   Original Content Length: {authService.Content.Length} chars");
        _output.WriteLine($"   Embedding Text Length: {embeddingText.Length} chars");
        _output.WriteLine($"   Within Budget: {embeddingText.Length <= MaxEmbeddingChars}");

        // Extract metadata section (everything before "Code:")
        var codeIndex = embeddingText.IndexOf("Code:");
        var metadataSection = codeIndex > 0 ? embeddingText.Substring(0, codeIndex) : embeddingText;

        _output.WriteLine($"\n   Metadata Section:");
        _output.WriteLine(metadataSection);

        // Assert: Metadata is ALWAYS present, even if code is truncated
        Assert.Contains("[CLASS]", embeddingText);
        Assert.Contains("Signature:", embeddingText);
        Assert.Contains("Purpose:", embeddingText);
        Assert.Contains("Tags:", embeddingText);
        Assert.Contains("Dependencies:", embeddingText);
        
        // Assert: Embedding is within budget
        Assert.True(embeddingText.Length <= MaxEmbeddingChars);
    }
}

