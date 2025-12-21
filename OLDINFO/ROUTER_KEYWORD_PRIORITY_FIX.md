# Router Keyword Priority Fix

## Problem Identified

**Bug:** "What's the status on indexing?" was routing to `index` instead of `list_tasks`

**Root Cause:** Keyword check order was wrong - `"index"` was checked before `"status"`

```
User: "status on indexing"
         ↓
Check: Contains "index"? YES ✅
         ↓
Route to: index tool ❌ WRONG!
         ↓
Never checks: Contains "status"
```

## Fix Applied

**Moved "status" check BEFORE "index" check** in both:
- `FunctionGemmaClient.cs` (Tier 1 AI prompt)
- `RouterService.cs` (Tier 3 C# fallback)

### New Priority Order:

```
1. list + task → list_tasks
2. workspace + status → workspace_status
3. status/progress/check → get_task_status or list_tasks ← MOVED UP
4. index → index ← MOVED DOWN
5. find/where/search → smartsearch
6. create plan → manage_plan
7. create/build/generate → orchestrate_task
8. default → smartsearch
```

## Example Flows

### Before Fix ❌:
```
"status on indexing"
  → Contains "index" 
  → Routes to index
  → Triggers ANOTHER indexing job!
```

### After Fix ✅:
```
"status on indexing"
  → Contains "status"
  → No job ID found
  → Routes to list_tasks
  → Shows all background jobs
```

## Testing

```powershell
# Test 1: Status query
"What is the status on indexing" 
→ Should route to list_tasks ✅

# Test 2: Index command  
"Index the docs folder"
→ Should route to index ✅

# Test 3: With job ID
"Check status of abc-123-def-456"
→ Should route to get_task_status ✅
```

## Files Modified

1. **FunctionGemmaClient.cs** (Lines 258-305)
   - Moved "status" check to line 258 (before "index")
   - Removed duplicate "status" check from line 292

2. **RouterService.cs** (Lines 317-372)
   - Moved "status" check to line 317 (before "index")  
   - Removed duplicate "status" check from line 361

## Related Issue: MemoryAgent Connection

The error stacktrace also showed:
```
System.Net.Http.HttpRequestException: An established connection was aborted
```

**Cause:** MemoryAgent container may not be running or network timeout

**Check:** `docker ps | grep memory-agent`

**Fix if needed:** `docker-compose -f docker-compose-shared-Calzaretta.yml up -d memory-agent`

## Keyword Conflict Resolution Strategy

When a query matches multiple keywords, priority matters:

| Priority | Keyword Pattern | Tool | Example |
|----------|----------------|------|---------|
| 1 | list + task | list_tasks | "list all tasks" |
| 2 | workspace + status | workspace_status | "workspace status" |
| **3** | **status/progress/check** | **list_tasks** | **"status on indexing"** ✅ |
| 4 | index | index | "index docs" |
| 5 | find/where/search | smartsearch | "find auth code" |

**Rule:** More **specific combinations** beat **single keywords**

## Why This Matters

Background jobs like indexing can take minutes. Users need to:
1. Start job → Get Job ID
2. Check status → See progress
3. Continue working → No blocking

If "status" query triggers another index job, it:
- ❌ Wastes resources
- ❌ Confuses users ("why is it indexing again?")
- ❌ Doesn't answer the question

## Verification

After deployment, test these queries:

```bash
# Should route to list_tasks
- "What's the status?"
- "Check progress on indexing"  
- "Are there any running tasks?"

# Should route to get_task_status (if UUID present)
- "Status of 12345678-1234-1234-1234-123456789012"
- "Check job abc-def-ghi"

# Should route to index
- "Index the docs directory"
- "Index all markdown files"
- "Re-index the workspace"
```

## Conclusion

✅ **Keyword priority fixed**
✅ **"Status" queries now work correctly**
✅ **Won't accidentally trigger duplicate jobs**

This fix ensures the router interprets user intent correctly when queries contain overlapping keywords.
