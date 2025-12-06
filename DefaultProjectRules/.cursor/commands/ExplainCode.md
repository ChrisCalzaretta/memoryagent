# Explain Code - Understand Unfamiliar Code

Get a detailed explanation of what a piece of code does.

## When to Use
- When encountering unfamiliar code
- Before modifying complex code
- To understand dependencies and impact
- To see what patterns are used

## Steps

1. **Call explain_code with the file/class/method:**
```
Use MCP tool: explain_code
- filePath: "[path to file]"
- className: "[optional: specific class]"
- methodName: "[optional: specific method]"
- context: "[workspace name]"
```

2. **Review the explanation:**
   - Code structure
   - Dependencies (what this code uses)
   - Dependents (what uses this code)
   - Patterns detected

## Example

```
explain_code(
  filePath: "Services/AuthService.cs",
  className: "AuthService",
  context: "myproject"
)
```

Returns:
- Structure breakdown
- Dependencies: UserRepository, TokenService, ILogger
- Used by: LoginController, ApiMiddleware
- Patterns: Repository, DI, Logging

## Benefits
- Quickly understand unfamiliar code
- Know impact before making changes
- See the full dependency graph
- Identify patterns in use



