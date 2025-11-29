using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Helper Methods
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region Helper Methods

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        int lineNumber,
        string content,
        string bestPractice,
        string azureUrl,
        string? context,
        string language)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = language,
            FilePath = filePath,
            LineNumber = lineNumber,
            EndLineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default"
        };
    }

    private string GetContext(string[] lines, int centerLine, int contextLines)
    {
        var start = Math.Max(0, centerLine - contextLines);
        var end = Math.Min(lines.Length - 1, centerLine + contextLines);
        
        var contextList = new List<string>();
        for (int i = start; i <= end; i++)
        {
            contextList.Add(lines[i]);
        }

        return string.Join("\n", contextList).Trim();
    }

    
    #endregion
}
