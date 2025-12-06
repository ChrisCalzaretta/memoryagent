---
name: Review File Important
---
# Review File Importance

Review the most important files in the project based on learned patterns.

## When to Use
- Starting work on unfamiliar area
- Prioritizing code review
- Understanding project structure
- Onboarding new developers

## Steps

1. Get important files:
   - Call `get_important_files` with context and limit (e.g., 20)
   - Review the importance scores and access patterns

2. Understand file clusters:
   - Call `get_file_clusters` to see logical groupings
   - These are files frequently edited together

3. For a specific file, find co-edited files:
   - Call `get_coedited_files` with the file path
   - These files likely need updates when the main file changes

4. Refresh rankings if stale:
   - Call `recalculate_importance` to decay old scores
   - Do this weekly or after major refactoring

