using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Vue State Management
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region Vue State Management

    private List<CodePattern> DetectVueStatePatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // Vue Composition API - ref
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"const\s+\w+\s*=\s*ref\("))
            {
                patterns.Add(CreatePattern(
                    name: "Vue_Ref",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Vue ref (Composition API)",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use ref for reactive primitive values in Vue Composition API, access value with .value",
                    azureUrl: "https://vuejs.org/api/reactivity-core.html#ref",
                    context: context,
                    language: language
                ));
            }
        }

        // Vue Composition API - reactive
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"const\s+\w+\s*=\s*reactive\("))
            {
                patterns.Add(CreatePattern(
                    name: "Vue_Reactive",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Vue reactive (Composition API)",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use reactive for reactive objects in Vue, prefer ref for primitives",
                    azureUrl: "https://vuejs.org/api/reactivity-core.html#reactive",
                    context: context,
                    language: language
                ));
            }
        }

        // Vuex Store
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"new\s+Vuex\.Store|createStore\(\{") && sourceCode.Contains("vuex"))
            {
                patterns.Add(CreatePattern(
                    name: "Vuex_Store",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Vuex Store",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use Vuex for centralized state management in Vue, organize state into modules",
                    azureUrl: "https://vuex.vuejs.org/",
                    context: context,
                    language: language
                ));
            }
        }

        // Pinia Store
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"defineStore\(|usePinia\("))
            {
                patterns.Add(CreatePattern(
                    name: "Pinia_Store",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Pinia Store",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use Pinia for Vue 3 state management, simpler and more intuitive than Vuex",
                    azureUrl: "https://pinia.vuejs.org/",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
