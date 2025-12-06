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

1. Get active session:
   - Call `get_active_session` to get session ID
   - If no session, call `start_session` first

2. For files DISCUSSED (viewed, mentioned, analyzed):
   - Call `record_file_discussed` with session ID and file path
   - Do this for EVERY file mentioned in conversation

3. For files EDITED (modified, created, refactored):
   - Call `record_file_edited` with session ID and file path
   - Do this AFTER every file edit
   - Call `index_file` to update the search index

4. Check related files:
   - Call `get_coedited_files` to find files that should also be updated
   - Call `get_file_clusters` to understand module boundaries

