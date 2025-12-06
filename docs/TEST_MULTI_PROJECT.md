# 🧪 Testing Multi-Project Cursor MCP Integration

This guide will help you test the shared stack with multiple workspaces.

---

## ✅ Pre-Test Checklist

- [ ] Node.js is installed
- [ ] Docker Desktop is running
- [ ] All existing project stacks are stopped (`docker ps` shows no memory-agent containers)
- [ ] Cursor is closed

---

## 🚀 Test Procedure

### **Test 1: Start Shared Stack**

```powershell
.\start-shared-stack.ps1
```

**Expected Output:**
```
✅ Shared MCP Stack Started!

Services:
  • MCP Server:  http://localhost:5000
  • Qdrant:      http://localhost:6333
  • Neo4j:       http://localhost:7474
  • Ollama:      http://localhost:11434
```

**Verify:**
```powershell
docker ps
```

Should show 4 containers:
- `memory-agent-server`
- `memory-agent-qdrant`
- `memory-agent-neo4j`
- `memory-agent-ollama`

---

### **Test 2: Configure Cursor**

1. Open Cursor Settings (Ctrl+,)
2. Search for "MCP"
3. Click "Edit in settings.json"
4. Add this configuration:

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

5. Save and **restart Cursor completely**

---

### **Test 3: Open First Project**

1. Open `E:\GitHub\MemoryAgent` in Cursor
2. Wait 2 seconds for initialization
3. Check wrapper log:

```powershell
Get-Content E:\GitHub\MemoryAgent\mcp-wrapper.log -Tail 10
```

**Expected:**
```
[2025-...] MCP Wrapper started (multi-workspace mode)
[2025-...]   Workspace: E:\GitHub\MemoryAgent
[2025-...]   Context: MemoryAgent
[2025-...]   MCP Port: 5000
[2025-...] ✅ Workspace registered: E:\GitHub\MemoryAgent → MemoryAgent
```

4. In Cursor AI chat, type:

```
@memory list all MCP tools
```

**Expected:** Should list 30+ tools including `register_workspace` and `unregister_workspace`

5. Test indexing:

```
@memory index this directory
```

**Expected:** Should index files with context=MemoryAgent

6. Test query with auto-context:

```
@memory search for "MCP service implementation"
```

**Expected:** Should search and auto-inject context=MemoryAgent

---

### **Test 4: Multi-Workspace Support**

1. **Keep MemoryAgent window open**
2. Open a **new Cursor window**
3. Open a different project (e.g., if you have one at `E:\GitHub\TradingSystem`)

**If you don't have another project, create a test one:**
```powershell
mkdir E:\GitHub\TestProject
cd E:\GitHub\TestProject
echo "// Test file" > test.cs
```

4. Open `E:\GitHub\TestProject` in the new Cursor window
5. Check wrapper log again:

```powershell
# Should have TWO wrapper instances
Get-Content E:\GitHub\MemoryAgent\mcp-wrapper.log -Tail 20
```

**Expected:** Log should show registrations for BOTH workspaces:
```
[...] ✅ Workspace registered: E:\GitHub\MemoryAgent → MemoryAgent
[...] ✅ Workspace registered: E:\GitHub\TestProject → TestProject
```

6. In the TestProject window, index the directory:

```
@memory index this directory
```

7. Query in TestProject window:

```
@memory search for test
```

8. Query in MemoryAgent window:

```
@memory search for MCP
```

**Expected:** Each window searches its own context!

---

### **Test 5: File Watching**

1. In MemoryAgent window, edit a file:

```csharp
// MemoryAgent.Server/Program.cs
// Add a comment somewhere: // Test auto-reindex
```

2. Save the file
3. Check MCP server logs:

```powershell
docker logs memory-agent-server --tail 20
```

**Expected (within 3 seconds):**
```
File changed in MemoryAgent: Program.cs
🔄 Auto-reindex triggered for MemoryAgent: 1 file(s)
✅ Auto-reindex completed for MemoryAgent: +0 -0 ~1 files in 0.5s
```

4. In TestProject window, create a file:

```powershell
# In E:\GitHub\TestProject
echo "function test() {}" > test.js
```

**Expected in logs:**
```
File changed in TestProject: test.js
🔄 Auto-reindex triggered for TestProject: 1 file(s)
```

**Each project triggers its own reindex!**

---

### **Test 6: Context Isolation**

1. In MemoryAgent window:

```
@memory how many files are indexed?
```

Should show files from MemoryAgent only.

2. In TestProject window:

```
@memory how many files are indexed?
```

Should show files from TestProject only.

**Expected:** Each context is isolated!

---

### **Test 7: Workspace Unregistration**

1. Close the TestProject Cursor window
2. Wait 5 seconds
3. Check MCP server logs:

```powershell
docker logs memory-agent-server --tail 10
```

**Expected:**
```
🛑 File watcher stopped: /workspace/TestProject
```

4. MemoryAgent window should still be watching:

```powershell
docker logs memory-agent-server | Select-String "Active watchers"
```

**Expected:**
```
Active watchers: 1
  • MemoryAgent: /workspace/MemoryAgent (last activity: 0m ago)
```

---

### **Test 8: Clean Shutdown**

1. Close all Cursor windows
2. Stop the stack:

```powershell
.\stop-shared-stack.ps1
```

**Expected:**
```
✅ Shared stack stopped

Note: Data is preserved in d:\Memory\shared\
```

3. Verify containers stopped:

```powershell
docker ps | Select-String memory
```

Should show nothing.

---

## ✅ Success Criteria

All of these should pass:

- [ ] Shared stack starts successfully
- [ ] Cursor connects to MCP server (port 5000)
- [ ] Workspace auto-registers on Cursor open
- [ ] Context is auto-detected from folder name
- [ ] Context is auto-injected into queries
- [ ] Multiple workspaces can run simultaneously
- [ ] Each workspace has its own file watcher
- [ ] File changes trigger auto-reindex per context
- [ ] Data is isolated by context (separate search results)
- [ ] Workspace unregisters when Cursor closes
- [ ] Stale watchers cleanup after inactivity

---

## 🐛 Common Issues

### **Issue: Wrapper log shows "WORKSPACE_PATH not set"**

**Fix:** Update Cursor config to include:
```json
"env": {
  "WORKSPACE_PATH": "${workspaceFolder}"
}
```

### **Issue: Auto-reindex not working**

**Check:**
```powershell
docker exec memory-agent-server bash -c "env | grep AutoReindex"
```

Should show:
```
AutoReindex__Enabled=true
```

**Fix:** Make sure docker-compose-shared.yml has:
```yaml
environment:
  - AutoReindex__Enabled=true
```

### **Issue: Wrong context used**

**Check wrapper log for detected context:**
```
Context: <detected-name>
```

The context is always the **folder name** of your workspace. If you want a different context, rename the folder or use a different workspace root.

### **Issue: MCP tools not showing in Cursor**

1. Restart Cursor completely
2. Check MCP server is running: `docker ps`
3. Check wrapper can connect:
```powershell
curl http://localhost:5000/tools
```

Should return JSON with tool list.

---

## 📊 Performance Testing

### **Test: Large Directory Indexing**

```
@memory index this directory
```

On a large codebase (1000+ files), should complete in <2 minutes.

### **Test: Concurrent Indexing**

1. Open 3 different projects in Cursor
2. Index all 3 simultaneously:
```
@memory index this directory
```

All 3 should index without conflicts.

---

## 🎉 Test Complete!

If all tests pass, your multi-project setup is working correctly!

**Next Steps:**
- Use it in your daily workflow
- Monitor `mcp-wrapper.log` for issues
- Report any bugs or suggestions

---

**Happy coding with AI-powered memory across all your projects!** 🚀

