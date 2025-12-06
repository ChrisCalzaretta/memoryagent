---
name: Start Session
---
# Start Learning Session

Start an Agent Lightning learning session to track context throughout this conversation.

## Steps

1. Start session:
   - Call `start_session` with project context
   - If active session exists, it will be returned automatically
   - Note the session ID for all subsequent tracking

2. Review recent history:
   - Call `get_insights` with category='sessions' to understand past work
   - Call `get_important_files` to see priority files

3. Report session status:
   - Display session ID
   - Show recent context if available
   - List most important files to review

## Example

```
start_session(context: "MyProject")
→ Returns session ID and status

get_insights(category: "sessions", context: "myproject", limit: 5)
→ Shows recent sessions

get_important_files(context: "myproject", limit: 10)
→ Shows priority files to review
```

