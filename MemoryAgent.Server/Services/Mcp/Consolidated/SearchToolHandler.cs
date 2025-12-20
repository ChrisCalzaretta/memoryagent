using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Consolidated tool handler for all search operations.
/// Tool: smartsearch
/// Combines: query, search_patterns
/// </summary>
public class SearchToolHandler : IMcpToolHandler
{
    private readonly ISmartSearchService _smartSearchService;
    private readonly ILearningService _learningService;
    private readonly ILogger<SearchToolHandler> _logger;

    public SearchToolHandler(
        ISmartSearchService smartSearchService,
        ILearningService learningService,
        ILogger<SearchToolHandler> logger)
    {
        _smartSearchService = smartSearchService;
        _learningService = learningService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "smartsearch",
                Description = "Unified smart search across all code memory. Automatically detects optimal search strategy (semantic, graph, pattern, or hybrid). Use this for ALL code questions.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Natural language question or search query (e.g., 'How do we handle authentication?', 'caching patterns', 'classes that implement IRepository')" },
                        context = new { type = "string", description = "Project context to search within" },
                        limit = new { type = "number", description = "Maximum results to return (default: 20)", @default = 20 },
                        minimumScore = new { type = "number", description = "Minimum relevance score 0-1 (default: 0.3)", @default = 0.3 },
                        includeRelationships = new { type = "boolean", description = "Include code relationships (dependencies, usages) in results", @default = true },
                        relationshipDepth = new { type = "number", description = "How deep to traverse relationships (default: 2)", @default = 2 }
                    },
                    required = new[] { "query", "context" }
                }
            }
        };
    }

    public async Task<McpToolResult> HandleToolAsync(
        string toolName,
        Dictionary<string, object>? args,
        CancellationToken cancellationToken = default)
    {
        if (toolName == "smartsearch")
            return await SmartSearchToolAsync(args, cancellationToken);

        return ErrorResult($"Unknown tool: {toolName}");
    }

    private async Task<McpToolResult> SmartSearchToolAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var query = args?.GetValueOrDefault("query")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(query))
            return ErrorResult("query is required");
        if (string.IsNullOrWhiteSpace(context))
            return ErrorResult("context is required");

        var request = new SmartSearchRequest
        {
            Query = query,
            Context = context,
            Limit = SafeParseInt(args?.GetValueOrDefault("limit"), 20),
            MinimumScore = SafeParseFloat(args?.GetValueOrDefault("minimumScore"), 0.3f),
            IncludeRelationships = SafeParseBool(args?.GetValueOrDefault("includeRelationships"), true),
            RelationshipDepth = SafeParseInt(args?.GetValueOrDefault("relationshipDepth"), 2)
        };

        var response = await _smartSearchService.SearchAsync(request, ct);

        // Format output
        var output = $"🔍 Smart Search Results\n\n";
        output += $"Query: {query}\n";
        output += $"Strategy: {response.Strategy}\n";
        output += $"Found: {response.TotalFound} results (showing {response.Results.Count})\n";
        output += $"Time: {response.ProcessingTime.TotalMilliseconds:F0}ms\n";
        output += $"Learning Enhanced: {response.Metadata.GetValueOrDefault("learningEnhanced", false)}\n\n";
        output += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

        if (!response.Results.Any())
        {
            output += "No results found. Try:\n";
            output += "• Using different keywords\n";
            output += "• Broadening your search\n";
            output += "• Checking if the codebase is indexed\n";
        }
        else
        {
            foreach (var result in response.Results)
            {
                var score = result.Score;
                var scoreIcon = score >= 0.8f ? "🔥" : score >= 0.6f ? "✅" : score >= 0.4f ? "⚡" : "📎";
                
                output += $"{scoreIcon} {result.Name} ({result.Type})\n";
                output += $"   Score: {score:P0}";
                
                if (result.Metadata.TryGetValue("importance_score", out var importance))
                    output += $" | Importance: {importance:P0}";
                
                output += $"\n   File: {result.FilePath}:{result.LineNumber}\n";

                // Show code preview (truncated)
                if (!string.IsNullOrEmpty(result.Content))
                {
                    var preview = result.Content.Length > 150 
                        ? result.Content[..150].Replace("\n", " ").Replace("\r", "") + "..."
                        : result.Content.Replace("\n", " ").Replace("\r", "");
                    output += $"   Preview: {preview}\n";
                }

                // Show relationships if present
                if (result.Relationships?.Any() == true)
                {
                    output += "   Relationships:\n";
                    foreach (var rel in result.Relationships.Take(3))
                    {
                        output += $"     • {rel.Key}: {string.Join(", ", rel.Value.Take(3))}";
                        if (rel.Value.Count > 3)
                            output += $" (+{rel.Value.Count - 3} more)";
                        output += "\n";
                    }
                }

                // Show pattern metadata if present
                if (result.Metadata.TryGetValue("pattern_type", out var patternType))
                {
                    output += $"   🎯 Pattern: {patternType}";
                    if (result.Metadata.TryGetValue("best_practice", out var bp))
                        output += $" | Best Practice: {bp}";
                    output += "\n";
                }

                output += "\n";
            }

            if (response.HasMore)
                output += $"📄 {response.TotalFound - response.Results.Count} more results available. Increase 'limit' to see more.\n";
        }

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new McpContent { Type = "text", Text = output }
            }
        };
    }

    #region Helpers

    private static int SafeParseInt(object? value, int defaultValue) =>
        value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, out var i) => i,
            JsonElement je when je.TryGetInt32(out var i) => i,
            _ => defaultValue
        };

    private static float SafeParseFloat(object? value, float defaultValue) =>
        value switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            string s when float.TryParse(s, out var f) => f,
            JsonElement je when je.TryGetSingle(out var f) => f,
            _ => defaultValue
        };

    private static bool SafeParseBool(object? value, bool defaultValue) =>
        value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            _ => defaultValue
        };

    private static McpToolResult ErrorResult(string error) => new()
    {
        IsError = true,
        Content = new List<McpContent>
        {
            new McpContent { Type = "text", Text = $"Error: {error}" }
        }
    };

    #endregion
}












