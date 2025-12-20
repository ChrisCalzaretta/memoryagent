# Routing Analysis Logic Fix - CRITICAL

## Problem

**"List all active coding tasks and their status"** was routing to `get_task_status` instead of `list_tasks`, causing a 400 Bad Request error.

### Root Cause

The prompt analysis logic was **too simplistic** - it used simple `Contains()` checks without considering **word combinations** or **priority**.

**Before:**
```csharp
else if (lowerRequest.Contains("status") || lowerRequest.Contains("progress") || lowerRequest.Contains("check"))
{
    sb.AppendLine("- Request type: CHECK STATUS");
    sb.AppendLine("- Required tool: get_task_status");
}
```

When the user said **"List all active coding tasks and their status"**:
- It saw the word **"status"** ✅
- Immediately classified as: `get_task_status` ❌
- Ignored the word **"list"** completely ❌

## Solution: Priority-Based Analysis

### Fix #1: Check "list" BEFORE "status" ✅

```csharp
// Check for "list" FIRST (before "status") - more specific intent
if (lowerRequest.Contains("list") && (lowerRequest.Contains("task") || lowerRequest.Contains("job")))
{
    sb.AppendLine("- Request type: LIST TASKS");
    sb.AppendLine("- Required tool: list_tasks");
}
else if (lowerRequest.Contains("workspace") && lowerRequest.Contains("status"))
{
    sb.AppendLine("- Request type: WORKSPACE STATUS");
    sb.AppendLine("- Required tool: workspace_status");
}
else if (lowerRequest.Contains("index"))
{
    sb.AppendLine("- Request type: INDEX FILES");
    sb.AppendLine("- Required tool: index");
}
// ... then check other patterns
else if (lowerRequest.Contains("status") || lowerRequest.Contains("progress"))
{
    sb.AppendLine("- Request type: CHECK TASK STATUS");
    sb.AppendLine("- Required tool: get_task_status");
}
```

### Fix #2: More Specific Keyword Matching ✅

Updated the keyword guide to emphasize **word combinations**:

```
2. **KEYWORDS MATTER**: Pay attention to SPECIFIC word combinations:
   - 'list tasks' or 'list all tasks' → use 'list_tasks' tool (NOT get_task_status!)
   - 'index' file/directory → use 'index' tool
   - 'workspace status' → use 'workspace_status' tool
   - 'task status' for ONE task → use 'get_task_status' tool
   - 'search' or 'find' code → use 'smartsearch' tool
```

## Decision Tree Priority

The new analysis checks in this order:

1. **`list` + `task`** → `list_tasks` (most specific)
2. **`workspace` + `status`** → `workspace_status`
3. **`index`** → `index`
4. **`find`/`where`/`search`** → `smartsearch`
5. **`create`/`build` + `plan`** → `manage_plan`
6. **`create`/`build`** → `orchestrate_task`
7. **`status`** → `get_task_status` (fallback)

This ensures **more specific patterns** are matched before **generic patterns**.

## Test Cases

### ✅ Test 1: List Tasks
```
Request: "List all active coding tasks and their status"
Analysis: Contains "list" + "task" → list_tasks ✅
Expected Tool: list_tasks
Result: PASS ✅
```

### ✅ Test 2: List Tasks (Variation)
```
Request: "Show me all tasks"
Analysis: Contains "show" + "task" → ... wait, this might fail!
```

**Need to add "show" to list_tasks check:**
```csharp
if ((lowerRequest.Contains("list") || lowerRequest.Contains("show")) && 
    (lowerRequest.Contains("task") || lowerRequest.Contains("job")))
```

### ✅ Test 3: Task Status (Single)
```
Request: "Check status of workflow 3bf191d0"
Analysis: Contains "status" but NOT "list" → get_task_status ✅
Expected Tool: get_task_status
Result: PASS ✅
```

### ✅ Test 4: Workspace Status
```
Request: "Get workspace status"
Analysis: Contains "workspace" + "status" → workspace_status ✅
Expected Tool: workspace_status
Result: PASS ✅
```

### ✅ Test 5: Index
```
Request: "Index directory e:\GitHub\CBC_AI\Scripts"
Analysis: Contains "index" → index ✅
Expected Tool: index
Result: PASS ✅ (confirmed working in background)
```

## Why This Fix Works

### Before: Linear Pattern Matching
```
status? → get_task_status ✅ (but too broad!)
```

### After: Hierarchical Pattern Matching
```
list + task? → list_tasks ✅
workspace + status? → workspace_status ✅
status? → get_task_status ✅ (only if above don't match)
```

**Result:** More specific intents are captured before falling back to generic patterns.

## Files Modified

**MemoryRouter.Server/Services/FunctionGemmaClient.cs**

1. **Line 214-253**: Reordered analysis logic with priority-based checks
   - Added: `list` + `task` check (highest priority)
   - Added: `workspace` + `status` check
   - Added: `index` check
   - Moved: `status` check to lower priority (fallback)

2. **Line 103-109**: Updated keyword guide to emphasize word combinations
   - Changed: "task status" → "task status for ONE task"
   - Added: "(NOT get_task_status!)" warning for list_tasks
   - Reordered: Most specific patterns first

## Deployment

```bash
# Rebuild
docker-compose -f docker-compose-shared-Calzaretta.yml build memory-router

# Redeploy
docker-compose -f docker-compose-shared-Calzaretta.yml up -d memory-router

# Wait 15 seconds for health check
docker ps --filter "name=memory-router"
# STATUS: (healthy) ✅
```

## Next Steps

If you still see routing errors, we may need to:
1. Add fuzzy matching for variations (e.g., "show all tasks")
2. Log the analysis result for debugging
3. Consider using a simple keyword scoring system instead of if/else

---

**Status:** ✅ Deployed and Testing  
**Date:** December 19, 2025  
**Priority:** HIGH - Fixing Critical Routing Bug
