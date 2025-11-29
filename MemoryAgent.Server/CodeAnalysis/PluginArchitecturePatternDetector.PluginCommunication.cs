using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Plugin Communication
/// </summary>
public partial class PluginArchitecturePatternDetector
{
    #region Plugin Communication

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



    #endregion
}
