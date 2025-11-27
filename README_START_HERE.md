# 🚀 START HERE - Quick Setup Guide

## **What You Need to Do:**

### **Step 1: Update Your Cursor MCP Config**

**File:** `C:\Users\chris\.cursor\mcp.json`

**Replace entire contents with:**

```json
{
  "mcpServers": {
    "code-memory": {
      "command": "node",
      "args": [
        "E:\\GitHub\\MemoryAgent\\mcp-stdio-wrapper.js",
        "${workspaceFolder}"
      ]
    }
  }
}
```

**What changed:** Workspace is now passed as a **command-line argument** instead of environment variable.

---

### **Step 2: Start Docker**

```powershell
cd E:\GitHub\MemoryAgent
.\start-shared-stack.ps1
```

Wait for all containers to be healthy (~15 seconds).

---

### **Step 3: Restart Cursor**

1. **Quit Cursor completely** (File → Exit)
2. **Start Cursor again**
3. **Open your workspace:** `E:\GitHub\MemoryAgent`

---

### **Step 4: Verify It's Working**

**Check the log:**
```powershell
Get-Content E:\GitHub\MemoryAgent\mcp-wrapper.log -Tail 5
```

**Should see:**
```
Using workspace from command-line argument: E:\GitHub\MemoryAgent
Workspace: E:\GitHub\MemoryAgent
Context: MemoryAgent
```

**If you see `Context: chris`** → The variable didn't expand, see CURSOR_MCP_CONFIG_FINAL.md for fallback option.

---

### **Step 5: Test in Cursor**

In Cursor chat:
```
@memory index E:\GitHub\MemoryAgent\README.md
```

**Check collections:**
```powershell
curl http://localhost:6333/collections | ConvertFrom-Json | Select-Object -ExpandProperty result | Select-Object -ExpandProperty collections | Select-Object -ExpandProperty name | Sort-Object
```

**Should see:**
```
memoryagent_classes    ✅
memoryagent_files      ✅
memoryagent_methods    ✅
memoryagent_patterns   ✅
```

**NOT:**
```
chris_*                ❌
${workspacefolder}_*   ❌
```

---

## **Files You Modified:**

1. `C:\Users\chris\.cursor\mcp.json` - Your Cursor MCP configuration
2. `E:\GitHub\MemoryAgent\start-shared-stack.ps1` - Start Docker script (already exists)
3. `E:\GitHub\MemoryAgent\mcp-stdio-wrapper.js` - Updated to accept command-line argument

---

## **What's Automatic:**

Once configured:
- ✅ Context auto-detected from workspace folder name
- ✅ Context auto-injected into all queries
- ✅ Per-workspace collections created automatically
- ✅ File watcher monitors for changes
- ✅ Complete data isolation between workspaces

---

## **That's It!**

After these 5 steps, you can:
- Open any workspace in Cursor
- Use `@memory` commands
- Each workspace gets its own isolated storage
- No manual configuration needed per workspace

---

**Questions? Check:**
- `CURSOR_MCP_CONFIG_FINAL.md` - Detailed config options
- `WORKSPACE_ISOLATION_SUCCESS.md` - Architecture explanation
- `WHERE_TO_HANDLE_CONTEXT.md` - How context works

