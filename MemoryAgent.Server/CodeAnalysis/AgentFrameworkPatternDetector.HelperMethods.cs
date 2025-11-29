using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Helper Methods
/// </summary>
public partial class AgentFrameworkPatternDetector
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
        float confidence,
        Dictionary<string, object>? metadata = null,
        bool isPositivePattern = true)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            Language = "csharp",
            FilePath = filePath,
            LineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            Context = context ?? "default",
            Confidence = confidence,
            IsPositivePattern = isPositivePattern,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    private int GetLineNumber(SyntaxNode root, SyntaxNode node, string sourceCode)
    {
        var span = node.GetLocation().SourceSpan;
        var lineSpan = root.SyntaxTree.GetLineSpan(span);
        return lineSpan.StartLinePosition.Line + 1;
    }

    private string GetContextAroundNode(SyntaxNode node, string sourceCode, int contextLines)
    {
        var lines = sourceCode.Split('\n');
        var span = node.GetLocation().SourceSpan;
        var lineSpan = node.SyntaxTree.GetLineSpan(span);
        
        var startLine = Math.Max(0, lineSpan.StartLinePosition.Line - contextLines);
        var endLine = Math.Min(lines.Length - 1, lineSpan.EndLinePosition.Line + contextLines);
        
        return string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));
    }

    
    #endregion
}
