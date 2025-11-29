using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Helper Methods
/// </summary>
public partial class AIAgentPatternDetector
{
    #region Helper Methods

    private bool ContainsInstructionKeywords(string text)
    {
        var keywords = new[] { 
            "You are", "Act as", "Your role", "You must", "Never", 
            "Always", "Follow these", "Instructions:", "Guidelines:",
            "helpful assistant", "expert in"
        };
        
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

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

        return string.Join('\n', relevantLines);
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
        bool isPositive = true,
        Dictionary<string, object>? metadata = null)
    {
        return new CodePattern
        {
            Name = name,
            Type = type,
            Category = category,
            Implementation = implementation,
            FilePath = filePath,
            LineNumber = lineNumber,
            Content = content,
            BestPractice = bestPractice,
            AzureBestPracticeUrl = azureUrl,
            DetectedAt = DateTime.UtcNow,
            Context = context ?? "",
            Confidence = confidence,
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }

    
    #endregion
}
