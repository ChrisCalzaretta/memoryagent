---
name: Record Context
---
# Record Context - Track Files Discussed & Edited

Record file interactions to help Agent Lightning learn importance and co-edit patterns.

## When to Use
- After discussing any file
- After editing any file
- After reviewing code

## Steps

1. Ensure you have an active session:
   - Call `start_session` if not already started
   - Use the session ID for all tracking

2. For files DISCUSSED (viewed, mentioned, analyzed):
   - Call `record_file_discussed` with session ID and file path
   - Do this for EVERY file mentioned in conversation

3. For files EDITED (modified, created, refactored):
   - Call `record_file_edited` with session ID and file path
   - Do this AFTER every file edit
   - Call `index` with scope='file' to update the search index

4. Check related files:
   - Call `get_coedited_files` to find files that should also be updated
   - Use `includeClusters: true` to see file groupings

## Example

```
record_file_discussed(sessionId: "abc-123", filePath: "Services/AuthService.cs")

record_file_edited(sessionId: "abc-123", filePath: "Services/AuthService.cs")

index(scope: "file", path: "Services/AuthService.cs", context: "myproject")

get_coedited_files(filePath: "Services/AuthService.cs", context: "myproject", includeClusters: true)
```

