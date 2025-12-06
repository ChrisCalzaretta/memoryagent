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

1. Get active session:
   - Call `get_active_session` to get session ID

2. End the session:
   - Call `end_session` with:
     - `sessionId`: The active session ID
     - `summary`: Brief summary of what was accomplished

3. Example summary:
   "Fixed authentication bug in AuthService, added retry logic, updated tests"

4. Recalculate importance (optional):
   - Call `recalculate_importance` if significant work was done
   - This updates file importance rankings based on activity

