using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Plugin Versioning & Compatibility
/// </summary>
public partial class PluginArchitecturePatternDetector
{
    #region Plugin Versioning & Compatibility

    private List<CodePattern> DetectSemanticVersioning(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: [assembly: AssemblyVersion("...")]
        var attributes = root.DescendantNodes().OfType<AttributeSyntax>();
        foreach (var attribute in attributes)
        {
            if (attribute.Name.ToString().Contains("AssemblyVersion") ||
                attribute.Name.ToString().Contains("AssemblyFileVersion"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_SemanticVersioning",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginVersioning,
                    implementation: "AssemblyVersion",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, attribute.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, attribute.Span.End),
                    content: attribute.ToString(),
                    bestPractice: "Use semantic versioning (SemVer) for plugins to clearly communicate breaking changes and enable version negotiation.",
                    azureUrl: VersioningUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCompatibilityMatrix(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: [ExportMetadata("MinHostVersion", "...")] or similar version metadata
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var versionMetadata = classDecl.AttributeLists.SelectMany(al => al.Attributes)
                .Where(a => a.Name.ToString().Contains("ExportMetadata") &&
                           a.ArgumentList?.Arguments.Any(arg => arg.ToString().Contains("Version")) == true);

            if (versionMetadata.Any())
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_CompatibilityMatrix",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginVersioning,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Maintain compatibility matrix with MinHostVersion/MaxHostVersion metadata to prevent incompatible plugin loading.",
                    azureUrl: VersioningUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSideBySideVersioning(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: Multiple PluginLoadContext or AssemblyLoadContext object creations
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(oc => oc.Type.ToString().Contains("PluginLoadContext") ||
                        oc.Type.ToString().Contains("AssemblyLoadContext"))
            .ToList();

        if (objectCreations.Count > 1)
        {
            patterns.Add(CreatePattern(
                name: "Plugin_SideBySideVersioning",
                type: PatternType.PluginArchitecture,
                category: PatternCategory.PluginVersioning,
                implementation: "Multiple AssemblyLoadContext",
                filePath: filePath,
                context: context,
                lineNumber: GetLineNumber(sourceCode, objectCreations.First().SpanStart),
                endLineNumber: GetLineNumber(sourceCode, objectCreations.Last().Span.End),
                content: $"{objectCreations.Count} AssemblyLoadContext instances detected",
                bestPractice: "Load multiple versions of the same plugin side-by-side using separate AssemblyLoadContext instances for gradual migration and A/B testing.",
                azureUrl: PluginTutorialUrl
            ));
        }

        return patterns;
    }

    // ===== HELPER METHODS =====

    private CodePattern CreatePattern(
        string name,
        PatternType type,
        PatternCategory category,
        string implementation,
        string filePath,
        string? context,
        int lineNumber,
        int endLineNumber,
        string content,
        string bestPractice,
        string azureUrl,
        Dictionary<string, object>? metadata = null)
    {
        var pattern = new CodePattern
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
            Confidence = 0.9f,
            IsPositivePattern = true,
            Context = context ?? string.Empty,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        // Add Azure URL and IsPositive to metadata as well
        pattern.Metadata["AzureUrl"] = azureUrl;
        pattern.Metadata["IsPositive"] = true;

        return pattern;
    }

    private int GetLineNumber(string sourceCode, int position)
    {
        return sourceCode.Substring(0, position).Count(c => c == '\n') + 1;
    }

    private int GetLineNumberByContent(string sourceCode, string searchText)
    {
        var index = sourceCode.IndexOf(searchText, StringComparison.Ordinal);
        return index >= 0 ? GetLineNumber(sourceCode, index) : 1;
    }

    #endregion
}
