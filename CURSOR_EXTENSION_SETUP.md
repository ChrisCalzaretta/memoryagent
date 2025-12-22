# 🚀 **CURSOR MCP SETUP - Your Actual Configuration**

## 📋 **What You Actually Have**

Based on your Cursor MCP configuration:

1. ✅ **@memory-agent** - 4 tools (workflows, monitoring)
2. ✅ **@code-agent** - 8 tools (generation, search, analysis)

---

## 🛠️ **YOUR ACTUAL FILES**

| File | Purpose | MCP Server |
|------|---------|------------|
| `memory-router-mcp-wrapper.js` | Memory Agent wrapper | `@memory-agent` |
| `mcp-wrapper-router.js` | Code Agent wrapper | `@code-agent` |
| `mcp.json` | Cursor MCP configuration | Both servers |

---

## 🎨 **YOUR CURSOR CONFIGURATION (mcp.json)**

```json
{
  "mcpServers": {
    "memory-agent": {
      "command": "node",
      "args": ["E:\\GitHub\\MemoryAgent\\memory-router-mcp-wrapper.js", "${workspaceFolder}"],
      "description": "🧠 MemoryAgent: Search, analyze, validate code via MemoryRouter (port 5010)"
    },
    "code-agent": {
      "command": "node",
      "args": ["E:\\GitHub\\MemoryAgent\\mcp-wrapper-router.js", "${workspaceFolder}"],
      "description": "🤖 CodingAgent: Generate code with multi-model AI (port 5001)"
    }
  }
}
```

---

## 💬 **HOW TO USE IN CURSOR**

### **YOUR ACTUAL MCP TOOLS**

#### **@memory-agent (4 tools):**
1. `execute_task` - Smart router for workflows
2. `list_available_tools` - Show all tools
3. `get_workflow_status` - Track progress
4. `list_workflows` - List active workflows

#### **@code-agent (8 tools):**
1. **`generate_code`** ⭐ - Create new code
2. `search_code` - Semantic search
3. `ask_question` - Q&A with learning
4. `validate_code` - Code validation
5. `analyze_project` - Project insights
6. `test_code` - Code testing
7. `refactor_code` - Code refactoring
8. `get_context` - Context retrieval

---

## 📝 **USAGE EXAMPLES**

### **1. Generate Code**

```
@code-agent generate_code
task: "Create a Calculator class with Add, Subtract, Multiply, Divide methods"
language: "csharp"
maxIterations: 10
```

**Response:**
```
🚀 Code generation started
Job ID: job_20251222_abc123

🔍 Exploring codebase...
⚙️ Generating code...
✅ Validation score: 9/10

📁 Files generated:
  - Calculator.cs
  - ICalculator.cs
  - CalculatorTests.cs

Files written to: workspace/Generated/calculator_20251222/
```

---

### **2. Search Existing Code**

```
@code-agent search_code
query: "authentication patterns"
limit: 20
```

**Returns:** Semantic search results with code snippets

---

### **3. Ask Questions**

```
@code-agent ask_question
question: "How does the payment processing work?"
```

**Returns:** Explanation with context from codebase

---

### **4. Validate Code**

```
@code-agent validate_code
scope: "security"
minSeverity: "medium"
```

**Returns:** Validation score, issues, recommendations

---

### **5. Analyze Project**

```
@code-agent analyze_project
includeRecommendations: true
```

**Returns:** Project health, important files, patterns

---

### **6. Get Context**

```
@code-agent get_context
task: "implement user authentication"
includePatterns: true
includeQA: true
```

**Returns:** Relevant files, patterns, Q&A

---

### **7. Background Workflow**

```
@memory-agent execute_task
request: "Create a complete implementation plan for user authentication system"
```

**Returns:** Multi-step plan with orchestration

---

## 🎯 **ARCHITECTURE**

```
┌─────────────────────────────────────────────┐
│  CURSOR IDE                                 │
│  - MCP Servers Panel                        │
│  - AI Chat (@memory-agent, @code-agent)     │
└────────┬────────────────────┬───────────────┘
         │                    │
         │ MCP Protocol       │ MCP Protocol
         │ (JSON-RPC)         │ (JSON-RPC)
         ↓                    ↓
┌─────────────────────┐  ┌─────────────────────┐
│ memory-router-mcp-  │  │ mcp-wrapper-        │
│ wrapper.js          │  │ router.js           │
│ (Port 5010)         │  │ (Port 5001)         │
└────────┬────────────┘  └────────┬────────────┘
         │                        │
         ↓                        ↓
┌─────────────────────┐  ┌─────────────────────┐
│ MemoryRouter        │  │ CodingAgent.Server  │
│ - Smart Router      │  │ - Multi-model LLMs  │
│ - FunctionGemma     │  │ - Code Generation   │
│ - Tool Selection    │  │ - Validation        │
└────────┬────────────┘  └─────────────────────┘
         │
         ↓
┌─────────────────────┐
│ MemoryAgent         │
│ - Qdrant (vectors)  │
│ - Neo4j (graph)     │
│ - AI Lightning      │
└─────────────────────┘
```

---

## 🧪 **TESTING**

### **Test Your Setup**

Run the MCP tool test:
```bash
cd E:\GitHub\MemoryAgent
node test-mcp-tools.js
```

**Should see:**
```
✅ Memory Agent Initialize: Protocol 2024-11-05
✅ Memory Agent List Tools: Found 4 tools
✅ Code Generator Initialize: Protocol 2024-11-05
✅ Code Generator List Tools: Found 8 tools
✅ Orchestrate Task: Code generation started
✅ Get Task Status: Status retrieved

🎉 ALL TESTS PASSED! (8/8)
```

---

## 📚 **DOCUMENTATION FILES**

| File | Purpose |
|------|---------|
| `.cursor/cursorrules.mdc` | Cursor AI routing rules |
| `.cursor/commands/RoutingGuide.md` | Which server to use? |
| `.cursor/commands/GenerateCode.md` | How to use generate_code |
| `.cursor/commands/README.md` | Quick reference |
| `test-mcp-tools.js` | Test suite |
| `TEST_RESULTS_SUMMARY.md` | Test results |

---

## 🎓 **USAGE PATTERNS**

### **Pattern 1: Generate with Context**
```
1. @code-agent get_context
   task: "create user service"
   
2. @code-agent generate_code
   task: "Create UserService following existing patterns"
   
3. @code-agent validate_code
   scope: "best_practices"
```

### **Pattern 2: Search then Generate**
```
1. @code-agent search_code
   query: "existing authentication patterns"
   
2. @code-agent generate_code
   task: "Create improved authentication service"
```

### **Pattern 3: Question then Implement**
```
1. @code-agent ask_question
   question: "How do we handle database connections?"
   
2. @code-agent generate_code
   task: "Create new database service using our pattern"
```

---

## ❓ **TROUBLESHOOTING**

### **"No MCP servers found"**

**Check:**
```bash
# Verify mcp.json location
cat mcp.json

# Check file paths in mcp.json are correct
```

### **"Servers not healthy"**

**Fix:**
```bash
# Check containers
docker ps

# Should see:
# - memory-router (port 5010)
# - coding-agent (port 5001)
# - mcp-server (port 5000)

# If not running:
cd E:\GitHub\MemoryAgent
docker-compose -f docker-compose-shared-Calzaretta.yml up -d
```

### **"Tools not appearing"**

1. Restart Cursor completely
2. Check Settings → MCP Servers
3. Should see both servers enabled (green toggle)
4. Check output panel (Ctrl+Shift+U) → "MCP: memory-agent" or "MCP: code-agent"

---

## 🚦 **HEALTH CHECK**

```bash
# Check Memory Agent
curl http://localhost:5010/health
# Should return: {"status":"healthy","service":"MemoryRouter"}

# Check Code Agent
curl http://localhost:5001/health
# Should return: {"status":"Healthy"}
```

---

## 🎉 **WHAT YOU HAVE**

✅ **2 MCP Servers** - memory-agent (4 tools) + code-agent (8 tools)
✅ **12 Total Tools** - All verified and working
✅ **Multi-Model AI** - Qwen, Gemma, Phi4, Codestral, Claude
✅ **Smart Routing** - FunctionGemma selects best tools
✅ **AI Lightning** - All prompts in Qdrant/Neo4j
✅ **Real-time Generation** - See progress as code is created
✅ **Automatic Validation** - Must score >= 8/10
✅ **Context-Aware** - Searches existing code before generating
✅ **Learning System** - Q&A storage for future reference

---

## 🚀 **MAIN COMMAND FOR CODE GENERATION**

```
@code-agent generate_code
task: "<your task description>"
language: "csharp"  // or python, typescript, etc.
maxIterations: 10
```

**This is your primary code generation tool!** ⭐

---

## 📞 **SERVICE PORTS**

| Service | Port | Purpose |
|---------|------|---------|
| MemoryRouter | 5010 | Smart routing + MemoryAgent tools |
| CodingAgent | 5001 | Code generation + validation |
| MemoryAgent (MCP) | 5000 | Backend tools (accessed via 5010) |

---

**All services are running, tested, and ready to use!** 🎊

