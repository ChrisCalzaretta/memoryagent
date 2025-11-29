using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Browser Storage Patterns
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region Browser Storage Patterns

    private List<CodePattern> DetectBrowserStoragePatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // localStorage
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"localStorage\.setItem|localStorage\.getItem|localStorage\.removeItem"))
            {
                patterns.Add(CreatePattern(
                    name: "Browser_LocalStorage",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "localStorage",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "WARNING: Never store sensitive data in localStorage (XSS risk), use for user preferences only",
                    azureUrl: "https://developer.mozilla.org/en-US/docs/Web/API/Window/localStorage",
                    context: context,
                    language: language
                ));
            }
        }

        // sessionStorage
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"sessionStorage\.setItem|sessionStorage\.getItem"))
            {
                patterns.Add(CreatePattern(
                    name: "Browser_SessionStorage",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "sessionStorage",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use sessionStorage for temporary tab-specific state, data cleared when tab closes",
                    azureUrl: "https://developer.mozilla.org/en-US/docs/Web/API/Window/sessionStorage",
                    context: context,
                    language: language
                ));
            }
        }

        // IndexedDB
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"indexedDB\.open\(|\.createObjectStore\(|\.transaction\("))
            {
                patterns.Add(CreatePattern(
                    name: "Browser_IndexedDB",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "IndexedDB",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use IndexedDB for large structured client-side data (>5MB), implement versioning for schema changes",
                    azureUrl: "https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API",
                    context: context,
                    language: language
                ));
            }
        }

        // Cookies (document.cookie)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"document\.cookie\s*="))
            {
                patterns.Add(CreatePattern(
                    name: "Browser_Cookies",
                    type: PatternType.StateManagement,
                    category: PatternCategory.Security,
                    implementation: "document.cookie",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "WARNING: Always set Secure, HttpOnly, SameSite attributes on cookies",
                    azureUrl: "https://developer.mozilla.org/en-US/docs/Web/API/Document/cookie",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
