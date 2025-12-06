---
name: Workspace Status
---
# Workspace Status - What Does Agent Lightning Know?

Get a quick overview of what Agent Lightning has learned about this workspace.

## When to Use
- At the start of a conversation to see context
- To check what files have been tracked
- To see Q&A knowledge base status
- To review recent activity

## What It Shows

- **Active Session**: Current session info (auto-created)
- **Recent Sessions**: Past work sessions
- **Q&A Knowledge**: Stored question-answer pairs
- **Important Files**: Top files by importance score
- **Tool Usage**: How many tool calls in this workspace

## Example

```
workspace_status(context: "memoryagent")

→ Shows:
  📁 Context: memoryagent
  🟢 Active Session (auto-created)
  📜 Recent Sessions: 5
  💡 Q&A Knowledge Base: Active
  ⭐ Top Important Files
  🔧 Tool Usage: 42 calls
```

## Notes

- Sessions are **automatic** - no need to start/end them
- Context = workspace folder name (lowercase)
- Files are auto-tracked from tool arguments

