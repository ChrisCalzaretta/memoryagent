using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Validation Patterns
/// </summary>
public partial class VBNetPatternDetector : IPatternDetector
{
    #region Validation Patterns

    private List<CodePattern> DetectValidationPatterns(string[] lines, string filePath, string? context)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Data Annotations
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("<Required") || lines[i].Contains("<StringLength") || 
                lines[i].Contains("<Range") || lines[i].Contains("<EmailAddress"))
            {
                var attrName = ExtractAttributeName(lines[i]);
                var pattern = CreatePattern(
                    name: $"DataAnnotation_{attrName}",
                    type: PatternType.Validation,
                    category: PatternCategory.Security,
                    implementation: "DataAnnotations",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Input validation using {attrName} attribute",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["validation_type"] = attrName;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Manual validation
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("String.IsNullOrEmpty") || lines[i].Contains("String.IsNullOrWhiteSpace"))
            {
                var pattern = CreatePattern(
                    name: "Manual_Validation",
                    type: PatternType.Validation,
                    category: PatternCategory.Security,
                    implementation: "ManualValidation",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Manual string validation",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Confidence = 0.6f;
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    
    #endregion
}
