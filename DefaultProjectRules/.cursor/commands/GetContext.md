# Get Context - Gather Information Before Starting

Gather relevant context from the codebase before starting any task.

## When to Use
- Before starting ANY new task
- When switching to a different area of the codebase
- When you need to understand what's relevant to a question

## Steps

1. **Call get_context with your task description:**
```
Use MCP tool: get_context
- task: "[describe what you're trying to do]"
- context: "[workspace name]"
```

2. **Review the returned context:**
   - Relevant files
   - Similar past Q&A
   - Important files in workspace
   - Related patterns

3. **Use this context to inform your approach**

## Example

```
get_context(
  task: "implement user authentication",
  context: "myproject"
)
```

Returns:
- Files related to auth
- Past Q&A about authentication
- Important project files
- Security patterns detected

## Benefits
- Don't miss relevant code
- Learn from past solutions
- Understand project structure
- Follow existing patterns








