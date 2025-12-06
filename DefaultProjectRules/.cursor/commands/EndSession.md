---
name: End Learning Session
---
# End Learning Session

End the current Agent Lightning session with a summary of what was accomplished.

## When to Use
- At the end of a conversation
- When switching to a different task
- Before closing the IDE

## Steps

1. End the session:
   - Call `end_session` with:
     - `sessionId`: The active session ID (from start_session)
     - `summary`: Brief summary of what was accomplished

2. Example summary:
   "Fixed authentication bug in AuthService, added retry logic, updated tests"

3. Recalculate importance (optional):
   - Call `get_insights` with category='recalculate' and context
   - This updates file importance rankings based on activity

## Example

```
end_session(
  sessionId: "abc-123",
  summary: "Consolidated MCP tools from 73 to 25, updated documentation"
)

get_insights(category: "recalculate", context: "myproject")
→ Updates file importance rankings
```

