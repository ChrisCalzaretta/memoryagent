using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Server State Management (React Query, SWR, Apollo)
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region Server State Management (React Query, SWR, Apollo)

    private List<CodePattern> DetectServerStatePatterns(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // React Query - useQuery
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useQuery\(|useMutation\(") && sourceCode.Contains("@tanstack/react-query"))
            {
                patterns.Add(CreatePattern(
                    name: "ReactQuery_ServerState",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "React Query (TanStack Query)",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use React Query for server state management, automatic caching, background refetching, and stale-while-revalidate",
                    azureUrl: "https://tanstack.com/query/latest",
                    context: context,
                    language: language
                ));
            }
        }

        // SWR (stale-while-revalidate)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useSWR\("))
            {
                patterns.Add(CreatePattern(
                    name: "SWR_ServerState",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "SWR (Vercel)",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use SWR for server state caching with automatic revalidation and real-time updates",
                    azureUrl: "https://swr.vercel.app/",
                    context: context,
                    language: language
                ));
            }
        }

        // Apollo Client (GraphQL)
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"useQuery\(|useMutation\(|ApolloClient\(") && sourceCode.Contains("@apollo/client"))
            {
                patterns.Add(CreatePattern(
                    name: "Apollo_GraphQLState",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "Apollo Client",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use Apollo Client for GraphQL state management with normalized cache",
                    azureUrl: "https://www.apollographql.com/docs/react/",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
