using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Dart language patterns
/// Covers async, null safety, performance, security, error handling, and code quality
/// </summary>
public class DartPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.Dart };

    public PatternQualityResult Validate(CodePattern pattern)
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

            // Check for CWE references (security issues)
            if (pattern.Metadata.TryGetValue("cwe", out var cwe))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = $"Security Issue: {pattern.Implementation}",
                    ScoreImpact = 5,
                    SecurityReference = cwe.ToString(),
                    FixGuidance = pattern.BestPractice
                });
                result.Score -= 5;
                result.SecurityScore -= 5;
            }
            else
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = severityLevel,
                    Category = IssueCategory.BestPractice,
                    Message = $"Dart Anti-Pattern: {pattern.Implementation}",
                    ScoreImpact = severity == "critical" ? 5 : severity == "high" ? 3 : 2,
                    FixGuidance = pattern.BestPractice
                });
                result.Score -= severity == "critical" ? 5 : severity == "high" ? 3 : 2;
            }
        }

        // Dart-specific validations
        switch (pattern.Name)
        {
            // Security patterns
            case "Dart_HardcodedCredentials_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = "CRITICAL: Hardcoded credentials detected",
                    ScoreImpact = 5,
                    SecurityReference = "CWE-798",
                    FixGuidance = "Use environment variables or flutter_secure_storage for credentials"
                });
                result.Score -= 5;
                result.SecurityScore -= 5;
                break;

            case "Dart_InsecureHttp_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Security,
                    Message = "HTTP used instead of HTTPS - data transmitted in plaintext",
                    ScoreImpact = 3,
                    SecurityReference = "CWE-319",
                    FixGuidance = "Always use HTTPS for network requests"
                });
                result.Score -= 3;
                result.SecurityScore -= 3;
                break;

            case "Dart_DisabledCertVerification_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = "Certificate verification disabled - enables MITM attacks",
                    ScoreImpact = 5,
                    SecurityReference = "CWE-295",
                    FixGuidance = "Never disable certificate verification in production"
                });
                result.Score -= 5;
                result.SecurityScore -= 5;
                break;

            case "Dart_SQLInjection_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Category = IssueCategory.Security,
                    Message = "Potential SQL injection - string interpolation in raw query",
                    ScoreImpact = 5,
                    SecurityReference = "CWE-89",
                    FixGuidance = "Use parameterized queries: rawQuery('SELECT * WHERE id = ?', [id])"
                });
                result.Score -= 5;
                result.SecurityScore -= 5;
                break;

            // Null safety patterns
            case "Dart_ExcessiveBangOperator_AntiPattern":
                var bangCount = pattern.Metadata.TryGetValue("bang_count", out var bc) ? (int)(long)bc : 0;
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Correctness,
                    Message = $"Excessive use of ! operator ({bangCount} times) - defeats null safety",
                    ScoreImpact = 3,
                    FixGuidance = "Use null checks (?.), provide defaults (??), or refactor to avoid nulls"
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
                        ScoreImpact = 2,
                        FixGuidance = "Use nullable types with null checks or constructor initialization instead"
                    });
                    result.Score -= 2;
                }
                break;

            // Performance patterns
            case "Dart_StringConcatInLoop_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Performance,
                    Message = "String concatenation in loop - O(n²) performance",
                    ScoreImpact = 3,
                    FixGuidance = "Use StringBuffer for efficient string building in loops"
                });
                result.Score -= 3;
                break;

            case "Dart_SyncIO_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.Performance,
                    Message = "Synchronous file I/O blocks the event loop",
                    ScoreImpact = 3,
                    FixGuidance = "Use async file operations: readAsString() instead of readAsStringSync()"
                });
                result.Score -= 3;
                break;

            // Async patterns
            case "Dart_ThenChain_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Medium,
                    Category = IssueCategory.BestPractice,
                    Message = "Chained .then() calls reduce readability",
                    ScoreImpact = 1,
                    FixGuidance = "Use async/await for cleaner async code"
                });
                result.Score -= 1;
                break;

            case "Dart_Stream":
                if (pattern.Metadata.TryGetValue("has_cancel", out var hasCancel) && !(bool)hasCancel)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Reliability,
                        Message = "Stream subscription without cancel - potential memory leak",
                        ScoreImpact = 3,
                        FixGuidance = "Store subscription and call cancel() in dispose()"
                    });
                    result.Score -= 3;
                }
                break;

            // Error handling patterns
            case "Dart_EmptyCatch_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.High,
                    Category = IssueCategory.BestPractice,
                    Message = "Empty catch block swallows exceptions silently",
                    ScoreImpact = 3,
                    FixGuidance = "At minimum, log the error: catch (e) { debugPrint('Error: $e'); }"
                });
                result.Score -= 3;
                break;

            case "Dart_TryCatch":
                if (pattern.Metadata.TryGetValue("has_typed_catch", out var hasTyped) && (bool)hasTyped)
                {
                    result.Recommendations.Add("Good practice: Using typed catch clauses (on SpecificException)");
                }
                break;

            // Code quality patterns
            case "Dart_PrintStatements_AntiPattern":
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Low,
                    Category = IssueCategory.BestPractice,
                    Message = $"Excessive print() statements ({pattern.Metadata.GetValueOrDefault("print_count", 0)})",
                    ScoreImpact = 1,
                    FixGuidance = "Use logger package for proper logging in production"
                });
                result.Score -= 1;
                break;

            // Input validation
            case "Dart_InputValidation":
                if (pattern.Metadata.TryGetValue("has_validation", out var hasVal) && !(bool)hasVal)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.Security,
                        Message = "Form fields without validation",
                        ScoreImpact = 2,
                        FixGuidance = "Add validator property to form fields"
                    });
                    result.Score -= 2;
                    result.SecurityScore -= 1;
                }
                break;
        }

        // Add positive recommendations for good patterns
        if (pattern.Name == "Dart_SecureStorage")
            result.Recommendations.Add("Excellent! Using FlutterSecureStorage for sensitive data");
        if (pattern.Name == "Dart_CertificatePinning")
            result.Recommendations.Add("Good security: Certificate pinning prevents MITM attacks");
        if (pattern.Name == "Dart_BiometricAuth")
            result.Recommendations.Add("Strong authentication with biometrics");
        if (pattern.Name == "Dart_ConstConstructor")
            result.Recommendations.Add("Good: const constructors enable compile-time optimization");
        if (pattern.Name == "Dart_StringBuffer")
            result.Recommendations.Add("Efficient string building with StringBuffer");
        if (pattern.Name == "Dart_Isolate")
            result.Recommendations.Add("Excellent: Using Isolate for CPU-intensive work");
        if (pattern.Name == "Dart_ResultType")
            result.Recommendations.Add("Good practice: Result/Either types for explicit error handling");
        if (pattern.Name == "Dart_SealedClass")
            result.Recommendations.Add("Excellent: Sealed classes enable exhaustive switch");
        if (pattern.Name == "Dart_CustomException")
            result.Recommendations.Add("Good: Custom exceptions provide clear error semantics");

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        result.Summary = $"Dart Pattern Quality: {result.Grade} ({result.Score}/10) | Security: {result.SecurityScore}/10";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

