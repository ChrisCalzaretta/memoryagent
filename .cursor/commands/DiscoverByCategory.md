# Discover Tools By Category

**Note:** This file describes how @memory-agent's `list_available_tools` works when it categorizes tools from the backend services.

## Quick Reference

Your 12 MCP tools organized by function:

### 🤖 Code Generation
**Main tool for creating NEW code:**
- `@code-agent generate_code` ⭐

### 🔍 Discovery (Search & Understand)
**Finding and understanding EXISTING code:**
- `@code-agent search_code`
- `@code-agent ask_question`
- `@code-agent get_context`

### ✅ Validation & Analysis
**Checking quality and project health:**
- `@code-agent validate_code`
- `@code-agent analyze_project`

### 🧪 Testing & Refactoring
**Testing and improving code:**
- `@code-agent test_code`
- `@code-agent refactor_code`

### 🔄 Workflows & Monitoring
**Background jobs and orchestration:**
- `@memory-agent execute_task`
- `@memory-agent get_workflow_status`
- `@memory-agent list_workflows`
- `@memory-agent list_available_tools`

---

## By Use Case

### "I want to CREATE something new"
```
@code-agent generate_code
task: "Create a user authentication service"
language: "csharp"
```

### "I want to FIND existing code"
```
@code-agent search_code
query: "authentication patterns"
```

### "I want to UNDERSTAND how something works"
```
@code-agent ask_question
question: "How does the payment system work?"
```

### "I want to VALIDATE code quality"
```
@code-agent validate_code
scope: "security"
```

### "I want to ANALYZE the project"
```
@code-agent analyze_project
includeRecommendations: true
```

### "I want CONTEXT before coding"
```
@code-agent get_context
task: "implement user login"
```

### "I want to TEST code"
```
@code-agent test_code
jobId: "job_12345"
```

### "I want to REFACTOR code"
```
@code-agent refactor_code
type: "css"
sourcePath: "/path/to/file"
```

### "I want a complex WORKFLOW"
```
@memory-agent execute_task
request: "Create complete implementation plan for user auth"
```

---

## Category Definitions

### 🤖 **Generation**
Creating NEW code, features, or applications
- **Tool:** `generate_code`
- **When:** Building from scratch

### 🔍 **Discovery**
Finding and understanding EXISTING code
- **Tools:** `search_code`, `ask_question`, `get_context`
- **When:** Exploring codebase

### ✅ **Validation**
Checking quality, security, best practices
- **Tools:** `validate_code`, `analyze_project`
- **When:** Quality assurance

### 🧪 **Testing & Refactoring**
Testing and improving code
- **Tools:** `test_code`, `refactor_code`
- **When:** Quality improvement

### 🔄 **Workflows**
Complex multi-step orchestration
- **Tools:** `execute_task`, monitoring tools
- **When:** Multi-service coordination

---

## Remember

**Most of the time, go directly to @code-agent:**

| Task | Direct Command |
|------|----------------|
| Create code | `@code-agent generate_code` |
| Find code | `@code-agent search_code` |
| Understand | `@code-agent ask_question` |
| Validate | `@code-agent validate_code` |

**Only use @memory-agent for:**
- Complex workflows
- Monitoring
- Listing tools

---

**Your 12 tools are ready to use!**
