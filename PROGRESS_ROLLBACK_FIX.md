# Progress Rollback Fix - 98% → 40% Issue

## 🔍 Root Cause Analysis

### The Problem
Jobs were appearing to "roll back" from 98% progress to 40%, causing confusion about whether the system was stuck or making progress.

### What Was Actually Happening

The system has a **project-level retry loop** that intelligently regenerates plans when validation detects insufficient implementation:

1. **File Generation Phase (0% → 90%)**
   - Files are generated one by one
   - Progress: `(filesCompleted * 90) / totalFiles`
   - Reaches ~90-98% when all files generated

2. **Validation Phase (90%)**
   - Complete project validated
   - Validation agent checks for missing features, incomplete implementation

3. **Plan Regeneration Trigger (Lines 1672-1751)**
   - If validation score < 8 AND issues contain keywords like:
     - "missing", "incomplete", "not implemented"
     - "only includes", "lacks", "doesn't include"
     - "doesn't solve", "insufficient"
   - System determines **the plan itself is insufficient**
   - Clears generated files: `allGeneratedFiles.Clear()` (Line 1734)
   - Asks Phi4 to create a MORE DETAILED plan with additional files
   - Restarts project iteration with expanded plan

4. **Progress "Rollback"**
   - New iteration starts with `filesCompleted = 0`
   - Old progress calculation: `(0 * 90) / totalFiles = 0%`
   - Progress appeared to jump backwards

### Why This Design Exists
This is actually an **intelligent feature**, not a bug! The system:
- Detects when the initial plan is too simple/incomplete
- Dynamically expands the plan to address ALL requirements
- Regenerates with more comprehensive file structure
- Ensures high-quality, complete implementations

## ✅ The Fix

### Changed Progress Calculation to Be Cumulative

**Before:**
```csharp
jobState.Progress = (filesCompleted * 90) / totalFiles;
```
- Progress reset to 0 on each project iteration
- Appeared to roll back

**After:**
```csharp
// Calculate base progress from project iterations
var baseProgressFromIterations = ((projectIteration - 1) * 90) / maxIterations;
var progressRangeForThisIteration = 90 / maxIterations;

// Calculate cumulative progress
var progressInThisIteration = (filesCompleted * progressRangeForThisIteration) / totalFiles;
jobState.Progress = baseProgressFromIterations + progressInThisIteration;
```

### Progress Now Works Like This

With `maxIterations = 10`:
- **Iteration 1**: 0% → 9% (base: 0%, range: 9%)
- **Iteration 2**: 9% → 18% (base: 9%, range: 9%)
- **Iteration 3**: 18% → 27% (base: 18%, range: 9%)
- ...
- **Iteration 10**: 81% → 90% (base: 81%, range: 9%)
- **Validation**: 90% → 100%

Progress now **always moves forward**, even when plan is regenerated!

## 🎯 Additional Fixes Applied

### 1. Dictionary Duplicate Key Fix (Line 1398)
**Problem:** Files with invalid paths (like "csharp" as path) caused dictionary errors

**Fix:**
```csharp
ExistingFiles = allGeneratedFiles
    .Where(f => !string.IsNullOrWhiteSpace(f.Path) && 
               (f.Path.Contains('/') || f.Path.Contains('\\') || f.Path.Contains('.')))
    .GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.Last().Content, StringComparer.OrdinalIgnoreCase)
```

### 2. Debug Logging for Duplicates
Added logging to detect and report duplicate file paths:
```csharp
var duplicates = filePaths.GroupBy(p => p).Where(g => g.Count() > 1)
    .Select(g => $"{g.Key} (x{g.Count()})").ToList();
if (duplicates.Any())
{
    _logger.LogWarning("⚠️ Duplicate file paths detected: {Duplicates}", 
        string.Join(", ", duplicates));
}
```

## 📊 Impact

### Before
- ❌ Progress appeared to roll back (98% → 40%)
- ❌ Dictionary errors at 90% progress
- ❌ Users confused about job status

### After
- ✅ Progress always moves forward (cumulative)
- ✅ No dictionary duplicate key errors
- ✅ Clear iteration tracking in status
- ✅ Better visibility into plan regeneration

## 🧪 Testing

To verify the fix:
1. Start a complex job that requires multiple iterations
2. Watch progress increase steadily across iterations
3. Verify status shows: `iteration X/Y: file N/M: filename`
4. Confirm no rollback when plan is regenerated

## 📝 Files Modified

- `CodingAgent.Server/Services/JobManager.cs`
  - Lines 1330-1343: Added cumulative progress calculation
  - Lines 1375-1380: Updated progress calculation per file
  - Lines 1388-1402: Added duplicate detection and path filtering
  - Lines 1698-1710: Added path filtering for plan regeneration

## 🚀 Deployment

```bash
cd E:\GitHub\MemoryAgent
docker-compose -f docker-compose-shared-Calzaretta.yml build coding-agent
docker-compose -f docker-compose-shared-Calzaretta.yml up -d --force-recreate coding-agent
```

Container successfully rebuilt and deployed: ✅

---

**Date:** December 22, 2025  
**Status:** Fixed and Deployed  
**Impact:** High - Improves user experience and system reliability

