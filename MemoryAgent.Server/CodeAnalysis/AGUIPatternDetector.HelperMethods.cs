using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Helper Methods
/// </summary>
public partial class AGUIPatternDetector
{
    #region Helper Methods

    private int GetLineNumber(SyntaxNode root, SyntaxNode node, string sourceCode)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        return lineSpan.StartLinePosition.Line + 1;
    }

    private string GetContextAroundNode(SyntaxNode node, string sourceCode, int contextLines)
    {
        var lineSpan = node.GetLocation().GetLineSpan();
        var startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        var endLine = Math.Min(sourceCode.Split('\n').Length - 1, lineSpan.EndLinePosition.Line + contextLines);

        var lines = sourceCode.Split('\n');
        var relevantLines = lines.Skip(startLine).Take(endLine - startLine + 1);

        return string.Join("\n", relevantLines);
    }

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
        float confidence,
        Dictionary<string, object>? metadata = null,
        bool isPositive = true)
    {
        return new CodePattern
        {
            Id = $"{name}_{filePath}_{lineNumber}",
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "C#",
            FilePath = filePath,
            LineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Confidence = confidence,
            IsPositivePattern = isPositive,
            Context = context ?? "",
            Metadata = metadata ?? new Dictionary<string, object>(),
            DetectedAt = DateTime.UtcNow
        };
    }

    
    #endregion
}
