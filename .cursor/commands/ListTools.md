# List Available Tools

**Server:** `@memory-agent`

Discover all 12 available tools across both MCP servers.

## Usage

```javascript
list_available_tools()
```

## What You Get

Shows all tools available from both servers:
- **@memory-agent:** 4 tools
- **@code-agent:** 8 tools

**Total:** 12 tools

---

## YOUR ACTUAL TOOLS

### @memory-agent (4 tools)

1. **execute_task**
   - Smart router for complex workflows
   - Multi-step orchestration
   
2. **list_available_tools**
   - Show all available tools
   
3. **get_workflow_status**
   - Track workflow progress
   - Monitor background jobs
   
4. **list_workflows**
   - List active workflows

### @code-agent (8 tools)

1. **generate_code** ⭐
   - Create new code with multi-model AI
   - Main code generation tool
   
2. **search_code**
   - Semantic code search
   - Find patterns and examples
   
3. **ask_question**
   - Q&A about codebase
   - Learn how things work
   
4. **validate_code**
   - Code quality validation
   - Security checks
   
5. **analyze_project**
   - Project-wide insights
   - Health score and recommendations
   
6. **test_code**
   - Code testing
   - Compilation and runtime tests
   
7. **refactor_code**
   - Code modernization
   - Pattern application
   
8. **get_context**
   - Context retrieval
   - Relevant files and patterns

---

## Quick Reference

| Task | Tool | Server |
|------|------|--------|
| Generate NEW code | `generate_code` | @code-agent |
| Search EXISTING code | `search_code` | @code-agent |
| Understand code | `ask_question` | @code-agent |
| Validate quality | `validate_code` | @code-agent |
| Analyze project | `analyze_project` | @code-agent |
| Get context | `get_context` | @code-agent |
| Test code | `test_code` | @code-agent |
| Refactor code | `refactor_code` | @code-agent |
| Background workflow | `execute_task` | @memory-agent |
| List tools | `list_available_tools` | @memory-agent |
| Check workflow | `get_workflow_status` | @memory-agent |
| List workflows | `list_workflows` | @memory-agent |

---

## Remember

**For most tasks, use @code-agent directly:**

```javascript
// Generate code
@code-agent generate_code

// Search code
@code-agent search_code

// Ask questions
@code-agent ask_question
```

**For complex multi-step workflows, use @memory-agent:**

```javascript
@memory-agent execute_task
```

---

## Example Response

When you call `list_available_tools()`, you'll see:

```
Available Tools (12 total):

@memory-agent (4 tools):
  - execute_task
  - list_available_tools
  - get_workflow_status
  - list_workflows

@code-agent (8 tools):
  - generate_code ⭐
  - search_code
  - ask_question
  - validate_code
  - analyze_project
  - test_code
  - refactor_code
  - get_context
```

---

**Use this command to explore what's available!**
