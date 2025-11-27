# 📋 Where You Need to Handle Context - Complete Guide

## **TL;DR: YOU DON'T NEED TO DO ANYTHING!**

The wrapper automatically handles context for you. But here's the full explanation:

---

## ✅ **AUTOMATIC (You Don't Touch These):**

### **1. Cursor Opens Workspace → Wrapper Detects Context**

**File:** `mcp-stdio-wrapper.js` (✅ Already implemented)

```javascript
// Line 11-15
const WORKSPACE_PATH = process.env.WORKSPACE_PATH;  // "E:\GitHub\TradingSystem"
const CONTEXT_NAME = path.basename(WORKSPACE_PATH); // "TradingSystem"

// Automatically determined - NO user action needed!
```

**What happens:**
- You open: `E:\GitHub\TradingSystem`
- Wrapper extracts: `context = "TradingSystem"`
- You open: `E:\GitHub\MemoryAgent`
- Wrapper extracts: `context = "MemoryAgent"`

---

### **2. Wrapper Auto-Injects Context Into All Queries**

**File:** `mcp-stdio-wrapper.js` (✅ Already implemented)

```javascript
// Lines 30-38 (approx)
if (jsonRpcRequest.method === 'tools/call') {
  const params = jsonRpcRequest.params;
  if (params && params.arguments && !params.arguments.context) {
    params.arguments.context = CONTEXT_NAME;  // ← AUTO-INJECTED!
    log(`Auto-injected context: ${CONTEXT_NAME}`);
  }
}
```

**What this means:**
- You type in Cursor: `@memory search for error handling`
- Wrapper adds: `context="TradingSystem"` automatically
- You NEVER need to manually specify context!

---

### **3. Wrapper Registers Workspace on Startup**

**File:** `mcp-stdio-wrapper.js` (✅ Already implemented)

```javascript
// On startup (line ~140):
async function registerWorkspace() {
  const request = {
    method: 'tools/call',
    params: {
      name: 'register_workspace',
      arguments: {
        workspacePath: WORKSPACE_PATH,
        context: CONTEXT_NAME  // ← Passes context to server
      }
    }
  };
  
  await sendToMcpServer(request);
}

// Called automatically on startup
registerWorkspace();
```

**What happens:**
- Cursor opens → Wrapper starts
- Wrapper immediately calls `register_workspace("E:\GitHub\TradingSystem", "TradingSystem")`
- MCP Server creates isolated storage for TradingSystem
- Done! No user action needed.

---

## 🔧 **BACKEND (I'm Implementing - You Don't Touch):**

### **4. MCP Server Creates Isolated Storage**

**File:** `MemoryAgent.Server/Services/McpService.cs` (⏳ Being updated)

```csharp
private async Task<McpToolResult> RegisterWorkspaceToolAsync(...)
{
    var workspacePath = args.GetValueOrDefault("workspacePath");
    var context = args.GetValueOrDefault("context");  // "TradingSystem"
    
    // Create isolated storage for this workspace
    await _vectorService.InitializeCollectionsForContextAsync(context);
    // Creates: tradingsystem_files, tradingsystem_classes, etc.
    
    await _graphService.CreateDatabaseAsync(context);
    // Creates: Neo4j database "tradingsystem"
    
    await _autoReindexService.RegisterWorkspaceAsync(workspacePath, context);
    // Starts file watcher for this workspace
}
```

---

### **5. All Services Use Context-Specific Storage**

**Files Being Updated:**
- `VectorService.cs` - ✅ Uses per-workspace collections
- `GraphService.cs` - ⏳ Will use per-workspace databases
- `IndexingService.cs` - ✅ Already passes context through
- `SmartSearchService.cs` - ✅ Already passes context through

**Example:**
```csharp
// When you query with context="TradingSystem":
await _vectorService.SearchSimilarCodeAsync(embedding, context: "TradingSystem");

// This searches in:
- tradingsystem_files
- tradingsystem_classes  
- tradingsystem_methods
- tradingsystem_patterns

// NOT in memoryagent_* collections!
```

---

## ❌ **WHAT YOU DON'T NEED TO DO:**

1. ❌ Don't manually specify context in Cursor
2. ❌ Don't configure context anywhere
3. ❌ Don't manage databases/collections
4. ❌ Don't worry about isolation

---

## ✅ **WHAT HAPPENS AUTOMATICALLY:**

| Action | Automatic Behavior |
|--------|-------------------|
| Open workspace in Cursor | Context extracted from folder name |
| Index files | Context auto-injected into request |
| Search/query | Context auto-injected, searches only your workspace |
| File changes | Auto-reindex with correct context |
| Multiple workspaces | Each gets its own isolated storage |
| Close Cursor | Workspace unregistered, file watcher stopped |

---

## 🎯 **EXAMPLE WORKFLOW:**

### **Day 1: Working on TradingSystem**

```
1. Open E:\GitHub\TradingSystem in Cursor
   ↓
   Wrapper: context="TradingSystem" (automatic!)
   ↓
   MCP Server creates:
   - Qdrant: tradingsystem_* collections
   - Neo4j: tradingsystem database
   ↓

2. In Cursor: "@memory index this directory"
   ↓
   Wrapper adds: context="TradingSystem"
   ↓
   Data stored in: tradingsystem_* collections

3. In Cursor: "@memory search for authentication"
   ↓
   Wrapper adds: context="TradingSystem"
   ↓
   Searches only: tradingsystem_* collections
   ↓
   Results: Only from TradingSystem!
```

### **Day 2: Switch to MemoryAgent**

```
1. Open E:\GitHub\MemoryAgent in Cursor
   ↓
   Wrapper: context="MemoryAgent" (automatic!)
   ↓
   MCP Server creates:
   - Qdrant: memoryagent_* collections
   - Neo4j: memoryagent database
   ↓

2. In Cursor: "@memory search for MCP"
   ↓
   Wrapper adds: context="MemoryAgent"
   ↓
   Searches only: memoryagent_* collections
   ↓
   Results: Only from MemoryAgent!
```

**TradingSystem and MemoryAgent are COMPLETELY ISOLATED!**

---

## 🤔 **ONLY ONE THING YOU MIGHT CUSTOMIZE:**

### **Context Name Override (Advanced, Optional)**

If you want a different context than the folder name:

```javascript
// In mcp-stdio-wrapper.js, you could change:
const CONTEXT_NAME = path.basename(WORKSPACE_PATH); // "MemoryAgent"

// To:
const CONTEXT_NAME = "MyCustomName";

// Or read from a config file:
const CONTEXT_NAME = loadContextConfig(WORKSPACE_PATH) || path.basename(WORKSPACE_PATH);
```

**But 99% of users won't need this!**

---

## 🎉 **SUMMARY:**

| What | Who Handles It | Where |
|------|----------------|-------|
| **Context Detection** | Wrapper (Automatic) | `mcp-stdio-wrapper.js` |
| **Context Injection** | Wrapper (Automatic) | `mcp-stdio-wrapper.js` |
| **Workspace Registration** | Wrapper (Automatic) | `mcp-stdio-wrapper.js` |
| **Storage Creation** | MCP Server (Automatic) | `McpService.cs` |
| **Data Isolation** | Services (Automatic) | `VectorService.cs`, `GraphService.cs` |

**YOU:** Just open Cursor and use it! Everything else is automatic! ✨

---

## 🚀 **When Implementation is Complete:**

You will:
1. Start shared stack: `.\start-shared-stack.ps1`
2. Configure Cursor once (see `.cursor/mcp_settings.json.example`)
3. Open any workspace
4. Use `@memory` commands - context is automatic!

**That's it!** No manual context handling needed.

---

**Continuing implementation now...**

