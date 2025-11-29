using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Plugin Discovery & Composition
/// </summary>
public partial class PluginArchitecturePatternDetector
{
    #region Plugin Discovery & Composition

    private List<CodePattern> DetectMEFDirectoryCatalog(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: new DirectoryCatalog(...)
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            if (creation.Type.ToString().Contains("DirectoryCatalog") ||
                creation.Type.ToString().Contains("AssemblyCatalog") ||
                creation.Type.ToString().Contains("TypeCatalog"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_MEFCatalog",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: creation.Type.ToString(),
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, creation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, creation.Span.End),
                    content: creation.ToString(),
                    bestPractice: "Use MEF catalogs (DirectoryCatalog, AssemblyCatalog, TypeCatalog) to discover plugins from directories or assemblies at runtime.",
                    azureUrl: MEFUrl
                ));
            }
        }

        // Detect: new CompositionContainer(...)
        foreach (var creation in objectCreations)
        {
            if (creation.Type.ToString().Contains("CompositionContainer"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_MEFCompositionContainer",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: "CompositionContainer",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, creation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, creation.Span.End),
                    content: creation.ToString(),
                    bestPractice: "Use CompositionContainer to manage MEF composition and resolve plugin dependencies automatically.",
                    azureUrl: MEFUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMEFImportExport(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: [Export(typeof(IPlugin))] or [Export]
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var hasExport = classDecl.AttributeLists.SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("Export"));

            if (hasExport)
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_MEFExport",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Use [Export] attribute for declarative plugin registration, enabling automatic discovery and composition via MEF.",
                    azureUrl: MEFUrl
                ));
            }
        }

        // Detect: [Import] or [ImportMany]
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        foreach (var prop in properties)
        {
            var hasImport = prop.AttributeLists.SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("Import"));

            if (hasImport)
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_MEFImport",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: prop.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, prop.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, prop.Span.End),
                    content: prop.ToString(),
                    bestPractice: "Use [Import] or [ImportMany] attributes to automatically inject plugins or collections of plugins via MEF composition.",
                    azureUrl: MEFUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectMEFMetadata(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: [ExportMetadata("...", "...")]
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var hasMetadata = classDecl.AttributeLists.SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("ExportMetadata"));

            if (hasMetadata)
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_MEFMetadata",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Use [ExportMetadata] to attach metadata (version, priority, capabilities) to plugins for filtering and selection without loading.",
                    azureUrl: MEFUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectLazyPluginLoading(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: Lazy<IPlugin>, Lazy<IPlugin, TMetadata>, IEnumerable<Lazy<...>>
        var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
        foreach (var prop in properties)
        {
            var typeString = prop.Type.ToString();
            if (typeString.Contains("Lazy<"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_LazyLoading",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: "Lazy<T>",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, prop.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, prop.Span.End),
                    content: prop.ToString(),
                    bestPractice: "Use Lazy<T> or Lazy<T, TMetadata> to defer plugin instantiation until first use, improving startup time and reducing memory footprint.",
                    azureUrl: MEFUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPluginRegistry(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: interface IPluginRegistry or class PluginRegistry
        var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var interfaceDecl in interfaceDeclarations)
        {
            if (interfaceDecl.Identifier.Text.Contains("PluginRegistry") ||
                interfaceDecl.Identifier.Text.Contains("PluginManager"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_RegistryInterface",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: interfaceDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, interfaceDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, interfaceDecl.Span.End),
                    content: interfaceDecl.ToString().Substring(0, Math.Min(200, interfaceDecl.ToString().Length)),
                    bestPractice: "Implement a central plugin registry to track loaded plugins, manage lifecycle, and resolve dependencies.",
                    azureUrl: DevProxyPluginUrl
                ));
            }
        }

        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            if (classDecl.Identifier.Text.Contains("PluginRegistry") ||
                classDecl.Identifier.Text.Contains("PluginManager"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_RegistryImplementation",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Implement a plugin registry with Register, Unregister, and Get methods to manage plugin lifecycle and discovery.",
                    azureUrl: DevProxyPluginUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectTypeScanning(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: assembly.GetTypes(), typeof(IPlugin).IsAssignableFrom(type)
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var methodText = invocation.ToString();
            if (methodText.Contains("GetTypes()") || methodText.Contains("IsAssignableFrom"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_TypeScanning",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: "Reflection",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Scan assemblies for types implementing plugin interfaces using reflection, providing full control over plugin instantiation without MEF dependency.",
                    azureUrl: PluginTutorialUrl
                ));
            }

            if (methodText.Contains("Activator.CreateInstance"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_DynamicActivation",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: "Activator",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Use Activator.CreateInstance to dynamically instantiate plugin types discovered via reflection.",
                    azureUrl: PluginTutorialUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectConfigurationBasedDiscovery(string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect in JSON files: "plugins": [...] or similar configuration
        if (filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceCode.Contains("\"plugins\"") || sourceCode.Contains("\"pluginPaths\""))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_ConfigurationDiscovery",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginComposition,
                    implementation: "JSON Configuration",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumberByContent(sourceCode, "\"plugins\""),
                    endLineNumber: GetLineNumberByContent(sourceCode, "\"plugins\""),
                    content: "Plugin configuration in JSON",
                    bestPractice: "Use JSON configuration files to declaratively specify plugins to load, enabling environment-specific plugin enablement.",
                    azureUrl: DevProxyPluginUrl
                ));
            }
        }

        return patterns;
    }

    // ===== CATEGORY 3: Plugin Lifecycle Management Patterns =====



    #endregion
}
