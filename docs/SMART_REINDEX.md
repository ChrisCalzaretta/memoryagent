# Smart Reindex System

## 🧠 Overview

The **Smart Reindex** system automatically detects changes in your codebase and only reindexes what's needed:
- ✅ **New files** - Auto-detected and indexed
- ✅ **Modified files** - Timestamp-based change detection
- ✅ **Deleted files** - Auto-removed from index
- ✅ **All file types** - Supports `.cs`, `.cshtml`, `.razor`, `.css`, `.scss`, `.less`, `.py`, `.md`

---

## 🚀 How It Works

### **1. File Discovery**
Scans the directory for all supported file types:
```
*.cs, *.cshtml, *.razor, *.py, *.md, *.css, *.scss, *.less
```

### **2. Change Detection**
Compares current files with indexed files:

| Status | Detection Method | Action |
|--------|-----------------|--------|
| **New files** | File exists on disk but not in index | Index it |
| **Modified files** | File modification time > last indexed time | Reindex it |
| **Deleted files** | File in index but not on disk | Remove it |
| **Unchanged files** | Modification time ≤ last indexed time | Skip it |

### **3. Smart Updates**
- **Deletes** old data before reindexing (prevents duplicates)
- **Preserves** unchanged files (fast!)
- **Logs** all changes for transparency

---

## 📊 Usage

### **Basic Reindex**
```powershell
$body = @{
    path='/workspace/CBC_AI'
    context='CBC_AI'
    removeStale=$true
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'
```

### **Reindex Specific Directory**
```powershell
# Reindex just your Views folder
$body = @{
    path='/workspace/CBC_AI/Views'
    context='CBC_AI'
    removeStale=$true
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'
```

### **Reindex Without Removing Stale Files**
```powershell
$body = @{
    path='/workspace/CBC_AI'
    context='CBC_AI'
    removeStale=$false  # Keep deleted files in index
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'
```

---

## 📈 Example Output

```json
{
    "success": true,
    "totalProcessed": 872,
    "filesAdded": 45,        // New files indexed
    "filesUpdated": 23,      // Modified files reindexed
    "filesRemoved": 12,      // Deleted files removed
    "errors": [],
    "duration": "00:02:34"
}
```

---

## 🔍 What Gets Tracked

### **Indexed Metadata**
Each file stores:
- **`file_path`**: Full path to the file
- **`indexed_at`**: UTC timestamp of last index
- **`context`**: Project context

### **Change Detection Logic**
```csharp
// Pseudo-code
if (fileExists && !inIndex) {
    → Add as NEW file
}
else if (fileExists && inIndex && modifiedTime > indexedTime) {
    → Mark as MODIFIED, reindex
}
else if (!fileExists && inIndex) {
    → Mark as DELETED, remove
}
else {
    → UNCHANGED, skip
}
```

---

## ⚡ Performance

### **Before Smart Reindex:**
- Reindexed ALL files every time
- 872 files = 18 minutes
- No change detection

### **After Smart Reindex:**
```
Scenario: Changed 5 files, added 2, deleted 1

Total files: 872
New files: 2        → Index (30 seconds)
Modified: 5         → Reindex (1 minute)
Deleted: 1          → Remove (instant)
Unchanged: 864      → Skip (instant)

Total time: ~1.5 minutes (vs 18 minutes!)
```

**10-12x faster** for incremental changes! 🚀

---

## 🎯 Use Cases

### **1. After Code Changes**
```powershell
# Made some edits, run smart reindex
.\smart-reindex.ps1 -ProjectPath "E:\GitHub\CBC_AI"
```

### **2. After Git Pull**
```powershell
# Pulled latest changes, update index
$body = @{path='/workspace/CBC_AI';context='CBC_AI'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'
```

### **3. After Adding New Files**
```powershell
# Added new CSS/Razor files, they'll auto-detect
$body = @{path='/workspace/CBC_AI/Views';context='CBC_AI'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'
```

### **4. Clean Up Deleted Files**
```powershell
# Remove files that no longer exist
$body = @{path='/workspace/CBC_AI';context='CBC_AI';removeStale=$true} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'
```

---

## 🛡️ Safety Features

### **1. Pre-Delete Before Reindex**
When reindexing a modified file:
1. Delete old data from Qdrant
2. Delete old data from Neo4j
3. Reindex file with fresh data

**Prevents:** Duplicate entries, stale relationships

### **2. Path Normalization**
Handles Windows/Linux path differences:
```
E:\GitHub\Project\File.cs → /workspace/Project/File.cs
```

### **3. Excluded Directories**
Automatically skips:
- `obj/` and `bin/` (build artifacts)
- `node_modules/` (npm packages)

### **4. Error Handling**
If a file fails to index:
- Logs the error
- Continues with other files
- Reports errors in response

---

## 📝 Logging

### **Example Logs:**
```
[INFO] Starting reindex for context: CBC_AI, path: /workspace/CBC_AI
[INFO] Found 872 current files in /workspace/CBC_AI
[INFO] Found 842 previously indexed files for context CBC_AI
[INFO] File detection: 45 new, 12 deleted, 815 potentially modified
[INFO] Removing 12 deleted files from index
[INFO] Indexing 45 new files
[INFO] Checking 815 files for modifications
[INFO] Reindexing 23 modified files
[INFO] No modified files detected for 792 files
[INFO] Reindex completed: 45 added, 23 updated, 12 removed in 94s
```

---

## 🔧 Technical Details

### **API Endpoint:**
```
POST /api/reindex
```

### **Request Body:**
```json
{
    "context": "CBC_AI",
    "path": "/workspace/CBC_AI",
    "removeStale": true
}
```

### **Response:**
```json
{
    "success": true,
    "totalProcessed": 872,
    "filesAdded": 45,
    "filesUpdated": 23,
    "filesRemoved": 12,
    "errors": [],
    "duration": "00:01:34.2341234"
}
```

---

## 🎨 File Type Support

| File Type | Extension | Extracted Elements |
|-----------|-----------|-------------------|
| **C#** | `.cs` | Classes, methods, properties, interfaces |
| **Razor** | `.cshtml`, `.razor` | Sections, code blocks, HTML elements, forms, tables |
| **Python** | `.py` | Classes, functions, decorators |
| **Markdown** | `.md` | Headers, sections, code blocks |
| **CSS** | `.css` | Rules, variables, media queries, animations |
| **SCSS** | `.scss` | Same as CSS + mixins, functions |
| **LESS** | `.less` | Same as CSS + mixins |

---

## 📊 Statistics Example

```
Before Reindex:
- 842 files indexed
- Last indexed: 2025-11-21

Changes Made:
- Added new CSS files: 15
- Modified Razor files: 8
- Deleted old views: 3
- Added new controllers: 5

Reindex Results:
✅ Added: 20 files (15 CSS + 5 controllers)
✅ Updated: 8 files (Razor views)
✅ Removed: 3 files (deleted views)
✅ Skipped: 811 files (unchanged)

Total Time: 1 minute 34 seconds
vs Full Reindex: 18 minutes 48 seconds

Time Saved: 17 minutes 14 seconds (92% faster!)
```

---

## ✅ Benefits

1. **10-12x Faster** - Only process changed files
2. **Auto-Detection** - No manual tracking needed
3. **All File Types** - CSS, Razor, Python, Markdown, etc.
4. **Stale Removal** - Clean up deleted files
5. **Incremental** - Perfect for CI/CD pipelines
6. **Transparent** - Full logging of all changes
7. **Safe** - Deletes old data before reindexing

---

## 🚀 Quick Start

### **After Making Code Changes:**
```powershell
# Option 1: Full project reindex (smart - only changed files)
$body = @{path='/workspace/CBC_AI';context='CBC_AI'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'

# Option 2: Specific folder reindex
$body = @{path='/workspace/CBC_AI/Views';context='CBC_AI'} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/reindex -Method POST -Body $body -ContentType 'application/json'
```

---

**Status:** ✅ Active and Production Ready  
**Version:** Latest  
**Last Updated:** 2025-11-22

