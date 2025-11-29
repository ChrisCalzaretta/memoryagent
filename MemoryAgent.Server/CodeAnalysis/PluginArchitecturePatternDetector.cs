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
public partial class PluginArchitecturePatternDetector
{
    private readonly ILogger<PluginArchitecturePatternDetector>? _logger;

    // Azure documentation URLs
    protected const string PluginTutorialUrl = "https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support";
    protected const string MEFUrl = "https://learn.microsoft.com/en-us/dotnet/framework/mef/";
    protected const string DevProxyPluginUrl = "https://learn.microsoft.com/en-us/microsoft-cloud/dev/dev-proxy/technical-reference/plugin-architecture";
    protected const string SemanticKernelPluginUrl = "https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/";
    protected const string PowerAppsPluginUrl = "https://learn.microsoft.com/en-us/power-apps/developer/data-platform/best-practices/business-logic/";
    protected const string SecurityPatternsUrl = "https://learn.microsoft.com/en-us/azure/well-architected/security/design-patterns";
    protected const string ReliabilityPatternsUrl = "https://learn.microsoft.com/en-us/azure/well-architected/reliability/design-patterns";
    protected const string AssemblyUnloadUrl = "https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability";
    protected const string StrongNamingUrl = "https://learn.microsoft.com/en-us/dotnet/standard/assembly/strong-named";
    protected const string VersioningUrl = "https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning";

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
}
