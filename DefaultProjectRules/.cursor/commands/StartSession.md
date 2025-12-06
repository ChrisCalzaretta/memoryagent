# Session Management - AUTOMATIC

## ✅ Sessions Are Now Automatic!

**You don't need to manually start sessions anymore.**

Sessions automatically start on the first MCP tool call in a workspace.

## What Happens Automatically

1. **Auto-Start**: When you call ANY MCP tool, a session starts if none exists
2. **Auto-Context**: Workspace name is automatically used as context
3. **Auto-Recording**: Files in tool arguments are automatically recorded as "discussed"

## Check Session Status

Use `workspace_status` to see your current session:

```
Use MCP tool: workspace_status
- context: "[workspace name]"
```

## Manual Recording (Optional)

If you discuss a file that wasn't in a tool argument:

```
Use MCP tool: record_file_discussed
- sessionId: "[from workspace_status]"
- filePath: "[file you discussed]"
```

If you edit a file outside of MCP tools:

```
Use MCP tool: record_file_edited
- sessionId: "[from workspace_status]"  
- filePath: "[file you edited]"
```

## Note

Sessions persist across chat sessions in the same workspace.
The learning system continuously learns from your interactions.
