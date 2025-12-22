# 🎯 Routing Guide - Which MCP Server & Tool?

## 🚨 **YOUR ACTUAL MCP SETUP**

Based on your Cursor configuration, you have:

### **@memory-agent** (4 tools)
- `execute_task`
- `list_available_tools`
- `get_workflow_status`
- `list_workflows`

### **@code-agent** (8 tools)
- **`generate_code`** ⭐
- `search_code`
- `ask_question`
- `validate_code`
- `analyze_project`
- `test_code`
- `refactor_code`
- `get_context`

---

## 🎯 **QUICK DECISION TREE**

```
What does the user want?
        ↓
   ┌────┴────┐
   │         │
CREATE     FIND/UNDERSTAND
NEW code   existing code
   │         │
   ↓         ↓
generate   search_code
_code      ask_question
```

---

## 📋 **ROUTING TABLE**

| User Says... | Use Server | Use Tool | Example |
|--------------|------------|----------|---------|
| "Create..." | `@code-agent` | `generate_code` | "Create a Calculator class" |
| "Generate..." | `@code-agent` | `generate_code` | "Generate a REST API" |
| "Build..." | `@code-agent` | `generate_code` | "Build a payment service" |
| "Make..." | `@code-agent` | `generate_code` | "Make a user service" |
| "Implement..." | `@code-agent` | `generate_code` | "Implement authentication" |
| "Find..." | `@code-agent` | `search_code` | "Find all API endpoints" |
| "Search..." | `@code-agent` | `search_code` | "Search for error handling" |
| "Where is..." | `@code-agent` | `search_code` | "Where is user validation?" |
| "How does..." | `@code-agent` | `ask_question` | "How does payment work?" |
| "Explain..." | `@code-agent` | `ask_question` | "Explain this function" |
| "What is..." | `@code-agent` | `ask_question` | "What is UserService?" |
| "Validate..." | `@code-agent` | `validate_code` | "Validate this code" |
| "Check..." | `@code-agent` | `validate_code` | "Check for security issues" |
| "Analyze..." | `@code-agent` | `analyze_project` | "Analyze project health" |
| "Get context..." | `@code-agent` | `get_context` | "Get context for user login" |
| "Refactor..." | `@code-agent` | `refactor_code` | "Refactor this CSS" |
| "Test..." | `@code-agent` | `test_code` | "Test this code" |
| "Workflow..." | `@memory-agent` | `execute_task` | "Complex multi-step task" |

---

## ✅ **CORRECT USAGE EXAMPLES**

### ✅ Code Generation
```javascript
@code-agent generate_code
task: "Create a Calculator class with Add, Subtract, Multiply, Divide methods"
language: "csharp"
```

### ✅ Search Existing Code
```javascript
@code-agent search_code
query: "authentication patterns"
limit: 20
```

### ✅ Ask About Code
```javascript
@code-agent ask_question
question: "How does the payment processing work?"
```

### ✅ Validate Code
```javascript
@code-agent validate_code
scope: "security"
minSeverity: "high"
```

### ✅ Analyze Project
```javascript
@code-agent analyze_project
includeRecommendations: true
```

### ✅ Get Context
```javascript
@code-agent get_context
task: "implement user authentication"
includePatterns: true
```

---

## ❌ **COMMON MISTAKES**

### ❌ Wrong Server for Generation
```javascript
// WRONG - memory-agent can't generate code!
@memory-agent execute_task
request: "Create a Calculator class"

// RIGHT
@code-agent generate_code
task: "Create a Calculator class"
```

### ❌ Wrong Server for Search
```javascript
// WRONG - memory-agent doesn't have search_code!
@memory-agent execute_task
request: "Find authentication code"

// RIGHT
@code-agent search_code
query: "authentication code"
```

### ❌ Using Tools That Don't Exist
```javascript
// WRONG - orchestrate_task doesn't exist!
@code-agent orchestrate_task

// RIGHT
@code-agent generate_code
```

---

## 🔄 **RECOMMENDED WORKFLOWS**

### Workflow 1: Generate with Context
```
1. @code-agent get_context
   task: "create user service"
   
2. @code-agent generate_code
   task: "Create UserService following existing patterns"
   
3. @code-agent validate_code
   scope: "best_practices"
```

### Workflow 2: Search then Generate
```
1. @code-agent search_code
   query: "existing API patterns"
   
2. @code-agent generate_code
   task: "Create new API following those patterns"
   
3. @code-agent validate_code
   scope: "security"
```

### Workflow 3: Question then Implement
```
1. @code-agent ask_question
   question: "How do we handle database connections?"
   
2. @code-agent generate_code
   task: "Create new database service using our pattern"
```

---

## 🎯 **DECISION SHORTCUTS**

**Quick questions to ask yourself:**

1. **Am I creating NEW code?**
   - YES → `@code-agent generate_code`
   
2. **Am I looking for EXISTING code?**
   - YES → `@code-agent search_code`
   
3. **Do I want to UNDERSTAND code?**
   - YES → `@code-agent ask_question`
   
4. **Do I want to CHECK quality?**
   - YES → `@code-agent validate_code`
   
5. **Do I need context BEFORE coding?**
   - YES → `@code-agent get_context`

---

## 📊 **TOOL CAPABILITIES**

| Tool | What It Does | When to Use |
|------|-------------|-------------|
| `generate_code` | Creates NEW code with multi-model AI | Creating features, apps, services |
| `search_code` | Finds EXISTING code semantically | Finding patterns, examples, specific code |
| `ask_question` | Answers questions about code | Understanding how things work |
| `validate_code` | Checks code quality | Security audits, best practices |
| `analyze_project` | Project-wide insights | Overall health, recommendations |
| `get_context` | Gets relevant context | Before starting a task |
| `refactor_code` | Modernizes code | Legacy code updates |
| `test_code` | Tests code | Compilation, runtime testing |
| `execute_task` | Complex workflows | Multi-step orchestration |

---

## ⚡ **PRO TIPS**

1. **Always use `get_context` before `generate_code`** for better results
2. **Use `search_code` to find existing patterns** before generating new code
3. **Validate after generation** with `validate_code`
4. **Be specific** in your task descriptions for better code
5. **Use `ask_question` to learn** before implementing

---

## ❌ **TOOLS THAT DON'T EXIST**

**DO NOT use these** (they are not in your MCP config):
- ❌ `orchestrate_task`
- ❌ `get_task_status`
- ❌ `list_tasks`
- ❌ `cancel_task`
- ❌ `apply_task_files`
- ❌ `design_create_brand`
- ❌ `design_validate`
- ❌ `design_get_brand`

**ONLY use the 12 tools listed at the top of this guide!**

---

## ✅ **SUMMARY**

### Your MCP Setup:
- **@memory-agent:** Background workflows (4 tools)
- **@code-agent:** Code generation & analysis (8 tools)

### Primary Code Generation Tool:
**`@code-agent generate_code`** ⭐

### Rule of Thumb:
- **Creating NEW code?** → `@code-agent generate_code`
- **Finding EXISTING code?** → `@code-agent search_code`
- **Understanding code?** → `@code-agent ask_question`
- **Everything else?** → Check the table above!

**When in doubt, choose `@code-agent` - it has most of the tools you need!**
