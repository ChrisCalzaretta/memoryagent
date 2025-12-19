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
                    top_k = 10 // Limit token choices for more focused selection
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

            _logger.LogDebug("📄 FunctionGemma raw response: {Response}", ollamaResponse.Response);

            // Parse FunctionGemma's plan
            var plan = ParseWorkflowPlan(ollamaResponse.Response);
            
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
        
        sb.AppendLine("You are a PRECISE tool selector. Your ONLY job is to pick the correct tool.");
        sb.AppendLine();
        sb.AppendLine("## 🎯 CRITICAL DECISION TREE (Follow this EXACTLY):");
        sb.AppendLine();
        sb.AppendLine("### Step 1: Identify Intent");
        sb.AppendLine("- Does request ask to FIND/LOCATE existing code? → **smartsearch**");
        sb.AppendLine("  Keywords: find, where, show me, locate, search for, get");
        sb.AppendLine();
        sb.AppendLine("- Does request ask to CREATE/BUILD NEW code? → **orchestrate_task**");
        sb.AppendLine("  Keywords: create, build, generate, write, make, develop (WITHOUT 'plan')");
        sb.AppendLine();
        sb.AppendLine("- Does request ask for PLAN/STRATEGY/BREAKDOWN? → **manage_plan**");
        sb.AppendLine("  Keywords: plan, strategy, roadmap, breakdown, execution plan");
        sb.AppendLine();
        sb.AppendLine("- Does request ask to CHECK STATUS? → **get_task_status**");
        sb.AppendLine("  Keywords: status, progress, check, how's it going");
        sb.AppendLine();
        sb.AppendLine("## 📚 Available Tools:");
        sb.AppendLine();
        
        // Show tools in priority order: search first, then generate, then plan
        var coreToolNames = new[] { "smartsearch", "orchestrate_task", "manage_plan", "get_task_status" };
        var coreTools = tools.Where(t => coreToolNames.Contains(t.Name)).ToList();
        
        // Order: search → generate → plan → status
        var orderedTools = new List<ToolDefinition>();
        orderedTools.Add(coreTools.FirstOrDefault(t => t.Name == "smartsearch")!);
        orderedTools.Add(coreTools.FirstOrDefault(t => t.Name == "orchestrate_task")!);
        orderedTools.Add(coreTools.FirstOrDefault(t => t.Name == "manage_plan")!);
        orderedTools.Add(coreTools.FirstOrDefault(t => t.Name == "get_task_status")!);
        
        foreach (var tool in orderedTools.Where(t => t != null))
        {
            sb.AppendLine($"### {tool.Name}");
            sb.AppendLine($"{tool.Description}");
            sb.AppendLine($"**Schema:** {JsonSerializer.Serialize(tool.InputSchema, _jsonOptions)}");
            sb.AppendLine();
        }

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
        sb.AppendLine("## ✅ CORRECT Examples (DO THIS):");
        sb.AppendLine();
        sb.AppendLine("### Example 1: FIND = smartsearch");
        sb.AppendLine("Request: \"Find all authentication code\"");
        sb.AppendLine("Analysis: Contains 'find' → USE smartsearch");
        sb.AppendLine(@"{""name"":""smartsearch"",""parameters"":{""query"":""authentication login"",""context"":""project""}}");
        sb.AppendLine();
        sb.AppendLine("### Example 2: WHERE = smartsearch");
        sb.AppendLine("Request: \"Where is the database connection code?\"");
        sb.AppendLine("Analysis: Contains 'where' → USE smartsearch");
        sb.AppendLine(@"{""name"":""smartsearch"",""parameters"":{""query"":""database connection"",""context"":""project""}}");
        sb.AppendLine();
        sb.AppendLine("### Example 3: SHOW ME = smartsearch");
        sb.AppendLine("Request: \"Show me how user registration works\"");
        sb.AppendLine("Analysis: Contains 'show me' → USE smartsearch");
        sb.AppendLine(@"{""name"":""smartsearch"",""parameters"":{""query"":""user registration"",""context"":""project""}}");
        sb.AppendLine();
        sb.AppendLine("### Example 4: CREATE CODE = orchestrate_task");
        sb.AppendLine("Request: \"Create a REST API in Python\"");
        sb.AppendLine("Analysis: 'create' + 'API' (no 'plan') → USE orchestrate_task");
        sb.AppendLine(@"{""name"":""orchestrate_task"",""parameters"":{""task"":""Create REST API with Flask"",""language"":""python""}}");
        sb.AppendLine();
        sb.AppendLine("### Example 5: BUILD = orchestrate_task");
        sb.AppendLine("Request: \"Build a shopping cart\"");
        sb.AppendLine("Analysis: 'build' → USE orchestrate_task");
        sb.AppendLine(@"{""name"":""orchestrate_task"",""parameters"":{""task"":""Build shopping cart with cart management"",""language"":""csharp""}}");
        sb.AppendLine();
        sb.AppendLine("### Example 6: CREATE PLAN = manage_plan");
        sb.AppendLine("Request: \"Create an execution plan for a chess game\"");
        sb.AppendLine("Analysis: 'create' + 'PLAN' → USE manage_plan");
        sb.AppendLine(@"{""name"":""manage_plan"",""parameters"":{""action"":""create"",""goal"":""chess game implementation""}}");
        sb.AppendLine();
        sb.AppendLine("## ❌ WRONG Examples (DO NOT DO THIS):");
        sb.AppendLine();
        sb.AppendLine("### WRONG 1:");
        sb.AppendLine("Request: \"Find authentication code\"");
        sb.AppendLine(@"❌ WRONG: {""name"":""manage_plan""}  ← NO! Contains 'find' → USE smartsearch");
        sb.AppendLine(@"✅ RIGHT: {""name"":""smartsearch"",""parameters"":{""query"":""authentication""}}");
        sb.AppendLine();
        sb.AppendLine("### WRONG 2:");
        sb.AppendLine("Request: \"Where is the API code?\"");
        sb.AppendLine(@"❌ WRONG: {""name"":""manage_plan""}  ← NO! Contains 'where' → USE smartsearch");
        sb.AppendLine(@"✅ RIGHT: {""name"":""smartsearch"",""parameters"":{""query"":""API code""}}");
        sb.AppendLine();
        sb.AppendLine("### WRONG 3:");
        sb.AppendLine("Request: \"Create a Python API\"");
        sb.AppendLine(@"❌ WRONG: {""name"":""manage_plan""}  ← NO! 'create API' (no 'plan') → USE orchestrate_task");
        sb.AppendLine(@"✅ RIGHT: {""name"":""orchestrate_task"",""parameters"":{""task"":""Create Python API""}}");
        sb.AppendLine();
        sb.AppendLine("## 🎯 FINAL INSTRUCTION:");
        sb.AppendLine("1. Read the user request carefully");
        sb.AppendLine("2. Check for keywords: find/where/show → smartsearch");
        sb.AppendLine("3. Check for: create/build (no 'plan') → orchestrate_task");
        sb.AppendLine("4. Check for: plan/roadmap/strategy → manage_plan");
        sb.AppendLine("5. Return ONLY JSON with 'name' and 'parameters'");
        sb.AppendLine("6. DO NOT default to manage_plan unless request explicitly mentions 'plan'");

        return sb.ToString();
    }

    private string BuildUserPrompt(string userRequest, Dictionary<string, object>? context)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("## User Request:");
        sb.AppendLine($"\"{userRequest}\"");
        sb.AppendLine();
        
        // Add explicit classification hints
        var lowerRequest = userRequest.ToLowerInvariant();
        sb.AppendLine("## Analysis:");
        
        if (lowerRequest.Contains("find") || lowerRequest.Contains("where") || lowerRequest.Contains("show me") || lowerRequest.Contains("locate"))
        {
            sb.AppendLine("- Request type: SEARCH for existing code");
            sb.AppendLine("- Required tool: smartsearch");
        }
        else if ((lowerRequest.Contains("create") || lowerRequest.Contains("build") || lowerRequest.Contains("generate")) 
                 && lowerRequest.Contains("plan"))
        {
            sb.AppendLine("- Request type: CREATE PLAN");
            sb.AppendLine("- Required tool: manage_plan");
        }
        else if (lowerRequest.Contains("create") || lowerRequest.Contains("build") || lowerRequest.Contains("generate") || lowerRequest.Contains("write"))
        {
            sb.AppendLine("- Request type: GENERATE new code");
            sb.AppendLine("- Required tool: orchestrate_task");
        }
        else if (lowerRequest.Contains("status") || lowerRequest.Contains("progress") || lowerRequest.Contains("check"))
        {
            sb.AppendLine("- Request type: CHECK STATUS");
            sb.AppendLine("- Required tool: get_task_status");
        }
        else
        {
            sb.AppendLine("- Request type: UNCLEAR → Default to smartsearch");
        }
        
        sb.AppendLine();

        if (context != null && context.Any())
        {
            sb.AppendLine("## Additional Context:");
            foreach (var (key, value) in context)
            {
                sb.AppendLine($"- {key}: {value}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Return the JSON function call based on the analysis above:");

        return sb.ToString();
    }

    private WorkflowPlan ParseWorkflowPlan(string response)
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
}

