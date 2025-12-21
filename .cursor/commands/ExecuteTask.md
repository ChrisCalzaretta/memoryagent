# Execute Task - Memory Agent

**Server:** `memory-agent` (via MemoryRouter)

Use this for **search, analysis, and understanding** of existing code.

## Usage

```javascript
execute_task({ request: "your natural language query" })
```

## Examples

### Code Search
```javascript
execute_task({ request: "Find all authentication code" })
execute_task({ request: "Search for database query patterns" })
execute_task({ request: "Find uses of deprecated API" })
```

### Code Understanding
```javascript
execute_task({ request: "Explain how the payment system works" })
execute_task({ request: "What does this function do?" })
execute_task({ request: "How are user permissions handled?" })
```

### Analysis
```javascript
execute_task({ request: "Analyze security vulnerabilities" })
execute_task({ request: "Check code complexity" })
execute_task({ request: "Find potential bugs" })
```

### Knowledge
```javascript
execute_task({ request: "Store this solution for future reference" })
execute_task({ request: "What patterns have we used before?" })
execute_task({ request: "Get historical context on this feature" })
```

### Planning
```javascript
execute_task({ request: "Create a plan for implementing OAuth" })
execute_task({ request: "Estimate complexity of this refactoring" })
execute_task({ request: "Break down this feature into tasks" })
```

## How It Works

1. Your request goes to MemoryRouter (port 5010)
2. FunctionGemma AI analyzes your intent
3. AI selects the right tools from 33+ available
4. Tools execute and return results
5. Results are formatted and returned

## When to Use

✅ **Use memory-agent for:**
- Finding existing code
- Understanding how something works
- Searching for patterns
- Analyzing code quality
- Getting project knowledge
- Creating plans

❌ **For code generation, use `code-generator` server instead**
- See `GenerateCode.md` for code generation
