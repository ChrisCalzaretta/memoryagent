# Cursor MCP Configuration for MemoryRouter

## 🎯 Quick Setup

### Option 1: Use the JSON Configuration File (Recommended)

1. Copy `memory-router-mcp-tools.json` to your Cursor configuration directory
2. In Cursor settings, add MemoryRouter as an MCP server

### Option 2: Manual Configuration

Add this to your Cursor MCP settings:

```json
{
  "mcpServers": {
    "memory-router": {
      "url": "http://localhost:5010/api/mcp",
      "transport": "http",
      "description": "AI-powered intelligent orchestration using FunctionGemma"
    }
  }
}
```

---

## 🧠 How to Use

### Main Command: `execute_task`

**Just describe what you want in natural language!**

```typescript
// Cursor will call:
execute_task({
  request: "Find all authentication code and validate for security issues"
})
```

**Examples:**
- `"Create a REST API for user management in TypeScript"`
- `"Analyze code complexity and suggest refactoring"`
- `"Search for existing database query patterns"`
- `"Generate a plan for implementing user profiles"`
- `"Create a brand system and validate UI code against it"`

### Discovery Command: `list_available_tools`

**Explore what's available:**

```typescript
list_available_tools()                    // Show all 44+ tools
list_available_tools({ filter: "search" }) // Filter by keyword
```

---

## 📊 What Cursor Sees

### Entry Point Tools (2)

1. **`execute_task`** - Main orchestration
   - Input: `{ request: string }`
   - AI figures out which of 44+ tools to call
   - Returns complete workflow results

2. **`list_available_tools`** - Discovery
   - Input: `{ filter?: string }`
   - Shows all available capabilities
   - Returns tool metadata with use cases

### Behind the Scenes (44 tools)

**MemoryAgent (33 tools):**
- Search, code understanding, validation
- Planning, intelligence, learning
- Transformation, indexing, patterns

**CodingOrchestrator (11 tools):**
- Multi-agent code generation
- Design system & brand validation
- Task management

---

## ✅ Verification

### Test Health
```bash
curl http://localhost:5010/health
# {"status":"healthy","service":"MemoryRouter"}
```

### Test MCP Endpoint
```bash
curl -X POST http://localhost:5010/api/mcp/tools/list \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1}'
```

### Test Natural Language
In Cursor, try:
```
@memory-router Find all authentication code
```

The AI will automatically:
1. Parse your intent
2. Create an execution plan
3. Call the right tools
4. Return comprehensive results

---

## 🎓 Pro Tips

### Be Specific
✅ Good: "Create a REST API for user management with JWT authentication and role-based access control"
❌ Vague: "create an API"

### Combine Operations
The AI can handle multi-step workflows:
```
"Find all database code, analyze for SQL injection vulnerabilities, 
and generate a security report with fixes"
```

### Trust the AI
You don't need to know:
- Which tools exist
- Which service provides them
- What parameters they need
- What order to call them

**Just describe the outcome you want!**

---

## 🚀 Example Workflows

### Code Search & Analysis
```
"Find all uses of deprecated authentication patterns and create a migration plan"
```

**What happens:**
1. `smartsearch` → Finds deprecated patterns
2. `get_migration_path` → Gets migration guide
3. `manage_plan` → Creates actionable plan
4. Returns complete refactoring roadmap

### Full-Stack Generation
```
"Create a user authentication service in TypeScript with JWT, 
validate for security, and check code complexity"
```

**What happens:**
1. `orchestrate_task` → Generates TypeScript auth service
2. `validate` (security) → Checks for vulnerabilities
3. `analyze_complexity` → Ensures code quality
4. Returns code + validation reports

### Design System
```
"Create a modern tech brand for 'CloudSync' and validate 
the homepage HTML against brand guidelines"
```

**What happens:**
1. `design_create_brand` → Creates complete brand system
2. `design_validate` → Checks HTML compliance
3. Returns brand tokens + validation results

---

## 📝 Configuration Files

### In This Repository

- **`.cursor/cursorrules.mdc`** - Comprehensive usage guide
- **`memory-router-mcp-tools.json`** - Complete MCP configuration
- **`.cursor/commands/ExecuteTask.md`** - Main command docs
- **`.cursor/commands/ListTools.md`** - Discovery command docs
- **`MEMORY_ROUTER_SETUP.md`** - Technical implementation details

### Key URLs

- **Health**: `http://localhost:5010/health`
- **MCP Endpoint**: `http://localhost:5010/api/mcp`
- **Tools List**: POST to `/api/mcp/tools/list`
- **Tool Call**: POST to `/api/mcp/tools/call`

---

## 🔧 Troubleshooting

### Router Not Responding
```bash
# Check if running
docker ps --filter "name=memory-router"

# Check logs
docker logs memory-router --tail 50

# Restart if needed
docker-compose -f docker-compose-shared-Calzaretta.yml restart memory-router
```

### Tools Not Discovered
```bash
# Check tool count in logs
docker logs memory-router | grep "ToolRegistry initialized"

# Should see:
# ✅ ToolRegistry initialized with 44 tools
#    📦 MemoryAgent tools: 33
#    🎯 CodingOrchestrator tools: 11
```

### MCP Not Connecting
1. Verify URL: `http://localhost:5010/api/mcp`
2. Check health endpoint first
3. Ensure MemoryAgent and CodingOrchestrator are running
4. Check Docker network connectivity

---

## 🎉 You're Ready!

MemoryRouter is your **single entry point** for:
- ✅ Code search & understanding
- ✅ Multi-agent code generation
- ✅ Security & quality validation
- ✅ Design system management
- ✅ Project planning & task tracking
- ✅ Learning & knowledge management

**Just describe what you want in natural language, and the AI handles the rest!** 🚀

