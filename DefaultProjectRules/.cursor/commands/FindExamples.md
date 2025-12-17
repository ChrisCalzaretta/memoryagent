# Find Examples - Learn How Something Is Used

Find usage examples of functions, classes, or patterns in the codebase.

## When to Use
- Learning how to use an API/function
- Understanding how a pattern is implemented
- Finding similar code to reference
- Before implementing something new

## Steps

1. **Call find_examples with what you're looking for:**
```
Use MCP tool: find_examples
- query: "[function/class/pattern name]"
- context: "[workspace name]"
- limit: [optional: max results, default 10]
```

2. **Review the examples:**
   - Files containing usage
   - Code snippets showing how it's used
   - Related patterns

## Example

```
find_examples(
  query: "IRepository",
  context: "myproject",
  limit: 5
)
```

Returns:
- UserRepository implements IRepository
- ProductRepository implements IRepository
- Usage in UserService: `_repository.GetByIdAsync()`
- Related patterns: Repository Pattern, Unit of Work

## Benefits
- Learn from existing code
- Follow established conventions
- Find reference implementations
- Understand usage patterns










