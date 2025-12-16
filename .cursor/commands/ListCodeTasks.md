# List Code Tasks

**Purpose:** List all active and recent multi-agent coding tasks.

---

## Execute This Command

```
Use `list_tasks` from the coding-orchestrator MCP server:

list_tasks()
```

---

## Understanding the Response

```
**Active Tasks:**

🔄 `abc123` - Running (40%) - validation_agent
✅ `def456` - Complete (100%) - N/A
❌ `ghi789` - Failed (60%) - coding_agent
⏳ `jkl012` - Queued (0%) - N/A
🚫 `mno345` - Cancelled (20%) - N/A
⏱️ `pqr678` - TimedOut (80%) - validation_agent
```

### Status Icons
| Icon | Status | Description |
|------|--------|-------------|
| ⏳ | Queued | Waiting to start |
| 🔄 | Running | Currently executing |
| ✅ | Complete | Finished successfully |
| ❌ | Failed | Error occurred |
| 🚫 | Cancelled | User cancelled |
| ⏱️ | TimedOut | Exceeded time limit |

---

## Next Steps

- For **Running** tasks: Use `get_task_status(jobId: "...")` to see details
- For **Complete** tasks: Use `get_task_status` to retrieve generated code
- For **Failed** tasks: Check error with `get_task_status`, then retry

---

## Tips

- No tasks? Use `orchestrate_task` to start one
- Tasks are kept in memory for the session duration
- Old completed tasks may be cleaned up automatically

