# Cancel Code Task

**Purpose:** Cancel a running multi-agent coding task.

---

## Execute This Command

```
Use `cancel_task` with the job ID:

cancel_task(jobId: "[JOB_ID_TO_CANCEL]")
```

---

## Response

### Success
```
✅ Job `abc123` has been cancelled
```

### Not Found
```
❌ Job `abc123` not found
```

---

## When to Cancel

- Task is taking too long
- You realized the task description was wrong
- You want to try a different approach
- Task is stuck in a validation loop

---

## After Cancelling

1. Use `list_tasks()` to verify cancellation
2. Start a new task with `orchestrate_task()` if needed
3. Consider adjusting:
   - Task description (be more specific)
   - `maxIterations` (increase for complex tasks)
   - `minValidationScore` (lower to 7 if 8 is too strict)

---

## Tips

- Cancelled tasks cannot be resumed
- You can immediately start a new task after cancelling
- Use `list_tasks()` first to find the job ID if you forgot it

