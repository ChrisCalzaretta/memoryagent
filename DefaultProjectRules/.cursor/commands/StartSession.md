---
name: Start Session
---
# Start Learning Session

Start an Agent Lightning learning session to track context throughout this conversation.

## Steps

1. Check for existing active session:
   - Call `get_active_session` with current context
   - If active session exists, use it
   - If no session, create new one

2. Start new session if needed:
   - Call `start_session` with project context
   - Note the session ID for all subsequent tracking

3. Review recent history:
   - Call `get_recent_sessions` to understand past work
   - Call `get_important_files` to see priority files

4. Report session status:
   - Display session ID
   - Show recent context if available
   - List most important files to review

