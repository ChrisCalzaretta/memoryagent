# ✅ **ROUTER ENDPOINT FIX**

## 🐛 **The Bug**

When calling `@code-agent generate_code`, you got:
```
{"error":"MCP error -32603: Cannot read properties of null (reading 'jobId')"}
```

## 🔍 **Root Cause**

The `mcp-wrapper-router.js` had **wrong endpoint URLs**:

### ❌ **Before (WRONG):**
```javascript
const ORCHESTRATE_URL = `${CODING_AGENT_URL}/api/orchestrate`;
const JOB_STATUS_URL = (jobId) => `${CODING_AGENT_URL}/api/jobs/${jobId}`;
```

These endpoints don't exist! The actual CodingAgent controller routes are:
- `/api/orchestrator/orchestrate` (not `/api/orchestrate`)
- `/api/orchestrator/status/{jobId}` (not `/api/jobs/{jobId}`)

### ✅ **After (FIXED):**
```javascript
const ORCHESTRATE_URL = `${CODING_AGENT_URL}/api/orchestrator/orchestrate`;
const JOB_STATUS_URL = (jobId) => `${CODING_AGENT_URL}/api/orchestrator/status/${jobId}`;
```

---

## 🛠️ **Additional Fix: Better Error Handling**

Added error checking so if the endpoint fails, you get a clear error message:

```javascript
if (!data || !data.jobId) {
  throw new Error('CodingAgent did not return a valid jobId. Check if the service is running on port 5001.');
}
```

---

## ✅ **What's Fixed**

1. ✅ Corrected ORCHESTRATE_URL endpoint
2. ✅ Corrected JOB_STATUS_URL endpoint
3. ✅ Added null checking
4. ✅ Added error messages with troubleshooting hints

---

## 🚀 **Now You Can Generate Code**

**Restart Cursor** and try again:

```
@code-agent generate_code
task: "Create a Blazor chess game"
language: "csharp"
maxIterations: 20
```

Should now work correctly! The code generation will:
1. ✅ Connect to correct endpoint
2. ✅ Get a valid jobId
3. ✅ Start code generation
4. ✅ Return progress updates

---

## 🔍 **How to Verify the Fix**

### Test the Endpoint Manually:
```bash
curl -X POST http://localhost:5001/api/orchestrator/orchestrate \
  -H "Content-Type: application/json" \
  -d '{
    "task": "Create a simple Calculator class",
    "language": "csharp",
    "maxIterations": 10,
    "workspacePath": "E:\\GitHub\\MemoryAgent"
  }'
```

**Should return:**
```json
{
  "jobId": "job_20251222_...",
  "status": "started"
}
```

---

## 📝 **Files Changed**

- ✅ `mcp-wrapper-router.js` - Fixed endpoints and added error handling

---

## 🎯 **Expected Behavior Now**

When you call:
```
@code-agent generate_code
task: "Create a Blazor chess game"
```

You should see:
```
🤖 Code generation started: job_20251222_abc123

Files will be auto-written to workspace/Generated/ on completion.
```

Instead of:
```
{"error":"MCP error -32603: Cannot read properties of null (reading 'jobId')"}
```

---

**The fix is live! Restart Cursor and try generating code again!** 🚀


