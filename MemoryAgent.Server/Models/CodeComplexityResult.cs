namespace MemoryAgent.Server.Models;

/// <summary>
/// Result of code complexity analysis
/// </summary>
public class CodeComplexityResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? MethodName { get; set; }
    public List<MethodComplexity> Methods { get; set; } = new();
    public FileComplexitySummary Summary { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Complexity metrics for a single method
/// </summary>
public class MethodComplexity
{
    public string MethodName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int LinesOfCode { get; set; }
    public int CyclomaticComplexity { get; set; }
    public int CognitiveComplexity { get; set; }
    public int MaxNestingDepth { get; set; }
    public int ParameterCount { get; set; }
    public int DatabaseCalls { get; set; }
    public bool HasHttpCalls { get; set; }
    public bool HasLogging { get; set; }
    public bool IsPublic { get; set; }
    public bool IsAsync { get; set; }
    public List<string> CodeSmells { get; set; } = new();
    public List<string> ExceptionTypes { get; set; } = new();
    public string Grade { get; set; } = "A"; // A-F based on complexity
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Summary of file-level complexity
/// </summary>
public class FileComplexitySummary
{
    public int TotalMethods { get; set; }
    public int AverageCyclomaticComplexity { get; set; }
    public int AverageCognitiveComplexity { get; set; }
    public int AverageLinesOfCode { get; set; }
    public int MaxCyclomaticComplexity { get; set; }
    public int MaxCognitiveComplexity { get; set; }
    public int MethodsWithHighComplexity { get; set; } // > 10
    public int MethodsWithCodeSmells { get; set; }
    public string OverallGrade { get; set; } = "A";
    public List<string> FileRecommendations { get; set; } = new();
}





















