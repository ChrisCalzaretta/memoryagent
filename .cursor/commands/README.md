# Cursor Commands - Split Architecture

## 🧠 Memory Agent Commands

| Command | File | Use For |
|---------|------|---------|
| **Execute Task** | `ExecuteTask.md` | Search, analysis, understanding |
| **List Tools** | `ListTools.md` | Discover available tools |
| **Discover by Category** | `DiscoverByCategory.md` | Browse tools by category |
| **Track Workflow** | `TrackWorkflow.md` | Monitor background jobs |

## 🚀 Code Generator Commands

| Command | File | Use For |
|---------|------|---------|
| **Generate Code** | `GenerateCode.md` | Multi-agent code generation |

---

## Quick Reference

### Search for Code (memory-agent)
```javascript
execute_task({ request: "Find all authentication code" })
```

### Generate Code (code-generator)
```javascript
orchestrate_task({ 
  task: "Create REST API for user management",
  language: "python"
})
```

### Check Job Status (code-generator)
```javascript
get_task_status({ jobId: "job_..." })
```

### Create Brand (code-generator)
```javascript
design_create_brand({
  brand_name: "MyApp",
  industry: "SaaS",
  // ...
})
```

---

## Decision Guide

| I want to... | Server | Command |
|--------------|--------|---------|
| Find existing code | memory-agent | ExecuteTask |
| Understand a feature | memory-agent | ExecuteTask |
| Create new code | code-generator | GenerateCode |
| Create brand system | code-generator | GenerateCode |
| Analyze code quality | memory-agent | ExecuteTask |
| Monitor code gen job | code-generator | GenerateCode |
