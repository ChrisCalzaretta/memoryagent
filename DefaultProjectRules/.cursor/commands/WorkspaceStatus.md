# Workspace Status - Check Learning System Health

Get a quick overview of what Agent Lightning knows about this workspace.

## When to Use
- At the start of a session
- To check what's been learned
- To see important files
- To verify learning system is working

## Steps

1. **Call workspace_status:**
```
Use MCP tool: workspace_status
- context: "[workspace name]"
```

2. **Review the status:**
   - Active session info
   - Recent sessions count
   - Q&A knowledge base size
   - Top important files
   - Tool usage metrics

## Example

```
workspace_status(context: "memoryagent")
```

Returns:
- Session: abc123 (active for 2h)
- Sessions (7 days): 15
- Q&A stored: 47 pairs
- Important files:
  - LearningService.cs (importance: 9.2)
  - McpService.cs (importance: 8.7)
- Tool calls today: 156

## Benefits
- Verify learning is happening
- See what the system has learned
- Find important files quickly
- Monitor tool usage



