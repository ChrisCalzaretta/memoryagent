using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Redux Patterns
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region Redux Patterns

    private List<CodePattern> DetectReduxPatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // Redux createStore
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"createStore\(|configureStore\("))
            {
                patterns.Add(CreatePattern(
                    name: "Redux_Store",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Redux Store",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use Redux Toolkit configureStore for modern Redux, keep state normalized and immutable",
                    azureUrl: "https://redux.js.org/introduction/getting-started",
                    context: context,
                    language: language
                ));
            }
        }

        // useSelector / useDispatch
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useSelector\(|useDispatch\(\)"))
            {
                patterns.Add(CreatePattern(
                    name: "Redux_Hooks",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Redux React Hooks",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use useSelector for reading state, useDispatch for dispatching actions, use memoized selectors",
                    azureUrl: "https://react-redux.js.org/api/hooks",
                    context: context,
                    language: language
                ));
            }
        }

        // Redux Toolkit createSlice
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"createSlice\("))
            {
                patterns.Add(CreatePattern(
                    name: "ReduxToolkit_Slice",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Redux Toolkit Slice",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use createSlice for Redux state with reducers and actions, use Immer for immutable updates",
                    azureUrl: "https://redux-toolkit.js.org/api/createSlice",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
