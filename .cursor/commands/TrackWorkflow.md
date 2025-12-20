# Track Workflow Status

Track the progress of background workflows and nested jobs.

## Usage

### Check a Specific Workflow

```
@user-memory-router-execute_task Get workflow status for {workflowId}
```

Or use the direct tool:

```
@user-memory-router-get_workflow_status workflowId: "{workflowId}"
```

### List All Active Workflows

```
@user-memory-router-list_workflows
```

### Check Nested Job Status (for code generation)

```
@user-memory-router-execute_task Get status of job {jobId}
```

## How It Works

1. When you start a background task, you get a **Workflow ID**
2. That workflow may create **Nested Jobs** (e.g., code generation jobs)
3. Use `get_workflow_status` to see overall progress
4. Use `get_task_status` to see detailed progress for nested code generation jobs

## Response Format

The response includes embedded JSON for programmatic parsing:

```html
<!-- MCP_WORKFLOW_STATUS
{
  "workflowId": "abc-123-def",
  "status": "running",
  "progress": 45,
  "currentStep": "executing_workflow",
  "nestedJobs": [
    {
      "jobId": "job_20251220162648_70b81f2f",
      "type": "orchestrate_task",
      "status": "running",
      "progress": 60
    }
  ],
  "canPoll": true
}
-->
```

## Auto-Polling (Future Enhancement)

The `canPoll` flag and `pollIntervalMs` hint allow clients to automatically poll for updates:

- Poll every 5 seconds while `canPoll: true`
- Stop polling when `status: "completed"` or `status: "failed"`

## Example Flow

```
1. execute_task("Generate a REST API for users")
   ↓
2. Returns: Workflow ID: abc-123-def
   ↓
3. get_workflow_status(workflowId: "abc-123-def")
   ↓
4. Shows: Progress 45%, Nested Job: job_20251220...
   ↓
5. get_task_status(jobId: "job_20251220...")
   ↓
6. Shows: Detailed code generation progress
```
