# Execute Task - Memory Agent

**Server:** `@memory-agent` (via MemoryRouter)

Use this for **complex background workflows and orchestration**.

## Usage

```javascript
execute_task({ request: "your natural language request" })
```

## What It Does

The `execute_task` tool is a **smart router** that can orchestrate complex multi-step workflows across multiple services.

- Routes to appropriate services
- Manages complex workflows
- Handles multi-step tasks
- Orchestrates between services

## When to Use

✅ **Use @memory-agent execute_task for:**
- Complex multi-step workflows
- Orchestration across multiple services
- Background job management
- Tasks requiring multiple tool calls

❌ **For most tasks, use @code-agent instead:**
- Code generation → `@code-agent generate_code`
- Search → `@code-agent search_code`
- Questions → `@code-agent ask_question`
- Validation → `@code-agent validate_code`

## Examples

### Complex Workflow
```javascript
execute_task({ 
  request: "Create a complete implementation plan for user authentication, including database schema, API endpoints, and security measures"
})
```

### Multi-Service Orchestration
```javascript
execute_task({ 
  request: "Analyze the entire codebase, create a refactoring plan, and generate improved versions of key services"
})
```

## Other Memory Agent Tools

### List Available Tools
```javascript
list_available_tools()
```
Shows all tools available across all services.

### Get Workflow Status
```javascript
get_workflow_status({ workflowId: "workflow_12345" })
```
Track progress of a background workflow.

### List Workflows
```javascript
list_workflows()
```
See all active workflows.

---

## 🎯 Quick Decision

**Most of the time, you want `@code-agent`, not `@memory-agent`!**

| Task | Use This |
|------|----------|
| Generate code | `@code-agent generate_code` |
| Search code | `@code-agent search_code` |
| Ask questions | `@code-agent ask_question` |
| Validate code | `@code-agent validate_code` |
| Analyze project | `@code-agent analyze_project` |
| Get context | `@code-agent get_context` |
| **Complex workflow** | **`@memory-agent execute_task`** |

---

## How It Works

1. Your request goes to MemoryRouter (port 5010)
2. MemoryRouter analyzes your intent
3. Routes to appropriate services:
   - Code generation → CodingAgent
   - Search → MemoryAgent tools
   - Analysis → Validation tools
4. Orchestrates multi-step workflows
5. Returns aggregated results

---

## Comparison

| Feature | @memory-agent | @code-agent |
|---------|--------------|-------------|
| **Tools** | 4 orchestration tools | 8 specialized tools |
| **Use For** | Complex workflows | Direct tasks |
| **Routing** | Auto-routes to services | Direct execution |
| **Best For** | Multi-step orchestration | Single-task execution |

---

**🎯 TIP: Start with @code-agent tools. Use @memory-agent only for complex multi-service workflows!**
