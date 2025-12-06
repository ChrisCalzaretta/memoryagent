using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.Mcp.Consolidated;

/// <summary>
/// Handles code understanding tools: get_context, explain_code, find_examples
/// These tools help the AI understand and work with the codebase more effectively.
/// </summary>
public class CodeUnderstandingToolHandler : IMcpToolHandler
{
    private readonly ISmartSearchService _searchService;
    private readonly IGraphService _graphService;
    private readonly ILearningService _learningService;
    private readonly ILogger<CodeUnderstandingToolHandler> _logger;

    public CodeUnderstandingToolHandler(
        ISmartSearchService searchService,
        IGraphService graphService,
        ILearningService learningService,
        ILogger<CodeUnderstandingToolHandler> logger)
    {
        _searchService = searchService;
        _graphService = graphService;
        _learningService = learningService;
        _logger = logger;
    }

    public IEnumerable<McpTool> GetTools()
    {
        return new[]
        {
            new McpTool
            {
                Name = "get_context",
                Description = "Get relevant context for a task or question from the codebase. Returns related files, patterns, and previous Q&A. CALL THIS BEFORE STARTING ANY TASK.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        task = new { type = "string", description = "Description of the task or question" },
                        context = new { type = "string", description = "Project context name" },
                        includePatterns = new { type = "boolean", description = "Include relevant patterns (default: true)", @default = true },
                        includeQA = new { type = "boolean", description = "Include similar Q&A (default: true)", @default = true },
                        limit = new { type = "number", description = "Max results per category (default: 5)", @default = 5 }
                    },
                    required = new[] { "task", "context" }
                }
            },
            new McpTool
            {
                Name = "explain_code",
                Description = "Explain what a piece of code or file does. Analyzes structure, dependencies, and purpose.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string", description = "Path to the file to explain" },
                        className = new { type = "string", description = "Specific class to explain (optional)" },
                        methodName = new { type = "string", description = "Specific method to explain (optional)" },
                        context = new { type = "string", description = "Project context name" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new McpTool
            {
                Name = "find_examples",
                Description = "Find usage examples of a function, class, or pattern in the codebase.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "What to find examples of (function name, class, pattern)" },
                        context = new { type = "string", description = "Project context name" },
                        limit = new { type = "number", description = "Max examples to return (default: 10)", @default = 10 }
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
        return toolName switch
        {
            "get_context" => await GetContextAsync(args, cancellationToken),
            "explain_code" => await ExplainCodeAsync(args, cancellationToken),
            "find_examples" => await FindExamplesAsync(args, cancellationToken),
            _ => ErrorResult($"Unknown tool: {toolName}")
        };
    }

    #region Get Context

    private async Task<McpToolResult> GetContextAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var task = args?.GetValueOrDefault("task")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var includePatterns = SafeParseBool(args?.GetValueOrDefault("includePatterns"), true);
        var includeQA = SafeParseBool(args?.GetValueOrDefault("includeQA"), true);
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 5);

        if (string.IsNullOrWhiteSpace(task) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("task and context are required");

        var output = $"📋 Context for: {task}\n";
        output += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

        // 1. Search for relevant code
        var searchRequest = new SmartSearchRequest
        {
            Query = task,
            Context = context,
            Limit = limit,
            IncludeRelationships = true
        };
        
        var searchResults = await _searchService.SearchAsync(searchRequest, ct);
        
        if (searchResults.Results.Any())
        {
            output += $"📁 Relevant Files ({searchResults.Results.Count}):\n\n";
            foreach (var result in searchResults.Results.Take(limit))
            {
                output += $"  • {result.FilePath}\n";
                output += $"    {result.Type}: {result.Name}\n";
                if (!string.IsNullOrEmpty(result.Content))
                {
                    var preview = result.Content.Length > 100 ? result.Content[..100] + "..." : result.Content;
                    output += $"    Preview: {preview}\n";
                }
                output += "\n";
            }
        }
        else
        {
            output += "📁 No directly relevant files found.\n\n";
        }

        // 2. Check for similar Q&A
        if (includeQA)
        {
            var similarQA = await _learningService.FindSimilarQuestionsAsync(task, context, limit, ct);
            if (similarQA.Any())
            {
                output += $"💡 Similar Past Q&A ({similarQA.Count}):\n\n";
                foreach (var qa in similarQA.Take(3))
                {
                    output += $"  ❓ {qa.Question}\n";
                    if (!string.IsNullOrEmpty(qa.Answer))
                    {
                        var answerPreview = qa.Answer.Length > 150 ? qa.Answer[..150] + "..." : qa.Answer;
                        output += $"  💬 {answerPreview}\n";
                    }
                    output += "\n";
                }
            }
        }

        // 3. Get important files for context
        var importantFiles = await _learningService.GetMostImportantFilesAsync(context, limit, ct);
        if (importantFiles.Any())
        {
            output += $"⭐ Important Files in Workspace:\n";
            foreach (var f in importantFiles.Take(5))
            {
                output += $"  • {Path.GetFileName(f.FilePath)} (importance: {f.ImportanceScore:F1})\n";
            }
            output += "\n";
        }

        // 4. Check for relevant patterns (search all pattern types)
        if (includePatterns)
        {
            var allPatterns = new List<CodePattern>();
            foreach (PatternType pType in Enum.GetValues(typeof(PatternType)))
            {
                var patterns = await _graphService.GetPatternsByTypeAsync(pType, context, ct);
                allPatterns.AddRange(patterns);
            }
            
            // Filter patterns that might be relevant to the task
            var relevantPatterns = allPatterns
                .Where(p => p.Name.Contains(task, StringComparison.OrdinalIgnoreCase) ||
                           p.Implementation?.Contains(task, StringComparison.OrdinalIgnoreCase) == true ||
                           p.BestPractice?.Contains(task, StringComparison.OrdinalIgnoreCase) == true)
                .Take(limit)
                .ToList();
                
            if (relevantPatterns.Any())
            {
                output += $"🎨 Relevant Patterns:\n";
                foreach (var p in relevantPatterns.Take(3))
                {
                    output += $"  • {p.Name} ({p.Type})\n";
                    if (!string.IsNullOrEmpty(p.BestPractice))
                        output += $"    Best Practice: {p.BestPractice}\n";
                }
                output += "\n";
            }
        }

        output += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
        output += "✨ Use this context to inform your approach.\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Explain Code

    private async Task<McpToolResult> ExplainCodeAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var filePath = args?.GetValueOrDefault("filePath")?.ToString();
        var className = args?.GetValueOrDefault("className")?.ToString();
        var methodName = args?.GetValueOrDefault("methodName")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant() ?? "default";

        if (string.IsNullOrWhiteSpace(filePath))
            return ErrorResult("filePath is required");

        var output = $"📖 Code Explanation\n";
        output += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";
        output += $"📁 File: {filePath}\n";
        if (!string.IsNullOrEmpty(className)) output += $"📦 Class: {className}\n";
        if (!string.IsNullOrEmpty(methodName)) output += $"🔧 Method: {methodName}\n";
        output += "\n";

        // Get file info from graph
        var searchQuery = !string.IsNullOrEmpty(className) 
            ? className 
            : Path.GetFileNameWithoutExtension(filePath);
            
        var nodeInfo = await _graphService.FullTextSearchAsync(searchQuery, context, 5, ct);
        
        if (nodeInfo.Any())
        {
            var fileNodes = nodeInfo.Where(n => 
                n.FilePath?.Contains(Path.GetFileName(filePath)) == true ||
                n.Name == className ||
                n.Name == methodName).ToList();

            if (fileNodes.Any())
            {
                output += "📊 Structure:\n\n";
                foreach (var node in fileNodes.Take(10))
                {
                    output += $"  [{node.Type}] {node.Name}\n";
                    if (!string.IsNullOrEmpty(node.Content))
                    {
                        var preview = node.Content.Length > 200 ? node.Content[..200] + "..." : node.Content;
                        output += $"    {preview}\n";
                    }
                    output += "\n";
                }
            }
        }

        // Get dependencies
        if (!string.IsNullOrEmpty(className))
        {
            var deps = await _graphService.GetDependencyChainAsync(className, 2, ct);
            if (deps.Any())
            {
                output += $"🔗 Dependencies ({deps.Count}):\n";
                foreach (var dep in deps.Take(10))
                {
                    output += $"  → {dep}\n";
                }
                output += "\n";
            }

            // Get what depends on this
            var dependents = await _graphService.GetImpactAnalysisAsync(className, ct);
            if (dependents.Any())
            {
                output += $"⚠️ Used By ({dependents.Count} classes):\n";
                foreach (var dep in dependents.Take(5))
                {
                    output += $"  ← {dep}\n";
                }
                output += "\n";
            }
        }

        // Get patterns in this file (search all pattern types and filter by file)
        var allPatterns = new List<CodePattern>();
        foreach (PatternType pType in Enum.GetValues(typeof(PatternType)))
        {
            var patterns = await _graphService.GetPatternsByTypeAsync(pType, context, ct);
            allPatterns.AddRange(patterns.Where(p => 
                p.FilePath?.Contains(Path.GetFileName(filePath)) == true));
        }
        
        if (allPatterns.Any())
        {
            output += $"🎨 Patterns Detected:\n";
            foreach (var p in allPatterns.Take(5))
            {
                var status = p.IsPositivePattern ? "✅" : "⚠️";
                output += $"  {status} {p.Name} ({p.Type})\n";
            }
            output += "\n";
        }

        output += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    #endregion

    #region Find Examples

    private async Task<McpToolResult> FindExamplesAsync(Dictionary<string, object>? args, CancellationToken ct)
    {
        var query = args?.GetValueOrDefault("query")?.ToString();
        var context = args?.GetValueOrDefault("context")?.ToString()?.ToLowerInvariant();
        var limit = SafeParseInt(args?.GetValueOrDefault("limit"), 10);

        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(context))
            return ErrorResult("query and context are required");

        var output = $"🔍 Examples of: {query}\n";
        output += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n";

        // Search for usages
        var searchRequest = new SmartSearchRequest
        {
            Query = $"usage of {query} OR implements {query} OR calls {query}",
            Context = context,
            Limit = limit,
            IncludeRelationships = true
        };
        
        var searchResults = await _searchService.SearchAsync(searchRequest, ct);

        if (searchResults.Results.Any())
        {
            output += $"📋 Found {searchResults.Results.Count} examples:\n\n";
            
            var groupedByFile = searchResults.Results
                .GroupBy(r => r.FilePath)
                .Take(limit);

            foreach (var fileGroup in groupedByFile)
            {
                output += $"📁 {Path.GetFileName(fileGroup.Key)}\n";
                foreach (var result in fileGroup.Take(3))
                {
                    output += $"   [{result.Type}] {result.Name}\n";
                    if (!string.IsNullOrEmpty(result.Content))
                    {
                        // Try to find the relevant line with the query
                        var lines = result.Content.Split('\n');
                        var relevantLines = lines
                            .Where(l => l.Contains(query, StringComparison.OrdinalIgnoreCase))
                            .Take(2);
                        
                        foreach (var line in relevantLines)
                        {
                            var trimmed = line.Trim();
                            if (trimmed.Length > 80) trimmed = trimmed[..80] + "...";
                            output += $"      → {trimmed}\n";
                        }
                    }
                }
                output += "\n";
            }
        }
        else
        {
            output += "No examples found. Try:\n";
            output += "  • Different search terms\n";
            output += "  • Checking if codebase is indexed\n";
            output += "  • Using smartsearch for broader search\n";
        }

        // Also check for patterns (search all types and filter by query)
        var allPatterns = new List<CodePattern>();
        foreach (PatternType pType in Enum.GetValues(typeof(PatternType)))
        {
            var patterns = await _graphService.GetPatternsByTypeAsync(pType, context, ct);
            allPatterns.AddRange(patterns.Where(p => 
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Implementation?.Contains(query, StringComparison.OrdinalIgnoreCase) == true));
        }
        
        if (allPatterns.Any())
        {
            output += $"\n🎨 Related Patterns:\n";
            foreach (var p in allPatterns.Take(3))
            {
                output += $"  • {p.Name}\n";
                if (!string.IsNullOrEmpty(p.Implementation))
                {
                    var impl = p.Implementation.Length > 100 ? p.Implementation[..100] + "..." : p.Implementation;
                    output += $"    Example: {impl}\n";
                }
            }
        }

        output += "\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";

        return new McpToolResult
        {
            Content = new List<McpContent>
            {
                new() { Type = "text", Text = output }
            }
        };
    }

    #endregion

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
            new() { Type = "text", Text = $"❌ Error: {error}" }
        }
    };

    #endregion
}

