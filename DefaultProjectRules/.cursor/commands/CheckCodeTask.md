# Check Code Task Status

**Purpose:** Check the progress and results of a multi-agent coding task.

---

## Execute This Command

### Check Task Status
```
Use `get_task_status` with the job ID:

get_task_status(jobId: "[JOB_ID_FROM_ORCHESTRATE_TASK]")
```

---

## Understanding the Response

### Running Task
```
📊 Task Status: Running
Job ID: abc123
Progress: 40%
Current Phase: validation_agent
Iteration: 2/5

Timeline:
- ✅ context_gathering (150ms)
- ✅ coding_agent [iter 1] (2500ms)
- ✅ validation_agent [iter 1] (800ms)
- ✅ coding_agent [iter 2] (3000ms)
```

### Completed Task
```
📊 Task Status: ✅ Complete
Job ID: abc123

✅ COMPLETED
- Validation Score: 9/10
- Total Iterations: 2
- Duration: 8500ms
- Files Generated: 1
- Summary: Successfully generated code...

---
### 📄 Services/UserService.cs
**Change Type:** Created
**Reason:** Generated csharp code

[FULL CODE IS SHOWN HERE - COPY TO YOUR PROJECT]
```

**⚠️ Files are returned in the response!** Copy them to your project manually, or use `autoWriteFiles: true` when starting the task.

### Failed Task
```
📊 Task Status: Failed
Job ID: abc123

❌ FAILED
- Error: Max iterations reached without passing validation
- Type: ValidationFailed
- Can retry: Yes
```

---

## Next Steps

| Status | Action |
|--------|--------|
| **Complete** | Review generated files, apply to project |
| **Running** | Wait and check again |
| **Failed** | Check error, adjust task, try again |

---

## Tips

- Tasks typically take 30-120 seconds depending on complexity
- Check every 10-15 seconds for progress updates
- If stuck, use `cancel_task` and retry with clearer task description

