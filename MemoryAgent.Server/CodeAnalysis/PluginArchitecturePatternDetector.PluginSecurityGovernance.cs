using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Plugin Security & Governance
/// </summary>
public partial class PluginArchitecturePatternDetector
{
    #region Plugin Security & Governance

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



    #endregion
}
