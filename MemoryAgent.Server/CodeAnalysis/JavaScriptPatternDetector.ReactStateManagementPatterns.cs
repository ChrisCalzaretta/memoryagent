using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// React State Management Patterns
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region React State Management Patterns

    private List<CodePattern> DetectReactStatePatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // useState Hook
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"const\s+\[([^,]+),\s*set([^\]]+)\]\s*=\s*useState");
            if (match.Success)
            {
                var stateName = match.Groups[1].Value.Trim();
                patterns.Add(CreatePattern(
                    name: $"React_useState_{stateName}",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "React useState Hook",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use useState for local component state, prefer functional updates for state based on previous state",
                    azureUrl: "https://react.dev/reference/react/useState",
                    context: context,
                    language: language
                ));
            }
        }

        // useReducer Hook
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"const\s+\[([^,]+),\s*dispatch\]\s*=\s*useReducer");
            if (match.Success)
            {
                patterns.Add(CreatePattern(
                    name: "React_useReducer",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "React useReducer Hook",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use useReducer for complex state logic with multiple sub-values, keep reducers pure",
                    azureUrl: "https://react.dev/reference/react/useReducer",
                    context: context,
                    language: language
                ));
            }
        }

        // useContext Hook
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useContext\(|createContext\("))
            {
                patterns.Add(CreatePattern(
                    name: "React_useContext",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "React Context API",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use Context API for global state without prop drilling, combine with useReducer for complex state",
                    azureUrl: "https://react.dev/reference/react/useContext",
                    context: context,
                    language: language
                ));
            }
        }

        // useEffect for side effects
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useEffect\("))
            {
                patterns.Add(CreatePattern(
                    name: "React_useEffect",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "React useEffect Hook",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use useEffect for state synchronization and side effects, always specify dependencies",
                    azureUrl: "https://react.dev/reference/react/useEffect",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
