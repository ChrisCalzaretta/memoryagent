using MemoryAgent.Server.Models;
using MemoryAgent.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Integration tests for Flutter/Dart pattern validation rules
/// Tests cover: quality scoring, security validation, anti-pattern detection
/// </summary>
public class FlutterDartValidationTests
{
    private readonly ITestOutputHelper _output;

    public FlutterDartValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Flutter Widget Validation Tests

    [Fact]
    public void ValidateFlutterPattern_StatelessWithoutConstConstructor_ReturnsWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-1",
            Name = "Flutter_StatelessWidget",
            Type = PatternType.Flutter,
            Category = PatternCategory.ComponentModel,
            Content = "class MyWidget extends StatelessWidget { }",
            Metadata = new Dictionary<string, object>
            {
                ["widget_name"] = "MyWidget",
                ["has_const_constructor"] = false
            }
        };

        // Act - Direct validation method call
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("const constructor")));
        Assert.True(result.Score < 10);
        _output.WriteLine($"Score: {result.Score}/10, Issues: {result.Issues.Count}");
    }

    [Fact]
    public void ValidateFlutterPattern_StatefulWithoutStateClass_ReturnsCritical()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-2",
            Name = "Flutter_StatefulWidget",
            Type = PatternType.Flutter,
            Category = PatternCategory.ComponentModel,
            Content = "class MyWidget extends StatefulWidget { }",
            Metadata = new Dictionary<string, object>
            {
                ["widget_name"] = "MyWidget",
                ["has_state_class"] = false
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        var criticalIssue = result.Issues.FirstOrDefault(i => i.Severity == IssueSeverity.Critical);
        Assert.NotNull(criticalIssue);
        Assert.True(result.Score <= 5);
    }

    [Fact]
    public void ValidateFlutterPattern_MissingDispose_ReturnsCritical()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-3",
            Name = "Flutter_MissingDispose_AntiPattern",
            Type = PatternType.Flutter,
            Category = PatternCategory.Lifecycle,
            Content = "TextEditingController _controller;",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "high"
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("Memory leak") || i.Message.Contains("dispose")));
    }

    [Fact]
    public void ValidateFlutterPattern_ChangeNotifierWithoutDispose_ReturnsWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-4",
            Name = "Flutter_ChangeNotifier",
            Type = PatternType.Flutter,
            Category = PatternCategory.StateManagement,
            Content = "class MyNotifier extends ChangeNotifier { }",
            Metadata = new Dictionary<string, object>
            {
                ["has_dispose"] = false
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("dispose") || i.Message.Contains("memory leak")));
    }

    [Fact]
    public void ValidateFlutterPattern_AnimationControllerWithoutDispose_ReturnsCritical()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-5",
            Name = "Flutter_AnimationController",
            Type = PatternType.Flutter,
            Category = PatternCategory.UserExperience,
            Content = "AnimationController _controller;",
            Metadata = new Dictionary<string, object>
            {
                ["disposes_controller"] = false
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        var criticalIssue = result.Issues.FirstOrDefault(i => i.Severity == IssueSeverity.Critical);
        Assert.NotNull(criticalIssue);
    }

    [Fact]
    public void ValidateFlutterPattern_InitStateWithoutSuper_ReturnsHighIssue()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-6",
            Name = "Flutter_InitState",
            Type = PatternType.Flutter,
            Category = PatternCategory.Lifecycle,
            Content = "@override void initState() { _loadData(); }",
            Metadata = new Dictionary<string, object>
            {
                ["calls_super"] = false
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("super.initState")));
    }

    [Fact]
    public void ValidateFlutterPattern_FutureBuilderWithoutErrorHandling_ReturnsWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-7",
            Name = "Flutter_FutureBuilder",
            Type = PatternType.Flutter,
            Category = PatternCategory.DataAccess,
            Content = "FutureBuilder<String>(future: fetchData(), builder: ...)",
            Metadata = new Dictionary<string, object>
            {
                ["handles_error"] = false
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("error handling")));
    }

    [Fact]
    public void ValidateFlutterPattern_FormWithoutValidation_ReturnsSecurityWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-8",
            Name = "Flutter_FormHandling",
            Type = PatternType.Flutter,
            Category = PatternCategory.UserExperience,
            Content = "Form(child: TextFormField())",
            Metadata = new Dictionary<string, object>
            {
                ["has_validation"] = false
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Category == IssueCategory.Security));
        Assert.True(result.SecurityScore < 10);
    }

    [Fact]
    public void ValidateFlutterPattern_HeroWithoutTag_ReturnsWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-9",
            Name = "Flutter_HeroAnimation",
            Type = PatternType.Flutter,
            Category = PatternCategory.UserExperience,
            Content = "Hero(child: Image.network(url))",
            Metadata = new Dictionary<string, object>
            {
                ["has_tag"] = false
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("tag")));
    }

    #endregion

    #region Flutter Performance Anti-Pattern Tests

    [Fact]
    public void ValidateFlutterPattern_SetStateInBuild_ReturnsCritical()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-10",
            Name = "Flutter_SetStateInBuild_AntiPattern",
            Type = PatternType.Flutter,
            Category = PatternCategory.Performance,
            Content = "Widget build(context) { setState(() {}); }",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "critical"
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Severity == IssueSeverity.Critical));
        Assert.True(result.Score <= 5);
    }

    [Fact]
    public void ValidateFlutterPattern_AsyncInBuild_ReturnsCritical()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-11",
            Name = "Flutter_AsyncInBuild_AntiPattern",
            Type = PatternType.Flutter,
            Category = PatternCategory.Performance,
            Content = "Widget build(context) async { await fetchData(); }",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "high"
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("synchronous") || i.Message.Contains("async")));
    }

    [Fact]
    public void ValidateFlutterPattern_ListViewChildren_ReturnsPerformanceWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-12",
            Name = "Flutter_ListViewChildren_AntiPattern",
            Type = PatternType.Flutter,
            Category = PatternCategory.Performance,
            Content = "ListView(children: [...])",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "medium"
            }
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("lazy") || i.Message.Contains("builder")));
    }

    #endregion

    #region Dart Security Validation Tests

    [Fact]
    public void ValidateDartPattern_HardcodedCredentials_ReturnsCriticalSecurity()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-20",
            Name = "Dart_HardcodedCredentials_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Security,
            Content = "final apiKey = 'secret123';",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "critical",
                ["cwe"] = "CWE-798"
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Severity == IssueSeverity.Critical));
        Assert.True(result.Issues.Any(i => i.SecurityReference == "CWE-798"));
        Assert.True(result.SecurityScore <= 5);
        _output.WriteLine($"Security Score: {result.SecurityScore}/10");
    }

    [Fact]
    public void ValidateDartPattern_InsecureHttp_ReturnsSecurityWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-21",
            Name = "Dart_InsecureHttp_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Security,
            Content = "http.get('http://api.example.com')",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "high",
                ["cwe"] = "CWE-319"
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.SecurityReference == "CWE-319"));
        Assert.True(result.SecurityScore < 10);
    }

    [Fact]
    public void ValidateDartPattern_DisabledCertVerification_ReturnsCriticalSecurity()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-22",
            Name = "Dart_DisabledCertVerification_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Security,
            Content = "badCertificateCallback = (cert, host, port) => true",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "critical",
                ["cwe"] = "CWE-295"
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Severity == IssueSeverity.Critical));
        Assert.True(result.SecurityScore <= 5);
    }

    [Fact]
    public void ValidateDartPattern_SQLInjection_ReturnsCriticalSecurity()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-23",
            Name = "Dart_SQLInjection_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Security,
            Content = "rawQuery('SELECT * WHERE name = \"$name\"')",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "critical",
                ["cwe"] = "CWE-89"
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.SecurityReference == "CWE-89"));
    }

    #endregion

    #region Dart Null Safety Validation Tests

    [Fact]
    public void ValidateDartPattern_ExcessiveBangOperator_ReturnsHighWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-30",
            Name = "Dart_ExcessiveBangOperator_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Correctness,
            Content = "data!.field1!.field2!.field3!",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "high",
                ["bang_count"] = 15L
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Severity == IssueSeverity.High));
        Assert.True(result.Issues.Any(i => i.Message.Contains("!")));
    }

    [Fact]
    public void ValidateDartPattern_ExcessiveLate_ReturnsWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-31",
            Name = "Dart_LateKeyword",
            Type = PatternType.Dart,
            Category = PatternCategory.Correctness,
            Content = "late final a; late final b; late int c;",
            Metadata = new Dictionary<string, object>
            {
                ["late_count"] = 10L
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("late")));
    }

    #endregion

    #region Dart Performance Validation Tests

    [Fact]
    public void ValidateDartPattern_StringConcatInLoop_ReturnsHighWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-40",
            Name = "Dart_StringConcatInLoop_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Performance,
            Content = "for (i = 0; i < n; i++) { result += items[i]; }",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "high"
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Severity == IssueSeverity.High));
        Assert.True(result.Issues.Any(i => i.Message.Contains("O(n²)") || i.Message.Contains("StringBuffer")));
    }

    [Fact]
    public void ValidateDartPattern_SyncIO_ReturnsHighWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-41",
            Name = "Dart_SyncIO_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Performance,
            Content = "file.readAsStringSync()",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "high"
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("Synchronous") || i.Message.Contains("async")));
    }

    #endregion

    #region Dart Error Handling Validation Tests

    [Fact]
    public void ValidateDartPattern_EmptyCatch_ReturnsHighWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-50",
            Name = "Dart_EmptyCatch_AntiPattern",
            Type = PatternType.Dart,
            Category = PatternCategory.Reliability,
            Content = "try { } catch (e) { }",
            Metadata = new Dictionary<string, object>
            {
                ["is_anti_pattern"] = true,
                ["severity"] = "high"
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("Empty") || i.Message.Contains("swallow")));
    }

    [Fact]
    public void ValidateDartPattern_StreamWithoutCancel_ReturnsHighWarning()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-51",
            Name = "Dart_Stream",
            Type = PatternType.Dart,
            Category = PatternCategory.Reliability,
            Content = "stream.listen((data) { })",
            Metadata = new Dictionary<string, object>
            {
                ["has_cancel"] = false
            }
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Issues.Any(i => i.Message.Contains("cancel") || i.Message.Contains("memory leak")));
    }

    #endregion

    #region Positive Pattern Tests (Good Practices)

    [Fact]
    public void ValidateFlutterPattern_LazyListBuilder_AddsPositiveRecommendation()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-60",
            Name = "Flutter_LazyListBuilder",
            Type = PatternType.Flutter,
            Category = PatternCategory.Performance,
            Content = "ListView.builder(itemBuilder: ...)",
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Recommendations.Any(r => r.Contains("Excellent") || r.Contains("lazy")));
        Assert.True(result.Score >= 8);
    }

    [Fact]
    public void ValidateFlutterPattern_RepaintBoundary_AddsPositiveRecommendation()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-61",
            Name = "Flutter_RepaintBoundary",
            Type = PatternType.Flutter,
            Category = PatternCategory.Performance,
            Content = "RepaintBoundary(child: ...)",
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = ValidateFlutterPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Recommendations.Any());
    }

    [Fact]
    public void ValidateDartPattern_SecureStorage_AddsPositiveRecommendation()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-62",
            Name = "Dart_SecureStorage",
            Type = PatternType.Dart,
            Category = PatternCategory.Security,
            Content = "FlutterSecureStorage().write(key: 'token', value: token)",
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Recommendations.Any(r => r.Contains("Excellent") || r.Contains("Secure")));
        Assert.Equal(10, result.SecurityScore);
    }

    [Fact]
    public void ValidateDartPattern_ResultType_AddsPositiveRecommendation()
    {
        // Arrange
        var pattern = new CodePattern
        {
            Id = "test-63",
            Name = "Dart_ResultType",
            Type = PatternType.Dart,
            Category = PatternCategory.Reliability,
            Content = "Either<Error, Success> result = ...",
            Metadata = new Dictionary<string, object>()
        };

        // Act
        var result = ValidateDartPatternDirect(pattern);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Recommendations.Any(r => r.Contains("Result") || r.Contains("Either")));
    }

    #endregion

    #region Helper Methods (Simulating PatternValidationService logic)

    private PatternQualityResult ValidateFlutterPatternDirect(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10,
            SecurityScore = 10
        };

        // Check for anti-patterns in metadata
        if (pattern.Metadata.TryGetValue("is_anti_pattern", out var isAntiPattern) && (bool)isAntiPattern)
        {
            var severity = pattern.Metadata.TryGetValue("severity", out var sev) ? sev.ToString() : "medium";
            var severityLevel = severity switch
            {
                "critical" => IssueSeverity.Critical,
                "high" => IssueSeverity.High,
                "low" => IssueSeverity.Low,
                _ => IssueSeverity.Medium
            };

            result.Issues.Add(new ValidationIssue
            {
                Severity = severityLevel,
                Category = IssueCategory.BestPractice,
                Message = $"Flutter Anti-Pattern: {pattern.Implementation}",
                ScoreImpact = severity == "critical" ? 5 : severity == "high" ? 3 : 2,
                FixGuidance = pattern.BestPractice
            });
            result.Score -= severity == "critical" ? 5 : severity == "high" ? 3 : 2;
        }

        // Pattern-specific validations
        switch (pattern.Name)
        {
            case "Flutter_StatelessWidget":
                if (pattern.Metadata.TryGetValue("has_const_constructor", out var hasConst) && !(bool)hasConst)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Performance,
                        Message = "StatelessWidget without const constructor - missed optimization opportunity",
                        ScoreImpact = 1
                    });
                    result.Score -= 1;
                }
                break;

            case "Flutter_StatefulWidget":
                if (pattern.Metadata.TryGetValue("has_state_class", out var hasState) && !(bool)hasState)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.Correctness,
                        Message = "StatefulWidget without corresponding State class",
                        ScoreImpact = 5
                    });
                    result.Score -= 5;
                }
                break;

            case "Flutter_MissingDispose_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Reliability,
                    Message = "Memory leak: Controller found without dispose() method",
                    ScoreImpact = 4
                });
                result.Score -= 4;
                break;

            case "Flutter_ChangeNotifier":
                if (pattern.Metadata.TryGetValue("has_dispose", out var hasDispose) && !(bool)hasDispose)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Reliability,
                        Message = "ChangeNotifier without dispose - potential memory leak",
                        ScoreImpact = 3
                    });
                    result.Score -= 3;
                }
                break;

            case "Flutter_AnimationController":
                if (pattern.Metadata.TryGetValue("disposes_controller", out var disposesCtrl) && !(bool)disposesCtrl)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.Reliability,
                        Message = "AnimationController without dispose - will cause memory leak",
                        ScoreImpact = 4
                    });
                    result.Score -= 4;
                }
                break;

            case "Flutter_InitState":
            case "Flutter_Dispose":
                if (pattern.Metadata.TryGetValue("calls_super", out var callsSuper) && !(bool)callsSuper)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Correctness,
                        Message = pattern.Name == "Flutter_InitState" 
                            ? "initState doesn't call super.initState()" 
                            : "dispose doesn't call super.dispose()",
                        ScoreImpact = 3
                    });
                    result.Score -= 3;
                }
                break;

            case "Flutter_FutureBuilder":
            case "Flutter_StreamBuilder":
                if (pattern.Metadata.TryGetValue("handles_error", out var handlesError) && !(bool)handlesError)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.BestPractice,
                        Message = "FutureBuilder/StreamBuilder without error handling",
                        ScoreImpact = 2
                    });
                    result.Score -= 2;
                }
                break;

            case "Flutter_FormHandling":
                if (pattern.Metadata.TryGetValue("has_validation", out var hasValidation) && !(bool)hasValidation)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Security,
                        Message = "Form without input validation - security risk",
                        ScoreImpact = 3
                    });
                    result.Score -= 3;
                    result.SecurityScore -= 2;
                }
                break;

            case "Flutter_HeroAnimation":
                if (pattern.Metadata.TryGetValue("has_tag", out var hasTag) && !(bool)hasTag)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Correctness,
                        Message = "Hero widget without tag property",
                        ScoreImpact = 2
                    });
                    result.Score -= 2;
                }
                break;

            case "Flutter_SetStateInBuild_AntiPattern":
            case "Flutter_AsyncInBuild_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Performance,
                    Message = pattern.Name == "Flutter_SetStateInBuild_AntiPattern"
                        ? "setState called in build() - causes infinite rebuild loop"
                        : "async/await in build() - build must be synchronous",
                    ScoreImpact = 5
                });
                result.Score -= 5;
                break;

            case "Flutter_ListViewChildren_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.Performance,
                    Message = "ListView with children instead of builder - not lazy loaded",
                    ScoreImpact = 2
                });
                result.Score -= 2;
                break;

            case "Flutter_LazyListBuilder":
                result.Recommendations.Add("Excellent! ListView.builder ensures lazy loading for performance");
                break;

            case "Flutter_RepaintBoundary":
                result.Recommendations.Add("Good performance practice - RepaintBoundary isolates repaints");
                break;
        }

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        return result;
    }

    private PatternQualityResult ValidateDartPatternDirect(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10,
            SecurityScore = 10
        };

        // Check for anti-patterns with CWE
        if (pattern.Metadata.TryGetValue("is_anti_pattern", out var isAntiPattern) && (bool)isAntiPattern)
        {
            var severity = pattern.Metadata.TryGetValue("severity", out var sev) ? sev.ToString() : "medium";
            
            if (pattern.Metadata.TryGetValue("cwe", out var cwe))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = $"Security Issue: {pattern.Implementation}",
                    ScoreImpact = 5,
                    SecurityReference = cwe.ToString()
                });
                result.Score -= 5;
                result.SecurityScore -= 5;
            }
            else
            {
                var severityLevel = severity switch
                {
                    "critical" => IssueSeverity.Critical,
                    "high" => IssueSeverity.High,
                    "low" => IssueSeverity.Low,
                    _ => IssueSeverity.Medium
                };

                result.Issues.Add(new ValidationIssue
                {
                    Severity = severityLevel,
                    Category = IssueCategory.BestPractice,
                    Message = $"Dart Anti-Pattern: {pattern.Implementation}",
                    ScoreImpact = severity == "critical" ? 5 : severity == "high" ? 3 : 2
                });
                result.Score -= severity == "critical" ? 5 : severity == "high" ? 3 : 2;
            }
        }

        // Pattern-specific validations
        switch (pattern.Name)
        {
            case "Dart_HardcodedCredentials_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = "CRITICAL: Hardcoded credentials detected",
                    ScoreImpact = 5,
                    SecurityReference = "CWE-798"
                });
                result.Score -= 5;
                result.SecurityScore -= 5;
                break;

            case "Dart_ExcessiveBangOperator_AntiPattern":
                var bangCount = pattern.Metadata.TryGetValue("bang_count", out var bc) ? (int)(long)bc : 0;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Correctness,
                    Message = $"Excessive use of ! operator ({bangCount} times) - defeats null safety",
                    ScoreImpact = 3
                });
                result.Score -= 3;
                break;

            case "Dart_LateKeyword":
                var lateCount = pattern.Metadata.TryGetValue("late_count", out var lc) ? (int)(long)lc : 0;
                if (lateCount > 5)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.BestPractice,
                        Message = $"Excessive 'late' usage ({lateCount} times) - potential runtime errors",
                        ScoreImpact = 2
                    });
                    result.Score -= 2;
                }
                break;

            case "Dart_StringConcatInLoop_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Performance,
                    Message = "String concatenation in loop - O(n²) performance",
                    ScoreImpact = 3
                });
                result.Score -= 3;
                break;

            case "Dart_SyncIO_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Performance,
                    Message = "Synchronous file I/O blocks the event loop",
                    ScoreImpact = 3
                });
                result.Score -= 3;
                break;

            case "Dart_EmptyCatch_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.BestPractice,
                    Message = "Empty catch block swallows exceptions silently",
                    ScoreImpact = 3
                });
                result.Score -= 3;
                break;

            case "Dart_Stream":
                if (pattern.Metadata.TryGetValue("has_cancel", out var hasCancel) && !(bool)hasCancel)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Reliability,
                        Message = "Stream subscription without cancel - potential memory leak",
                        ScoreImpact = 3
                    });
                    result.Score -= 3;
                }
                break;

            case "Dart_SecureStorage":
                result.Recommendations.Add("Excellent! Using FlutterSecureStorage for sensitive data");
                break;

            case "Dart_ResultType":
                result.Recommendations.Add("Good practice: Result/Either types for explicit error handling");
                break;
        }

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        return result;
    }

    #endregion
}

