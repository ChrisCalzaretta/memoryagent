using System.Text;
using System.Text.Json;
using AgentContracts.Models;
using AgentContracts.Requests;
using AgentContracts.Responses;
using AgentContracts.Services;
using CodingAgent.Server.Clients;

namespace CodingAgent.Server.Services;

/// <summary>
/// Agentic code generation with TOOL USE (like Claude!)
/// LLMs can call functions to read files, search code, explore workspace
/// </summary>
public interface IAgenticCodingService
{
    Task<AgenticCodingResult> GenerateWithToolsAsync(
        string task,
        string? language,
        string? workspacePath,
        string? jobWorkspacePath,
        CodebaseContext? codebaseContext,
        ValidationFeedback? previousFeedback,
        CancellationToken cancellationToken);
}

public class AgenticCodingService : IAgenticCodingService
{
    private readonly IOllamaClient _ollama;
    private readonly IMemoryAgentClient _memoryAgent;
    private readonly ISelfReviewService _selfReview;
    private readonly IHistoryFormatterService _historyFormatter;
    private readonly IPromptSeedService _promptSeed;
    private readonly IHierarchicalContextManager _contextManager;
    private readonly ILogger<AgenticCodingService> _logger;
    private const int MaxToolIterations = 15; // Prevent infinite loops
    private const int MaxSelfReviewAttempts = 3; // Max times to self-review and fix
    
    public AgenticCodingService(
        IOllamaClient ollama,
        IMemoryAgentClient memoryAgent,
        ISelfReviewService selfReview,
        IHistoryFormatterService historyFormatter,
        IPromptSeedService promptSeed,
        IHierarchicalContextManager contextManager,
        ILogger<AgenticCodingService> logger)
    {
        _ollama = ollama;
        _memoryAgent = memoryAgent;
        _selfReview = selfReview;
        _historyFormatter = historyFormatter;
        _promptSeed = promptSeed;
        _contextManager = contextManager;
        _logger = logger;
    }
    
    public async Task<AgenticCodingResult> GenerateWithToolsAsync(
        string task,
        string? language,
        string? workspacePath,
        string? jobWorkspacePath,
        CodebaseContext? codebaseContext,
        ValidationFeedback? previousFeedback,
        CancellationToken cancellationToken)
    {
        var result = new AgenticCodingResult
        {
            Task = task,
            Language = language ?? "csharp"
        };
        
        // Define available tools for the LLM
        var tools = GetAvailableTools(workspacePath, codebaseContext);
        
        // 🌱 GET BEST PROMPTS FROM LIGHTNING (seeded prompts)
        var promptMetadata = await _promptSeed.GetBestPromptAsync("agentic_coding_system_v1", cancellationToken);
        var toolUsageRulesPrompt = await _promptSeed.GetBestPromptAsync("tool_usage_rules_v1", cancellationToken);
        
        // 📊 BUILD HIERARCHICAL CONTEXT (project overview + guidance)
        string? hierarchicalContext = null;
        if (!string.IsNullOrEmpty(workspacePath))
        {
            try
            {
                _logger.LogInformation("📊 Building hierarchical context for {WorkspacePath}", workspacePath);
                hierarchicalContext = await _contextManager.BuildInitialContextAsync(task, workspacePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to build hierarchical context, continuing without it");
            }
        }
        
        // Build initial prompt with FULL HISTORY + HIERARCHICAL CONTEXT
        var systemPrompt = BuildSystemPrompt(task, language, codebaseContext, workspacePath, jobWorkspacePath, previousFeedback, promptMetadata, hierarchicalContext);
        var conversationHistory = new List<ConversationMessage>
        {
            new("system", systemPrompt),
            new("user", task)
        };
        
        _logger.LogInformation("🤖 Starting agentic code generation with {ToolCount} tools available", tools.Count);
        _logger.LogInformation("📂 Search paths: User={User}, Job={Job}", 
            workspacePath ?? "(none)", jobWorkspacePath ?? "(none)");
        
        // Agentic loop: LLM can call tools iteratively
        for (int iteration = 0; iteration < MaxToolIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Generate response (may include tool calls)
            var response = await GenerateWithToolCallsAsync(
                conversationHistory, 
                tools,
                toolUsageRulesPrompt,
                cancellationToken);
            
            result.ToolCallsExecuted += response.ToolCalls?.Count ?? 0;
            result.Iterations = iteration + 1;
            
            // Check if LLM wants to use tools
            if (response.ToolCalls?.Any() == true)
            {
                _logger.LogInformation("🔧 LLM requested {Count} tool calls in iteration {Iteration}", 
                    response.ToolCalls.Count, iteration + 1);
                
                // Execute all requested tools
                foreach (var toolCall in response.ToolCalls)
                {
                    var toolResult = await ExecuteToolAsync(
                        toolCall, 
                        workspacePath,
                        jobWorkspacePath,
                        codebaseContext,
                        cancellationToken);
                    
                    result.ToolResults.Add(toolResult);
                    
                    // Add tool result to conversation
                    conversationHistory.Add(new ConversationMessage("assistant", 
                        $"[TOOL_CALL: {toolCall.Name}({toolCall.Arguments})]"));
                    conversationHistory.Add(new ConversationMessage("tool", 
                        toolResult.Result, toolCall.Name));
                    
                    _logger.LogInformation("✅ Tool {Name} executed: {Preview}...", 
                        toolCall.Name, 
                        toolResult.Result.Length > 100 ? toolResult.Result.Substring(0, 100) : toolResult.Result);
                }
                
                // Continue conversation with tool results
                continue;
            }
            
            // No more tool calls - LLM thinks it's done
            _logger.LogInformation("💭 LLM finished generation after {Iterations} iterations, {ToolCalls} tool calls",
                iteration + 1, result.ToolCallsExecuted);
            
            // 🔍 SELF-REVIEW (like Claude!)
            // Let LLM review its own code before submitting
            result.GeneratedCode = response.Content;
            result.GeneratedFiles = ParseGeneratedFiles(response.Content, result.Language);
            
            _logger.LogInformation("🔍 Running self-review on generated code...");
            
            var review = await _selfReview.ReviewCodeAsync(
                result.GeneratedCode,
                result.GeneratedFiles,
                task,
                result.Language,
                cancellationToken);
            
            result.SelfReviewResults.Add(review);
            
            if (review.Approved || !review.HasCriticalIssues)
            {
                _logger.LogInformation("✅ Self-review PASSED: {Summary} (confidence: {Confidence:P0})",
                    review.Summary, review.Confidence);
                
                result.Success = true;
                result.Reasoning = ExtractReasoning(conversationHistory);
                
                // 📊 RECORD SUCCESSFUL PROMPT USAGE
                if (promptMetadata != null)
                {
                    try
                    {
                        await _promptSeed.RecordPromptUsageAsync(promptMetadata.Id, new PromptUsageResult
                        {
                            Success = true,
                            Score = (int)(review.Confidence * 10),
                            Iterations = result.Iterations,
                            Issues = review.Issues.Select(i => i.Description).ToList(),
                            Metadata = new Dictionary<string, object>
                            {
                                ["tool_calls"] = result.ToolCallsExecuted,
                                ["files_generated"] = result.GeneratedFiles.Count,
                                ["self_reviews"] = result.SelfReviewResults.Count
                            }
                        }, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to record prompt usage (non-fatal)");
                    }
                }
                
                return result;
            }
            
            // Self-review found issues - ask LLM to fix them
            _logger.LogWarning("⚠️ Self-review found {IssueCount} issues, asking LLM to fix...",
                review.Issues.Count);
            
            // Add review feedback to conversation and continue loop
            var feedbackMessage = BuildReviewFeedback(review);
            conversationHistory.Add(new ConversationMessage("system", feedbackMessage));
            conversationHistory.Add(new ConversationMessage("user", "Please fix the issues identified in the review and regenerate the code."));
            
            // Continue to next iteration - LLM will fix issues
            continue;
        }
        
        // Max iterations reached
        _logger.LogWarning("⚠️ Max iterations ({Max}) reached, returning current state", MaxToolIterations);
        result.Success = false;
        result.Error = "Max tool call iterations reached";
        
        // 📊 RECORD FAILED PROMPT USAGE
        if (promptMetadata != null)
        {
            try
            {
                await _promptSeed.RecordPromptUsageAsync(promptMetadata.Id, new PromptUsageResult
                {
                    Success = false,
                    Score = 0,
                    Iterations = MaxToolIterations,
                    Issues = new List<string> { "Max iterations reached without approval" },
                    Metadata = new Dictionary<string, object>
                    {
                        ["tool_calls"] = result.ToolCallsExecuted,
                        ["reason"] = "timeout"
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record prompt usage (non-fatal)");
            }
        }
        
        return result;
    }
    
    private Dictionary<string, ToolDefinition> GetAvailableTools(string? workspacePath, CodebaseContext? codebaseContext)
    {
        return new Dictionary<string, ToolDefinition>
        {
            ["read_file"] = new ToolDefinition
            {
                Name = "read_file",
                Description = "Read the complete contents of a file from the workspace. Use this to understand existing code before generating.",
                Parameters = new { path = "string: relative path to file (e.g. 'Services/ChessGame.cs')" }
            },
            ["search_codebase"] = new ToolDefinition
            {
                Name = "search_codebase",
                Description = "Semantic search across the codebase using vector embeddings. Returns relevant code snippets. Use this to find how things are implemented.",
                Parameters = new { query = "string: natural language query (e.g. 'how are services registered')" }
            },
            ["list_files"] = new ToolDefinition
            {
                Name = "list_files",
                Description = "List all files in a directory. Use this to explore the project structure.",
                Parameters = new { directory = "string: relative directory path (e.g. 'Services/' or '.')" }
            },
            ["grep"] = new ToolDefinition
            {
                Name = "grep",
                Description = "Search for a pattern across all files. Use this to find specific code patterns or references.",
                Parameters = new { pattern = "string: search pattern (e.g. 'AddScoped')" }
            },
            ["get_file_relationships"] = new ToolDefinition
            {
                Name = "get_file_relationships",
                Description = "Get related files using Neo4j graph. Returns files that are commonly edited together or reference each other.",
                Parameters = new { file_path = "string: file to find relationships for" }
            },
            ["compile_code"] = new ToolDefinition
            {
                Name = "compile_code",
                Description = "Compile the generated code and check for errors. Use this BEFORE finalizing your code to catch compilation errors early.",
                Parameters = new { files = "array of {path: string, content: string} to compile" }
            },
            ["check_lints"] = new ToolDefinition
            {
                Name = "check_lints",
                Description = "Run linter/code analyzer on your code. Checks for style issues, unused variables, etc.",
                Parameters = new { files = "array of {path: string, content: string} to lint" }
            },
            ["search_replace"] = new ToolDefinition
            {
                Name = "search_replace",
                Description = "Make surgical edits to a file. Search for exact text and replace it. Much safer than regenerating entire files!",
                Parameters = new { 
                    file_path = "string: path to file", 
                    old_string = "string: exact text to find and replace",
                    new_string = "string: replacement text"
                }
            },
            ["create_file"] = new ToolDefinition
            {
                Name = "create_file",
                Description = "Create a new file with the given content.",
                Parameters = new { 
                    file_path = "string: path to new file", 
                    content = "string: file content"
                }
            },
            ["create_directory"] = new ToolDefinition
            {
                Name = "create_directory",
                Description = "Create a directory (folder). Use this to organize code into subdirectories.",
                Parameters = new { 
                    path = "string: relative directory path (e.g. 'Models/' or 'Services/Chess/')"
                }
            },
            ["run_code"] = new ToolDefinition
            {
                Name = "run_code",
                Description = "Execute your code and see runtime output/errors. Use this to test if your code actually works!",
                Parameters = new { 
                    files = "array of {path: string, content: string} to run",
                    entry_point = "string: optional entry point (e.g. 'Program.cs')"
                }
            }
        };
    }
    
    private async Task<OllamaToolResponse> GenerateWithToolCallsAsync(
        List<ConversationMessage> conversation,
        Dictionary<string, ToolDefinition> tools,
        PromptMetadata? toolUsageRules,
        CancellationToken cancellationToken)
    {
        // Build prompt with tool definitions
        var prompt = BuildPromptWithTools(conversation, tools, toolUsageRules);
        
        // Call Ollama (models like qwen2.5-coder support function calling)
        var response = await _ollama.GenerateAsync(
            model: "qwen2.5-coder:14b", // Use a model that supports tools
            prompt: prompt,
            cancellationToken: cancellationToken);
        
        // Parse response for tool calls
        var parsed = ParseToolCalls(response.Response);
        
        return parsed;
    }
    
    private async Task<ToolExecutionResult> ExecuteToolAsync(
        ToolCall toolCall,
        string? workspacePath,
        string? jobWorkspacePath,
        CodebaseContext? codebaseContext,
        CancellationToken cancellationToken)
    {
        var result = new ToolExecutionResult
        {
            ToolName = toolCall.Name,
            Arguments = toolCall.Arguments
        };
        
        try
        {
            switch (toolCall.Name)
            {
                case "read_file":
                    result.Result = await ReadFileToolAsync(toolCall.Arguments, workspacePath, jobWorkspacePath, cancellationToken);
                    break;
                    
                case "search_codebase":
                    result.Result = await SearchCodebaseToolAsync(toolCall.Arguments, cancellationToken);
                    break;
                    
                case "list_files":
                    result.Result = await ListFilesToolAsync(toolCall.Arguments, workspacePath, jobWorkspacePath, cancellationToken);
                    break;
                    
                case "grep":
                    result.Result = await GrepToolAsync(toolCall.Arguments, workspacePath, cancellationToken);
                    break;
                    
                case "get_file_relationships":
                    result.Result = await GetFileRelationshipsToolAsync(toolCall.Arguments, cancellationToken);
                    break;
                    
                case "compile_code":
                    result.Result = await CompileCodeToolAsync(toolCall.Arguments, workspacePath, jobWorkspacePath, cancellationToken);
                    break;
                    
                case "check_lints":
                    result.Result = await CheckLintsToolAsync(toolCall.Arguments, cancellationToken);
                    break;
                    
                case "search_replace":
                    result.Result = await SearchReplaceToolAsync(toolCall.Arguments, workspacePath, jobWorkspacePath, cancellationToken);
                    break;
                    
                case "create_directory":
                    result.Result = await CreateDirectoryToolAsync(toolCall.Arguments, jobWorkspacePath, cancellationToken);
                    break;
                    
                case "create_file":
                    result.Result = await CreateFileToolAsync(toolCall.Arguments, jobWorkspacePath, cancellationToken);
                    break;
                    
                case "run_code":
                    result.Result = await RunCodeToolAsync(toolCall.Arguments, jobWorkspacePath, cancellationToken);
                    break;
                    
                default:
                    result.Result = $"ERROR: Unknown tool '{toolCall.Name}'";
                    _logger.LogWarning("Unknown tool requested: {Tool}", toolCall.Name);
                    break;
            }
            
            // Check if tool returned an error (even if it didn't throw exception)
            if (result.Result?.StartsWith("ERROR:") == true)
            {
                result.Success = false;
                _logger.LogWarning("⚠️ Tool {Tool} returned error: {Error}", 
                    toolCall.Name, 
                    result.Result.Length > 200 ? result.Result.Substring(0, 200) + "..." : result.Result);
            }
            else
            {
                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Result = $"ERROR: {ex.Message}";
            _logger.LogError(ex, "Tool execution failed: {Tool}", toolCall.Name);
        }
        
        return result;
    }
    
    // TOOL IMPLEMENTATIONS
    
    private async Task<string> ReadFileToolAsync(string arguments, string? workspacePath, string? jobWorkspacePath, CancellationToken cancellationToken)
    {
        // Parse arguments (simple JSON: {"path": "Services/ChessGame.cs"})
        // Fix malformed JSON (add quotes around property names if missing)
        var fixedArgs = FixMalformedJson(arguments);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
        var path = args?["path"] ?? throw new ArgumentException("Missing 'path' parameter");
        
        // 🔍 SMART FILE RESOLUTION - Check multiple locations in priority order:
        // 1. User's original workspace (existing code)
        // 2. Job workspace (files LLM just generated)
        // 3. Absolute path (if provided)
        
        var searchPaths = new List<(string Location, string FullPath)>();
        
        // Add user workspace if available
        if (!string.IsNullOrEmpty(workspacePath))
        {
            searchPaths.Add(("user workspace", Path.Combine(workspacePath, path)));
        }
        
        // Add job workspace if available
        if (!string.IsNullOrEmpty(jobWorkspacePath))
        {
            searchPaths.Add(("generated files", Path.Combine(jobWorkspacePath, path)));
        }
        
        // Add absolute path if it looks like one
        if (Path.IsPathRooted(path))
        {
            searchPaths.Add(("absolute path", path));
        }
        
        // Try each location
        foreach (var (location, fullPath) in searchPaths)
        {
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("📖 Reading file from {Location}: {Path}", location, path);
                
                var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                
                // Truncate if too large
                if (content.Length > 10000)
                {
                    content = content.Substring(0, 10000) + $"\n\n... (truncated, {content.Length - 10000} more characters)";
                }
                
                return $"File: {path} (from {location})\n\n{content}";
            }
        }
        
        // Not found anywhere
        var searchedLocations = string.Join(", ", searchPaths.Select(s => s.Location));
        return $"ERROR: File not found: {path}\nSearched in: {searchedLocations}";
    }
    
    private async Task<string> SearchCodebaseToolAsync(string arguments, CancellationToken cancellationToken)
    {
        var fixedArgs = FixMalformedJson(arguments);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
        var query = args?["query"] ?? throw new ArgumentException("Missing 'query' parameter");
        
        try
        {
            // Call MemoryAgent's smart search (Qdrant semantic search!)
            var results = await _memoryAgent.SmartSearchAsync(query, limit: 5, cancellationToken);
            
            if (!results.Any())
            {
                return "No results found";
            }
            
            var sb = new StringBuilder();
            sb.AppendLine($"Search results for: '{query}'\n");
            
            foreach (var (i, r) in results.Select((r, i) => (i + 1, r)))
            {
                sb.AppendLine($"{i}. {r.Path} (score: {r.Score:F2})");
                sb.AppendLine($"   {r.Snippet}");
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }
    
    private async Task<string> ListFilesToolAsync(string arguments, string? workspacePath, string? jobWorkspacePath, CancellationToken cancellationToken)
    {
        var fixedArgs = FixMalformedJson(arguments);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
        var directory = args?["directory"] ?? ".";
        
        // 🔍 SMART DIRECTORY RESOLUTION - Check multiple locations
        var searchPaths = new List<(string Location, string FullPath)>();
        
        if (!string.IsNullOrEmpty(workspacePath))
        {
            searchPaths.Add(("user workspace", Path.Combine(workspacePath, directory)));
        }
        
        if (!string.IsNullOrEmpty(jobWorkspacePath))
        {
            searchPaths.Add(("generated files", Path.Combine(jobWorkspacePath, directory)));
        }
        
        var results = new StringBuilder();
        var foundAny = false;
        
        foreach (var (location, fullPath) in searchPaths)
        {
            if (Directory.Exists(fullPath))
            {
                var files = Directory.GetFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
                    .Select(f => Path.GetFileName(f))
                    .ToList();
                
                var dirs = Directory.GetDirectories(fullPath, "*", SearchOption.TopDirectoryOnly)
                    .Select(d => Path.GetFileName(d) + "/")
                    .ToList();
                
                if (files.Any() || dirs.Any())
                {
                    // Show full root path for clarity
                    var locationLabel = location == "user workspace" 
                        ? $"EXISTING CODE (read-only reference): {fullPath}" 
                        : $"YOUR GENERATED CODE (read/write): {fullPath}";
                    
                    results.AppendLine($"\n📁 {locationLabel}");
                    
                    foreach (var dir in dirs.OrderBy(d => d))
                    {
                        results.AppendLine($"  {dir}");
                    }
                    
                    foreach (var file in files.OrderBy(f => f))
                    {
                        results.AppendLine($"  {file}");
                    }
                    
                    foundAny = true;
                }
            }
        }
        
        if (!foundAny)
        {
            var searchedLocations = string.Join(", ", searchPaths.Select(s => s.Location));
            return $"ERROR: Directory not found: {directory}\nSearched in: {searchedLocations}";
        }
        
        return results.ToString();
    }
    
    private async Task<string> GrepToolAsync(string arguments, string? workspacePath, CancellationToken cancellationToken)
    {
        var fixedArgs = FixMalformedJson(arguments);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
        var pattern = args?["pattern"] ?? throw new ArgumentException("Missing 'pattern' parameter");
        
        // Simple grep implementation (could enhance with ripgrep later)
        var results = new List<string>();
        var searchPath = workspacePath ?? "/workspace";
        
        foreach (var file in Directory.GetFiles(searchPath, "*.cs", SearchOption.AllDirectories).Take(100))
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = Path.GetRelativePath(searchPath, file);
                results.Add(relativePath);
            }
        }
        
        return $"Files containing '{pattern}':\n" + string.Join("\n", results);
    }
    
    private async Task<string> GetFileRelationshipsToolAsync(string arguments, CancellationToken cancellationToken)
    {
        var fixedArgs = FixMalformedJson(arguments);
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
        var filePath = args?["file_path"] ?? throw new ArgumentException("Missing 'file_path' parameter");
        
        try
        {
            // Call MemoryAgent to get co-edited files (Neo4j graph!)
            var related = await _memoryAgent.GetCoEditedFilesAsync(filePath, cancellationToken);
            
            return $"Files related to {filePath}:\n" + string.Join("\n", related.Select(r => $"- {r.Path} (score: {r.Score})"));
        }
        catch (Exception ex)
        {
            return $"Failed to get relationships: {ex.Message}";
        }
    }
    
    private async Task<string> CompileCodeToolAsync(string arguments, string? workspacePath, string? jobWorkspacePath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔨 Compiling code via tool call");
            
            // Parse files from arguments
            var fixedArgs = FixMalformedJson(arguments);
            _logger.LogDebug("📋 Compile tool args (fixed): {Args}", fixedArgs.Length > 500 ? fixedArgs.Substring(0, 500) + "..." : fixedArgs);
            
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fixedArgs);
            if (args == null || !args.ContainsKey("files"))
            {
                _logger.LogWarning("⚠️ Compile tool missing 'files' parameter. Args: {Args}", arguments.Length > 200 ? arguments.Substring(0, 200) + "..." : arguments);
                return "ERROR: Missing 'files' parameter. Expected format: {\"files\": [{\"path\": \"file.cs\", \"content\": \"...\"}]}";
            }
            
            var filesJson = args["files"].GetRawText();
            var files = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(filesJson);
            
            if (files == null || !files.Any())
            {
                return "ERROR: No files provided";
            }
            
            // Write files to a temp directory
            var tempDir = Path.Combine(jobWorkspacePath ?? Path.GetTempPath(), "compile_check");
            Directory.CreateDirectory(tempDir);
            
            var codeFiles = new List<AgentContracts.Responses.FileChange>();
            foreach (var file in files)
            {
                var path = file.ContainsKey("path") ? file["path"] : $"File{files.IndexOf(file)}.cs";
                var content = file.ContainsKey("content") ? file["content"] : "";
                
                codeFiles.Add(new AgentContracts.Responses.FileChange
                {
                    Path = path,
                    Content = content,
                    Type = FileChangeType.Created
                });
                
                var filePath = Path.Combine(tempDir, path);
                var fileDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }
                await File.WriteAllTextAsync(filePath, content, cancellationToken);
            }
            
            // Use dotnet build to compile (assuming .NET code)
            // For other languages, we'd call different compilers
            var buildResult = await CompileWithDotnetAsync(tempDir, codeFiles, cancellationToken);
            
            return buildResult;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "❌ Failed to parse compile tool arguments");
            return $"ERROR: Invalid JSON in tool arguments. The LLM generated malformed JSON.\n\nExpected format: {{\"files\": [{{\"path\": \"Calculator.cs\", \"content\": \"code here\"}}]}}\n\nError: {jsonEx.Message}\n\nTip: Try generating simpler code or fewer files at once.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compilation check failed");
            return $"ERROR: Compilation failed: {ex.Message}";
        }
    }
    
    private async Task<string> CompileWithDotnetAsync(string projectDir, List<AgentContracts.Responses.FileChange> files, CancellationToken cancellationToken)
    {
        try
        {
            // Create a minimal .csproj file
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";
            
            var csprojPath = Path.Combine(projectDir, "TempProject.csproj");
            await File.WriteAllTextAsync(csprojPath, csprojContent, cancellationToken);
            
            // Run dotnet build inside Docker container
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --rm -v {projectDir}:/project codingagent-dotnet-multi dotnet build /project/TempProject.csproj",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
            {
                return "ERROR: Failed to start compilation process";
            }
            
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            
            var sb = new StringBuilder();
            sb.AppendLine("COMPILATION RESULT:");
            sb.AppendLine();
            
            if (process.ExitCode == 0)
            {
                sb.AppendLine("✅ SUCCESS - Code compiles without errors!");
                return sb.ToString();
            }
            else
            {
                sb.AppendLine("❌ FAILED - Compilation errors found:");
                sb.AppendLine();
                sb.AppendLine(output);
                if (!string.IsNullOrEmpty(errors))
                {
                    sb.AppendLine(errors);
                }
                return sb.ToString();
            }
        }
        catch (Exception ex)
        {
            return $"ERROR: Compilation process failed: {ex.Message}";
        }
    }
    
    private async Task<string> SearchReplaceToolAsync(string arguments, string? workspacePath, string? jobWorkspacePath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("✏️ Performing search_replace");
            
            var fixedArgs = FixMalformedJson(arguments);
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
            var filePath = args?["file_path"] ?? throw new ArgumentException("Missing 'file_path' parameter");
            var oldString = args?["old_string"] ?? throw new ArgumentException("Missing 'old_string' parameter");
            var newString = args?["new_string"] ?? throw new ArgumentException("Missing 'new_string' parameter");
            
            // Find the file (check both workspaces)
            string? fullPath = null;
            string? location = null;
            
            if (!string.IsNullOrEmpty(jobWorkspacePath))
            {
                var jobPath = Path.Combine(jobWorkspacePath, filePath);
                if (File.Exists(jobPath))
                {
                    fullPath = jobPath;
                    location = "job workspace";
                }
            }
            
            if (fullPath == null && !string.IsNullOrEmpty(workspacePath))
            {
                var userPath = Path.Combine(workspacePath, filePath);
                if (File.Exists(userPath))
                {
                    fullPath = userPath;
                    location = "user workspace";
                }
            }
            
            if (fullPath == null)
            {
                return $"ERROR: File not found: {filePath}";
            }
            
            // Read file
            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            
            // Check if old_string exists
            if (!content.Contains(oldString))
            {
                return $"ERROR: Could not find the old_string in {filePath}. Make sure the text matches exactly (including whitespace/indentation).";
            }
            
            // Count occurrences
            var occurrences = (content.Length - content.Replace(oldString, "").Length) / oldString.Length;
            
            if (occurrences > 1)
            {
                return $"ERROR: Found {occurrences} occurrences of old_string in {filePath}. search_replace requires a unique match. Please provide more context to make it unique.";
            }
            
            // Replace
            var newContent = content.Replace(oldString, newString);
            
            // Write back (always to job workspace)
            var outputPath = Path.Combine(jobWorkspacePath ?? Path.GetTempPath(), filePath);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            await File.WriteAllTextAsync(outputPath, newContent, cancellationToken);
            
            _logger.LogInformation("✅ search_replace: Updated {Path} ({Location})", filePath, location);
            
            return $"✅ SUCCESS: Updated {filePath}\n\nReplaced:\n{oldString.Substring(0, Math.Min(100, oldString.Length))}...\n\nWith:\n{newString.Substring(0, Math.Min(100, newString.Length))}...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "search_replace failed");
            return $"ERROR: search_replace failed: {ex.Message}";
        }
    }
    
    private async Task<string> CreateDirectoryToolAsync(string arguments, string? jobWorkspacePath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("📁 Creating new directory");
            
            var fixedArgs = FixMalformedJson(arguments);
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
            var dirPath = args?["path"] ?? throw new ArgumentException("Missing 'path' parameter");
            
            // Create directory in job workspace
            var outputPath = Path.Combine(jobWorkspacePath ?? Path.GetTempPath(), dirPath);
            Directory.CreateDirectory(outputPath);
            
            _logger.LogInformation("✅ Created directory: {Path}", dirPath);
            
            return $"✅ SUCCESS: Created directory {dirPath}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "create_directory failed");
            return $"ERROR: create_directory failed: {ex.Message}";
        }
    }
    
    private async Task<string> CreateFileToolAsync(string arguments, string? jobWorkspacePath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("📝 Creating new file");
            
            var fixedArgs = FixMalformedJson(arguments);
            var args = JsonSerializer.Deserialize<Dictionary<string, string>>(fixedArgs);
            var filePath = args?["file_path"] ?? throw new ArgumentException("Missing 'file_path' parameter");
            var content = args?["content"] ?? "";
            
            // Write to job workspace
            var outputPath = Path.Combine(jobWorkspacePath ?? Path.GetTempPath(), filePath);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            await File.WriteAllTextAsync(outputPath, content, cancellationToken);
            
            _logger.LogInformation("✅ Created file: {Path} ({Size} bytes)", filePath, content.Length);
            
            return $"✅ SUCCESS: Created {filePath} ({content.Length} bytes)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "create_file failed");
            return $"ERROR: create_file failed: {ex.Message}";
        }
    }
    
    private async Task<string> RunCodeToolAsync(string arguments, string? jobWorkspacePath, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🏃 Running code to check for runtime errors");
            
            // Parse files from arguments
            var fixedArgs = FixMalformedJson(arguments);
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fixedArgs);
            if (args == null || !args.ContainsKey("files"))
            {
                return "ERROR: Missing 'files' parameter";
            }
            
            var filesJson = args["files"].GetRawText();
            var files = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(filesJson);
            
            if (files == null || !files.Any())
            {
                return "ERROR: No files provided";
            }
            
            // Write files to a temp directory
            var tempDir = Path.Combine(jobWorkspacePath ?? Path.GetTempPath(), "run_check");
            Directory.CreateDirectory(tempDir);
            
            var codeFiles = new List<AgentContracts.Responses.FileChange>();
            foreach (var file in files)
            {
                var path = file.ContainsKey("path") ? file["path"] : $"File{files.IndexOf(file)}.cs";
                var content = file.ContainsKey("content") ? file["content"] : "";
                
                codeFiles.Add(new AgentContracts.Responses.FileChange
                {
                    Path = path,
                    Content = content,
                    Type = FileChangeType.Created
                });
                
                var filePath = Path.Combine(tempDir, path);
                var fileDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }
                await File.WriteAllTextAsync(filePath, content, cancellationToken);
            }
            
            // Run the code in Docker
            var runResult = await RunWithDotnetAsync(tempDir, codeFiles, cancellationToken);
            
            return runResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code execution failed");
            return $"ERROR: Execution failed: {ex.Message}";
        }
    }
    
    private async Task<string> RunWithDotnetAsync(string projectDir, List<AgentContracts.Responses.FileChange> files, CancellationToken cancellationToken)
    {
        try
        {
            // Create a minimal console app .csproj
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>";
            
            var csprojPath = Path.Combine(projectDir, "TempConsoleApp.csproj");
            await File.WriteAllTextAsync(csprojPath, csprojContent, cancellationToken);
            
            // Run dotnet run inside Docker container
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"run --rm -v {projectDir}:/project codingagent-dotnet-multi sh -c \"cd /project && dotnet build && dotnet run --no-build\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
            {
                return "ERROR: Failed to start execution process";
            }
            
            // Wait with timeout (max 30 seconds)
            using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            {
                try
                {
                    await process.WaitForExitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill();
                    return "ERROR: Execution timed out after 30 seconds";
                }
            }
            
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errors = await process.StandardError.ReadToEndAsync(cancellationToken);
            
            var sb = new StringBuilder();
            sb.AppendLine("RUNTIME EXECUTION RESULT:");
            sb.AppendLine();
            
            if (process.ExitCode == 0)
            {
                sb.AppendLine("✅ SUCCESS - Code ran without errors!");
                sb.AppendLine();
                sb.AppendLine("Output:");
                sb.AppendLine(output.Length > 2000 ? output.Substring(0, 2000) + "\n... (truncated)" : output);
            }
            else
            {
                sb.AppendLine($"❌ FAILED - Exit code: {process.ExitCode}");
                sb.AppendLine();
                sb.AppendLine("Runtime Errors:");
                sb.AppendLine(errors.Length > 2000 ? errors.Substring(0, 2000) + "\n... (truncated)" : errors);
                if (!string.IsNullOrEmpty(output))
                {
                    sb.AppendLine();
                    sb.AppendLine("Output:");
                    sb.AppendLine(output.Length > 1000 ? output.Substring(0, 1000) + "\n... (truncated)" : output);
                }
            }
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: Execution process failed: {ex.Message}";
        }
    }
    
    private async Task<string> CheckLintsToolAsync(string arguments, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔍 Running linter via tool call");
            
            // Parse files from arguments
            var fixedArgs = FixMalformedJson(arguments);
            _logger.LogDebug("📋 Lint tool args (fixed): {Args}", fixedArgs.Length > 500 ? fixedArgs.Substring(0, 500) + "..." : fixedArgs);
            
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fixedArgs);
            if (args == null || !args.ContainsKey("files"))
            {
                _logger.LogWarning("⚠️ Lint tool missing 'files' parameter. Args: {Args}", arguments.Length > 200 ? arguments.Substring(0, 200) + "..." : arguments);
                return "ERROR: Missing 'files' parameter. Expected format: {\"files\": [{\"path\": \"file.cs\", \"content\": \"...\"}]}";
            }
            
            var filesJson = args["files"].GetRawText();
            var files = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(filesJson);
            
            if (files == null || !files.Any())
            {
                return "ERROR: No files provided";
            }
            
            var sb = new StringBuilder();
            sb.AppendLine("LINTER RESULTS:");
            sb.AppendLine();
            
            foreach (var file in files)
            {
                var path = file.ContainsKey("path") ? file["path"] : "unknown";
                var content = file.ContainsKey("content") ? file["content"] : "";
                
                // Simple linting checks (could be enhanced with real linters)
                var issues = new List<string>();
                
                // Check for common issues
                if (content.Contains("TODO"))
                    issues.Add("Contains TODO comments");
                if (content.Contains("//") && content.Contains("fix", StringComparison.OrdinalIgnoreCase))
                    issues.Add("Contains 'fix' comments");
                if (!content.Contains("namespace") && path.EndsWith(".cs"))
                    issues.Add("Missing namespace declaration");
                
                sb.AppendLine($"File: {path}");
                if (issues.Any())
                {
                    foreach (var issue in issues)
                    {
                        sb.AppendLine($"  ⚠️ {issue}");
                    }
                }
                else
                {
                    sb.AppendLine("  ✅ No issues found");
                }
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "❌ Failed to parse lint tool arguments");
            return $"ERROR: Invalid JSON in tool arguments. The LLM generated malformed JSON.\n\nExpected format: {{\"files\": [{{\"path\": \"Calculator.cs\", \"content\": \"code here\"}}]}}\n\nError: {jsonEx.Message}\n\nTip: Try generating simpler code or fewer files at once.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Linting failed");
            return $"ERROR: Linting failed: {ex.Message}";
        }
    }
    
    // Helper methods for parsing and building prompts
    
    private string BuildSystemPrompt(string task, string? language, CodebaseContext? codebaseContext, string? workspacePath, string? jobWorkspacePath, ValidationFeedback? previousFeedback, PromptMetadata? promptMetadata, string? hierarchicalContext)
    {
        var sb = new StringBuilder();
        
        // 🌱 100% LIGHTNING-DRIVEN PROMPTS (NO FALLBACK!)
        if (promptMetadata == null || string.IsNullOrEmpty(promptMetadata.Content))
        {
            _logger.LogCritical("🚨 CRITICAL: Prompt 'agentic_coding_system_v1' not found in AI Lightning!");
            _logger.LogCritical("   → Prompts MUST be stored in Lightning (Qdrant/Neo4j)");
            _logger.LogCritical("   → Ensure PromptSeedService has run on startup");
            _logger.LogCritical("   → Check that promptseed.json exists and is valid");
            
            throw new InvalidOperationException(
                "Prompt not found in AI Lightning. " +
                "All prompts must be stored in Lightning for learning and evolution. " +
                "Ensure PromptSeedService.SeedPromptsAsync() has run successfully on startup.");
        }
        
        // Use prompt from Lightning
        _logger.LogInformation("📋 Using Lightning prompt: {Id} (success rate: {Rate:P0}, usage: {Count}, avg score: {Score:F1})",
            promptMetadata.Id, 
            promptMetadata.SuccessRate, 
            promptMetadata.UsageCount,
            promptMetadata.AvgScore);
        
        sb.AppendLine(promptMetadata.Content);
        sb.AppendLine();
        
        // ═══════════════════════════════════════════════════════════
        // DYNAMIC CONTEXT (Data, not instructions)
        // ═══════════════════════════════════════════════════════════
        
        // 📊 HIERARCHICAL CONTEXT (project overview, file summaries)
        if (!string.IsNullOrEmpty(hierarchicalContext))
        {
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine(hierarchicalContext);
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine();
        }
        
        // 📜 HISTORY (previous attempt failures/successes)
        if (previousFeedback?.History != null && previousFeedback.History.Any())
        {
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("📜 PREVIOUS ATTEMPTS - LEARN FROM THESE!");
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine(_historyFormatter.FormatHistoryForLLM(previousFeedback.History, maxAttempts: 2));
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine();
        }
        
        // 🗂️ CODEBASE CONTEXT (existing project structure, patterns, dependencies)
        if (codebaseContext != null && !codebaseContext.IsEmpty)
        {
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("🗂️ CODEBASE CONTEXT (Existing Project)");
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine(codebaseContext.ToLLMSummary());
            sb.AppendLine();
        }
        
        // 📂 FILE PATHS (where to search for files)
        if (!string.IsNullOrEmpty(workspacePath) || !string.IsNullOrEmpty(jobWorkspacePath))
        {
            sb.AppendLine("📂 FILE SEARCH PATHS:");
            if (!string.IsNullOrEmpty(workspacePath))
            {
                sb.AppendLine($"1. User workspace: {workspacePath}");
            }
            if (!string.IsNullOrEmpty(jobWorkspacePath))
            {
                sb.AppendLine($"2. Generated workspace: {jobWorkspacePath}");
            }
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private string BuildPromptWithTools(List<ConversationMessage> conversation, Dictionary<string, ToolDefinition> tools, PromptMetadata? toolUsageRules)
    {
        var sb = new StringBuilder();
        
        // Add conversation history
        foreach (var msg in conversation)
        {
            sb.AppendLine($"[{msg.Role}]: {msg.Content}");
            sb.AppendLine();
        }
        
        // Add tool definitions
        sb.AppendLine("AVAILABLE TOOLS:");
        foreach (var tool in tools.Values)
        {
            sb.AppendLine($"- {tool.Name}: {tool.Description}");
            sb.AppendLine($"  Parameters: {JsonSerializer.Serialize(tool.Parameters)}");
        }
        sb.AppendLine();
        
        // 🌱 TOOL USAGE RULES FROM AI LIGHTNING (no hardcoded prompts!)
        if (toolUsageRules != null)
        {
            sb.AppendLine(toolUsageRules.Content);
            sb.AppendLine();
        }
        else
        {
            _logger.LogWarning("⚠️ Tool usage rules not found in Lightning - falling back to basic instructions");
            sb.AppendLine("To finish, respond with your final code.");
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private OllamaToolResponse ParseToolCalls(string response)
    {
        // Parse tool calls from response
        // Format: TOOL_CALL: tool_name({"param": "value"})
        
        var toolCalls = new List<ToolCall>();
        var lines = response.Split('\n');
        
        foreach (var line in lines)
        {
            if (line.StartsWith("TOOL_CALL:", StringComparison.OrdinalIgnoreCase))
            {
                var callStr = line.Substring("TOOL_CALL:".Length).Trim();
                var match = System.Text.RegularExpressions.Regex.Match(callStr, @"(\w+)\((.+)\)");
                
                if (match.Success)
                {
                    toolCalls.Add(new ToolCall
                    {
                        Name = match.Groups[1].Value,
                        Arguments = match.Groups[2].Value
                    });
                }
            }
        }
        
        return new OllamaToolResponse
        {
            Content = response,
            ToolCalls = toolCalls.Any() ? toolCalls : null
        };
    }
    
    private string ExtractReasoning(List<ConversationMessage> conversation)
    {
        // Extract the thought process from conversation
        return string.Join("\n", conversation
            .Where(m => m.Role == "assistant" || m.Role == "tool")
            .Select(m => $"[{m.Role}]: {m.Content.Substring(0, Math.Min(200, m.Content.Length))}..."));
    }
    
    private string BuildReviewFeedback(SelfReviewResult review)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SELF-REVIEW FEEDBACK");
        sb.AppendLine();
        sb.AppendLine($"Status: {(review.Approved ? "APPROVED" : "NEEDS FIXES")}");
        sb.AppendLine($"Confidence: {review.Confidence:P0}");
        sb.AppendLine();
        
        if (review.Issues.Any())
        {
            sb.AppendLine("## Issues Found:");
            foreach (var issue in review.Issues)
            {
                sb.AppendLine($"- [{issue.Severity}] {issue.Description}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine($"Summary: {review.Summary}");
        sb.AppendLine();
        sb.AppendLine("Please address the issues above and regenerate the code.");
        
        return sb.ToString();
    }
    
    private List<AgentContracts.Responses.FileChange> ParseGeneratedFiles(string content, string language)
    {
        var files = new List<AgentContracts.Responses.FileChange>();
        
        // Known language tags that should NOT be treated as filenames
        var knownLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "csharp", "c#", "cs", "python", "py", "javascript", "js", "typescript", "ts",
            "razor", "html", "css", "scss", "sass", "json", "xml", "yaml", "yml",
            "sql", "bash", "sh", "powershell", "ps1", "go", "rust", "java", "kotlin",
            "swift", "ruby", "php", "dart", "flutter", "markdown", "md", "txt"
        };
        
        // Parse code fences: ```language Filename.ext or ```Filename.ext
        var pattern = @"```(?:(\w+)\s+)?([^\s\n]+)?\n(.*?)```";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern, 
            System.Text.RegularExpressions.RegexOptions.Singleline);
        
        var fileCounter = 1;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var lang = match.Groups[1].Value;
            var filename = match.Groups[2].Value;
            var code = match.Groups[3].Value.Trim();
            
            if (string.IsNullOrWhiteSpace(code))
                continue;
            
            // 🔍 FIX: If "filename" is actually a language tag, treat it as language instead!
            if (string.IsNullOrWhiteSpace(lang) && !string.IsNullOrWhiteSpace(filename))
            {
                if (knownLanguages.Contains(filename))
                {
                    lang = filename;
                    filename = ""; // Clear filename, will be generated below
                    _logger.LogDebug("📝 Detected language tag '{Lang}' misidentified as filename, correcting...", lang);
                }
            }
            
            // Infer filename if missing
            if (string.IsNullOrWhiteSpace(filename))
            {
                var ext = (lang.ToLowerInvariant()) switch
                {
                    "csharp" or "c#" or "cs" => ".cs",
                    "python" or "py" => ".py",
                    "javascript" or "js" => ".js",
                    "typescript" or "ts" => ".ts",
                    "razor" => ".razor",
                    "html" => ".html",
                    "css" => ".css",
                    "json" => ".json",
                    "xml" => ".xml",
                    _ => language switch
                    {
                        "csharp" => ".cs",
                        "python" => ".py",
                        "javascript" => ".js",
                        "typescript" => ".ts",
                        "razor" => ".razor",
                        _ => ".txt"
                    }
                };
                
                filename = $"Generated{fileCounter++}{ext}";
            }
            
            // Infer .razor for Blazor components
            if ((lang == "razor" || code.Contains("@page") || code.Contains("@code")) && 
                !filename.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            {
                filename += ".razor";
            }
            
            // 🔍 VALIDATION: Ensure filename has an extension and is not just a language name
            if (!Path.HasExtension(filename) || knownLanguages.Contains(Path.GetFileNameWithoutExtension(filename)))
            {
                _logger.LogWarning("⚠️ Invalid filename detected: '{Filename}', regenerating...", filename);
                var ext = (lang.ToLowerInvariant()) switch
                {
                    "csharp" or "c#" or "cs" => ".cs",
                    "razor" => ".razor",
                    _ => language == "csharp" ? ".cs" : ".txt"
                };
                filename = $"Generated{fileCounter++}{ext}";
            }
            
            files.Add(new AgentContracts.Responses.FileChange
            {
                Path = filename,
                Content = code,
                Type = FileChangeType.Created
            });
            
            _logger.LogDebug("📄 Parsed file: {Filename} ({Size} chars)", filename, code.Length);
        }
        
        _logger.LogInformation("✅ Parsed {Count} files from generated code", files.Count);
        
        return files;
    }
    
    /// <summary>
    /// Fix malformed JSON from LLM (add quotes around unquoted property names)
    /// Transforms: {path: "value"} → {"path": "value"}
    /// </summary>
    private string FixMalformedJson(string json)
    {
        try
        {
            // Try to deserialize as-is first (check if it's already valid JSON)
            using (JsonDocument.Parse(json))
            {
                return json; // Already valid
            }
        }
        catch (JsonException originalEx)
        {
            try
            {
                // Fix common LLM mistakes: unquoted property names
                // Pattern: {word: → {"word":
                // But avoid matching words that are already in strings or are array/object markers
                var fixedJson = System.Text.RegularExpressions.Regex.Replace(
                    json,
                    @"(?<![""'])\b(\w+):",  // Match: word: (but not if preceded by quote)
                    "\"$1\":"               // Replace with: "word":
                );
                
                // Validate the fix
                using (JsonDocument.Parse(fixedJson))
                {
                    _logger.LogDebug("🔧 Fixed malformed JSON: {Original} → {Fixed}", 
                        json.Length > 150 ? json.Substring(0, 150) + "..." : json,
                        fixedJson.Length > 150 ? fixedJson.Substring(0, 150) + "..." : fixedJson);
                    return fixedJson;
                }
            }
            catch (JsonException fixEx)
            {
                // Log both errors for debugging
                _logger.LogError("❌ Failed to fix malformed JSON. Original error: {OrigErr}, After fix: {FixErr}\nJSON: {Json}", 
                    originalEx.Message, fixEx.Message, 
                    json.Length > 300 ? json.Substring(0, 300) + "..." : json);
                
                // Return original - let the caller handle the error
                return json;
            }
        }
    }
}

// Supporting classes

public record ConversationMessage(string Role, string Content, string? ToolName = null);

public record ToolDefinition
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public object Parameters { get; init; } = new();
}

public record ToolCall
{
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "";
}

public record OllamaToolResponse
{
    public string Content { get; init; } = "";
    public List<ToolCall>? ToolCalls { get; init; }
}

public class AgenticCodingResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Task { get; set; } = "";
    public string Language { get; set; } = "";
    public string GeneratedCode { get; set; } = "";
    public List<AgentContracts.Responses.FileChange> GeneratedFiles { get; set; } = new();
    public string Reasoning { get; set; } = "";
    public int Iterations { get; set; }
    public int ToolCallsExecuted { get; set; }
    public List<ToolExecutionResult> ToolResults { get; set; } = new();
    public List<SelfReviewResult> SelfReviewResults { get; set; } = new();
}

public class ToolExecutionResult
{
    public string ToolName { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool Success { get; set; }
    public string Result { get; set; } = "";
}

