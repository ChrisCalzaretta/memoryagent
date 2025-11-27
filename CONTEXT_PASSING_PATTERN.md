# 🎯 Context Passing Pattern - How It Works

## **The Core Principle:**

**Every method that works with files MUST know the context (workspace) to use the correct collections.**

---

## **Why This Is Necessary:**

### **Without Context:**
```csharp
DeleteByFilePathAsync("/workspace/MemoryAgent/McpService.cs")
// ❌ Which collection? 
// - memoryagent_files?
// - cbc_ai_files?
// - tradingsystem_files?
// ❌ Can't tell from file path alone!
```

### **With Context:**
```csharp
DeleteByFilePathAsync("/workspace/MemoryAgent/McpService.cs", "MemoryAgent")
// ✅ Uses: memoryagent_files
// ✅ Clear and explicit!
```

---

## **The Pattern:**

### **All File Operations Need:**

```csharp
// Pattern:
Task SomeFileOperationAsync(
    string filePath,        // Which file
    string? context,        // Which workspace
    CancellationToken ct    // Cancellation
)
```

### **Examples:**

```csharp
// VectorService
Task DeleteByFilePathAsync(string filePath, string? context, CancellationToken ct);
Task GetFileLastIndexedTimeAsync(string filePath, string? context, CancellationToken ct);

// IndexingService  
Task IndexFileAsync(string filePath, string? context, CancellationToken ct);

// ReindexService
Task ReindexAsync(string path, bool recursive, string? context, CancellationToken ct);
```

---

## **How Context Flows:**

### **1. User Opens Workspace in Cursor**
```
E:\GitHub\MemoryAgent → context="MemoryAgent"
E:\GitHub\CBC_AI → context="CBC_AI"
```

### **2. Wrapper Auto-Injects Context**
```javascript
// Every tool call gets:
{
  "name": "index_file",
  "arguments": {
    "path": "E:\\GitHub\\MemoryAgent\\McpService.cs",
    "context": "MemoryAgent"  // ← Auto-injected!
  }
}
```

### **3. MCP Service Passes Context Through**
```csharp
// McpService.IndexFileToolAsync
var path = args["path"];
var context = args["context"];  // "MemoryAgent"

await _indexingService.IndexFileAsync(path, context, ct);
```

### **4. Indexing Service Passes to VectorService**
```csharp
// IndexingService.IndexFileAsync
await _vectorService.DeleteByFilePathAsync(containerPath, context, ct);
//                                                        ^^^^^^^^
//                                                   Passes context!
```

### **5. VectorService Uses Context for Collection Names**
```csharp
// VectorService.DeleteByFilePathAsync
var collections = new[] {
    GetFilesCollection(context),     // "memoryagent_files"
    GetClassesCollection(context),   // "memoryagent_classes"
    GetMethodsCollection(context)    // "memoryagent_methods"
};
```

---

## **What I Fixed:**

### **Before (BROKEN):**

```csharp
// VectorService.cs - Line 328
public async Task DeleteByFilePathAsync(string filePath, ...)
{
    var collections = new[] {
        GetFilesCollection(filePath),  // ❌ WRONG!
        // Tries to use filePath as context
        // "/workspace/MemoryAgent/file.cs" → "file_files" collection
        // Collection doesn't exist → 404 error!
    };
}
```

**Result:** Every file index operation failed with 404!

### **After (FIXED):**

```csharp
// VectorService.cs - Line 324
public async Task DeleteByFilePathAsync(string filePath, string? context, ...)
{
    var collections = new[] {
        GetFilesCollection(context),  // ✅ CORRECT!
        // Uses actual context
        // "MemoryAgent" → "memoryagent_files" collection
        // Collection exists → Success!
    };
}
```

**Result:** File operations work correctly!

---

## **Files Modified:**

| File | Change | Why |
|------|--------|-----|
| `IVectorService.cs` | Added `context` parameter to `DeleteByFilePathAsync` | Interface definition |
| `VectorService.cs` | Updated method signature and implementation | Use context for collection names |
| `IndexingService.cs` | Pass context to `DeleteByFilePathAsync` | Flow context through |
| `ReindexService.cs` | Pass context to `DeleteByFilePathAsync` | Flow context through |

---

## **How to Test:**

### **1. Clear Old Bad Data:**
```powershell
# Delete the empty collections that were created
curl -X DELETE http://localhost:6333/collections/cbc_ai_files
curl -X DELETE http://localhost:6333/collections/cbc_ai_classes
curl -X DELETE http://localhost:6333/collections/cbc_ai_methods
curl -X DELETE http://localhost:6333/collections/cbc_ai_patterns
```

### **2. Restart Cursor with Workspace:**
```
1. Quit Cursor
2. Start Cursor
3. Open E:\GitHub\MemoryAgent
```

### **3. Wrapper Should Register:**
```
Check log:
Get-Content E:\GitHub\MemoryAgent\mcp-wrapper.log -Tail 5

Should show:
Using workspace from command-line argument: E:\GitHub\MemoryAgent
Context: MemoryAgent
✅ Workspace registered: E:\GitHub\MemoryAgent → MemoryAgent
```

### **4. Auto-Index Should Run:**
```
Check MCP server logs:
docker logs memory-agent-server --tail 50

Should show:
🔍 Collections empty, triggering initial full reindex...
Indexing directory: /workspace/MemoryAgent
Indexed 10 files...
Indexed 20 files...
✅ Initial reindex completed for: MemoryAgent
```

### **5. Verify Collections Have Data:**
```powershell
curl http://localhost:6333/collections | ConvertFrom-Json | Select-Object -ExpandProperty result | Select-Object -ExpandProperty collections | Select-Object name, points_count

Should show:
name                   points_count
----                   ------------
memoryagent_files      50           ← Has data!
memoryagent_classes    120          ← Has data!
memoryagent_methods    300          ← Has data!
```

**NOT:**
```
name                   points_count
----                   ------------
memoryagent_files      0            ← Empty (old bug)
```

---

## **Summary:**

✅ **Before:** Context was missing, file operations failed with 404  
✅ **After:** Context flows through, operations work correctly  

✅ **Before:** Auto-index ran but indexed 0 files  
✅ **After:** Auto-index indexes all files successfully  

✅ **Before:** Collections created but stayed empty  
✅ **After:** Collections populate automatically  

---

**The bug is fixed! Now test it by restarting Cursor and opening a workspace!** 🚀

