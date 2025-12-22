# Cursor Commands - Actual Available Tools

## 🚨 **YOUR ACTUAL MCP SERVERS**

Based on your Cursor MCP settings, you have **9 tools total**:

### **@memory-agent** (4 tools)
- `execute_task` - Smart router for search, analysis, planning
- `list_available_tools` - Show all available tools
- `get_workflow_status` - Track background workflow progress
- `list_workflows` - List all active and recent workflows

### **@code-agent** (5 tools)
- `orchestrate_task` - Start multi-agent code generation ⭐
- `get_task_status` - Check job progress and get files
- `cancel_task` - Stop a running job
- `list_tasks` - See all active jobs
- `apply_task_files` - Apply generated files to workspace

---

## 🎯 Quick Routing

| Task | Use | Tool |
|------|-----|------|
| **Create NEW code** | `@code-agent` | `orchestrate_task` |
| Check job status | `@code-agent` | `get_task_status` |
| Cancel job | `@code-agent` | `cancel_task` |
| List jobs | `@code-agent` | `list_tasks` |
| Apply files | `@code-agent` | `apply_task_files` |
| Search code | `@memory-agent` | `execute_task` |
| Analyze code | `@memory-agent` | `execute_task` |
| Plan features | `@memory-agent` | `execute_task` |

---

## 📚 Available Commands

| File | What It Documents |
|------|-------------------|
| `GenerateCode.md` | How to use `orchestrate_task` for code generation |
| `ExecuteTask.md` | How to use `execute_task` for search/analysis |
| `RoutingGuide.md` | Which server to use when |
| `TrackWorkflow.md` | How to monitor workflows |

---

## Quick Examples

### Generate Code
```javascript
orchestrate_task({
  task: "Create a Calculator class",
  language: "csharp",
  maxIterations: 20
})
```

### Check Job Status
```javascript
get_task_status({
  jobId: "job_20251222_abc123"
})
```

### Search/Analyze Code
```javascript
execute_task({
  request: "Find all authentication patterns in the codebase"
})
```

### Apply Generated Files
```javascript
apply_task_files({
  jobId: "job_20251222_abc123",
  basePath: "E:\\GitHub\\MyProject"
})
```

---

## ❌ **TOOLS THAT DON'T EXIST**

**DO NOT use these** (they were in old documentation but are not available):
- ❌ `generate_code` (use `orchestrate_task`)
- ❌ `search_code` (use `execute_task`)
- ❌ `ask_question` (use `execute_task`)
- ❌ `validate_code` (automatic in orchestrate_task)
- ❌ `analyze_project` (use `execute_task`)
- ❌ `test_code` (automatic in orchestrate_task)
- ❌ `refactor_code` (use `orchestrate_task`)
- ❌ `get_context` (automatic in orchestrate_task)

---

**🎯 For code generation, use `@code-agent orchestrate_task`**  
**🔍 For search/analysis, use `@memory-agent execute_task`**
