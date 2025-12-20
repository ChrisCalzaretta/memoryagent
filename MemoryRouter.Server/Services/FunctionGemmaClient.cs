using System.Text;
using System.Text.Json;
using MemoryRouter.Server.Models;

namespace MemoryRouter.Server.Services;

/// <summary>
/// Client for calling FunctionGemma via Ollama for intelligent tool routing
/// FunctionGemma is trained to understand function calling patterns and create execution plans
/// </summary>
public class FunctionGemmaClient : IFunctionGemmaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FunctionGemmaClient> _logger;
    
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public FunctionGemmaClient(HttpClient httpClient, ILogger<FunctionGemmaClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WorkflowPlan> PlanWorkflowAsync(
        string userRequest,
        IEnumerable<ToolDefinition> availableTools,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🤖 FunctionGemma planning workflow for: {Request}", userRequest);

        // Build system prompt with tool definitions
        var systemPrompt = BuildSystemPrompt(availableTools);
        
        // Build user prompt with request and context
        var userPrompt = BuildUserPrompt(userRequest, context);

        try
        {
            // Call Ollama with FunctionGemma
            var request = new
            {
                model = "functiongemma:latest",
                prompt = userPrompt,
                system = systemPrompt,
                format = "json",
                stream = false,
                options = new
                {
                    temperature = 0.1, // Ultra-low for deterministic, rule-based routing
                    top_p = 0.8,
                    top_k = 10, // Limit token choices for more focused selection
                    num_predict = 512 // Ensure enough tokens for complete response
                }
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            _logger.LogDebug("🔄 Calling Ollama FunctionGemma...");
            var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseJson, _jsonOptions);

            if (ollamaResponse?.Response == null)
            {
                throw new InvalidOperationException("FunctionGemma returned empty response");
            }

            // Check for incomplete/garbage response (common with streaming issues or model corruption)
            if (ollamaResponse.Response.Length < 10 || 
                !ollamaResponse.Response.Contains("name") ||
                ollamaResponse.Response.Contains("IIII") ||
                ollamaResponse.Response.Contains("{{{{") ||
                ollamaResponse.Response.All(c => c == '{' || c == '"' || c == ':' || char.IsWhiteSpace(c)))
            {
                _logger.LogWarning("⚠️ FunctionGemma returned garbage response (length: {Length}): {Response}", 
                    ollamaResponse.Response.Length, ollamaResponse.Response.Substring(0, Math.Min(100, ollamaResponse.Response.Length)));
                
                // Try to reset Ollama model
                _logger.LogInformation("🔄 Attempting to reset Ollama model...");
                await ResetOllamaModelAsync(cancellationToken);
                
                throw new InvalidOperationException($"FunctionGemma returned garbage response - model has been reset, please retry");
            }

            _logger.LogDebug("📄 FunctionGemma raw response: {Response}", ollamaResponse.Response);

            // Parse FunctionGemma's plan (pass tools for validation)
            var plan = ParseWorkflowPlan(ollamaResponse.Response, availableTools);
            
            _logger.LogInformation("✅ FunctionGemma generated plan with {StepCount} steps", plan.FunctionCalls.Count);
            foreach (var step in plan.FunctionCalls)
            {
                _logger.LogInformation("  📌 Step {Order}: {Tool} - {Reasoning}", 
                    step.Order, step.Name, step.Reasoning);
            }

            return plan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ FunctionGemma planning failed");
            throw new InvalidOperationException($"Failed to generate workflow plan: {ex.Message}", ex);
        }
    }

    private string BuildSystemPrompt(IEnumerable<ToolDefinition> tools)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("You are a PRECISE tool selector. Your ONLY job is to pick the EXACT tool that matches the user's request.");
        sb.AppendLine();
        sb.AppendLine("## 🎯 CRITICAL RULES:");
        sb.AppendLine();
        sb.AppendLine("1. **EXACT MATCH**: If user says 'use tool X', pick tool X (even if it sounds similar to another tool)");
        sb.AppendLine("2. **KEYWORDS MATTER**: Pay attention to SPECIFIC word combinations:");
        sb.AppendLine("   - 'list tasks' or 'list all tasks' → use 'list_tasks' tool (NOT get_task_status!)");
        sb.AppendLine("   - 'index' file/directory → use 'index' tool");
        sb.AppendLine("   - 'workspace status' → use 'workspace_status' tool");
        sb.AppendLine("   - 'task status' for ONE task → use 'get_task_status' tool");
        sb.AppendLine("   - 'search' or 'find' code → use 'smartsearch' tool");
        sb.AppendLine("   - 'create code' → use 'orchestrate_task' tool");
        sb.AppendLine("   - 'create plan' → use 'manage_plan' tool");
        sb.AppendLine();
        sb.AppendLine("3. **DO NOT default to smartsearch** unless explicitly searching/finding code");
        sb.AppendLine("4. **Read tool descriptions** carefully - each tool has a specific purpose");
        sb.AppendLine();
        sb.AppendLine("## 📚 Available Tools (CHOOSE ONLY FROM THIS LIST):");
        sb.AppendLine();
        
        // Group tools by category - show NAMES ONLY to keep prompt concise
        var toolsByCategory = tools.GroupBy(t => t.Category).OrderBy(g => g.Key);
        
        foreach (var categoryGroup in toolsByCategory)
        {
            sb.AppendLine($"### {categoryGroup.Key}:");
            var toolNames = categoryGroup.OrderBy(t => t.Name).Select(t => t.Name);
            sb.AppendLine($"  {string.Join(", ", toolNames)}");
            sb.AppendLine();
        }
        
        sb.AppendLine("## 🔍 MOST COMMON TOOLS (use these 90% of the time):");
        sb.AppendLine();
        sb.AppendLine("**smartsearch** - Find/search/locate existing code (use for: find, where, show me, locate)");
        sb.AppendLine("**index** - Index files into memory (use for: index file, index directory, make searchable)");
        sb.AppendLine("**orchestrate_task** - Generate/create/build NEW code (use for: create, build, generate code)");
        sb.AppendLine("**manage_plan** - Create execution plans (use for: create plan, roadmap, strategy)");
        sb.AppendLine("**workspace_status** - Get workspace overview (use for: workspace status, what do you know)");
        sb.AppendLine("**get_workflow_status** - Check WORKFLOW progress (use for: workflow status, UUID workflow ID)");
        sb.AppendLine("**list_workflows** - List all workflows (use for: list workflows, show workflows)");
        sb.AppendLine("**get_task_status** - Check CODE GENERATION progress (use for: job_ status, coding task)");
        sb.AppendLine("**list_tasks** - List all coding tasks (use for: list tasks, show tasks, active tasks)");
        sb.AppendLine();
        sb.AppendLine("⚠️ **IMPORTANT ID FORMATS**:");
        sb.AppendLine("  - UUID (abc-123-def-...) = WORKFLOW → use get_workflow_status");
        sb.AppendLine("  - job_20251220... = CODE GEN JOB → use get_task_status");
        sb.AppendLine();
        sb.AppendLine("⚠️ **CRITICAL**: You MUST choose a tool name from the list above. DO NOT invent new tool names!");

        sb.AppendLine("## Your Task:");
        sb.AppendLine("1. Analyze the user's request carefully");
        sb.AppendLine("2. Determine which SINGLE tool is most appropriate");
        sb.AppendLine("3. Decide what parameters to pass to that tool");
        sb.AppendLine("4. Return a structured JSON function call");
        sb.AppendLine();
        sb.AppendLine("## Response Format:");
        sb.AppendLine("Return ONLY valid JSON in this exact format (no markdown, no extra text):");
        sb.AppendLine(@"{
  ""name"": ""tool_name"",
  ""parameters"": {
    ""param1"": ""value1"",
    ""param2"": ""value2""
  }
}");
        sb.AppendLine();
        sb.AppendLine("## ⚠️ CRITICAL RULES (DO NOT VIOLATE):");
        sb.AppendLine();
        sb.AppendLine("1. **FINDING = smartsearch** (NOT manage_plan)");
        sb.AppendLine("   - \"find X\" → smartsearch");
        sb.AppendLine("   - \"where is X\" → smartsearch");
        sb.AppendLine("   - \"show me X\" → smartsearch");
        sb.AppendLine("   - \"locate X\" → smartsearch");
        sb.AppendLine();
        sb.AppendLine("2. **CREATING CODE = orchestrate_task** (NOT manage_plan)");
        sb.AppendLine("   - \"create an API\" → orchestrate_task");
        sb.AppendLine("   - \"build a feature\" → orchestrate_task");
        sb.AppendLine("   - \"generate code for X\" → orchestrate_task");
        sb.AppendLine();
        sb.AppendLine("3. **CREATING PLAN = manage_plan** (ONLY if request explicitly says 'plan')");
        sb.AppendLine("   - \"create a plan\" → manage_plan");
        sb.AppendLine("   - \"create an execution plan\" → manage_plan");
        sb.AppendLine("   - \"roadmap for X\" → manage_plan");
        sb.AppendLine();
        sb.AppendLine("4. If request contains BOTH 'find' AND 'plan' → CHOOSE 'find' (smartsearch)");
        sb.AppendLine("5. If unclear → Use smartsearch (default to search)");
        sb.AppendLine();
        sb.AppendLine("## ✅ Quick Examples (with CORRECT parameters):");
        sb.AppendLine(@"1. ""Find auth code"" → {""name"":""smartsearch"",""parameters"":{""query"":""authentication""}}");
        sb.AppendLine(@"2. ""Index file X"" → {""name"":""index"",""parameters"":{""path"":""X"",""scope"":""file""}}");
        sb.AppendLine(@"3. ""Create REST API"" → {""name"":""orchestrate_task"",""parameters"":{""task"":""Create REST API""}}");
        sb.AppendLine(@"4. ""Workspace status"" → {""name"":""workspace_status"",""parameters"":{}}");
        sb.AppendLine(@"5. ""List tasks"" → {""name"":""list_tasks"",""parameters"":{}}");
        sb.AppendLine(@"6. ""Get status of job_20251220162648_70b81f2f"" → {""name"":""get_task_status"",""parameters"":{""jobId"":""job_20251220162648_70b81f2f""}}");
        sb.AppendLine(@"7. ""Show generated files for job_20251220162648_70b81f2f"" → {""name"":""get_generated_files"",""parameters"":{""jobId"":""job_20251220162648_70b81f2f""}}");
        sb.AppendLine(@"8. ""Workflow status for a70a1678-18b4-44b4-b074-e96c6d132552"" → {""name"":""get_workflow_status"",""parameters"":{""workflowId"":""a70a1678-18b4-44b4-b074-e96c6d132552""}}");
        sb.AppendLine(@"9. ""List workflows"" → {""name"":""list_workflows"",""parameters"":{}}");
        sb.AppendLine();
        sb.AppendLine("## ⚠️ PARAMETER RULES (CRITICAL - GET THESE RIGHT!):");
        sb.AppendLine();
        sb.AppendLine(@"**get_workflow_status** (for WORKFLOW IDs - UUID format):");
        sb.AppendLine(@"  Schema: {""workflowId"":""string""} (REQUIRED)");
        sb.AppendLine(@"  Workflow ID format: UUID (e.g., a70a1678-18b4-44b4-b074-e96c6d132552)");
        sb.AppendLine(@"  Example: {""name"":""get_workflow_status"",""parameters"":{""workflowId"":""a70a1678-18b4-44b4-b074-e96c6d132552""}}");
        sb.AppendLine();
        sb.AppendLine(@"**list_workflows**:");
        sb.AppendLine(@"  Schema: {} (EMPTY OBJECT - NO PARAMETERS!)");
        sb.AppendLine(@"  Example: {""name"":""list_workflows"",""parameters"":{}}");
        sb.AppendLine();
        sb.AppendLine(@"**list_tasks**:");
        sb.AppendLine(@"  Schema: {} (EMPTY OBJECT - NO PARAMETERS!)");
        sb.AppendLine(@"  Example: {""name"":""list_tasks"",""parameters"":{}}");
        sb.AppendLine();
        sb.AppendLine(@"**get_task_status** (for CODE GEN JOBS - job_YYYYMMDDHHMMSS format):");
        sb.AppendLine(@"  Schema: {""jobId"":""string""} (REQUIRED)");
        sb.AppendLine(@"  Job ID format: job_YYYYMMDDHHMMSS_XXXXXXXX (e.g., job_20251220162648_70b81f2f)");
        sb.AppendLine(@"  Example: {""name"":""get_task_status"",""parameters"":{""jobId"":""job_20251220162648_70b81f2f""}}");
        sb.AppendLine();
        sb.AppendLine(@"**get_generated_files**:");
        sb.AppendLine(@"  Schema: {""jobId"":""string"",""outputPath"":""string""} (BOTH REQUIRED)");
        sb.AppendLine(@"  Use when: user asks for 'generated files', 'show files', 'what was generated'");
        sb.AppendLine(@"  Example: {""name"":""get_generated_files"",""parameters"":{""jobId"":""job_20251220162648_70b81f2f"",""outputPath"":"".""}}");
        sb.AppendLine(@"  NOTE: outputPath defaults to '.' (current directory) if not specified");
        sb.AppendLine();
        sb.AppendLine(@"**workspace_status**:");
        sb.AppendLine(@"  Schema: {} (EMPTY OBJECT - NO PARAMETERS!)");
        sb.AppendLine(@"  Example: {""name"":""workspace_status"",""parameters"":{}}");
        sb.AppendLine();
        sb.AppendLine(@"**smartsearch**:");
        sb.AppendLine(@"  Schema: {""query"":""string""}");
        sb.AppendLine(@"  Example: {""name"":""smartsearch"",""parameters"":{""query"":""auth code""}}");
        sb.AppendLine();
        sb.AppendLine(@"**index**:");
        sb.AppendLine(@"  Schema: {""path"":""string"",""scope"":""file|directory""}");
        sb.AppendLine(@"  Example: {""name"":""index"",""parameters"":{""path"":""/src/app.py"",""scope"":""file""}}");
        sb.AppendLine();
        sb.AppendLine(@"**orchestrate_task**:");
        sb.AppendLine(@"  Schema: {""task"":""string""}");
        sb.AppendLine(@"  Example: {""name"":""orchestrate_task"",""parameters"":{""task"":""Create API""}}");
        sb.AppendLine();
        sb.AppendLine("## 🎯 CRITICAL INSTRUCTIONS:");
        sb.AppendLine("1. **ALWAYS** follow the 'Required tool' in the Analysis section EXACTLY");
        sb.AppendLine("2. The Analysis section is PRE-COMPUTED and CORRECT");
        sb.AppendLine("3. DO NOT override the Analysis with your own keyword matching");
        sb.AppendLine("4. Return ONLY JSON: {\"name\":\"<tool from Analysis>\",\"parameters\":{...}}");
        sb.AppendLine("5. If Analysis says 'workspace_status', use workspace_status (NOT get_task_status!)");
        sb.AppendLine("6. If Analysis says 'list_tasks', use list_tasks (NOT get_task_status!)");

        return sb.ToString();
    }

    private string BuildUserPrompt(string userRequest, Dictionary<string, object>? context)
    {
        var sb = new StringBuilder();
        
        // Pre-classify the request and put the answer RIGHT AT THE TOP
        var lowerRequest = userRequest.ToLowerInvariant();
        string toolName = "smartsearch"; // default
        string parameters = "{}";
        
        // Check for "list" FIRST (before "status") - more specific intent
        if (lowerRequest.Contains("list") && (lowerRequest.Contains("task") || lowerRequest.Contains("job")))
        {
            toolName = "list_tasks";
            parameters = "{}";
        }
        else if (lowerRequest.Contains("workspace") && lowerRequest.Contains("status"))
        {
            toolName = "workspace_status";
            parameters = "{}";
        }
        // Check for "generated files" - route to get_generated_files
        else if (lowerRequest.Contains("generated") && lowerRequest.Contains("file"))
        {
            toolName = "get_generated_files";
            // Extract job ID (CodingOrchestrator format: job_YYYYMMDDHHMMSS_XXXXXXXX)
            var jobIdMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"(job_\d{14}_[a-f0-9]+)");
            var outputPath = context?.ContainsKey("workspacePath") == true ? context["workspacePath"].ToString() : ".";
            var escapedPath = outputPath?.Replace("\\", "\\\\") ?? ".";
            if (jobIdMatch.Success)
            {
                parameters = $"{{\"jobId\":\"{jobIdMatch.Value}\",\"outputPath\":\"{escapedPath}\"}}";
            }
            else
            {
                parameters = $"{{\"outputPath\":\"{escapedPath}\"}}";
            }
        }
        // Check for "workflow status" specifically - route to get_workflow_status
        else if (lowerRequest.Contains("workflow") && (lowerRequest.Contains("status") || lowerRequest.Contains("progress")))
        {
            var uuidMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})");
            if (uuidMatch.Success)
            {
                toolName = "get_workflow_status";
                parameters = $"{{\"workflowId\":\"{uuidMatch.Value}\"}}";
            }
            else
            {
                toolName = "list_workflows"; // No workflow ID = list all workflows
                parameters = "{}";
            }
        }
        // Check for "list workflows" 
        else if (lowerRequest.Contains("list") && lowerRequest.Contains("workflow"))
        {
            toolName = "list_workflows";
            parameters = "{}";
        }
        // Check for "status" BEFORE "index" to handle "status on indexing"
        else if (lowerRequest.Contains("status") || lowerRequest.Contains("progress") || lowerRequest.Contains("check"))
        {
            // CodingOrchestrator jobs: job_YYYYMMDDHHMMSS_XXXXXXXX → get_task_status
            // MemoryRouter workflows: UUID format → get_workflow_status
            var codingOrchestratorMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"(job_\d{14}_[a-f0-9]+)");
            var uuidMatch = System.Text.RegularExpressions.Regex.Match(lowerRequest, @"([a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12})");
            
            if (codingOrchestratorMatch.Success)
            {
                // CodingOrchestrator job → get_task_status
                toolName = "get_task_status";
                parameters = $"{{\"jobId\":\"{codingOrchestratorMatch.Value}\"}}";
            }
            else if (uuidMatch.Success)
            {
                // UUID = MemoryRouter workflow → get_workflow_status (NOT get_task_status!)
                toolName = "get_workflow_status";
                parameters = $"{{\"workflowId\":\"{uuidMatch.Value}\"}}";
            }
            else
            {
                toolName = "list_tasks"; // No job ID = list all tasks
                parameters = "{}";
            }
        }
        else if (lowerRequest.Contains("index"))
        {
            toolName = "index";
            var path = context?.ContainsKey("workspacePath") == true ? context["workspacePath"].ToString() : "/workspace";
            var ctx = context?.ContainsKey("context") == true ? context["context"].ToString() : "default";
            // Properly escape backslashes for JSON
            var escapedPath = path?.Replace("\\", "\\\\") ?? "/workspace";
            parameters = $"{{\"path\":\"{escapedPath}\",\"scope\":\"directory\",\"context\":\"{ctx}\"}}";
        }
        else if (lowerRequest.Contains("find") || lowerRequest.Contains("where") || lowerRequest.Contains("show me") || lowerRequest.Contains("locate") || lowerRequest.Contains("search"))
        {
            toolName = "smartsearch";
            var query = lowerRequest.Replace("find", "").Replace("where", "").Replace("show me", "").Replace("locate", "").Replace("search", "").Trim();
            var ctx = context?.ContainsKey("context") == true ? context["context"].ToString() : "default";
            // Escape quotes in query for JSON
            var escapedQuery = query.Replace("\"", "\\\"");
            parameters = $"{{\"query\":\"{escapedQuery}\",\"context\":\"{ctx}\"}}";
        }
        else if ((lowerRequest.Contains("create") || lowerRequest.Contains("build") || lowerRequest.Contains("generate")) 
                 && lowerRequest.Contains("plan"))
        {
            toolName = "manage_plan";
            parameters = "{\"action\":\"create\"}";
        }
        else if (lowerRequest.Contains("create") || lowerRequest.Contains("build") || lowerRequest.Contains("generate") || lowerRequest.Contains("write"))
        {
            toolName = "orchestrate_task";
            parameters = $"{{\"task\":\"{userRequest}\"}}";
        }
        
        // Direct, simple instruction
        sb.AppendLine($"User wants: \"{userRequest}\"");
        sb.AppendLine();
        sb.AppendLine($"TOOL TO USE: {toolName}");
        sb.AppendLine($"PARAMETERS: {parameters}");
        sb.AppendLine();
        sb.AppendLine("Return ONLY this JSON (no extra text):");
        sb.AppendLine($"{{\"name\":\"{toolName}\",\"parameters\":{parameters}}}");

        return sb.ToString();
    }

    private WorkflowPlan ParseWorkflowPlan(string response, IEnumerable<ToolDefinition>? availableTools = null)
    {
        try
        {
            // Clean up response (remove markdown, extra text)
            var cleaned = CleanJsonResponse(response);
            
            _logger.LogDebug("🧹 Cleaned JSON: {Cleaned}", cleaned);

            // Parse as Google's function call format
            var googleCall = JsonSerializer.Deserialize<GoogleFunctionCall>(cleaned, _jsonOptions);
            
            if (googleCall == null || string.IsNullOrEmpty(googleCall.Name))
            {
                throw new InvalidOperationException("Parsed function call is empty or invalid");
            }

            _logger.LogInformation("✅ Parsed Google function call: {Name}", googleCall.Name);
            
            // VALIDATE: Check if tool exists (prevent hallucinated tool names)
            if (availableTools != null)
            {
                var toolExists = availableTools.Any(t => t.Name.Equals(googleCall.Name, StringComparison.OrdinalIgnoreCase));
                if (!toolExists)
                {
                    _logger.LogWarning("⚠️ FunctionGemma hallucinated tool name: {Name}", googleCall.Name);
                    
                    // Try to find a similar tool name (fuzzy match)
                    var similarTool = availableTools.FirstOrDefault(t => 
                        t.Name.Contains(googleCall.Name, StringComparison.OrdinalIgnoreCase) ||
                        googleCall.Name.Contains(t.Name, StringComparison.OrdinalIgnoreCase));
                    
                    if (similarTool != null)
                    {
                        _logger.LogInformation("🔄 Correcting to similar tool: {ToolName}", similarTool.Name);
                        googleCall.Name = similarTool.Name;
                    }
                    else
                    {
                        // Default to smartsearch for unknown tools
                        _logger.LogWarning("🔄 Defaulting to smartsearch (hallucinated tool not found)");
                        googleCall.Name = "smartsearch";
                        googleCall.Parameters = new Dictionary<string, object>
                        {
                            ["query"] = response,
                            ["context"] = "project"
                        };
                    }
                }
            }

            // Convert Google's format to our WorkflowPlan format
            var arguments = googleCall.Parameters ?? new Dictionary<string, object>();
            
            var workflowPlan = new WorkflowPlan
            {
                Reasoning = $"Execute {googleCall.Name}",
                FunctionCalls = new List<FunctionCall>
                {
                    new FunctionCall
                    {
                        Name = googleCall.Name,
                        Arguments = arguments,
                        Reasoning = $"Selected tool: {googleCall.Name}",
                        Order = 1
                    }
                }
            };

            return workflowPlan;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse FunctionGemma response: {Response}", response);
            throw new InvalidOperationException($"FunctionGemma returned invalid JSON: {ex.Message}", ex);
        }
    }

    private string CleanJsonResponse(string response)
    {
        // Remove markdown code blocks
        response = response.Trim();
        if (response.StartsWith("```json"))
        {
            response = response[7..]; // Remove ```json
        }
        if (response.StartsWith("```"))
        {
            response = response[3..]; // Remove ```
        }
        if (response.EndsWith("```"))
        {
            response = response[..^3]; // Remove trailing ```
        }

        // Find the first { and last }
        int start = response.IndexOf('{');
        int end = response.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            response = response.Substring(start, end - start + 1);
        }

        return response.Trim();
    }

    // Response model for Ollama
    private class OllamaResponse
    {
        public string? Response { get; set; }
    }

    /// <summary>
    /// Reset Ollama model by unloading and reloading it
    /// </summary>
    private async Task ResetOllamaModelAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Unload the model
            var unloadRequest = new
            {
                model = "functiongemma:latest",
                keep_alive = 0 // Unload immediately
            };
            var unloadJson = JsonSerializer.Serialize(unloadRequest, _jsonOptions);
            var unloadContent = new StringContent(unloadJson, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("📤 Unloading functiongemma model...");
            await _httpClient.PostAsync("/api/generate", unloadContent, cancellationToken);
            
            // Wait a moment
            await Task.Delay(2000, cancellationToken);
            
            // Reload the model with a simple prompt
            var reloadRequest = new
            {
                model = "functiongemma:latest",
                prompt = "test",
                stream = false,
                options = new { num_predict = 1 }
            };
            var reloadJson = JsonSerializer.Serialize(reloadRequest, _jsonOptions);
            var reloadContent = new StringContent(reloadJson, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("📥 Reloading functiongemma model...");
            await _httpClient.PostAsync("/api/generate", reloadContent, cancellationToken);
            
            _logger.LogInformation("✅ Ollama model reset completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to reset Ollama model, but continuing anyway");
        }
    }
}


