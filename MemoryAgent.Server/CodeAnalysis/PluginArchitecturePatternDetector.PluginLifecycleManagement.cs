using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Plugin Lifecycle Management
/// </summary>
public partial class PluginArchitecturePatternDetector
{
    #region Plugin Lifecycle Management

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



    #endregion
}
