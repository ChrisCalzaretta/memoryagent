using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Helper Methods
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Helper Methods

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        SyntaxNode node,
        string sourceCode,
        string bestPractice,
        string azureUrl,
        string? context)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var lineNumber = lineSpan.StartLinePosition.Line + 1;
        var endLineNumber = lineSpan.EndLinePosition.Line + 1;

        // Get context (surrounding code)
        var content = GetCodeContext(node, sourceCode, lineNumber, endLineNumber);

        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "C#",
            FilePath = filePath,
            LineNumber = lineNumber,
            EndLineNumber = endLineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default"
        };
    }

    private string GetCodeContext(SyntaxNode node, string sourceCode, int startLine, int endLine)
    {
        // Get surrounding context (up to 5 lines before/after)
        var lines = sourceCode.Split('\n');
        var contextStart = Math.Max(0, startLine - 6); // -1 for 0-based, -5 for context
        var contextEnd = Math.Min(lines.Length - 1, endLine + 4);

        var contextLines = lines.Skip(contextStart).Take(contextEnd - contextStart + 1);
        return string.Join("\n", contextLines).Trim();
    }


    #endregion
}
