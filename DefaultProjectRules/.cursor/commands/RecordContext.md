---
name: Record Context
---
# Record Context - Track Files Discussed & Edited

Record file interactions to help Agent Lightning learn importance and co-edit patterns.

**Note**: File recording is now AUTOMATIC for files mentioned in tool arguments. Use this for manual recording only.

## When to Use
- For files discussed outside of tool calls
- For files edited manually (not through MCP tools)
- To explicitly track important files

## Steps

1. For files DISCUSSED (viewed, mentioned, analyzed):
   - Call `record_file_discussed` with session ID and file path
   - Sessions are automatic - use `workspace_status` to get the session ID

2. For files EDITED (modified, created, refactored):
   - Call `record_file_edited` with session ID and file path
   - Call `index` with scope='file' to update the search index

3. Check related files:
   - Call `get_coedited_files` to find files that should also be updated
   - Use `includeClusters: true` to see file groupings

## Example

```
# Get current session ID
workspace_status(context: "myproject")
→ Session: abc-123 (active)

# Record file interactions
record_file_discussed(sessionId: "abc-123", filePath: "Services/AuthService.cs")

record_file_edited(sessionId: "abc-123", filePath: "Services/AuthService.cs")

# Reindex the file
index(scope: "file", path: "Services/AuthService.cs", context: "myproject")

# Find related files
get_coedited_files(filePath: "Services/AuthService.cs", context: "myproject", includeClusters: true)
```
