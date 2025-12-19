# 🧠 MemoryRouter - FunctionGemma-Powered Smart Agent Router

MemoryRouter is an intelligent orchestration layer that uses **FunctionGemma** to automatically route development tasks to the right tools. It provides a single entry point (`execute_task`) that understands natural language requests and creates optimal execution plans.

## 🎯 What It Does

MemoryRouter acts as the **brain** of the MemoryAgent system:

```
┌────────────────────────────────────────────────────────────┐
│  Cursor IDE (User)                                         │
│  "Create a REST API with authentication"                   │
└──────────────────────┬─────────────────────────────────────┘
                       │ MCP Call: execute_task
                       ▼
┌────────────────────────────────────────────────────────────┐
│  🧠 MemoryRouter                                           │
│  ┌──────────────────────────────────────────────────────┐ │
│  │  FunctionGemma (AI Decision Maker)                   │ │
│  │  • Analyzes request                                  │ │
│  │  • Discovers available tools                         │ │
│  │  • Creates execution plan                            │ │
│  └──────────────────────────────────────────────────────┘ │
└──────────────────────┬─────────────────────────────────────┘
                       │
        ┌──────────────┴────────────────┐
        ▼                               ▼
┌──────────────────┐          ┌──────────────────┐
│  📚 MemoryAgent  │          │ 🎯 CodingOrch    │
│  • Search        │          │ • Generate Code  │
│  • Index         │          │ • Validate       │
│  • Learn         │          │ • Design         │
└──────────────────┘          └──────────────────┘
```

## 🚀 Key Features

### 1. **Single Entry Point**
- One tool: `execute_task`
- Natural language input
- Automatic tool discovery
- No need to know which tools exist

### 2. **FunctionGemma Intelligence**
- Analyzes your request
- Decides which tools to call
- Determines optimal order
- Handles context passing

### 3. **Smart Workflows**
- **Search Before Generate**: Finds existing code before creating new
- **Validate After Generate**: Automatically checks quality
- **Multi-Step Planning**: Breaks complex tasks into steps

## 📋 Available Tools (Auto-Discovered)

### MemoryAgent Tools (18+)
- `semantic_search` - Find code by meaning
- `smart_search` - Advanced multi-strategy search
- `explain_code` - Understand how code works
- `index_workspace` - Make code searchable
- `validate_pattern` - Check against best practices
- `create_plan` - Break down complex tasks
- `learn_from_conversation` - Remember decisions
- And more...

### CodingOrchestrator Tools (11+)
- `orchestrate_task` - Generate code (any language)
- `get_task_status` - Check generation progress
- `design_create_brand` - Create brand systems
- `design_validate` - Check UI compliance
- And more...

## 🎮 Usage Examples

### Example 1: Simple Search
```javascript
// User: "Find all authentication code"

// FunctionGemma's Plan:
{
  "reasoning": "User wants to search for existing authentication code",
  "functionCalls": [
    {
      "name": "semantic_search",
      "arguments": { "query": "authentication" },
      "reasoning": "Search memory for auth-related code",
      "order": 1
    }
  ]
}
```

### Example 2: Code Generation with Context
```javascript
// User: "Create a user service with authentication"

// FunctionGemma's Plan:
{
  "reasoning": "Search for existing patterns first, then generate using those patterns",
  "functionCalls": [
    {
      "name": "semantic_search",
      "arguments": { "query": "user service patterns" },
      "reasoning": "Find existing user service implementations",
      "order": 1
    },
    {
      "name": "semantic_search",
      "arguments": { "query": "authentication patterns" },
      "reasoning": "Find auth implementation patterns",
      "order": 2
    },
    {
      "name": "orchestrate_task",
      "arguments": {
        "task": "Create UserService with authentication",
        "context": "Results from previous searches"
      },
      "reasoning": "Generate service using found patterns",
      "order": 3
    }
  ]
}
```

### Example 3: Design System Workflow
```javascript
// User: "Design a brand for my fintech app and validate the homepage"

// FunctionGemma's Plan:
{
  "reasoning": "Create brand, then validate existing UI against it",
  "functionCalls": [
    {
      "name": "design_create_brand",
      "arguments": {
        "brand_name": "Fintech App",
        "industry": "Finance",
        "personality_traits": ["Professional", "Trustworthy", "Modern"]
      },
      "reasoning": "Create comprehensive brand system",
      "order": 1
    },
    {
      "name": "semantic_search",
      "arguments": { "query": "homepage UI code" },
      "reasoning": "Find the homepage code to validate",
      "order": 2
    },
    {
      "name": "design_validate",
      "arguments": {
        "context": "fintech-app",
        "code": "{{results_from_step_2}}"
      },
      "reasoning": "Check homepage against brand guidelines",
      "order": 3
    }
  ]
}
```

## 🏗️ Architecture

### Components

#### 1. **FunctionGemmaClient**
- Calls Ollama with `functiongemma:latest` model
- Sends: User request + available tools
- Receives: Execution plan (JSON)
- Handles: JSON parsing, retries, errors

#### 2. **ToolRegistry**
- Discovers all tools from MemoryAgent and CodingOrchestrator
- Provides tool definitions to FunctionGemma
- Enables tool search by keywords/description

#### 3. **RouterService**
- Executes workflows step-by-step
- Calls MemoryAgent or CodingOrchestrator as needed
- Handles context passing between steps
- Manages errors and retries

#### 4. **McpHandler**
- Exposes MCP protocol endpoints
- Provides `execute_task` tool to Cursor
- Formats results for display

## 🔧 Configuration

### appsettings.json
```json
{
  "Ollama": {
    "BaseUrl": "http://ollama:11435"  // FunctionGemma endpoint
  },
  "MemoryAgent": {
    "BaseUrl": "http://memory-agent:5000"
  },
  "CodingOrchestrator": {
    "BaseUrl": "http://coding-orchestrator:5003"
  }
}
```

### Docker Compose
```yaml
memory-router:
  build:
    context: .
    dockerfile: MemoryRouter.Server/Dockerfile
  ports:
    - "5010:5010"
  environment:
    - Ollama__BaseUrl=http://ollama:11435
    - MemoryAgent__BaseUrl=http://memory-agent-server:5000
    - CodingOrchestrator__BaseUrl=http://memory-coding-orchestrator:5003
  depends_on:
    - mcp-server
    - coding-orchestrator
```

## 🧪 Testing

### Run Unit Tests
```bash
cd MemoryRouter.Server.Tests
dotnet test
```

### Test Coverage
- ✅ FunctionGemmaClient (7 tests)
  - Valid JSON parsing
  - Markdown code block handling
  - Context passing
  - Error handling
  
- ✅ ToolRegistry (10 tests)
  - Tool discovery
  - Tool search
  - Validation
  
- ✅ RouterService (8 tests)
  - Workflow execution
  - Multi-step coordination
  - Error recovery
  
- ✅ Integration scenarios (4 tests)
  - End-to-end workflows
  - Complex multi-tool tasks

## 🌟 Why FunctionGemma?

FunctionGemma is specifically trained for function calling:

- ✅ **Understands tool definitions** - Reads JSON schemas naturally
- ✅ **Plans logically** - Search before generate, validate after
- ✅ **Handles context** - Passes data between steps
- ✅ **Fast** - Smaller model, quick planning
- ✅ **Deterministic** - Consistent decisions with low temperature

## 🚦 Status Codes

### Success
- `200` - Workflow completed successfully
- All steps executed
- Final result available

### Partial Success
- `207` - Some steps failed
- Partial results available
- Error details in response

### Failure
- `500` - Workflow failed
- Planning error or critical tool failure
- Error message in response

## 📊 Monitoring

### Logs
```csharp
🧠 [Request abc-123] Processing: Create user service
📋 [Request abc-123] Asking FunctionGemma to plan workflow...
✅ [Request abc-123] FunctionGemma created plan with 3 steps
▶️ [Request abc-123] Step 1: Executing semantic_search
✅ [Request abc-123] Step 1 completed in 245ms
▶️ [Request abc-123] Step 2: Executing orchestrate_task
✅ [Request abc-123] Step 2 completed in 5234ms
🎉 [Request abc-123] Workflow completed successfully in 5543ms
```

## 🔮 Future Enhancements

- [ ] **Plan Caching** - Cache common patterns
- [ ] **Parallel Execution** - Run independent steps in parallel
- [ ] **Plan Optimization** - Learn from past executions
- [ ] **Cost Tracking** - Monitor token usage
- [ ] **Plan Visualization** - Show workflow graphs
- [ ] **Checkpoint/Resume** - Handle long-running workflows

## 🤝 Contributing

To add new tools:

1. Add tool to MemoryAgent or CodingOrchestrator
2. Register in `ToolRegistry.cs`
3. FunctionGemma automatically discovers it!

## 📚 Related Documentation

- [FunctionGemma Model Card](https://ollama.com/library/functiongemma)
- [MCP Protocol](https://modelcontextprotocol.io)
- [MemoryAgent Architecture](../docs/)
- [CodingOrchestrator Design](../CodingOrchestrator.Server/)

---

**Built with ❤️ using FunctionGemma, ASP.NET Core 9, and the Model Context Protocol**

