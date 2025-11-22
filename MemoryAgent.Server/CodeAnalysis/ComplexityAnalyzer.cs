using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Analyzes code complexity metrics
/// </summary>
public static class ComplexityAnalyzer
{
    /// <summary>
    /// Calculate cyclomatic complexity for a method
    /// </summary>
    public static int CalculateCyclomaticComplexity(MethodDeclarationSyntax method)
    {
        var complexity = 1; // Base complexity

        var decisionPoints = method.DescendantNodes().Where(node =>
            node.IsKind(SyntaxKind.IfStatement) ||
            node.IsKind(SyntaxKind.WhileStatement) ||
            node.IsKind(SyntaxKind.ForStatement) ||
            node.IsKind(SyntaxKind.ForEachStatement) ||
            node.IsKind(SyntaxKind.CaseSwitchLabel) ||
            node.IsKind(SyntaxKind.CatchClause) ||
            node.IsKind(SyntaxKind.CoalesceExpression) ||
            node.IsKind(SyntaxKind.ConditionalExpression) ||
            node.IsKind(SyntaxKind.LogicalAndExpression) ||
            node.IsKind(SyntaxKind.LogicalOrExpression)
        );

        complexity += decisionPoints.Count();
        return complexity;
    }

    /// <summary>
    /// Calculate lines of code (excluding blank lines and comments)
    /// </summary>
    public static int CalculateLinesOfCode(SyntaxNode node)
    {
        var text = node.ToString();
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("//"))
            .Count();
        
        return lines;
    }

    /// <summary>
    /// Calculate cognitive complexity (how hard to understand)
    /// Simpler than cyclomatic, focuses on mental burden
    /// </summary>
    public static int CalculateCognitiveComplexity(MethodDeclarationSyntax method)
    {
        var complexity = 0;
        var nestingLevel = 0;

        void Visit(SyntaxNode node, int nesting)
        {
            if (node is IfStatementSyntax ||
                node is WhileStatementSyntax ||
                node is ForStatementSyntax ||
                node is ForEachStatementSyntax)
            {
                complexity += 1 + nesting; // Increment by 1 + nesting level
                Visit(node, nesting + 1);
            }
            else if (node is SwitchStatementSyntax)
            {
                complexity += 1 + nesting;
                Visit(node, nesting + 1);
            }
            else
            {
                foreach (var child in node.ChildNodes())
                {
                    Visit(child, nesting);
                }
            }
        }

        foreach (var child in method.Body?.ChildNodes() ?? Enumerable.Empty<SyntaxNode>())
        {
            Visit(child, 0);
        }

        return complexity;
    }

    /// <summary>
    /// Detect code smells in a method
    /// </summary>
    public static List<string> DetectCodeSmells(MethodDeclarationSyntax method, ClassDeclarationSyntax? containingClass = null)
    {
        var smells = new List<string>();

        // Long method
        var loc = CalculateLinesOfCode(method);
        if (loc > 50)
        {
            smells.Add("long_method");
        }

        // Too many parameters
        if (method.ParameterList.Parameters.Count > 5)
        {
            smells.Add("too_many_parameters");
        }

        // High complexity
        var complexity = CalculateCyclomaticComplexity(method);
        if (complexity > 10)
        {
            smells.Add("high_complexity");
        }

        // Deep nesting (more than 3 levels)
        var maxNesting = CalculateMaxNesting(method);
        if (maxNesting > 3)
        {
            smells.Add("deep_nesting");
        }

        // Missing error handling in async methods
        if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
        {
            var hasTryCatch = method.DescendantNodes().OfType<TryStatementSyntax>().Any();
            if (!hasTryCatch)
            {
                smells.Add("async_without_error_handling");
            }
        }

        return smells;
    }

    /// <summary>
    /// Calculate maximum nesting depth
    /// </summary>
    public static int CalculateMaxNesting(MethodDeclarationSyntax method)
    {
        var maxDepth = 0;

        void Visit(SyntaxNode node, int depth)
        {
            if (depth > maxDepth)
            {
                maxDepth = depth;
            }

            if (node is IfStatementSyntax ||
                node is WhileStatementSyntax ||
                node is ForStatementSyntax ||
                node is ForEachStatementSyntax ||
                node is SwitchStatementSyntax ||
                node is TryStatementSyntax)
            {
                foreach (var child in node.ChildNodes())
                {
                    Visit(child, depth + 1);
                }
            }
            else
            {
                foreach (var child in node.ChildNodes())
                {
                    Visit(child, depth);
                }
            }
        }

        if (method.Body != null)
        {
            Visit(method.Body, 0);
        }

        return maxDepth;
    }

    /// <summary>
    /// Count database access patterns
    /// </summary>
    public static int CountDatabaseCalls(MethodDeclarationSyntax method)
    {
        var dbPatterns = new[]
        {
            "ExecuteAsync", "QueryAsync", "FirstOrDefaultAsync", "ToListAsync",
            "SaveChangesAsync", "AddAsync", "UpdateAsync", "RemoveAsync",
            "ExecuteReader", "ExecuteNonQuery", "ExecuteScalar"
        };

        return method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(inv => dbPatterns.Any(pattern => inv.ToString().Contains(pattern)));
    }

    /// <summary>
    /// Extract exception types that can be thrown
    /// </summary>
    public static List<string> ExtractExceptionTypes(MethodDeclarationSyntax method)
    {
        var exceptions = new HashSet<string>();

        // From throw statements
        var throwStatements = method.DescendantNodes().OfType<ThrowStatementSyntax>();
        foreach (var throwStmt in throwStatements)
        {
            if (throwStmt.Expression is ObjectCreationExpressionSyntax objCreation)
            {
                exceptions.Add(objCreation.Type.ToString());
            }
        }

        // From throw expressions (throw expressions)
        var throwExpressions = method.DescendantNodes().OfType<ThrowExpressionSyntax>();
        foreach (var throwExpr in throwExpressions)
        {
            if (throwExpr.Expression is ObjectCreationExpressionSyntax objCreation)
            {
                exceptions.Add(objCreation.Type.ToString());
            }
        }

        return exceptions.ToList();
    }

    /// <summary>
    /// Detect if method makes HTTP calls
    /// </summary>
    public static bool HasHttpCalls(MethodDeclarationSyntax method)
    {
        var httpPatterns = new[] { "HttpClient", "SendAsync", "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "RestClient" };
        
        return method.DescendantNodes()
            .Any(node => httpPatterns.Any(pattern => node.ToString().Contains(pattern)));
    }

    /// <summary>
    /// Detect if method has logging
    /// </summary>
    public static bool HasLogging(MethodDeclarationSyntax method)
    {
        var loggingPatterns = new[] { "Log", "ILogger", "LogInformation", "LogError", "LogWarning", "LogDebug" };
        
        return method.DescendantNodes()
            .Any(node => loggingPatterns.Any(pattern => node.ToString().Contains(pattern)));
    }

    /// <summary>
    /// Check if method/class is public API
    /// </summary>
    public static bool IsPublicApi(SyntaxNode node)
    {
        if (node is ClassDeclarationSyntax classDecl)
        {
            return classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }
        else if (node is MethodDeclarationSyntax methodDecl)
        {
            return methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }
        
        return false;
    }
}

