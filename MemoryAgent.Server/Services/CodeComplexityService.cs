using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Caching.Memory;
using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for analyzing code complexity metrics with caching
/// </summary>
public class CodeComplexityService : ICodeComplexityService
{
    private readonly IPathTranslationService _pathTranslation;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CodeComplexityService> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public CodeComplexityService(
        IPathTranslationService pathTranslation,
        IMemoryCache cache,
        ILogger<CodeComplexityService> logger)
    {
        _pathTranslation = pathTranslation;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CodeComplexityResult> AnalyzeFileAsync(
        string filePath,
        string? methodName = null,
        CancellationToken cancellationToken = default)
    {
        var containerPath = _pathTranslation.TranslateToContainerPath(filePath);
        var cacheKey = $"complexity_{containerPath}_{methodName ?? "all"}";

        // Try cache first
        if (_cache.TryGetValue(cacheKey, out CodeComplexityResult? cachedResult))
        {
            _logger.LogDebug("Cache hit for complexity analysis: {FilePath}", containerPath);
            return cachedResult!;
        }

        _logger.LogInformation("Analyzing code complexity for: {FilePath}", containerPath);

        var result = new CodeComplexityResult
        {
            FilePath = containerPath,
            MethodName = methodName
        };

        try
        {
            if (!File.Exists(containerPath))
            {
                result.Errors.Add($"File not found: {containerPath}");
                return result;
            }

            var code = await File.ReadAllTextAsync(containerPath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Find all methods in the file
            var methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .ToList();

            // Filter by method name if specified
            if (!string.IsNullOrEmpty(methodName))
            {
                methods = methods.Where(m => m.Identifier.Text == methodName).ToList();
            }

            // Analyze each method
            foreach (var method in methods)
            {
                var complexity = AnalyzeMethod(method);
                result.Methods.Add(complexity);
            }

            // Calculate summary
            if (result.Methods.Any())
            {
                result.Summary = CalculateSummary(result.Methods);
            }

            result.Success = true;

            // Cache the result
            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                SlidingExpiration = TimeSpan.FromMinutes(2)
            });

            _logger.LogInformation(
                "Complexity analysis completed for {FilePath}: {Methods} methods analyzed, Overall Grade: {Grade}",
                containerPath, result.Methods.Count, result.Summary.OverallGrade);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing code complexity for: {FilePath}", containerPath);
            result.Errors.Add($"Error: {ex.Message}");
        }

        return result;
    }

    private MethodComplexity AnalyzeMethod(MethodDeclarationSyntax method)
    {
        var className = method.FirstAncestorOrSelf<ClassDeclarationSyntax>()?.Identifier.Text ?? "Unknown";
        var lineSpan = method.GetLocation().GetLineSpan();

        var cyclomaticComplexity = ComplexityAnalyzer.CalculateCyclomaticComplexity(method);
        var cognitiveComplexity = ComplexityAnalyzer.CalculateCognitiveComplexity(method);
        var linesOfCode = ComplexityAnalyzer.CalculateLinesOfCode(method);
        var maxNesting = ComplexityAnalyzer.CalculateMaxNesting(method);
        var codeSmells = ComplexityAnalyzer.DetectCodeSmells(method);
        var exceptionTypes = ComplexityAnalyzer.ExtractExceptionTypes(method);
        var dbCalls = ComplexityAnalyzer.CountDatabaseCalls(method);
        var hasHttpCalls = ComplexityAnalyzer.HasHttpCalls(method);
        var hasLogging = ComplexityAnalyzer.HasLogging(method);
        var isPublic = ComplexityAnalyzer.IsPublicApi(method);

        var complexity = new MethodComplexity
        {
            MethodName = method.Identifier.Text,
            ClassName = className,
            StartLine = lineSpan.StartLinePosition.Line + 1,
            EndLine = lineSpan.EndLinePosition.Line + 1,
            LinesOfCode = linesOfCode,
            CyclomaticComplexity = cyclomaticComplexity,
            CognitiveComplexity = cognitiveComplexity,
            MaxNestingDepth = maxNesting,
            ParameterCount = method.ParameterList.Parameters.Count,
            DatabaseCalls = dbCalls,
            HasHttpCalls = hasHttpCalls,
            HasLogging = hasLogging,
            IsPublic = isPublic,
            IsAsync = method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
            CodeSmells = codeSmells,
            ExceptionTypes = exceptionTypes
        };

        // Calculate grade and recommendations
        AssignGradeAndRecommendations(complexity);

        return complexity;
    }

    private void AssignGradeAndRecommendations(MethodComplexity complexity)
    {
        var score = 100;
        var recommendations = new List<string>();

        // Deduct points for various complexity factors
        if (complexity.CyclomaticComplexity > 15)
        {
            score -= 30;
            recommendations.Add($"🔴 Very high cyclomatic complexity ({complexity.CyclomaticComplexity}). Consider refactoring.");
        }
        else if (complexity.CyclomaticComplexity > 10)
        {
            score -= 20;
            recommendations.Add($"⚠️ High cyclomatic complexity ({complexity.CyclomaticComplexity}). Consider simplifying.");
        }
        else if (complexity.CyclomaticComplexity > 7)
        {
            score -= 10;
            recommendations.Add($"📌 Moderate cyclomatic complexity ({complexity.CyclomaticComplexity}).");
        }

        if (complexity.CognitiveComplexity > 15)
        {
            score -= 25;
            recommendations.Add($"🔴 Very high cognitive complexity ({complexity.CognitiveComplexity}). Hard to understand.");
        }
        else if (complexity.CognitiveComplexity > 10)
        {
            score -= 15;
            recommendations.Add($"⚠️ High cognitive complexity ({complexity.CognitiveComplexity}). Consider simplifying logic.");
        }

        if (complexity.LinesOfCode > 100)
        {
            score -= 25;
            recommendations.Add($"🔴 Method too long ({complexity.LinesOfCode} LOC). Split into smaller methods.");
        }
        else if (complexity.LinesOfCode > 50)
        {
            score -= 15;
            recommendations.Add($"⚠️ Long method ({complexity.LinesOfCode} LOC). Consider extracting logic.");
        }

        if (complexity.MaxNestingDepth > 4)
        {
            score -= 20;
            recommendations.Add($"🔴 Deep nesting ({complexity.MaxNestingDepth} levels). Use early returns or extract methods.");
        }
        else if (complexity.MaxNestingDepth > 3)
        {
            score -= 10;
            recommendations.Add($"⚠️ Moderate nesting ({complexity.MaxNestingDepth} levels). Consider flattening.");
        }

        if (complexity.ParameterCount > 5)
        {
            score -= 15;
            recommendations.Add($"⚠️ Too many parameters ({complexity.ParameterCount}). Use parameter object or builder pattern.");
        }

        // Code smell penalties
        if (complexity.CodeSmells.Contains("async_without_error_handling"))
        {
            score -= 20;
            recommendations.Add("🔴 Async method without try/catch. Add error handling.");
        }

        if (complexity.DatabaseCalls > 5)
        {
            score -= 10;
            recommendations.Add($"⚠️ Multiple database calls ({complexity.DatabaseCalls}). Consider batch operations.");
        }

        // Bonuses for good practices
        if (complexity.HasLogging && complexity.LinesOfCode > 20)
        {
            score += 5; // Good practice
        }

        if (complexity.IsAsync && complexity.ExceptionTypes.Any())
        {
            score += 5; // Good error handling in async
        }

        // Assign grade
        complexity.Grade = score switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };

        complexity.Recommendations = recommendations;
    }

    private FileComplexitySummary CalculateSummary(List<MethodComplexity> methods)
    {
        var summary = new FileComplexitySummary
        {
            TotalMethods = methods.Count,
            AverageCyclomaticComplexity = (int)methods.Average(m => m.CyclomaticComplexity),
            AverageCognitiveComplexity = (int)methods.Average(m => m.CognitiveComplexity),
            AverageLinesOfCode = (int)methods.Average(m => m.LinesOfCode),
            MaxCyclomaticComplexity = methods.Max(m => m.CyclomaticComplexity),
            MaxCognitiveComplexity = methods.Max(m => m.CognitiveComplexity),
            MethodsWithHighComplexity = methods.Count(m => m.CyclomaticComplexity > 10),
            MethodsWithCodeSmells = methods.Count(m => m.CodeSmells.Any())
        };

        // File-level recommendations
        var fileRecommendations = new List<string>();

        if (summary.AverageCyclomaticComplexity > 10)
        {
            fileRecommendations.Add($"⚠️ High average cyclomatic complexity ({summary.AverageCyclomaticComplexity}). Review all methods.");
        }

        if (summary.MethodsWithHighComplexity > summary.TotalMethods * 0.3)
        {
            fileRecommendations.Add($"🔴 {summary.MethodsWithHighComplexity}/{summary.TotalMethods} methods have high complexity. Refactor needed.");
        }

        if (summary.MethodsWithCodeSmells > 0)
        {
            fileRecommendations.Add($"📌 {summary.MethodsWithCodeSmells} methods have code smells. Review and fix.");
        }

        if (methods.Count > 20)
        {
            fileRecommendations.Add($"⚠️ Large file with {methods.Count} methods. Consider splitting into multiple classes.");
        }

        // Calculate overall grade
        var avgGrade = methods.Average(m => m.Grade switch
        {
            "A" => 95,
            "B" => 85,
            "C" => 75,
            "D" => 65,
            "F" => 50,
            _ => 50
        });

        summary.OverallGrade = avgGrade switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            >= 60 => "D",
            _ => "F"
        };

        summary.FileRecommendations = fileRecommendations;

        return summary;
    }
}



