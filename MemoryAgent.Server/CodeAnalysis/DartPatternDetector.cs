using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Pattern detector for Dart language - covers async, null safety, performance, and security patterns
/// Based on: https://dart.dev/docs and https://dart.dev/effective-dart
/// </summary>
public class DartPatternDetector
{
    private readonly ILogger<DartPatternDetector>? _logger;

    public DartPatternDetector(ILogger<DartPatternDetector>? logger = null)
    {
        _logger = logger;
    }

    public List<CodePattern> DetectPatterns(string sourceCode, string filePath, string? context = null)
    {
        var patterns = new List<CodePattern>();

        try
        {
            // Skip non-Dart files
            if (!filePath.EndsWith(".dart", StringComparison.OrdinalIgnoreCase))
                return patterns;

            patterns.AddRange(DetectAsyncPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectNullSafetyPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectPerformancePatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectSecurityPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectErrorHandlingPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectCodeQualityPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectCollectionPatterns(sourceCode, filePath, context));
            patterns.AddRange(DetectClassPatterns(sourceCode, filePath, context));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error detecting Dart patterns in {FilePath}", filePath);
        }

        return patterns;
    }

    #region Async Patterns

    private List<CodePattern> DetectAsyncPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: async/await usage
        var asyncMatches = Regex.Matches(sourceCode, @"async\s*\{|async\s*\*\s*\{|await\s+\w+");
        foreach (Match match in asyncMatches)
        {
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_AsyncAwait",
                Type = PatternType.Dart,
                Category = PatternCategory.Reliability,
                Implementation = "async/await pattern",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Use async/await for asynchronous operations. Always handle errors with try/catch.",
                AzureBestPracticeUrl = "https://dart.dev/codelabs/async-await",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["pattern_type"] = "async_await",
                    ["has_error_handling"] = sourceCode.Contains("try") && sourceCode.Contains("catch")
                }
            });
            break; // One pattern per file for this type
        }

        // Pattern: Future usage
        var futureMatches = Regex.Matches(sourceCode, @"Future<[^>]+>|Future\.delayed|Future\.wait|Future\.value");
        foreach (Match match in futureMatches)
        {
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_Future",
                Type = PatternType.Dart,
                Category = PatternCategory.Reliability,
                Implementation = match.Value,
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "Use Future for single async operations. Prefer async/await over .then() chains.",
                AzureBestPracticeUrl = "https://dart.dev/guides/libraries/library-tour#future",
                Context = context
            });
            break;
        }

        // Pattern: Stream usage
        var streamMatches = Regex.Matches(sourceCode, @"Stream<[^>]+>|StreamController|\.listen\(|async\*|yield\s+");
        foreach (Match match in streamMatches)
        {
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_Stream",
                Type = PatternType.Dart,
                Category = PatternCategory.Reliability,
                Implementation = "Stream pattern",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Always cancel stream subscriptions in dispose(). Use StreamController.broadcast() for multiple listeners.",
                AzureBestPracticeUrl = "https://dart.dev/tutorials/language/streams",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["has_cancel"] = sourceCode.Contains(".cancel()") || sourceCode.Contains("dispose")
                }
            });
            break;
        }

        // Pattern: Isolate for heavy computation
        if (Regex.IsMatch(sourceCode, @"Isolate\.spawn|Isolate\.run|compute\("))
        {
            var match = Regex.Match(sourceCode, @"Isolate\.spawn|Isolate\.run|compute\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_Isolate",
                Type = PatternType.Dart,
                Category = PatternCategory.Performance,
                Implementation = "Isolate/compute for background processing",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Use Isolate.run() or compute() for CPU-intensive work to avoid blocking the UI thread.",
                AzureBestPracticeUrl = "https://dart.dev/language/concurrency",
                Context = context
            });
        }

        // Anti-pattern: .then() chains (prefer async/await)
        var thenChainMatches = Regex.Matches(sourceCode, @"\.then\([^)]+\)\.then\(");
        if (thenChainMatches.Count > 0)
        {
            var match = thenChainMatches[0];
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_ThenChain_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Chained .then() calls",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.85f,
                BestPractice = "ANTI-PATTERN: Avoid chaining .then() calls. Use async/await for better readability.",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/usage#prefer-asyncawait-over-using-raw-futures",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "medium"
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Null Safety Patterns

    private List<CodePattern> DetectNullSafetyPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Null-aware operators
        var nullAwareMatches = Regex.Matches(sourceCode, @"\?\.|!\.|\.\.|\?\?|!\s*[;,\)]");
        if (nullAwareMatches.Count > 0)
        {
            var match = nullAwareMatches[0];
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_NullAwareOperators",
                Type = PatternType.Dart,
                Category = PatternCategory.Correctness,
                Implementation = "Null-aware operators (?., ??, !)",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.90f,
                BestPractice = "Use ?. for safe access, ?? for defaults. Avoid ! unless you're certain the value is non-null.",
                AzureBestPracticeUrl = "https://dart.dev/null-safety/understanding-null-safety",
                Context = context
            });
        }

        // Pattern: late keyword usage
        var lateMatches = Regex.Matches(sourceCode, @"late\s+(final\s+)?\w+");
        if (lateMatches.Count > 0)
        {
            var match = lateMatches[0];
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_LateKeyword",
                Type = PatternType.Dart,
                Category = PatternCategory.Correctness,
                Implementation = "late initialization",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.85f,
                BestPractice = "Use 'late' sparingly. Prefer nullable types with null checks or constructor initialization.",
                AzureBestPracticeUrl = "https://dart.dev/null-safety/understanding-null-safety#late-variables",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["late_count"] = lateMatches.Count,
                    ["potential_risk"] = lateMatches.Count > 5 ? "high" : "low"
                }
            });
        }

        // Anti-pattern: Excessive bang operator usage
        var bangOperatorCount = Regex.Matches(sourceCode, @"!\s*[;,\)\.[]").Count;
        if (bangOperatorCount > 10)
        {
            patterns.Add(new CodePattern
            {
                Name = "Dart_ExcessiveBangOperator_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.Correctness,
                Implementation = $"Excessive ! operator usage ({bangOperatorCount} occurrences)",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = $"Found {bangOperatorCount} bang (!) operators in file",
                Confidence = 0.80f,
                BestPractice = "ANTI-PATTERN: Excessive use of ! operator. Use null checks or provide defaults instead.",
                AzureBestPracticeUrl = "https://dart.dev/null-safety/understanding-null-safety",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "high",
                    ["bang_count"] = bangOperatorCount
                }
            });
        }

        // Pattern: required keyword for named parameters
        if (Regex.IsMatch(sourceCode, @"required\s+\w+"))
        {
            var match = Regex.Match(sourceCode, @"required\s+\w+");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_RequiredParameter",
                Type = PatternType.Dart,
                Category = PatternCategory.Correctness,
                Implementation = "required named parameter",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "Use 'required' for mandatory named parameters. This provides compile-time safety.",
                AzureBestPracticeUrl = "https://dart.dev/language/functions#named-parameters",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Performance Patterns

    private List<CodePattern> DetectPerformancePatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: const constructor
        var constMatches = Regex.Matches(sourceCode, @"const\s+\w+\(|const\s+\[|const\s+\{");
        if (constMatches.Count > 0)
        {
            var match = constMatches[0];
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_ConstConstructor",
                Type = PatternType.Dart,
                Category = PatternCategory.Performance,
                Implementation = "const constructor/literal",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "Use const constructors for immutable objects. This enables compile-time constants and better performance.",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/usage#use-const-constructors-whenever-possible",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["const_count"] = constMatches.Count
                }
            });
        }

        // Pattern: final keyword for immutability
        var finalCount = Regex.Matches(sourceCode, @"\bfinal\s+\w+").Count;
        if (finalCount > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Dart_FinalKeyword",
                Type = PatternType.Dart,
                Category = PatternCategory.Performance,
                Implementation = $"final keyword ({finalCount} usages)",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = $"Found {finalCount} final declarations",
                Confidence = 0.90f,
                BestPractice = "Use 'final' for variables that won't be reassigned. This improves code clarity and enables optimizations.",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/usage#prefer-final-for-local-variables",
                Context = context
            });
        }

        // Anti-pattern: Large string concatenation in loops
        if (Regex.IsMatch(sourceCode, @"for\s*\([^)]*\)\s*\{[^}]*\+\s*=\s*['""]"))
        {
            var match = Regex.Match(sourceCode, @"for\s*\([^)]*\)\s*\{[^}]*\+\s*=\s*['""]");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_StringConcatInLoop_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.Performance,
                Implementation = "String concatenation in loop",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.85f,
                BestPractice = "ANTI-PATTERN: Use StringBuffer or string interpolation instead of += in loops.",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/usage#do-use-adjacent-strings-to-concatenate-string-literals",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "high"
                }
            });
        }

        // Pattern: StringBuffer usage (good practice)
        if (sourceCode.Contains("StringBuffer"))
        {
            var match = Regex.Match(sourceCode, @"StringBuffer");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_StringBuffer",
                Type = PatternType.Dart,
                Category = PatternCategory.Performance,
                Implementation = "StringBuffer for efficient string building",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "Use StringBuffer for building strings incrementally. Much more efficient than concatenation.",
                AzureBestPracticeUrl = "https://api.dart.dev/stable/dart-core/StringBuffer-class.html",
                Context = context
            });
        }

        // Pattern: Lazy initialization with late final
        if (Regex.IsMatch(sourceCode, @"late\s+final\s+\w+\s*="))
        {
            var match = Regex.Match(sourceCode, @"late\s+final\s+\w+\s*=");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_LazyInitialization",
                Type = PatternType.Dart,
                Category = PatternCategory.Performance,
                Implementation = "Lazy initialization with late final",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.90f,
                BestPractice = "Use 'late final' for lazy initialization of expensive computations.",
                AzureBestPracticeUrl = "https://dart.dev/null-safety/understanding-null-safety#lazy-initialization",
                Context = context
            });
        }

        // Anti-pattern: Synchronous file I/O
        if (Regex.IsMatch(sourceCode, @"readAsStringSync|readAsBytesSync|writeAsStringSync|writeAsBytesSync"))
        {
            var match = Regex.Match(sourceCode, @"readAsStringSync|readAsBytesSync|writeAsStringSync|writeAsBytesSync");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_SyncIO_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.Performance,
                Implementation = "Synchronous file I/O",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.90f,
                BestPractice = "ANTI-PATTERN: Use async file operations (readAsString, readAsBytes) to avoid blocking.",
                AzureBestPracticeUrl = "https://dart.dev/guides/libraries/library-tour#files-and-directories",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "high"
                }
            });
        }

        return patterns;
    }

    #endregion

    #region Security Patterns

    private List<CodePattern> DetectSecurityPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Anti-pattern: Hardcoded credentials
        var credentialPatterns = new[]
        {
            @"['""]password['""]:\s*['""][^'""]+['""]",
            @"password\s*=\s*['""][^'""]+['""]",
            @"apiKey\s*=\s*['""][^'""]+['""]",
            @"api_key\s*=\s*['""][^'""]+['""]",
            @"secret\s*=\s*['""][^'""]+['""]",
            @"token\s*=\s*['""][a-zA-Z0-9]{20,}['""]"
        };

        foreach (var pattern in credentialPatterns)
        {
            if (Regex.IsMatch(sourceCode, pattern, RegexOptions.IgnoreCase))
            {
                var match = Regex.Match(sourceCode, pattern, RegexOptions.IgnoreCase);
                var lineNumber = GetLineNumber(sourceCode, match.Index);
                patterns.Add(new CodePattern
                {
                    Name = "Dart_HardcodedCredentials_AntiPattern",
                    Type = PatternType.Dart,
                    Category = PatternCategory.Security,
                    Implementation = "Hardcoded credentials detected",
                    Language = "Dart",
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    Content = "[REDACTED - Potential credential]",
                    Confidence = 0.95f,
                    BestPractice = "CRITICAL SECURITY: Never hardcode credentials. Use environment variables or secure storage.",
                    AzureBestPracticeUrl = "https://pub.dev/packages/flutter_secure_storage",
                    Context = context,
                    Metadata = new Dictionary<string, object>
                    {
                        ["is_anti_pattern"] = true,
                        ["severity"] = "critical",
                        ["cwe"] = "CWE-798"
                    }
                });
                break; // One is enough to flag the file
            }
        }

        // Pattern: Secure storage usage
        if (sourceCode.Contains("FlutterSecureStorage") || sourceCode.Contains("flutter_secure_storage"))
        {
            var match = Regex.Match(sourceCode, @"FlutterSecureStorage|flutter_secure_storage");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_SecureStorage",
                Type = PatternType.Dart,
                Category = PatternCategory.Security,
                Implementation = "FlutterSecureStorage for sensitive data",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "Use FlutterSecureStorage for sensitive data like tokens and credentials.",
                AzureBestPracticeUrl = "https://pub.dev/packages/flutter_secure_storage",
                Context = context
            });
        }

        // Anti-pattern: HTTP instead of HTTPS
        if (Regex.IsMatch(sourceCode, @"['""]http://(?!localhost|127\.0\.0\.1|10\.|192\.168\.)"))
        {
            var match = Regex.Match(sourceCode, @"['""]http://(?!localhost|127\.0\.0\.1|10\.|192\.168\.)");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_InsecureHttp_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.Security,
                Implementation = "HTTP instead of HTTPS",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 2),
                Confidence = 0.90f,
                BestPractice = "SECURITY: Always use HTTPS for network requests. HTTP transmits data in plaintext.",
                AzureBestPracticeUrl = "https://dart.dev/guides/libraries/library-tour#http-clients",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "high",
                    ["cwe"] = "CWE-319"
                }
            });
        }

        // Pattern: SSL/Certificate pinning
        if (Regex.IsMatch(sourceCode, @"SecurityContext|badCertificateCallback|certificatePinning"))
        {
            var match = Regex.Match(sourceCode, @"SecurityContext|badCertificateCallback|certificatePinning");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_CertificatePinning",
                Type = PatternType.Dart,
                Category = PatternCategory.Security,
                Implementation = "SSL/Certificate pinning",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Certificate pinning prevents MITM attacks. Ensure you handle certificate rotation.",
                AzureBestPracticeUrl = "https://api.dart.dev/stable/dart-io/SecurityContext-class.html",
                Context = context
            });
        }

        // Anti-pattern: Disabled certificate verification
        if (Regex.IsMatch(sourceCode, @"badCertificateCallback.*=>\s*true"))
        {
            var match = Regex.Match(sourceCode, @"badCertificateCallback.*=>\s*true");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_DisabledCertVerification_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.Security,
                Implementation = "Disabled certificate verification",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "CRITICAL SECURITY: Never disable certificate verification in production. This enables MITM attacks.",
                AzureBestPracticeUrl = "https://api.dart.dev/stable/dart-io/HttpClient/badCertificateCallback.html",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "critical",
                    ["cwe"] = "CWE-295"
                }
            });
        }

        // Pattern: Input validation
        if (Regex.IsMatch(sourceCode, @"FormField|TextFormField|validator:|InputDecoration"))
        {
            var hasValidator = sourceCode.Contains("validator:");
            patterns.Add(new CodePattern
            {
                Name = "Dart_InputValidation",
                Type = PatternType.Dart,
                Category = PatternCategory.Security,
                Implementation = hasValidator ? "Form with validation" : "Form without validation",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = hasValidator ? "Form validation detected" : "Form fields found but no validator",
                Confidence = hasValidator ? 0.90f : 0.70f,
                BestPractice = hasValidator 
                    ? "Form validation implemented. Ensure server-side validation as well."
                    : "WARNING: Add validator to form fields to prevent invalid input.",
                AzureBestPracticeUrl = "https://docs.flutter.dev/cookbook/forms/validation",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["has_validation"] = hasValidator,
                    ["severity"] = hasValidator ? "none" : "medium"
                }
            });
        }

        // Anti-pattern: SQL injection risk (raw SQL queries)
        if (Regex.IsMatch(sourceCode, @"rawQuery\([^)]*\$|rawInsert\([^)]*\$|rawDelete\([^)]*\$|rawUpdate\([^)]*\$"))
        {
            var match = Regex.Match(sourceCode, @"raw(Query|Insert|Delete|Update)\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_SQLInjection_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.Security,
                Implementation = "Potential SQL injection with string interpolation",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.85f,
                BestPractice = "SECURITY: Use parameterized queries instead of string interpolation in SQL.",
                AzureBestPracticeUrl = "https://pub.dev/packages/sqflite",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "critical",
                    ["cwe"] = "CWE-89"
                }
            });
        }

        // Pattern: Biometric authentication
        if (sourceCode.Contains("local_auth") || sourceCode.Contains("LocalAuthentication") || sourceCode.Contains("authenticate("))
        {
            var match = Regex.Match(sourceCode, @"local_auth|LocalAuthentication|authenticate\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_BiometricAuth",
                Type = PatternType.Dart,
                Category = PatternCategory.Security,
                Implementation = "Biometric/local authentication",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Biometric authentication provides strong user verification. Always have a fallback method.",
                AzureBestPracticeUrl = "https://pub.dev/packages/local_auth",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Error Handling Patterns

    private List<CodePattern> DetectErrorHandlingPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Try-catch blocks
        var tryCatchMatches = Regex.Matches(sourceCode, @"try\s*\{");
        if (tryCatchMatches.Count > 0)
        {
            var hasCatch = sourceCode.Contains("catch");
            var hasFinally = sourceCode.Contains("finally");
            var hasOnClause = Regex.IsMatch(sourceCode, @"on\s+\w+Exception");

            patterns.Add(new CodePattern
            {
                Name = "Dart_TryCatch",
                Type = PatternType.Dart,
                Category = PatternCategory.Reliability,
                Implementation = $"try-catch (catch: {hasCatch}, finally: {hasFinally}, typed: {hasOnClause})",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = GetLineNumber(sourceCode, tryCatchMatches[0].Index),
                Content = GetCodeSnippet(lines, GetLineNumber(sourceCode, tryCatchMatches[0].Index), 5),
                Confidence = 0.90f,
                BestPractice = "Use typed catch clauses (on SpecificException) when possible. Always log or handle errors.",
                AzureBestPracticeUrl = "https://dart.dev/language/error-handling",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["try_count"] = tryCatchMatches.Count,
                    ["has_typed_catch"] = hasOnClause,
                    ["has_finally"] = hasFinally
                }
            });
        }

        // Anti-pattern: Empty catch blocks
        if (Regex.IsMatch(sourceCode, @"catch\s*\([^)]*\)\s*\{\s*\}"))
        {
            var match = Regex.Match(sourceCode, @"catch\s*\([^)]*\)\s*\{\s*\}");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_EmptyCatch_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.Reliability,
                Implementation = "Empty catch block",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.95f,
                BestPractice = "ANTI-PATTERN: Never swallow exceptions silently. At minimum, log the error.",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/usage#avoid-catches-without-on-clauses",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "high"
                }
            });
        }

        // Pattern: Custom exception classes
        if (Regex.IsMatch(sourceCode, @"class\s+\w+Exception\s+(extends|implements)"))
        {
            var match = Regex.Match(sourceCode, @"class\s+\w+Exception\s+(extends|implements)");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_CustomException",
                Type = PatternType.Dart,
                Category = PatternCategory.Reliability,
                Implementation = "Custom exception class",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Custom exceptions provide clear error semantics. Include meaningful messages and error codes.",
                AzureBestPracticeUrl = "https://dart.dev/language/error-handling#throw",
                Context = context
            });
        }

        // Pattern: Result type pattern (Either/Result)
        if (Regex.IsMatch(sourceCode, @"Either<|Result<|sealed\s+class.*Result"))
        {
            var match = Regex.Match(sourceCode, @"Either<|Result<|sealed\s+class.*Result");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_ResultType",
                Type = PatternType.Dart,
                Category = PatternCategory.Reliability,
                Implementation = "Result/Either type pattern",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Result types make error handling explicit. Prefer over throwing exceptions for expected failures.",
                AzureBestPracticeUrl = "https://pub.dev/packages/fpdart",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Code Quality Patterns

    private List<CodePattern> DetectCodeQualityPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Extension methods
        if (Regex.IsMatch(sourceCode, @"extension\s+\w+\s+on\s+\w+"))
        {
            var match = Regex.Match(sourceCode, @"extension\s+\w+\s+on\s+\w+");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_ExtensionMethod",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Extension method",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Extensions add functionality to existing types without modifying them. Keep them focused and well-documented.",
                AzureBestPracticeUrl = "https://dart.dev/language/extension-methods",
                Context = context
            });
        }

        // Pattern: Mixin usage
        if (Regex.IsMatch(sourceCode, @"mixin\s+\w+|with\s+\w+"))
        {
            var match = Regex.Match(sourceCode, @"mixin\s+\w+|with\s+\w+");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_Mixin",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Mixin composition",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Mixins enable code reuse without inheritance. Use 'mixin on' to constrain usage.",
                AzureBestPracticeUrl = "https://dart.dev/language/mixins",
                Context = context
            });
        }

        // Pattern: Factory constructor
        if (Regex.IsMatch(sourceCode, @"factory\s+\w+"))
        {
            var match = Regex.Match(sourceCode, @"factory\s+\w+");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_FactoryConstructor",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Factory constructor",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Factory constructors enable returning cached instances or subtypes. Use for singletons or complex initialization.",
                AzureBestPracticeUrl = "https://dart.dev/language/constructors#factory-constructors",
                Context = context
            });
        }

        // Pattern: Sealed class (Dart 3)
        if (Regex.IsMatch(sourceCode, @"sealed\s+class"))
        {
            var match = Regex.Match(sourceCode, @"sealed\s+class");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_SealedClass",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Sealed class hierarchy",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "Sealed classes enable exhaustive switch expressions. Use for state classes and algebraic data types.",
                AzureBestPracticeUrl = "https://dart.dev/language/class-modifiers#sealed",
                Context = context
            });
        }

        // Pattern: Records (Dart 3)
        if (Regex.IsMatch(sourceCode, @"\(\w+,\s*\w+\)|record\s+\w+"))
        {
            var match = Regex.Match(sourceCode, @"\(\w+,\s*\w+\)|record\s+\w+");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_Record",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Record type",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.85f,
                BestPractice = "Records provide immutable, value-based data structures. Great for returning multiple values.",
                AzureBestPracticeUrl = "https://dart.dev/language/records",
                Context = context
            });
        }

        // Anti-pattern: print() in production code
        var printCount = Regex.Matches(sourceCode, @"\bprint\s*\(").Count;
        if (printCount > 5 && !filePath.Contains("test"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Dart_PrintStatements_AntiPattern",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = $"Excessive print statements ({printCount})",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = $"Found {printCount} print() statements",
                Confidence = 0.80f,
                BestPractice = "ANTI-PATTERN: Use proper logging (logger package) instead of print() in production code.",
                AzureBestPracticeUrl = "https://pub.dev/packages/logger",
                Context = context,
                Metadata = new Dictionary<string, object>
                {
                    ["is_anti_pattern"] = true,
                    ["severity"] = "low",
                    ["print_count"] = printCount
                }
            });
        }

        // Pattern: @override annotation
        var overrideCount = Regex.Matches(sourceCode, @"@override").Count;
        if (overrideCount > 0)
        {
            patterns.Add(new CodePattern
            {
                Name = "Dart_OverrideAnnotation",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = $"@override annotation ({overrideCount} usages)",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = $"Found {overrideCount} @override annotations",
                Confidence = 0.95f,
                BestPractice = "Always use @override when overriding methods. This catches errors if parent method signature changes.",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/usage#do-annotate-overridden-methods",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Collection Patterns

    private List<CodePattern> DetectCollectionPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Collection literals
        if (Regex.IsMatch(sourceCode, @"<\w+>\[\]|<\w+,\s*\w+>\{\}|<\w+>\{\}"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Dart_CollectionLiterals",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Typed collection literals",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Typed collection literals in use",
                Confidence = 0.90f,
                BestPractice = "Use collection literals with type inference: var list = <String>[] instead of new List<String>()",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/usage#do-use-collection-literals-when-possible",
                Context = context
            });
        }

        // Pattern: Spread operator
        if (Regex.IsMatch(sourceCode, @"\.\.\.\w+|\.\.\.\?"))
        {
            var match = Regex.Match(sourceCode, @"\.\.\.\w+|\.\.\.\?");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_SpreadOperator",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Spread operator (...)",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.90f,
                BestPractice = "Use spread operator for combining collections. Use ...? for nullable collections.",
                AzureBestPracticeUrl = "https://dart.dev/language/collections#spread-operators",
                Context = context
            });
        }

        // Pattern: Collection if/for
        if (Regex.IsMatch(sourceCode, @"\[.*if\s*\(.*\).*\]|\[.*for\s*\(.*\).*\]"))
        {
            var match = Regex.Match(sourceCode, @"\[.*if\s*\(|.*for\s*\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_CollectionIfFor",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Collection if/for expressions",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 3),
                Confidence = 0.90f,
                BestPractice = "Collection if/for enables declarative collection building. More readable than imperative loops.",
                AzureBestPracticeUrl = "https://dart.dev/language/collections#control-flow-operators",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Class Patterns

    private List<CodePattern> DetectClassPatterns(string sourceCode, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();
        var lines = sourceCode.Split('\n');

        // Pattern: Singleton implementation
        if (Regex.IsMatch(sourceCode, @"static\s+final\s+\w+\s+_instance|factory\s+\w+\s*\(\s*\)\s*=>\s*_instance"))
        {
            var match = Regex.Match(sourceCode, @"static\s+final\s+\w+\s+_instance");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_Singleton",
                Type = PatternType.Dart,
                Category = PatternCategory.DesignImplementation,
                Implementation = "Singleton pattern",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.90f,
                BestPractice = "Use singletons sparingly. Consider dependency injection for better testability.",
                AzureBestPracticeUrl = "https://dart.dev/language/constructors#factory-constructors",
                Context = context
            });
        }

        // Pattern: Equatable/value equality
        if (sourceCode.Contains("Equatable") || Regex.IsMatch(sourceCode, @"@override\s+bool\s+operator\s*=="))
        {
            patterns.Add(new CodePattern
            {
                Name = "Dart_ValueEquality",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "Value equality implementation",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Equatable or custom == operator",
                Confidence = 0.90f,
                BestPractice = "Use Equatable package or implement == and hashCode together. Essential for state management.",
                AzureBestPracticeUrl = "https://pub.dev/packages/equatable",
                Context = context
            });
        }

        // Pattern: Copyable/immutable pattern
        if (Regex.IsMatch(sourceCode, @"copyWith\s*\("))
        {
            var match = Regex.Match(sourceCode, @"copyWith\s*\(");
            var lineNumber = GetLineNumber(sourceCode, match.Index);
            patterns.Add(new CodePattern
            {
                Name = "Dart_CopyWith",
                Type = PatternType.Dart,
                Category = PatternCategory.DesignImplementation,
                Implementation = "copyWith pattern for immutability",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = lineNumber,
                Content = GetCodeSnippet(lines, lineNumber, 5),
                Confidence = 0.95f,
                BestPractice = "copyWith enables immutable state updates. Use freezed package to auto-generate.",
                AzureBestPracticeUrl = "https://pub.dev/packages/freezed",
                Context = context
            });
        }

        // Pattern: toString override
        if (Regex.IsMatch(sourceCode, @"@override\s+String\s+toString\s*\(\)"))
        {
            patterns.Add(new CodePattern
            {
                Name = "Dart_ToStringOverride",
                Type = PatternType.Dart,
                Category = PatternCategory.CodeQuality,
                Implementation = "toString override",
                Language = "Dart",
                FilePath = filePath,
                LineNumber = 1,
                Content = "Custom toString implementation",
                Confidence = 0.90f,
                BestPractice = "Override toString for better debugging. Include relevant property values.",
                AzureBestPracticeUrl = "https://dart.dev/effective-dart/design#do-override-tostring",
                Context = context
            });
        }

        return patterns;
    }

    #endregion

    #region Helper Methods

    private int GetLineNumber(string sourceCode, int charIndex)
    {
        if (charIndex < 0 || charIndex >= sourceCode.Length)
            return 1;

        return sourceCode.Substring(0, charIndex).Count(c => c == '\n') + 1;
    }

    private string GetCodeSnippet(string[] lines, int lineNumber, int contextLines)
    {
        var startLine = Math.Max(0, lineNumber - 1 - contextLines);
        var endLine = Math.Min(lines.Length - 1, lineNumber - 1 + contextLines);

        var snippetLines = lines.Skip(startLine).Take(endLine - startLine + 1);
        return string.Join("\n", snippetLines);
    }

    #endregion
}

