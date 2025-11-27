# 🚀 Shared Stack Setup - Multi-Project Support

This guide explains how to set up the **single shared MCP stack** that supports multiple projects with automatic workspace detection and file watching.

---

## 📋 Overview

Instead of running separate stacks for each project, you run **ONE shared stack** that all your Cursor workspaces connect to. Each project is isolated by `context` parameter.

```
┌─────────────────────────────────────┐
│  Single Shared Stack (Port 5000)    │
│  ├─ MCP Server                       │
│  ├─ Qdrant (shared, context-filtered)│
│  ├─ Neo4j (shared, context-filtered) │
│  └─ Ollama (shared)                  │
└─────────────────────────────────────┘
         ▲         ▲         ▲
         │         │         │
    Project A  Project B  Project C
    (Context)  (Context)  (Context)
```

---

## 🎯 Benefits

| Feature | Description |
|---------|-------------|
| ✅ **No Manual Startup** | Stack is always running - just open Cursor |
| ✅ **Auto File Watching** | Detects changes per project automatically |
| ✅ **Auto Context Injection** | Wrapper adds context from workspace path |
| ✅ **One Config** | Same Cursor settings for all projects |
| ✅ **Resource Efficient** | One Qdrant, Neo4j, Ollama for all projects |
| ✅ **Cross-Project Search** | Can search across contexts if needed |

---

## 🚀 Quick Start

### **Step 1: Start the Shared Stack**

```powershell
# Run once - stack stays running
.\start-shared-stack.ps1
```

This starts:
- **MCP Server**: `localhost:5000`
- **Qdrant**: `localhost:6333`
- **Neo4j**: `localhost:7474`
- **Ollama**: `localhost:11434`

### **Step 2: Configure Cursor (ONE TIME)**

Create or update your Cursor MCP settings:

**Option A: Global Settings** (recommended)
- Open Cursor Settings (Ctrl+,)
- Search for "MCP"
- Edit `mcp_settings.json`:

```json
{
  "mcpServers": {
    "code-memory": {
      "command": "node",
      "args": ["E:\\GitHub\\MemoryAgent\\mcp-stdio-wrapper.js"],
      "env": {
        "WORKSPACE_PATH": "${workspaceFolder}"
      }
    }
  }
}
```

**Option B: Per-Project Settings**
Create `.cursor/mcp_settings.json` in each project with the same content as above.

### **Step 3: Open Any Project in Cursor**

```
1. Open E:\GitHub\TradingSystem in Cursor
   ↓
2. Wrapper auto-detects workspace
   ↓
3. Registers file watcher for "TradingSystem"
   ↓
4. Auto-injects context="TradingSystem" in all queries
   ↓
5. ✅ Ready to use!
```

### **Step 4: Verify It Works**

In Cursor AI chat, try:
```
Index this directory
```

Check the logs:
```powershell
Get-Content E:\GitHub\MemoryAgent\mcp-wrapper.log -Tail 20
```

You should see:
```
[2025-...] MCP Wrapper started (multi-workspace mode)
[2025-...] Workspace: E:\GitHub\TradingSystem
[2025-...] Context: TradingSystem
[2025-...] ✅ Workspace registered: E:\GitHub\TradingSystem → TradingSystem
```

---

## 🔧 How It Works

### **Workspace Detection**

```javascript
// mcp-stdio-wrapper.js
const WORKSPACE_PATH = process.env.WORKSPACE_PATH;  // From Cursor: ${workspaceFolder}
const CONTEXT_NAME = path.basename(WORKSPACE_PATH); // "TradingSystem"
```

### **Auto File Watching**

```
On Cursor Opens:
├─ Wrapper sends: register_workspace(E:\GitHub\TradingSystem, "TradingSystem")
├─ MCP Server creates FileSystemWatcher for that directory
└─ Watches: *.cs, *.py, *.js, *.ts, etc.

On File Change:
├─ FileSystemWatcher detects change
├─ Debounces for 3 seconds (batch changes)
├─ Triggers: reindex(context="TradingSystem", path=E:\GitHub\TradingSystem)
└─ Only TradingSystem data updated!

On Cursor Closes:
├─ Wrapper sends: unregister_workspace(E:\GitHub\TradingSystem)
└─ FileSystemWatcher stopped
```

### **Auto Context Injection**

```javascript
// All MCP tool calls automatically get context injected
query("How do we handle errors?")
  ↓
{ query: "...", context: "TradingSystem" }  // Auto-added!
```

---

## 📊 Managing Multiple Projects

### **Example: Working on 3 Projects**

```powershell
# Start shared stack (once)
.\start-shared-stack.ps1
```

**Cursor Window 1: TradingSystem**
- Context: `TradingSystem`
- Watching: `E:\GitHub\TradingSystem`

**Cursor Window 2: MemoryAgent**
- Context: `MemoryAgent`
- Watching: `E:\GitHub\MemoryAgent`

**Cursor Window 3: WebApp**
- Context: `WebApp`
- Watching: `E:\GitHub\WebApp`

All use **the same MCP server (port 5000)** but with different contexts!

---

## 🛑 Stopping the Stack

```powershell
.\stop-shared-stack.ps1
```

**Note:** Data is preserved in `d:\Memory\shared\`

---

## 🔍 Troubleshooting

### **Problem: Workspace not registered**

**Check logs:**
```powershell
Get-Content E:\GitHub\MemoryAgent\mcp-wrapper.log -Tail 50
```

**Fix:** Make sure `WORKSPACE_PATH` is set in Cursor config:
```json
"env": {
  "WORKSPACE_PATH": "${workspaceFolder}"
}
```

### **Problem: Wrong context used**

**Check what context is detected:**
Look for log line:
```
Context: <your-project-name>
```

**Fix:** The context is the **folder name** of your workspace. If you want a different context, open Cursor from a different folder.

### **Problem: File changes not detected**

**Check if watcher is active:**
```powershell
# In MCP server logs (inside container)
docker logs memory-agent-server | Select-String "File watcher"
```

Should show:
```
✅ File watcher started: /workspace/TradingSystem (13 patterns monitored)
```

**Fix:** Make sure AutoReindex is enabled in docker-compose-shared.yml:
```yaml
environment:
  - AutoReindex__Enabled=true
```

### **Problem: Multiple projects conflict**

Each project should have its **own unique folder name**:
- ✅ `E:\GitHub\TradingSystem`
- ✅ `E:\GitHub\MemoryAgent`
- ❌ `E:\GitHub\Project` and `C:\Work\Project` (same folder name!)

**Fix:** Rename folders or use different parent paths.

---

## 📝 Data Storage

All projects share the same databases but are isolated by context:

```
d:\Memory\shared\
├─ qdrant\         (shared Qdrant storage, filtered by context)
├─ neo4j\          (shared Neo4j storage, filtered by context)
├─ ollama\         (shared Ollama models)
├─ logs\           (MCP server logs)
└─ memory\         (internal memory)
```

**To clear a project's data:**
```cypher
// In Neo4j Browser (localhost:7474)
MATCH (n {context: "TradingSystem"}) DETACH DELETE n
```

---

## 🎯 Best Practices

1. **Keep Stack Running** - No need to stop/start between projects
2. **One Cursor Window Per Project** - Opens separate watcher per workspace
3. **Use Unique Folder Names** - Avoids context collisions
4. **Monitor Logs** - Check `mcp-wrapper.log` for issues
5. **Restart Stack Monthly** - Clear stale watchers: `.\stop-shared-stack.ps1` → `.\start-shared-stack.ps1`

---

## 🔄 Migrating from Per-Project Stacks

If you were using `start-project.ps1` before:

**Old way:**
```powershell
# Had to run for each project
.\start-project.ps1 -ProjectPath "E:\GitHub\TradingSystem"
.\start-project.ps1 -ProjectPath "E:\GitHub\MemoryAgent"
```

**New way:**
```powershell
# Run once
.\start-shared-stack.ps1

# Then just open projects in Cursor - they auto-register!
```

---

## 📞 Support

If you encounter issues:

1. Check logs: `E:\GitHub\MemoryAgent\mcp-wrapper.log`
2. Check MCP server logs: `docker logs memory-agent-server`
3. Verify stack is running: `docker ps | Select-String memory`
4. Restart stack: `.\stop-shared-stack.ps1` → `.\start-shared-stack.ps1`

---

**You're all set! Open any project in Cursor and start coding with AI-powered memory!** 🚀

