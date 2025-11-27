using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Analyzes Python code complexity metrics
/// </summary>
public static class PythonComplexityAnalyzer
{
    /// <summary>
    /// Calculate cyclomatic complexity for Python method
    /// </summary>
    public static int CalculateCyclomaticComplexity(List<string> methodLines)
    {
        var complexity = 1; // Base complexity

        foreach (var line in methodLines)
        {
            var trimmed = line.Trim();
            
            // Decision points
            if (trimmed.StartsWith("if ") || trimmed.Contains(" if "))
                complexity++;
            if (trimmed.StartsWith("elif "))
                complexity++;
            if (trimmed.StartsWith("while "))
                complexity++;
            if (trimmed.StartsWith("for "))
                complexity++;
            if (trimmed.StartsWith("except "))
                complexity++;
            if (trimmed.Contains(" and ") || trimmed.Contains(" or "))
                complexity++;
        }

        return complexity;
    }

    /// <summary>
    /// Calculate cognitive complexity (how hard to understand)
    /// </summary>
    public static int CalculateCognitiveComplexity(List<string> methodLines)
    {
        var complexity = 0;
        var nestingLevel = 0;
        var previousIndent = 0;

        foreach (var line in methodLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var indent = line.TakeWhile(c => c == ' ' || c == '\t').Count();
            var trimmed = line.Trim();

            // Adjust nesting level based on indentation
            if (indent > previousIndent)
            {
                nestingLevel++;
            }
            else if (indent < previousIndent)
            {
                nestingLevel = Math.Max(0, nestingLevel - 1);
            }

            // Increment complexity for decision points
            if (trimmed.StartsWith("if ") || trimmed.StartsWith("elif ") ||
                trimmed.StartsWith("while ") || trimmed.StartsWith("for "))
            {
                complexity += 1 + nestingLevel;
            }

            previousIndent = indent;
        }

        return complexity;
    }

    /// <summary>
    /// Calculate lines of code (excluding blank lines and comments)
    /// </summary>
    public static int CalculateLinesOfCode(List<string> methodLines)
    {
        return methodLines
            .Select(l => l.Trim())
            .Count(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"));
    }

    /// <summary>
    /// Detect code smells in Python method
    /// </summary>
    public static List<string> DetectCodeSmells(List<string> methodLines, int parameterCount)
    {
        var smells = new List<string>();

        // Long method
        var loc = CalculateLinesOfCode(methodLines);
        if (loc > 50)
        {
            smells.Add("long_method");
        }

        // Too many parameters
        if (parameterCount > 5)
        {
            smells.Add("too_many_parameters");
        }

        // High complexity
        var complexity = CalculateCyclomaticComplexity(methodLines);
        if (complexity > 10)
        {
            smells.Add("high_complexity");
        }

        // Deep nesting
        var maxNesting = CalculateMaxNesting(methodLines);
        if (maxNesting > 3)
        {
            smells.Add("deep_nesting");
        }

        // Missing error handling in async methods
        var isAsync = methodLines.Any(l => l.Contains("async def") || l.Contains("await "));
        var hasTryExcept = methodLines.Any(l => l.Trim().StartsWith("try:") || l.Trim().StartsWith("except"));
        if (isAsync && !hasTryExcept)
        {
            smells.Add("async_without_error_handling");
        }

        return smells;
    }

    /// <summary>
    /// Calculate maximum nesting depth
    /// </summary>
    public static int CalculateMaxNesting(List<string> methodLines)
    {
        var maxDepth = 0;
        var currentDepth = 0;
        var previousIndent = 0;

        foreach (var line in methodLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var indent = line.TakeWhile(c => c == ' ' || c == '\t').Count();

            if (indent > previousIndent)
            {
                currentDepth++;
                if (currentDepth > maxDepth)
                {
                    maxDepth = currentDepth;
                }
            }
            else if (indent < previousIndent)
            {
                currentDepth = Math.Max(0, currentDepth - 1);
            }

            previousIndent = indent;
        }

        return maxDepth;
    }

    /// <summary>
    /// Count database/ORM access patterns
    /// </summary>
    public static int CountDatabaseCalls(List<string> methodLines)
    {
        var dbPatterns = new[]
        {
            ".execute(", ".fetchall(", ".fetchone(", ".commit(",
            ".query(", ".filter(", ".all(", ".first()",
            ".save(", ".update(", ".delete(", ".create(",
            "Session(", "session.", "db.", "models."
        };

        return methodLines.Count(line => 
            dbPatterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Extract exception types that can be raised
    /// </summary>
    public static List<string> ExtractExceptionTypes(List<string> methodLines)
    {
        var exceptions = new HashSet<string>();
        var raisePattern = @"raise\s+(\w+)";

        foreach (var line in methodLines)
        {
            var matches = Regex.Matches(line, raisePattern);
            foreach (Match match in matches)
            {
                exceptions.Add(match.Groups[1].Value);
            }
        }

        return exceptions.ToList();
    }

    /// <summary>
    /// Detect if method makes HTTP calls
    /// </summary>
    public static bool HasHttpCalls(List<string> methodLines)
    {
        var httpPatterns = new[] 
        { 
            "requests.", "httpx.", "aiohttp.", "urllib.", 
            ".get(", ".post(", ".put(", ".delete(", ".patch(",
            "http.", "https."
        };

        return methodLines.Any(line => 
            httpPatterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Detect if method has logging
    /// </summary>
    public static bool HasLogging(List<string> methodLines)
    {
        var loggingPatterns = new[] 
        { 
            "logger.", "logging.", "log.", 
            ".info(", ".error(", ".warning(", ".debug(",
            "print("  // Simple logging
        };

        return methodLines.Any(line => 
            loggingPatterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Detect if this is a test method
    /// </summary>
    public static bool IsTestMethod(string methodName, List<string> decorators)
    {
        // Test method naming conventions
        if (methodName.StartsWith("test_"))
            return true;

        // Test decorators
        var testDecorators = new[] { "@pytest", "@unittest", "@test" };
        return decorators.Any(d => testDecorators.Any(td => d.Contains(td, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Detect test framework
    /// </summary>
    public static string DetectTestFramework(List<string> decorators, List<string> imports)
    {
        if (decorators.Any(d => d.Contains("@pytest")) || imports.Any(i => i.Contains("pytest")))
            return "pytest";
        if (decorators.Any(d => d.Contains("@unittest")) || imports.Any(i => i.Contains("unittest")))
            return "unittest";
        
        return "unknown";
    }

    /// <summary>
    /// Check if method is async
    /// </summary>
    public static bool IsAsync(List<string> methodLines)
    {
        return methodLines.Any(l => l.Contains("async def") || l.Contains("await "));
    }

    /// <summary>
    /// Count assertions in test method
    /// </summary>
    public static int CountAssertions(List<string> methodLines)
    {
        var assertPatterns = new[] 
        { 
            "assert ", "self.assert", "pytest.assert", 
            ".should", ".expect" 
        };

        return methodLines.Count(line => 
            assertPatterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
    }
}




