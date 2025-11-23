# AI Agent Framework Pattern Detection - Complete Guide

**Based on:** [Microsoft Agent Framework Documentation](https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview)

---

## 📘 Overview

Microsoft Agent Framework is the **next generation** of both Semantic Kernel and AutoGen, created by the same teams. It unifies and extends their capabilities while adding new enterprise-grade features for building production AI agents.

This pattern detector identifies **33+ AI agent patterns** across:
- ✅ Microsoft Agent Framework (current, recommended)
- ⚠️ Semantic Kernel (legacy, migration recommended)
- ⚠️ AutoGen (legacy, migration recommended)

---

## 🎯 Why Agent Framework Matters

### The Evolution
```
AutoGen (Multi-Agent Patterns)
        +
Semantic Kernel (Enterprise Features)
        ↓
Microsoft Agent Framework (Unified, Next-Gen)
```

### Key Improvements
1. **Workflows** - Explicit control over multi-agent execution (vs. AutoGen's implicit orchestration)
2. **Type Safety** - Strong typing for messages and state (vs. AutoGen's dynamic typing)
3. **State Management** - Thread-based conversation state with checkpointing
4. **MCP Integration** - Native Model Context Protocol support for tool calling
5. **Enterprise Features** - Telemetry, filters, middleware, resilience

---

## 🔍 Detected Patterns

### 1. Agent Framework Patterns (Current Best Practices)

#### 1.1 ChatCompletionAgent (AI Agent Creation)
**Pattern:** `AgentFramework_ChatCompletionAgent`

```csharp
// BEST PRACTICE: Modern AI agent creation
var agent = new ChatCompletionAgent
{
    Name = "CustomerSupportAgent",
    Instructions = "You are a helpful customer support agent...",
    Model = "gpt-4",
    Tools = new[] { searchTool, ticketTool }
};
```

**When to Use:**
- ✅ Unstructured tasks requiring autonomous decision-making
- ✅ Multi-turn conversations with context
- ✅ Tasks requiring tool calling and exploration

**When NOT to Use:**
- ❌ Well-defined tasks with fixed steps (use functions instead!)
- ❌ Tasks requiring strict rule adherence
- ❌ Simple CRUD operations

**Benefits:**
- LLM-powered reasoning
- Native tool calling
- Context-aware responses

---

#### 1.2 Workflows (Multi-Step Orchestration)
**Pattern:** `AgentFramework_Workflow`

```csharp
// BEST PRACTICE: Complex multi-step AI processes
public class CustomerOnboardingWorkflow : Workflow
{
    public override async Task<WorkflowResult> ExecuteAsync(WorkflowContext context)
    {
        // Step 1: Validate customer info (agent)
        var validation = await _validationAgent.ProcessAsync(context.Input);
        
        // Step 2: Check duplicates (function)
        var isDuplicate = await CheckDuplicateAsync(validation);
        
        if (isDuplicate)
            return WorkflowResult.Failed("Duplicate found");
        
        // Step 3: Create account (agent)
        var account = await _creationAgent.ProcessAsync(validation);
        
        // Step 4: Send welcome email (function)
        await SendWelcomeEmailAsync(account);
        
        return WorkflowResult.Success(account);
    }
}
```

**Advantages over Single Agents:**
- ✅ Explicit execution flow (vs. agent-decided flow)
- ✅ Checkpointing for long-running processes
- ✅ Type-safe message passing
- ✅ Mix agents + functions + external APIs
- ✅ Human-in-the-loop support

**Use Cases:**
- Multi-step business processes
- Complex customer journeys
- Long-running approvals
- Tasks requiring multiple specialized agents

---

#### 1.3 AgentThread (State Management)
**Pattern:** `AgentFramework_AgentThread`

```csharp
// BEST PRACTICE: Thread-based conversation state
var thread = await agentClient.CreateThreadAsync();

// Multi-turn conversation with persistent state
await thread.AddUserMessageAsync("Book a flight to Seattle");
var response1 = await agent.RunAsync(thread);

await thread.AddUserMessageAsync("Actually, make it Portland");
var response2 = await agent.RunAsync(thread); // Context from previous turn

// Retrieve full conversation history
var history = await thread.GetMessagesAsync();
```

**Benefits:**
- Persistent conversation context
- Enterprise-grade state management
- Automatic message threading
- Built-in history retrieval

**Comparison:**
- **Agent Framework:** Thread-based (enterprise-grade)
- **Semantic Kernel:** ChatHistory (good, but less robust)
- **AutoGen:** ConversationHistory (basic)

---

#### 1.4 MCP Server Integration (Tool Calling)
**Pattern:** `AgentFramework_McpIntegration`

```csharp
// BEST PRACTICE: MCP server for tool discovery and calling
var mcpClient = new McpClient("http://localhost:5098/mcp");

var agent = new ChatCompletionAgent
{
    Name = "ResearchAgent",
    Instructions = "You can search databases and files...",
    McpServers = new[] { mcpClient }  // Auto-discover tools
};

// Agent can now call MCP tools dynamically
var result = await agent.ProcessAsync("Search for pattern detection code");
// Agent calls: mcp_code-memory_search("pattern detection")
```

**MCP Protocol Benefits:**
- ✅ Standardized tool interface
- ✅ Dynamic tool discovery
- ✅ Type-safe tool calling
- ✅ Server-side validation

**Your MCP Server Supports:**
- `search_patterns` - Find code patterns
- `validate_best_practices` - Check compliance
- `get_recommendations` - Get architecture advice
- `get_available_best_practices` - List all practices

---

#### 1.5 Agent Middleware (Interceptors)
**Pattern:** `AgentFramework_Middleware`

```csharp
// BEST PRACTICE: Safety, logging, telemetry via middleware
public class SafetyMiddleware : IAgentMiddleware
{
    public async Task<AgentResponse> OnInvokeAsync(
        AgentInvocationContext context,
        Func<Task<AgentResponse>> next)
    {
        // Pre-processing: Content safety check
        if (ContainsUnsafeContent(context.UserMessage))
        {
            return AgentResponse.Error("Unsafe content detected");
        }

        // Invoke agent
        var response = await next();

        // Post-processing: Log telemetry
        await _telemetry.LogAgentInvocationAsync(context, response);

        return response;
    }
}

// Register middleware
agent.UseMiddleware<SafetyMiddleware>();
```

**Use Cases:**
- 🛡️ Content safety checks
- 📊 Telemetry and monitoring
- 🔒 Authentication/authorization
- ⏱️ Rate limiting
- 📝 Audit logging

---

#### 1.6 Checkpointing (Fault Tolerance)
**Pattern:** `AgentFramework_Checkpointing`

```csharp
// BEST PRACTICE: Save state for long-running workflows
public class LongRunningWorkflow : Workflow
{
    public override async Task<WorkflowResult> ExecuteAsync(WorkflowContext context)
    {
        // Step 1: Process data (may take hours)
        var processed = await ProcessLargeDatasetAsync(context.Input);
        await context.SaveCheckpointAsync("step1_complete", processed);

        // Step 2: Wait for approval (may take days)
        var approval = await WaitForApprovalAsync(processed);
        await context.SaveCheckpointAsync("step2_approved", approval);

        // Step 3: Finalize
        var result = await FinalizeAsync(approval);
        return WorkflowResult.Success(result);
    }

    // Resume from checkpoint on server restart
    public override async Task<WorkflowResult> ResumeAsync(WorkflowContext context)
    {
        var checkpoint = await context.LoadCheckpointAsync();
        
        switch (checkpoint.Step)
        {
            case "step1_complete":
                // Resume from step 2
                var approval = await WaitForApprovalAsync(checkpoint.Data);
                // ...
        }
    }
}
```

**Benefits:**
- ✅ Survive server restarts
- ✅ Human-in-the-loop workflows
- ✅ Audit trail
- ✅ Debugging complex flows

---

### 2. Multi-Agent Orchestration Patterns

#### 2.1 Sequential Orchestration
**Pattern:** `MultiAgent_Sequential`

```csharp
// BEST PRACTICE: Chain of specialized agents
public async Task<string> ProcessDocumentAsync(string document)
{
    // Agent 1: Extract entities
    var entities = await _extractionAgent.ProcessAsync(document);
    
    // Agent 2: Classify sentiment (uses entity results)
    var sentiment = await _sentimentAgent.ProcessAsync(entities);
    
    // Agent 3: Generate summary (uses both)
    var summary = await _summaryAgent.ProcessAsync($"{entities}\n{sentiment}");
    
    return summary;
}
```

**When to Use:**
- Each step depends on previous results
- Clear linear flow
- Specialized agents per task

---

#### 2.2 Concurrent Orchestration
**Pattern:** `MultiAgent_Concurrent`

```csharp
// BEST PRACTICE: Parallel execution for speed
public async Task<AnalysisResult> AnalyzeCodebaseAsync(string path)
{
    // Run multiple agents in parallel
    var tasks = new[]
    {
        _securityAgent.AnalyzeAsync(path),
        _performanceAgent.AnalyzeAsync(path),
        _qualityAgent.AnalyzeAsync(path),
        _complianceAgent.AnalyzeAsync(path)
    };

    var results = await Task.WhenAll(tasks);
    
    return CombineResults(results);
}
```

**Benefits:**
- ⚡ Faster execution (parallel processing)
- 🎯 Independent specialized agents
- 📊 Comprehensive analysis

---

#### 2.3 Handoff Pattern
**Pattern:** `MultiAgent_Handoff`

```csharp
// BEST PRACTICE: Transfer between specialized agents
public async Task<string> HandleCustomerQueryAsync(string query)
{
    var currentAgent = _triageAgent;
    var context = query;

    while (true)
    {
        var response = await currentAgent.ProcessAsync(context);

        // Check if handoff needed
        if (response.NeedsHandoff)
        {
            currentAgent = GetSpecializedAgent(response.HandoffTo);
            context = response.Context;
            continue;
        }

        return response.FinalAnswer;
    }
}

private IAgent GetSpecializedAgent(string specialty)
{
    return specialty switch
    {
        "billing" => _billingAgent,
        "technical" => _technicalAgent,
        "sales" => _salesAgent,
        _ => _generalAgent
    };
}
```

**Use Cases:**
- Customer support escalation
- Task specialization
- Dynamic routing based on expertise

---

#### 2.4 Magentic Pattern (Dynamic Routing)
**Pattern:** `MultiAgent_Magentic`

```csharp
// BEST PRACTICE: AI-powered agent selection
public async Task<string> RouteTaskAsync(string task)
{
    // Use LLM to determine best agent
    var routing = await _routingAgent.DetermineAgentAsync(task);
    
    var selectedAgent = routing.AgentType switch
    {
        "code_generation" => _codeAgent,
        "data_analysis" => _dataAgent,
        "writing" => _writingAgent,
        _ => _generalAgent
    };

    return await selectedAgent.ProcessAsync(task);
}
```

**Benefits:**
- 🎯 Optimal agent selection
- 🔀 Flexible routing logic
- 🧠 LLM-powered decisions

---

### 3. Semantic Kernel Patterns (Legacy → Migration)

#### 3.1 Kernel Functions / Plugins
**Pattern:** `SemanticKernel_Plugin`

```csharp
// LEGACY PATTERN (Still supported, but migrate to Agent Framework)
public class WeatherPlugin
{
    [KernelFunction]
    [Description("Get current weather for a city")]
    public async Task<string> GetWeatherAsync(string city)
    {
        var weather = await _weatherService.GetWeatherAsync(city);
        return $"Weather in {city}: {weather.Temperature}°F, {weather.Condition}";
    }
}

// Usage
kernel.ImportPluginFromObject(new WeatherPlugin());
```

**Migration Path:**
```csharp
// MODERN APPROACH: Agent Framework + MCP
// Instead of [KernelFunction], create MCP tool:
public class McpWeatherService
{
    [McpTool("get_weather")]
    public async Task<McpToolResult> GetWeatherAsync(string city)
    {
        // MCP-compliant tool
    }
}
```

**Why Migrate:**
- ✅ Standardized MCP protocol
- ✅ Better tool discovery
- ✅ Works with any MCP-compliant client

---

#### 3.2 Semantic Kernel Planners
**Pattern:** `SemanticKernel_Planner_Legacy`

```csharp
// ⚠️ ANTI-PATTERN: SK Planners are deprecated
var planner = new ActionPlanner(kernel);
var plan = await planner.CreatePlanAsync("Book a hotel and flight");
var result = await plan.InvokeAsync();
```

**❌ Problems:**
- Unreliable planning
- Non-deterministic execution
- Hard to debug
- No checkpointing

**✅ MIGRATION → Agent Framework Workflows:**
```csharp
public class TravelBookingWorkflow : Workflow
{
    public override async Task<WorkflowResult> ExecuteAsync(WorkflowContext context)
    {
        // Explicit, debuggable, checkpointed flow
        var flight = await BookFlightAsync(context.Destination, context.Dates);
        await context.SaveCheckpointAsync("flight_booked", flight);
        
        var hotel = await BookHotelAsync(context.Destination, context.Dates);
        await context.SaveCheckpointAsync("hotel_booked", hotel);
        
        return WorkflowResult.Success(new { flight, hotel });
    }
}
```

---

### 4. AutoGen Patterns (Legacy → Migration)

#### 4.1 ConversableAgent
**Pattern:** `AutoGen_ConversableAgent`

```csharp
// ⚠️ LEGACY: AutoGen ConversableAgent
var agent = new ConversableAgent("assistant", llm_config);
```

**✅ MIGRATION → ChatCompletionAgent:**
```csharp
// MODERN: Agent Framework ChatCompletionAgent
var agent = new ChatCompletionAgent
{
    Name = "assistant",
    Model = "gpt-4",
    Instructions = "...",
    Tools = mcpTools
};
```

**Benefits:**
- Enterprise state management (AgentThread)
- Type-safe tool calling
- Better telemetry
- Middleware support

---

#### 4.2 GroupChat
**Pattern:** `AutoGen_GroupChat`

```csharp
// ⚠️ LEGACY: AutoGen GroupChat
var groupChat = new GroupChat(new[] { agent1, agent2, agent3 });
```

**✅ MIGRATION → Workflow:**
```csharp
// MODERN: Explicit workflow with control flow
public class CollaborationWorkflow : Workflow
{
    public override async Task<WorkflowResult> ExecuteAsync(WorkflowContext context)
    {
        var ideation = await _brainstormAgent.ProcessAsync(context.Input);
        var critique = await _critiqueAgent.ProcessAsync(ideation);
        var refinement = await _refinementAgent.ProcessAsync(critique);
        
        return WorkflowResult.Success(refinement);
    }
}
```

**Why:**
- ✅ Predictable execution
- ✅ Debuggable flow
- ✅ Type safety
- ✅ Checkpointing

---

## 🚫 Anti-Patterns Detected

### Anti-Pattern 1: Agent for Structured Tasks
**Pattern:** `AntiPattern_AgentForStructuredTask`

```csharp
// ❌ ANTI-PATTERN: Using agent for well-defined task
var agent = new ChatCompletionAgent
{
    Instructions = "Calculate order total: sum all item prices and add tax"
};
var total = await agent.ProcessAsync($"Items: {items}, Tax: 8%");
```

**Why It's Bad:**
- 💸 Unnecessary LLM cost
- ⏱️ Added latency
- 🎲 Non-deterministic (might get wrong answer!)

**✅ CORRECT APPROACH:**
```csharp
// Use a function!
public decimal CalculateOrderTotal(List<OrderItem> items, decimal taxRate)
{
    var subtotal = items.Sum(i => i.Price * i.Quantity);
    return subtotal * (1 + taxRate);
}
```

**Rule:** _If you can write a function, don't use an agent!_

---

### Anti-Pattern 2: Single Agent with Too Many Tools
**Pattern:** `AntiPattern_TooManyTools`

```csharp
// ❌ ANTI-PATTERN: 50 tools registered on one agent
agent.AddTool(tool1);
agent.AddTool(tool2);
// ... 48 more tools
agent.AddTool(tool50);
```

**Problems:**
- 🧠 LLM context overload
- 🎯 Poor tool selection
- 💸 Higher costs
- 📉 Lower accuracy

**✅ SOLUTION → Multi-Agent Workflow:**
```csharp
// Create specialized agents with fewer tools each
var databaseAgent = new ChatCompletionAgent
{
    Name = "DatabaseAgent",
    Tools = new[] { queryTool, updateTool, backupTool } // 3 tools
};

var fileAgent = new ChatCompletionAgent
{
    Name = "FileAgent",
    Tools = new[] { readTool, writeTool, searchTool } // 3 tools
};

// Workflow routes to appropriate agent
public async Task<string> ProcessRequestAsync(string request)
{
    var category = await DetermineCategory(request);
    
    var agent = category switch
    {
        "database" => databaseAgent,
        "files" => fileAgent,
        // ... more specialized agents
    };
    
    return await agent.ProcessAsync(request);
}
```

**Rule:** _Max 10-15 tools per agent. Use workflows for complex tasks._

---

## 📊 Pattern Detection Output Example

After reindexing your project, you'll see:

```
AI Agent Framework Analysis:
├── Agent Framework Patterns: 24 instances
│   ├── ChatCompletionAgent: 8 (✅ modern)
│   ├── Workflows: 5 (✅ best practice)
│   ├── AgentThread: 12 (✅ state management)
│   ├── MCP Integration: 6 (✅ tool calling)
│   └── Middleware: 3 (✅ safety/logging)
│
├── Semantic Kernel Patterns: 15 instances
│   ├── Plugins: 12 (⚠️ migrate to MCP)
│   ├── Planners: 2 (❌ deprecated, migrate to workflows)
│   └── Filters: 3 (✅ still supported)
│
├── AutoGen Patterns: 4 instances
│   ├── ConversableAgent: 3 (⚠️ migrate to ChatCompletionAgent)
│   └── GroupChat: 1 (⚠️ migrate to Workflow)
│
└── Multi-Agent Orchestration: 8 instances
    ├── Sequential: 4
    ├── Concurrent: 2
    ├── Handoff: 1
    └── Magentic: 1

Anti-Patterns Found: 2
├── Agent for Structured Task: 1 (❌ use functions)
└── Too Many Tools: 1 (❌ split into workflows)

Overall AI Agent Maturity: 78%
Recommendation: Migrate 5 legacy patterns to Agent Framework
```

---

## 🎯 Best Practices Summary

### DO ✅
1. **Use Agent Framework** for new projects (not SK or AutoGen)
2. **Use Workflows** for complex multi-step processes
3. **Use AgentThread** for conversation state management
4. **Use MCP** for tool integration (standardized protocol)
5. **Use Middleware** for safety, logging, telemetry
6. **Use Checkpointing** for long-running workflows
7. **Use Functions** for well-defined tasks (not agents!)
8. **Use Specialized Agents** in workflows (not one mega-agent)

### DON'T ❌
1. **Don't use agents for structured tasks** (use functions)
2. **Don't register >15 tools per agent** (use workflows)
3. **Don't use SK Planners** (use AF Workflows instead)
4. **Don't use AutoGen GroupChat** (use AF Workflows)
5. **Don't ignore migration warnings** (AF is the future)

---

## 🚀 Integration with Your MCP Server

Your Memory Code Agent already uses MCP! Here's how it integrates:

### Current Implementation:
```csharp
// Your McpService.cs
public class McpService : IMcpService
{
    // 4 pattern detection tools
    public async Task<McpToolResult> SearchPatternsAsync(...);
    public async Task<McpToolResult> ValidateBestPracticesAsync(...);
    public async Task<McpToolResult> GetRecommendationsAsync(...);
    public async Task<McpToolResult> GetAvailableBestPracticesAsync(...);
}
```

### Now Detects:
- ✅ Your MCP server implementation patterns
- ✅ AI agent usage in your code
- ✅ Migration opportunities (SK/AutoGen → AF)
- ✅ Anti-patterns and best practices

---

## 📚 References

1. **Microsoft Agent Framework**  
   https://learn.microsoft.com/en-us/agent-framework/overview/agent-framework-overview

2. **Semantic Kernel** (Legacy)  
   https://learn.microsoft.com/en-us/semantic-kernel/overview/

3. **AutoGen** (Legacy)  
   https://microsoft.github.io/autogen/

4. **Model Context Protocol (MCP)**  
   https://modelcontextprotocol.io/introduction

5. **Migration from Semantic Kernel**  
   https://learn.microsoft.com/en-us/agent-framework/migrate-from-semantic-kernel

6. **Migration from AutoGen**  
   https://learn.microsoft.com/en-us/agent-framework/migrate-from-autogen

---

## ✅ Summary

You now have **deep pattern detection** for:
- 📘 Microsoft Agent Framework (current, recommended)
- 📕 Semantic Kernel (legacy, migration paths provided)
- 📙 AutoGen (legacy, migration paths provided)
- 🎯 Multi-agent orchestration patterns
- 🚫 Anti-patterns and best practices
- 🔧 MCP integration patterns

This enables:
- ✅ Automated compliance checking against AI agent best practices
- ✅ Migration recommendations from legacy frameworks
- ✅ Anti-pattern detection
- ✅ Architecture analysis
- ✅ Best practice enforcement

**Ready to detect AI agent patterns in your code!** 🚀

