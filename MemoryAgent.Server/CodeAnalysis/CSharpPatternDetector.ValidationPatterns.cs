using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Validation Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Validation Patterns




    private List<CodePattern> DetectValidationPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Data Annotations ([Required], [Range], [EmailAddress], etc.)
        var validationAttrs = root.DescendantNodes()
            .OfType<AttributeSyntax>()
            .Where(attr => attr.Name.ToString() is "Required" or "Range" or "EmailAddress" or 
                          "StringLength" or "RegularExpression" or "MinLength" or "MaxLength" or 
                          "Phone" or "CreditCard" or "Url");

        foreach (var attr in validationAttrs)
        {
            var pattern = CreatePattern(
                name: $"DataAnnotation_{attr.Name}",
                type: PatternType.Validation,
                category: PatternCategory.Security,
                implementation: "DataAnnotations",
                filePath: filePath,
                node: attr,
                sourceCode: sourceCode,
                bestPractice: $"Input validation using {attr.Name} attribute",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["validation_type"] = attr.Name.ToString();
            pattern.Metadata["library"] = "System.ComponentModel.DataAnnotations";
            patterns.Add(pattern);
        }

        // Pattern 2: FluentValidation
        var fluentValidators = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.BaseList?.Types.Any(t => t.ToString().Contains("AbstractValidator")) == true);

        foreach (var validator in fluentValidators)
        {
            var pattern = CreatePattern(
                name: validator.Identifier.Text,
                type: PatternType.Validation,
                category: PatternCategory.Security,
                implementation: "FluentValidation",
                filePath: filePath,
                node: validator,
                sourceCode: sourceCode,
                bestPractice: "Fluent validation for complex business rules",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "FluentValidation";
            patterns.Add(pattern);
        }

        // Pattern 3: Guard clauses
        var guardCalls = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("Guard.Against"));

        foreach (var guard in guardCalls)
        {
            var pattern = CreatePattern(
                name: "Guard_Clause",
                type: PatternType.Validation,
                category: PatternCategory.Security,
                implementation: "GuardClauses",
                filePath: filePath,
                node: guard,
                sourceCode: sourceCode,
                bestPractice: "Guard clauses for defensive programming",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "Ardalis.GuardClauses";
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
