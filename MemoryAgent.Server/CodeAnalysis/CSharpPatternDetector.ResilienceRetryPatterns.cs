using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Resilience/Retry Patterns
/// </summary>
public partial class CSharpPatternDetector : IPatternDetector
{
    #region Resilience/Retry Patterns




    private List<CodePattern> DetectRetryPatterns(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern 1: Polly Policy.Handle
        var pollyPolicies = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("Policy.Handle") || 
                         inv.ToString().Contains("Policy.HandleResult"));

        foreach (var policy in pollyPolicies)
        {
            var pattern = CreatePattern(
                name: "Polly_Policy",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Polly",
                filePath: filePath,
                node: policy,
                sourceCode: sourceCode,
                bestPractice: "Polly resilience policies for transient fault handling",
                azureUrl: AzureRetryUrl,
                context: context
            );
            
            pattern.Metadata["library"] = "Polly";
            patterns.Add(pattern);
        }

        // Pattern 2: WaitAndRetry
        var retryPolicies = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("WaitAndRetry") || 
                         inv.ToString().Contains("RetryAsync"));

        foreach (var retry in retryPolicies)
        {
            var pattern = CreatePattern(
                name: "Polly_RetryPolicy",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Polly.Retry",
                filePath: filePath,
                node: retry,
                sourceCode: sourceCode,
                bestPractice: "Exponential backoff retry policy",
                azureUrl: AzureRetryUrl,
                context: context
            );
            
            pattern.Metadata["retry_type"] = "exponential_backoff";
            patterns.Add(pattern);
        }

        // Pattern 3: Circuit Breaker
        var circuitBreakers = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.ToString().Contains("CircuitBreaker"));

        foreach (var cb in circuitBreakers)
        {
            var pattern = CreatePattern(
                name: "Polly_CircuitBreaker",
                type: PatternType.Resilience,
                category: PatternCategory.Reliability,
                implementation: "Polly.CircuitBreaker",
                filePath: filePath,
                node: cb,
                sourceCode: sourceCode,
                bestPractice: "Circuit breaker pattern for cascading failure prevention",
                azureUrl: AzureRetryUrl,
                context: context
            );
            
            pattern.Metadata["pattern"] = "circuit_breaker";
            patterns.Add(pattern);
        }

        // Pattern 4: Manual retry loops
        var forLoops = root.DescendantNodes()
            .OfType<ForStatementSyntax>()
            .Where(f => f.ToString().Contains("retry") || f.ToString().Contains("attempt"));

        foreach (var loop in forLoops)
        {
            // Check if it has try-catch inside
            var hasTryCatch = loop.DescendantNodes().OfType<TryStatementSyntax>().Any();
            if (hasTryCatch)
            {
                var pattern = CreatePattern(
                    name: "Manual_RetryLoop",
                    type: PatternType.Resilience,
                    category: PatternCategory.Reliability,
                    implementation: "ManualRetry",
                    filePath: filePath,
                    node: loop,
                    sourceCode: sourceCode,
                    bestPractice: "Manual retry loop (consider using Polly)",
                    azureUrl: AzureRetryUrl,
                    context: context
                );
                
                pattern.Confidence = 0.7f; // Lower confidence for manual detection
                pattern.Metadata["suggestion"] = "Consider using Polly for robust retry logic";
                patterns.Add(pattern);
            }
        }

        return patterns;
    }


    #endregion
}
