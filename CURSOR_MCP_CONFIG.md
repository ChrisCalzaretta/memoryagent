# Cursor MCP Configuration - Split Architecture

## 🚀 Quick Setup

### Step 1: Copy the Configuration

Add the following to your Cursor MCP settings (`Settings > MCP`):

```json
{
  "mcpServers": {
    "memory-agent": {
      "command": "node",
      "args": [
        "E:\\GitHub\\MemoryAgent\\memory-router-mcp-wrapper.js",
        "${workspaceFolder}"
      ],
      "env": {
        "MEMORY_ROUTER_URL": "http://localhost:5010"
      }
    },
    "code-generator": {
      "command": "node",
      "args": [
        "E:\\GitHub\\MemoryAgent\\orchestrator-mcp-wrapper.js",
        "${workspaceFolder}"
      ],
      "env": {
        "ORCHESTRATOR_PORT": "5003"
      }
    }
  }
}
```

Or copy `CURSOR_MCP_CONFIG.json` from this repository.

### Step 2: Start Docker Services

```powershell
cd E:\GitHub\MemoryAgent
docker-compose -f docker-compose-shared-Calzaretta.yml up -d
```

### Step 3: Verify Health

```bash
curl http://localhost:5010/health  # MemoryRouter
curl http://localhost:5003/health  # CodingOrchestrator
```

---

## 🧠 memory-agent (Search & Analysis)

**33+ tools for code understanding and knowledge management.**

### Usage

```
@memory-agent Find all authentication code
@memory-agent Explain how the database layer works
@memory-agent Create a plan for implementing OAuth
@memory-agent Store this solution for future reference
```

### Main Entry Point

```javascript
execute_task({ request: "your natural language query" })
```

The AI (FunctionGemma) automatically selects the right tools.

### What It Can Do

| Task | Example |
|------|---------|
| **Search code** | "Find all SQL queries" |
| **Understand code** | "Explain how authentication works" |
| **Analyze quality** | "Check for security vulnerabilities" |
| **Store knowledge** | "Remember this pattern for later" |
| **Create plans** | "Plan the OAuth implementation" |
| **Index workspace** | "Index this project" |

---

## 🚀 code-generator (Multi-Agent Generation)

**12 tools for code generation with automatic validation.**

### Usage

```
@code-generator Create a REST API for user management
@code-generator Build a Blazor chess game
@code-generator Create a brand system for my app
```

### Direct Tool Calls

```javascript
// Start code generation
orchestrate_task({ 
  task: "Create user authentication with JWT",
  language: "typescript"
})

// Check progress
get_task_status({ jobId: "job_20241219_abc123" })

// Get files when done
apply_task_files({ 
  jobId: "job_20241219_abc123",
  basePath: "E:\\MyProject"
})
```

### What It Can Do

| Task | Tool |
|------|------|
| **Generate code** | `orchestrate_task` |
| **Check progress** | `get_task_status` |
| **Cancel job** | `cancel_task` |
| **List jobs** | `list_tasks` |
| **Get files** | `apply_task_files` |
| **Create brand** | `design_create_brand` |
| **Validate design** | `design_validate` |

---

## ⚡ Why Two Servers?

### Before (Single Router)
```
Request → MemoryRouter → FunctionGemma AI → CodingOrchestrator
                          (3-5 seconds)
```

### After (Split)
```
Search requests → MemoryRouter → AI routing → Fast!
Code requests   → CodingOrchestrator directly → No overhead!
```

**Benefits:**
- ✅ Code generation is faster (no routing delay)
- ✅ Clear separation of concerns
- ✅ Easier to debug
- ✅ More predictable behavior

---

## 🎯 Quick Reference

| I want to... | Use Server | Example |
|--------------|------------|---------|
| Find code | memory-agent | `execute_task({ request: "Find auth code" })` |
| Understand code | memory-agent | `execute_task({ request: "Explain this feature" })` |
| Generate code | code-generator | `orchestrate_task({ task: "Create API" })` |
| Create brand | code-generator | `design_create_brand({ ... })` |
| Plan work | memory-agent | `execute_task({ request: "Plan the refactor" })` |
| Check job | code-generator | `get_task_status({ jobId: "..." })` |

---

## 🔧 Troubleshooting

### MCP Server Not Connecting

1. Check services are running:
   ```powershell
   docker ps | findstr "memory-router\|coding-orchestrator"
   ```

2. Check logs:
   ```powershell
   type E:\GitHub\MemoryAgent\orchestrator-wrapper.log
   ```

3. Verify health endpoints:
   ```bash
   curl http://localhost:5010/health
   curl http://localhost:5003/health
   ```

### Code Generation Timeout

- Jobs run in background - use `get_task_status` to check
- Complex tasks may take 60+ seconds
- Use `cancel_task` to stop stuck jobs

### Search Not Working

- Index the workspace first: `execute_task({ request: "Index this workspace" })`
- Check Qdrant is healthy: `curl http://localhost:6333/health`


