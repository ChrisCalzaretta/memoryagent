using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Plugin Loading & Isolation
/// </summary>
public partial class PluginArchitecturePatternDetector
{
    #region Plugin Loading & Isolation

    private List<CodePattern> DetectAssemblyLoadContextPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: class X : AssemblyLoadContext
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            if (classDecl.BaseList?.Types.Any(t => t.ToString().Contains("AssemblyLoadContext")) == true)
            {
                // Check for Load override
                var hasLoadOverride = classDecl.Members.OfType<MethodDeclarationSyntax>()
                    .Any(m => m.Identifier.Text == "Load" && m.Modifiers.Any(SyntaxKind.OverrideKeyword));

                patterns.Add(CreatePattern(
                    name: "Plugin_AssemblyLoadContext",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Use custom AssemblyLoadContext to isolate plugin assemblies and their dependencies, preventing version conflicts and enabling side-by-side loading.",
                    azureUrl: PluginTutorialUrl,
                    metadata: new Dictionary<string, object>
                    {
                        ["HasLoadOverride"] = hasLoadOverride,
                        ["ClassName"] = classDecl.Identifier.Text
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectAssemblyDependencyResolver(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: new AssemblyDependencyResolver(...)
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            if (creation.Type.ToString().Contains("AssemblyDependencyResolver"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_AssemblyDependencyResolver",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: "AssemblyDependencyResolver",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, creation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, creation.Span.End),
                    content: creation.ToString(),
                    bestPractice: "Use AssemblyDependencyResolver to resolve plugin dependencies from .deps.json file, handling NuGet dependencies, native libraries, and satellite assemblies.",
                    azureUrl: PluginTutorialUrl
                ));
            }
        }

        // Detect: ResolveAssemblyToPath, ResolveUnmanagedDllToPath
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var methodName = invocation.Expression.ToString();
            if (methodName.Contains("ResolveAssemblyToPath") || methodName.Contains("ResolveUnmanagedDllToPath"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_DependencyResolution",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: "AssemblyDependencyResolver",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Resolve plugin dependencies using AssemblyDependencyResolver methods to ensure correct loading of managed and native dependencies.",
                    azureUrl: PluginTutorialUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectEnableDynamicLoading(string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect in .csproj files: <EnableDynamicLoading>true</EnableDynamicLoading>
        if (filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceCode.Contains("<EnableDynamicLoading>true</EnableDynamicLoading>"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_EnableDynamicLoading",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: "EnableDynamicLoading",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumberByContent(sourceCode, "<EnableDynamicLoading>true</EnableDynamicLoading>"),
                    endLineNumber: GetLineNumberByContent(sourceCode, "<EnableDynamicLoading>true</EnableDynamicLoading>"),
                    content: "<EnableDynamicLoading>true</EnableDynamicLoading>",
                    bestPractice: "Set EnableDynamicLoading to true in plugin project files to copy all dependencies to output and prepare for dynamic loading.",
                    azureUrl: PluginTutorialUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCollectibleLoadContext(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: AssemblyLoadContext(..., isCollectible: true)
        var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
        foreach (var creation in objectCreations)
        {
            if (creation.Type.ToString().Contains("AssemblyLoadContext"))
            {
                var hasCollectibleArg = creation.ArgumentList?.Arguments
                    .Any(a => a.ToString().Contains("isCollectible") && a.ToString().Contains("true")) == true;

                if (hasCollectibleArg)
                {
                    patterns.Add(CreatePattern(
                        name: "Plugin_CollectibleLoadContext",
                        type: PatternType.PluginArchitecture,
                        category: PatternCategory.PluginLoading,
                        implementation: "CollectibleAssemblyLoadContext",
                        filePath: filePath,
                        context: context,
                        lineNumber: GetLineNumber(sourceCode, creation.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, creation.Span.End),
                        content: creation.ToString(),
                        bestPractice: "Use collectible AssemblyLoadContext to enable plugin unloading and hot reload without application restart.",
                        azureUrl: AssemblyUnloadUrl
                    ));
                }
            }
        }

        // Detect: .Unload() calls
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.Expression.ToString().EndsWith(".Unload"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_DynamicUnload",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: "AssemblyLoadContext.Unload",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Call Unload() on collectible load contexts to dynamically unload plugins and enable hot reload scenarios.",
                    azureUrl: AssemblyUnloadUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPrivateFalseReference(string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect in .csproj files: <Private>false</Private>
        if (filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceCode.Contains("<Private>false</Private>"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_PrivateFalseReference",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: "Private=false",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumberByContent(sourceCode, "<Private>false</Private>"),
                    endLineNumber: GetLineNumberByContent(sourceCode, "<Private>false</Private>"),
                    content: "<Private>false</Private>",
                    bestPractice: "Set Private=false on plugin interface references to prevent copying to plugin output, ensuring plugins use the host's version of shared assemblies.",
                    azureUrl: PluginTutorialUrl
                ));
            }

            if (sourceCode.Contains("<ExcludeAssets>runtime</ExcludeAssets>"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_ExcludeRuntimeAssets",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: "ExcludeAssets=runtime",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumberByContent(sourceCode, "<ExcludeAssets>runtime</ExcludeAssets>"),
                    endLineNumber: GetLineNumberByContent(sourceCode, "<ExcludeAssets>runtime</ExcludeAssets>"),
                    content: "<ExcludeAssets>runtime</ExcludeAssets>",
                    bestPractice: "Use ExcludeAssets=runtime to prevent runtime dependencies from being copied to plugin output, aligning with Private=false for shared assemblies.",
                    azureUrl: PluginTutorialUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectNativeLibraryLoading(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Identifier.Text == "LoadUnmanagedDll" &&
                method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_NativeLibraryLoading",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLoading,
                    implementation: "LoadUnmanagedDll",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(200, method.ToString().Length)),
                    bestPractice: "Override LoadUnmanagedDll to load platform-specific native libraries for plugins with P/Invoke or native dependencies.",
                    azureUrl: PluginTutorialUrl
                ));
            }
        }

        return patterns;
    }

    // ===== CATEGORY 2: Plugin Discovery & Composition Patterns =====



    #endregion
}
