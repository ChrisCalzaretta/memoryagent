# Workflow Tracking System

**Implemented:** December 20, 2025

## Overview

The MemoryRouter now includes a comprehensive workflow tracking system that enables:

1. **Immediate Response** - Background jobs return instantly with a workflow ID
2. **Structured JSON** - Responses include parseable JSON for auto-polling
3. **Nested Job Tracking** - CodingOrchestrator job IDs are linked to parent workflows
4. **Status Polling** - New tools to check workflow and job status

---

## New Tools (2)

### `get_workflow_status`

Get detailed status of a background workflow.

**Parameters:**
- `workflowId` (required): The UUID returned by `execute_task`

**Returns:**
- Status (running/completed/failed)
- Progress (0-100%)
- Current step
- Elapsed time
- Nested job IDs
- Final result (when complete)

**Example:**
```json
{
  "name": "get_workflow_status",
  "arguments": {
    "workflowId": "84dceb49-6d1e-4feb-8922-52fe3e31b99e"
  }
}
```

### `list_workflows`

List all active and recent workflows.

**Parameters:**
- `includeCompleted` (optional, default: false): Show completed workflows too

**Returns:**
- Table of workflows with status, progress, and start time

---

## Response Format

When starting a background job, the response includes:

### 1. Human-Readable Markdown

```markdown
🚀 **Workflow Started in Background**

**Workflow ID:** `84dceb49-6d1e-4feb-8922-52fe3e31b99e`
**Request:** Generate a simple calculator class in C#

✅ The AI is now analyzing your request...
```

### 2. Structured JSON (for parsing)

```html
<!-- MCP_JOB_TRACKING
{
  "workflowId": "84dceb49-6d1e-4feb-8922-52fe3e31b99e",
  "pollTool": "get_workflow_status",
  "pollIntervalMs": 5000,
  "estimatedDurationMs": 90000,
  "canPoll": true
}
-->
```

### 3. Workflow Status Response

```html
<!-- MCP_WORKFLOW_STATUS
{
  "workflowId": "84dceb49-6d1e-4feb-8922-52fe3e31b99e",
  "status": "completed",
  "progress": 100,
  "currentStep": "completed",
  "elapsedMs": 15800,
  "nestedJobs": [
    {
      "jobId": "job_20251220164501_5e597bbe",
      "type": "orchestrate_task",
      "status": "running",
      "progress": 0
    }
  ],
  "canPoll": false
}
-->
```

---

## Nested Job Tracking

When a workflow triggers `orchestrate_task`, the CodingOrchestrator creates its own job ID. This system automatically:

1. **Captures** the nested job ID from the result
2. **Links** it to the parent workflow
3. **Displays** it in workflow status

### Job ID Formats

| Type | Format | Example |
|------|--------|---------|
| MemoryRouter Workflow | UUID | `84dceb49-6d1e-4feb-8922-52fe3e31b99e` |
| CodingOrchestrator Job | `job_YYYYMMDDHHMMSS_xxx` | `job_20251220164501_5e597bbe` |

---

## Usage Flow

```
1. execute_task("Generate a REST API")
   ↓
   Returns: Workflow ID: 84dceb49-...
   ↓
2. get_workflow_status(workflowId: "84dceb49-...")
   ↓
   Shows: Progress 45%, Nested Job: job_20251220...
   ↓
3. get_task_status(jobId: "job_20251220...")
   ↓
   Shows: Detailed code generation progress (10%, phase: coding_agent)
   ↓
4. Poll every 5 seconds until status = "completed"
```

---

## Auto-Polling (for Cursor Integration)

The structured JSON enables Cursor to auto-poll:

```javascript
// Pseudo-code for Cursor auto-polling
const response = await mcpCall('execute_task', { request: 'Generate API' });
const tracking = parseJson(response.match(/MCP_JOB_TRACKING\n(.*?)\n-->/s)[1]);

while (tracking.canPoll) {
  await sleep(tracking.pollIntervalMs);
  const status = await mcpCall('get_workflow_status', { workflowId: tracking.workflowId });
  const statusJson = parseJson(status.match(/MCP_WORKFLOW_STATUS\n(.*?)\n-->/s)[1]);
  
  if (statusJson.status !== 'running') {
    tracking.canPoll = false;
  }
  
  updateProgressUI(statusJson.progress);
}
```

---

## Duration Estimates

The system estimates workflow duration based on request content:

| Request Type | Estimated Duration |
|--------------|-------------------|
| Code generation (create/generate/build) | 90 seconds |
| Directory indexing | 120 seconds |
| File indexing | 30 seconds |
| Search operations | 10 seconds |
| Default | 30 seconds |

---

## Cursor Command File

A new command file is available at `.cursor/commands/TrackWorkflow.md` for easy access to workflow tracking.

---

## Total Tools Now

The MemoryRouter now exposes **46 tools** (was 44):

- `execute_task` - Smart AI router
- `list_available_tools` - Tool discovery
- **`get_workflow_status`** - NEW: Workflow tracking
- **`list_workflows`** - NEW: List all workflows
- Plus 42 tools from MemoryAgent and CodingOrchestrator

---

## Files Changed

1. `MemoryRouter.Server/Services/McpHandler.cs` - Added tracking classes and handlers
2. `.cursor/commands/TrackWorkflow.md` - New command file
3. `WORKFLOW_TRACKING.md` - This documentation
