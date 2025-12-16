---
name: Session Info
---
# Session Management - Automatic!

## Sessions Are Automatic

**You don't need to start or end sessions manually.**

- ✅ **Auto-Start**: Sessions start on first MCP tool call
- ✅ **Auto-Context**: Workspace name used as context
- ✅ **Auto-Recording**: Files in tool arguments tracked automatically
- ✅ **Persistent**: Learning persists across chat sessions

## Check Session Status

```
workspace_status(context: "myproject")
```

Returns:
- Active session info
- Q&A knowledge stored
- Important files learned
- Tool usage metrics

## Manual Recording (Optional)

Only needed for files NOT mentioned in tool arguments:

```
# Get session ID from workspace_status
record_file_discussed(sessionId: "abc-123", filePath: "some/file.cs")
record_file_edited(sessionId: "abc-123", filePath: "some/file.cs")
```

## Learning Never Stops

The system continuously learns from:
- Files you discuss and edit
- Q&A pairs you store
- Tool usage patterns
- Co-edit relationships

All learning persists indefinitely.

