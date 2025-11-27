using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Detects Plugin Architecture patterns based on Microsoft guidance for .NET Core,
/// including AssemblyLoadContext, MEF, plugin lifecycle, security, and versioning patterns.
/// </summary>
public class PluginArchitecturePatternDetector
{
    private readonly ILogger<PluginArchitecturePatternDetector>? _logger;

    // Azure documentation URLs
    private const string PluginTutorialUrl = "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support";
    private const string MEFUrl = "https://learn.microsoft.com/en-us/dotnet/framework/mef/";
    private const string DevProxyPluginUrl = "https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/technical-reference/plugin-architecture";
    private const string SemanticKernelPluginUrl = "https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/";
    private const string PowerAppsPluginUrl = "https://learn.microsoft.com/en-us/power-apps/developer/data-platform/best-practices/business-logic/";
    private const string SecurityPatternsUrl = "https://learn.microsoft.com/en-us/azure/well-architected/security/design-patterns";
    private const string ReliabilityPatternsUrl = "https://learn.microsoft.com/en-us/azure/well-architected/reliability/design-patterns";
    private const string AssemblyUnloadUrl = "https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability";
    private const string StrongNamingUrl = "https://learn.microsoft.com/en-us/dotnet/standard/assembly/strong-named";
    private const string VersioningUrl = "https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning";

    public PluginArchitecturePatternDetector(ILogger<PluginArchitecturePatternDetector>? logger = null)
    {
        _logger = logger;
    }

    public async Task<List<CodePattern>> DetectPatternsAsync(
        string filePath,
        string? context,
        string sourceCode,
        CancellationToken cancellationToken = default)
    {
        var patterns = new List<CodePattern>();

        try
        {
            var tree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = tree.GetRoot(cancellationToken);

            // Category 1: Plugin Loading & Isolation Patterns (6 patterns)
            patterns.AddRange(DetectAssemblyLoadContextPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectAssemblyDependencyResolver(root, filePath, context, sourceCode));
            patterns.AddRange(DetectEnableDynamicLoading(filePath, context, sourceCode));
            patterns.AddRange(DetectCollectibleLoadContext(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPrivateFalseReference(filePath, context, sourceCode));
            patterns.AddRange(DetectNativeLibraryLoading(root, filePath, context, sourceCode));

            // Category 2: Plugin Discovery & Composition Patterns (7 patterns)
            patterns.AddRange(DetectMEFDirectoryCatalog(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMEFImportExport(root, filePath, context, sourceCode));
            patterns.AddRange(DetectMEFMetadata(root, filePath, context, sourceCode));
            patterns.AddRange(DetectLazyPluginLoading(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPluginRegistry(root, filePath, context, sourceCode));
            patterns.AddRange(DetectTypeScanning(root, filePath, context, sourceCode));
            patterns.AddRange(DetectConfigurationBasedDiscovery(filePath, context, sourceCode));

            // Category 3: Plugin Lifecycle Management Patterns (5 patterns)
            patterns.AddRange(DetectIPluginInterface(root, filePath, context, sourceCode));
            patterns.AddRange(DetectStatelessPlugin(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPluginHealthCheck(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPluginStartStop(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPluginDependencyInjection(root, filePath, context, sourceCode));

            // Category 4: Plugin Communication Patterns (4 patterns)
            patterns.AddRange(DetectEventBus(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSharedServiceInterface(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPluginPipeline(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPluginContext(root, filePath, context, sourceCode));

            // Category 5: Plugin Security & Governance Patterns (5 patterns)
            patterns.AddRange(DetectGatekeeperPattern(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSandboxing(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCircuitBreakerPlugin(root, filePath, context, sourceCode));
            patterns.AddRange(DetectBulkheadIsolation(root, filePath, context, sourceCode));
            patterns.AddRange(DetectPluginSignatureVerification(root, filePath, context, sourceCode));

            // Category 6: Plugin Versioning & Compatibility Patterns (3 patterns)
            patterns.AddRange(DetectSemanticVersioning(root, filePath, context, sourceCode));
            patterns.AddRange(DetectCompatibilityMatrix(root, filePath, context, sourceCode));
            patterns.AddRange(DetectSideBySideVersioning(root, filePath, context, sourceCode));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error detecting plugin architecture patterns in {FilePath}", filePath);
        }

        return patterns;
    }

    // ===== CATEGORY 1: Plugin Loading & Isolation Patterns =====

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

    private List<CodePattern> DetectIPluginInterface(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: interface IPlugin
        var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var interfaceDecl in interfaceDeclarations)
        {
            if (interfaceDecl.Identifier.Text.StartsWith("IPlugin") ||
                interfaceDecl.Identifier.Text.StartsWith("ICommand"))
            {
                // Check for Initialize, Execute, Dispose methods
                var methods = interfaceDecl.Members.OfType<MethodDeclarationSyntax>().Select(m => m.Identifier.Text).ToList();
                var hasLifecycleMethods = methods.Any(m => m.Contains("Initialize") || m.Contains("Execute") || m.Contains("Start"));

                patterns.Add(CreatePattern(
                    name: "Plugin_InterfaceContract",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLifecycle,
                    implementation: interfaceDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, interfaceDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, interfaceDecl.Span.End),
                    content: interfaceDecl.ToString().Substring(0, Math.Min(300, interfaceDecl.ToString().Length)),
                    bestPractice: "Define a standard IPlugin interface with lifecycle methods (Initialize, Execute, Dispose) for consistent plugin contracts.",
                    azureUrl: PowerAppsPluginUrl,
                    metadata: new Dictionary<string, object>
                    {
                        ["HasLifecycleMethods"] = hasLifecycleMethods,
                        ["Methods"] = string.Join(", ", methods)
                    }
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectStatelessPlugin(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: class X : IPlugin with NO member fields (except readonly, const, or injected via constructor)
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var implementsIPlugin = classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IPlugin")) == true;
            if (implementsIPlugin)
            {
                var fields = classDecl.Members.OfType<FieldDeclarationSyntax>().ToList();
                var mutableFields = fields.Where(f => !f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) &&
                                                      !f.Modifiers.Any(SyntaxKind.ConstKeyword)).ToList();

                if (mutableFields.Count == 0)
                {
                    patterns.Add(CreatePattern(
                        name: "Plugin_StatelessDesign",
                        type: PatternType.PluginArchitecture,
                        category: PatternCategory.PluginLifecycle,
                        implementation: classDecl.Identifier.Text,
                        filePath: filePath,
                        context: context,
                        lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                        endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                        content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                        bestPractice: "Design plugins to be stateless by avoiding mutable member fields, ensuring thread-safety and scalability.",
                        azureUrl: PowerAppsPluginUrl
                    ));
                }
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPluginHealthCheck(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: IHealthCheck implementation or CheckHealthAsync method
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var implementsHealthCheck = classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IHealthCheck")) == true;
            if (implementsHealthCheck)
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_HealthCheck",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLifecycle,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Implement IHealthCheck for plugins to monitor health and availability, enabling detection of failing plugins.",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks"
                ));
            }
        }

        // Detect: CheckHealthAsync method
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Identifier.Text.Contains("CheckHealth"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_HealthCheckMethod",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLifecycle,
                    implementation: method.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(200, method.ToString().Length)),
                    bestPractice: "Provide CheckHealthAsync methods to enable health monitoring of plugin functionality and dependencies.",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks"
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPluginStartStop(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: StartAsync, StopAsync methods or IHostedService
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var implementsHostedService = classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IHostedService")) == true;
            if (implementsHostedService)
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_HostedService",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLifecycle,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Implement IHostedService for plugins with explicit StartAsync and StopAsync lifecycle methods for graceful startup and shutdown.",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services"
                ));
            }
        }

        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Identifier.Text == "StartAsync" || method.Identifier.Text == "StopAsync")
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_StartStopLifecycle",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginLifecycle,
                    implementation: method.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(200, method.ToString().Length)),
                    bestPractice: "Provide StartAsync and StopAsync methods for explicit plugin lifecycle management and resource cleanup.",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services"
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPluginDependencyInjection(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: plugin constructor with ILogger, IServiceProvider, etc.
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var implementsIPlugin = classDecl.BaseList?.Types.Any(t => t.ToString().Contains("IPlugin")) == true;
            if (implementsIPlugin)
            {
                var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
                foreach (var ctor in constructors)
                {
                    var parameters = ctor.ParameterList.Parameters;
                    if (parameters.Any(p => p.Type?.ToString().StartsWith("I") == true))
                    {
                        patterns.Add(CreatePattern(
                            name: "Plugin_DependencyInjection",
                            type: PatternType.PluginArchitecture,
                            category: PatternCategory.PluginLifecycle,
                            implementation: classDecl.Identifier.Text,
                            filePath: filePath,
                            context: context,
                            lineNumber: GetLineNumber(sourceCode, ctor.SpanStart),
                            endLineNumber: GetLineNumber(sourceCode, ctor.Span.End),
                            content: ctor.ToString(),
                            bestPractice: "Inject services (ILogger, IServiceProvider, etc.) into plugin constructors for loose coupling and testability.",
                            azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection"
                        ));
                    }
                }
            }
        }

        return patterns;
    }

    // ===== CATEGORY 4: Plugin Communication Patterns =====

    private List<CodePattern> DetectEventBus(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: IEventBus interface or event publishing/subscription
        var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var interfaceDecl in interfaceDeclarations)
        {
            if (interfaceDecl.Identifier.Text.Contains("EventBus") ||
                interfaceDecl.Identifier.Text.Contains("EventAggregator") ||
                interfaceDecl.Identifier.Text.Contains("MessageBus"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_EventBus",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginCommunication,
                    implementation: interfaceDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, interfaceDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, interfaceDecl.Span.End),
                    content: interfaceDecl.ToString().Substring(0, Math.Min(300, interfaceDecl.ToString().Length)),
                    bestPractice: "Implement an event bus for publish-subscribe inter-plugin communication, enabling loose coupling and event-driven architecture.",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber"
                ));
            }
        }

        // Detect: Publish<TEvent>, Subscribe<TEvent> methods
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Identifier.Text.StartsWith("Publish") || method.Identifier.Text.StartsWith("Subscribe"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_PubSubMessaging",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginCommunication,
                    implementation: method.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString().Substring(0, Math.Min(200, method.ToString().Length)),
                    bestPractice: "Use Publish/Subscribe methods for decoupled plugin messaging and event-driven communication.",
                    azureUrl: "https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber"
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSharedServiceInterface(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: plugins exposing services via interfaces with [Export]
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            var hasExport = classDecl.AttributeLists.SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("Export"));

            if (hasExport && classDecl.BaseList?.Types.Count > 0)
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_SharedServiceInterface",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginCommunication,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Plugins should expose services via shared interfaces, enabling plugin-to-plugin service calls and extensibility.",
                    azureUrl: MEFUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPluginPipeline(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: IPluginPipeline or middleware pattern with Use() method
        var interfaceDeclarations = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var interfaceDecl in interfaceDeclarations)
        {
            if (interfaceDecl.Identifier.Text.Contains("Pipeline") ||
                interfaceDecl.Identifier.Text.Contains("Middleware"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_PipelineInterface",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginCommunication,
                    implementation: interfaceDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, interfaceDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, interfaceDecl.Span.End),
                    content: interfaceDecl.ToString().Substring(0, Math.Min(300, interfaceDecl.ToString().Length)),
                    bestPractice: "Use pipeline/middleware pattern to chain plugins for sequential request processing and interceptors.",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/"
                ));
            }
        }

        // Detect: Use(IPlugin plugin) method
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Identifier.Text == "Use" &&
                method.ParameterList.Parameters.Any(p => p.Type?.ToString().Contains("Plugin") == true))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_PipelineRegistration",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginCommunication,
                    implementation: method.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, method.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, method.Span.End),
                    content: method.ToString(),
                    bestPractice: "Provide a Use() method to register plugins in the processing pipeline, enabling middleware-style composition.",
                    azureUrl: "https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/"
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPluginContext(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: PluginContext, ExecutionContext classes
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            if (classDecl.Identifier.Text.Contains("PluginContext") ||
                classDecl.Identifier.Text.Contains("ExecutionContext"))
            {
                // Check for correlation ID or properties dictionary
                var hasCorrelationId = classDecl.Members.OfType<PropertyDeclarationSyntax>()
                    .Any(p => p.Identifier.Text.Contains("CorrelationId"));
                var hasProperties = classDecl.Members.OfType<PropertyDeclarationSyntax>()
                    .Any(p => p.Type.ToString().Contains("Dictionary"));

                patterns.Add(CreatePattern(
                    name: "Plugin_ContextObject",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginCommunication,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(300, classDecl.ToString().Length)),
                    bestPractice: "Use a context object to pass shared state, correlation IDs, and properties through plugin execution pipeline.",
                    azureUrl: PowerAppsPluginUrl,
                    metadata: new Dictionary<string, object>
                    {
                        ["HasCorrelationId"] = hasCorrelationId,
                        ["HasProperties"] = hasProperties
                    }
                ));
            }
        }

        return patterns;
    }

    // ===== CATEGORY 5: Plugin Security & Governance Patterns =====

    private List<CodePattern> DetectGatekeeperPattern(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: PluginGatekeeper, authorization middleware, policy enforcement
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDecl in classDeclarations)
        {
            if (classDecl.Identifier.Text.Contains("Gatekeeper") ||
                classDecl.Identifier.Text.Contains("AuthorizationMiddleware") ||
                classDecl.Identifier.Text.Contains("SecurityFilter"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_GatekeeperPattern",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginSecurity,
                    implementation: classDecl.Identifier.Text,
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, classDecl.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, classDecl.Span.End),
                    content: classDecl.ToString().Substring(0, Math.Min(200, classDecl.ToString().Length)),
                    bestPractice: "Implement gatekeeper pattern for centralized security enforcement before plugin execution, validating authentication and authorization.",
                    azureUrl: SecurityPatternsUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectSandboxing(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: ProcessStartInfo, AppDomain (legacy), container execution
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.ToString().Contains("ProcessStartInfo") ||
                invocation.ToString().Contains("Process.Start"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_ProcessIsolation",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginSecurity,
                    implementation: "Process Isolation",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Execute plugins in isolated processes or containers with limited permissions to prevent malicious plugins from affecting the host.",
                    azureUrl: SecurityPatternsUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectCircuitBreakerPlugin(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: CircuitBreakerPolicy, Polly circuit breaker for plugins
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.ToString().Contains("CircuitBreaker"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_CircuitBreaker",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginSecurity,
                    implementation: "Polly CircuitBreaker",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Apply circuit breaker pattern to isolate failing plugins and prevent cascading failures across the system.",
                    azureUrl: ReliabilityPatternsUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectBulkheadIsolation(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: BulkheadPolicy, Polly bulkhead for resource isolation
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.ToString().Contains("Bulkhead"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_BulkheadIsolation",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginSecurity,
                    implementation: "Polly Bulkhead",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Use bulkhead pattern to isolate plugin resources, preventing one plugin from exhausting system resources and starving others.",
                    azureUrl: ReliabilityPatternsUrl
                ));
            }
        }

        return patterns;
    }

    private List<CodePattern> DetectPluginSignatureVerification(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Detect: AssemblyName.GetAssemblyName, GetPublicKey(), strong-name verification
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (invocation.ToString().Contains("GetAssemblyName") ||
                invocation.ToString().Contains("GetPublicKey"))
            {
                patterns.Add(CreatePattern(
                    name: "Plugin_SignatureVerification",
                    type: PatternType.PluginArchitecture,
                    category: PatternCategory.PluginSecurity,
                    implementation: "Strong-Name Verification",
                    filePath: filePath,
                    context: context,
                    lineNumber: GetLineNumber(sourceCode, invocation.SpanStart),
                    endLineNumber: GetLineNumber(sourceCode, invocation.Span.End),
                    content: invocation.ToString(),
                    bestPractice: "Verify plugin assembly signatures before loading to ensure trust and prevent tampering.",
                    azureUrl: StrongNamingUrl
                ));
            }
        }

        return patterns;
    }

    // ===== CATEGORY 6: Plugin Versioning & Compatibility Patterns =====

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
}

