# 🔧 Fix Your MCP Configuration - Step by Step

## **Problem:**
Collections created with literal name `${workspacefolder}_*` instead of actual workspace name.

---

## **Solution Steps:**

### **Step 1: Update Your MCP Config**

**File:** `C:\Users\chris\.cursor\mcp.json`

**REPLACE with this EXACT config:**

```json
{
  "mcpServers": {
    "code-memory": {
      "command": "node",
      "args": [
        "E:\\GitHub\\MemoryAgent\\mcp-stdio-wrapper.js"
      ]
    }
  }
}
```

**Notice:** 
- ✅ NO `env` section at all! 
- ✅ Wrapper will auto-detect workspace from current directory

---

### **Step 2: Restart Cursor**

**Important:** You MUST fully restart Cursor for MCP changes to take effect.

1. Close all Cursor windows
2. Quit Cursor completely
3. Start Cursor again
4. Open your workspace: `E:\GitHub\MemoryAgent`

---

### **Step 3: Test the Fix**

After Cursor restarts:

**Check the wrapper log:**
```powershell
Get-Content E:\GitHub\MemoryAgent\mcp-wrapper.log -Tail 20
```

**You should see:**
```
[2025-...] MCP Wrapper started (multi-workspace mode)
[2025-...] Using current working directory as workspace: E:\GitHub\MemoryAgent
[2025-...] Context: MemoryAgent
[2025-...] MCP Port: 5000
```

**If you see:** `Context: MemoryAgent` ✅ GOOD!
**If you see:** `Context: workspaceFolder` ❌ BAD - try Step 4

---

### **Step 4: Verify Collections**

After opening workspace, check Qdrant:

```powershell
curl http://localhost:6333/collections | ConvertFrom-Json | Select-Object -ExpandProperty result | Select-Object -ExpandProperty collections | Select-Object -ExpandProperty name
```

**You should see:**
```
memoryagent_classes    ✅ GOOD!
memoryagent_files      ✅ GOOD!
memoryagent_methods    ✅ GOOD!
memoryagent_patterns   ✅ GOOD!
```

**NOT:**
```
${workspacefolder}_classes   ❌ BAD!
```

---

### **Step 5: Clean Up (If Needed)**

If you still see bad collections, delete them:

```powershell
# PowerShell - escape the $ with backtick
curl.exe -X DELETE "http://localhost:6333/collections/`${workspacefolder}_classes"
curl.exe -X DELETE "http://localhost:6333/collections/`${workspacefolder}_files"
curl.exe -X DELETE "http://localhost:6333/collections/`${workspacefolder}_methods"
curl.exe -X DELETE "http://localhost:6333/collections/`${workspacefolder}_patterns"
```

---

## **What Changed:**

### **Before (Broken):**
```json
{
  "env": {
    "WORKSPACE_PATH": "${workspaceFolder}"  // ❌ Not expanded by Cursor
  }
}
```

Wrapper received: `WORKSPACE_PATH = "${workspaceFolder}"`  
Context became: `"workspaceFolder"`  
Collections: `${workspacefolder}_*`

### **After (Fixed):**
```json
{
  // No env section!
}
```

Wrapper uses: `process.cwd()` (Cursor's current directory)  
Gets actual path: `E:\GitHub\MemoryAgent`  
Context becomes: `"MemoryAgent"`  
Collections: `memoryagent_*` ✅

---

## **Why This Happens:**

Cursor/VS Code variables like `${workspaceFolder}` need special handling:

1. **In `.vscode/settings.json`** → Variables ARE expanded ✅
2. **In global `mcp.json`** → Variables may NOT be expanded ❌

So we removed the variable entirely and let the wrapper detect the workspace automatically!

---

## **Test It Works:**

Once you've restarted Cursor with the new config:

1. Open workspace: `E:\GitHub\MemoryAgent`
2. In Cursor chat, try:
   ```
   @memory index E:\GitHub\MemoryAgent\README.md
   ```
3. Check collections again - should create `memoryagent_*` collections

---

## **Quick Checklist:**

- [ ] Updated `C:\Users\chris\.cursor\mcp.json` (removed `env` section)
- [ ] Fully restarted Cursor (quit and reopen)
- [ ] Opened workspace: `E:\GitHub\MemoryAgent`
- [ ] Checked log file shows `Context: MemoryAgent`
- [ ] Verified collections are `memoryagent_*` not `${workspacefolder}_*`
- [ ] Deleted bad collections (if they exist)

---

**Try this and let me know if it works!** 🚀

