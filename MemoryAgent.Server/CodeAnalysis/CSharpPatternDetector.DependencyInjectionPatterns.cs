using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Dependency Injection Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Dependency Injection Patterns




    private List<CodePattern> DetectDependencyInjectionPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Constructor injection
        var constructorsWithParams = root.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.ParameterList.Parameters.Count > 0);

        foreach (var ctor in constructorsWithParams)
        {
            // Check if parameters are interfaces or have common service suffixes
            var hasServiceParams = ctor.ParameterList.Parameters.Any(p => 
                p.Type?.ToString().StartsWith("I") == true || // Interface
                p.Type?.ToString().Contains("Service") == true ||
                p.Type?.ToString().Contains("Repository") == true ||
                p.Type?.ToString().Contains("Logger") == true ||
                p.Type?.ToString().Contains("Options") == true);

            if (hasServiceParams)
            {
                var className = ctor.Parent is ClassDeclarationSyntax classDecl ? classDecl.Identifier.Text : "Unknown";
                var pattern = CreatePattern(
                    name: $"{className}_ConstructorInjection",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "ConstructorInjection",
                    filePath: filePath,
                    node: ctor,
                    sourceCode: sourceCode,
                    bestPractice: "Constructor dependency injection",
                    azureUrl: AzureApiDesignUrl,
                    context: context
                );
                
                pattern.Metadata["dependency_count"] = ctor.ParameterList.Parameters.Count;
                patterns.Add(pattern);
            }
        }

        // Pattern 2: Service registration (AddScoped, AddSingleton, AddTransient)
        var serviceRegistrations = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("AddScoped") || 
                         inv.ToString().Contains("AddSingleton") ||
                         inv.ToString().Contains("AddTransient"));

        foreach (var reg in serviceRegistrations)
        {
            var lifetimeType = reg.ToString().Contains("AddScoped") ? "Scoped" :
                             reg.ToString().Contains("AddSingleton") ? "Singleton" : "Transient";
            
            var pattern = CreatePattern(
                name: $"ServiceRegistration_{lifetimeType}",
                type: PatternType.DependencyInjection,
                category: PatternCategory.Operational,
                implementation: $"ServiceCollection.Add{lifetimeType}",
                filePath: filePath,
                node: reg,
                sourceCode: sourceCode,
                bestPractice: $"Service registration with {lifetimeType} lifetime",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["lifetime"] = lifetimeType;
            patterns.Add(pattern);
        }

        // Pattern 3: Options pattern
        var optionsParams = root.DescendantNodes()
            .OfType<ParameterSyntax>()
            .Where(p => p.Type?.ToString().Contains("IOptions") == true);

        foreach (var param in optionsParams)
        {
            var pattern = CreatePattern(
                name: "Options_Pattern",
                type: PatternType.Configuration,
                category: PatternCategory.Operational,
                implementation: "IOptions",
                filePath: filePath,
                node: param,
                sourceCode: sourceCode,
                bestPractice: "Options pattern for configuration",
                azureUrl: AzureApiDesignUrl,
                context: context
            );
            
            pattern.Metadata["pattern"] = "options";
            patterns.Add(pattern);
        }

        return patterns;
    }


    #endregion
}
