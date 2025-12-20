# Cursor MCP Setup for MemoryRouter

## Problem

Cursor is not connecting to the MemoryRouter MCP server, even though the server is running and healthy.

**Error:** `Server "cursor-memory-studio" not found`

**Root Cause:** Cursor's MCP configuration is missing or incorrect.

---

## Solution: Configure Cursor to Use MemoryRouter

### Step 1: Locate Cursor Settings

Open Cursor's `settings.json` file:

**Windows:**
```
%APPDATA%\Cursor\User\settings.json
```
Or:
```
C:\Users\<YourUsername>\AppData\Roaming\Cursor\User\settings.json
```

**macOS:**
```
~/Library/Application Support/Cursor/User/settings.json
```

**Linux:**
```
~/.config/Cursor/User/settings.json
```

### Step 2: Add MCP Server Configuration

Open Cursor and:
1. Press `Ctrl+,` (or `Cmd+,` on Mac) to open Settings
2. Search for **"MCP"** or **"Model Context Protocol"**
3. Click **"Edit in settings.json"**
4. Add the following configuration:

```json
{
  "mcpServers": {
    "cursor-memory-studio": {
      "command": "node",
      "args": [
        "E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js",
        "E:/GitHub/MemoryAgent"
      ],
      "disabled": false,
      "env": {
        "MEMORY_ROUTER_URL": "http://localhost:5010"
      }
    }
  }
}
```

**⚠️ IMPORTANT:** 
- Replace `E:/GitHub/MemoryAgent` with your actual project path
- Use forward slashes `/` even on Windows
- The wrapper script path must be absolute

### Step 3: Restart Cursor

After saving the configuration:
1. Close Cursor completely
2. Reopen Cursor
3. Open your MemoryAgent workspace

---

## Verification

### Check MCP Server Connection

1. Open Cursor's Command Palette (`Ctrl+Shift+P` or `Cmd+Shift+P`)
2. Search for **"MCP: Show Active Servers"**
3. You should see `cursor-memory-studio` listed and connected

### Test the Connection

In Cursor's chat, try:
```
@cursor-memory-studio list available tools
```

**Expected:** Should list 44+ tools from MemoryAgent and CodingOrchestrator

---

## Alternative: Direct HTTP Configuration

If the STDIO wrapper doesn't work, you can configure Cursor to use the HTTP endpoint directly:

```json
{
  "mcpServers": {
    "memory-router": {
      "command": "curl",
      "args": [
        "-X", "POST",
        "http://localhost:5010/api/mcp",
        "-H", "Content-Type: application/json",
        "-d", "@-"
      ],
      "disabled": false
    }
  }
}
```

---

## Troubleshooting

### Issue 1: "Server not found"

**Cause:** Cursor hasn't loaded the MCP configuration

**Fix:**
1. Verify `settings.json` has the `mcpServers` section
2. Restart Cursor completely
3. Check Cursor's Output panel for MCP logs

### Issue 2: "Command 'node' not found"

**Cause:** Node.js not in PATH or not installed

**Fix:**
1. Install Node.js: https://nodejs.org/
2. Verify: `node --version` in terminal
3. Restart Cursor after installing Node.js

### Issue 3: Wrapper script fails

**Cause:** Incorrect path or permissions

**Fix:**
1. Verify the path exists: `Get-Item E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js`
2. Test the wrapper manually:
   ```bash
   node E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js E:/GitHub/MemoryAgent
   ```
3. Check wrapper logs in Cursor's Output panel

### Issue 4: "Connection refused"

**Cause:** MemoryRouter not running

**Fix:**
```bash
# Check if running
docker ps | findstr memory-router

# If not running, start it
cd E:/GitHub/MemoryAgent
docker-compose -f docker-compose-shared-Calzaretta.yml up -d memory-router

# Verify
curl http://localhost:5010/health
```

### Issue 5: Wrapper starts but no tools

**Cause:** MemoryRouter or backend services not healthy

**Fix:**
```bash
# Check all services
docker ps | findstr memory

# All should show (healthy)
# If not:
docker-compose -f docker-compose-shared-Calzaretta.yml restart memory-agent-server
docker-compose -f docker-compose-shared-Calzaretta.yml restart memory-router
```

---

## Testing the Full Stack

### Test 1: Verify MemoryRouter Health
```powershell
Invoke-RestMethod -Uri 'http://localhost:5010/health' -Method GET
```
**Expected:** `{"status":"healthy"}`

### Test 2: List Tools via API
```powershell
$body = @{
    jsonrpc = '2.0'
    id = 1
    method = 'tools/list'
    params = @{}
} | ConvertTo-Json

Invoke-RestMethod -Uri 'http://localhost:5010/api/mcp' `
    -Method POST `
    -ContentType 'application/json' `
    -Body $body
```
**Expected:** JSON response with 44+ tools

### Test 3: Execute Task via API
```powershell
$body = @{
    jsonrpc = '2.0'
    id = 2
    method = 'tools/call'
    params = @{
        name = 'execute_task'
        arguments = @{
            request = 'What is the workspace status?'
        }
    }
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri 'http://localhost:5010/api/mcp' `
    -Method POST `
    -ContentType 'application/json' `
    -Body $body
```
**Expected:** Workspace status response

### Test 4: Test via Cursor Chat

In Cursor, type:
```
@cursor-memory-studio execute: Get workspace status
```

**Expected:** Real-time response with workspace info

---

## What the Wrapper Does

The `memory-router-mcp-wrapper.js` script:

1. **Bridges STDIO ↔ HTTP**
   - Cursor uses STDIO (stdin/stdout) for MCP
   - MemoryRouter uses HTTP (REST API)
   - Wrapper translates between them

2. **Extracts Context**
   - Reads workspace path from args
   - Passes context to MemoryRouter
   - Ensures proper file path resolution

3. **Handles Errors**
   - Retries on network failures
   - Logs to stderr (not stdout)
   - Returns proper JSON-RPC errors

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    Cursor IDE                           │
│  - MCP Client (STDIO)                                   │
│  - Sends commands via stdin/stdout                      │
└─────────────────────────┬───────────────────────────────┘
                          │ STDIO
                          ↓
┌─────────────────────────────────────────────────────────┐
│     memory-router-mcp-wrapper.js (Node.js)              │
│  - Translates STDIO ↔ HTTP                              │
│  - Extracts workspace context                           │
│  - Forwards to MemoryRouter                             │
└─────────────────────────┬───────────────────────────────┘
                          │ HTTP POST
                          ↓
┌─────────────────────────────────────────────────────────┐
│        MemoryRouter (Docker, port 5010)                 │
│  - 3-Tier AI Routing (FunctionGemma → Phi4 → C#)       │
│  - Tool orchestration                                   │
│  - Background job management                            │
└─────────────────────────┬───────────────────────────────┘
                          │
            ┌─────────────┴─────────────┐
            ↓                           ↓
┌───────────────────────┐   ┌───────────────────────┐
│   MemoryAgent         │   │ CodingOrchestrator    │
│   (port 5000)         │   │ (port 5003)           │
│   - 38 tools          │   │ - 6 tools             │
│   - Semantic search   │   │ - Code generation     │
│   - Indexing          │   │ - Planning            │
│   - Q&A               │   │ - Validation          │
└───────────────────────┘   └───────────────────────┘
```

---

## Expected Behavior After Setup

1. **Cursor Startup:**
   ```
   🧠 MemoryRouter MCP Wrapper Starting...
      Workspace: E:/GitHub/MemoryAgent
      Context: memoryagent
      Router URL: http://localhost:5010
   ✅ MemoryRouter connected
   ```

2. **Using in Chat:**
   ```
   You: @cursor-memory-studio index the docs directory
   
   AI: 📋 Indexing docs/ directory...
       ✅ Started background job: abc-123-def-456
       ⏱️ Estimated time: 2-3 minutes
   ```

3. **Status Queries:**
   ```
   You: @cursor-memory-studio what's the status?
   
   AI: 📊 Background Jobs:
       - docs/ indexing: 60% complete (1m remaining)
       - src/ indexing: ✅ Complete (833 files)
   ```

---

## Advanced Configuration

### Enable Debug Logging

```json
{
  "mcpServers": {
    "cursor-memory-studio": {
      "command": "node",
      "args": [
        "E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js",
        "E:/GitHub/MemoryAgent"
      ],
      "disabled": false,
      "env": {
        "MEMORY_ROUTER_URL": "http://localhost:5010",
        "DEBUG": "true"
      }
    }
  }
}
```

### Custom Timeout

```json
{
  "mcpServers": {
    "cursor-memory-studio": {
      "command": "node",
      "args": [
        "E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js",
        "E:/GitHub/MemoryAgent"
      ],
      "disabled": false,
      "env": {
        "MEMORY_ROUTER_URL": "http://localhost:5010",
        "REQUEST_TIMEOUT": "30000"
      }
    }
  }
}
```

### Multiple Contexts

```json
{
  "mcpServers": {
    "memory-studio-project1": {
      "command": "node",
      "args": [
        "E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js",
        "E:/Projects/Project1"
      ],
      "disabled": false
    },
    "memory-studio-project2": {
      "command": "node",
      "args": [
        "E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js",
        "E:/Projects/Project2"
      ],
      "disabled": false
    }
  }
}
```

---

## FAQ

**Q: Do I need to rebuild Docker containers after configuring Cursor?**  
A: No, this is just a Cursor configuration change.

**Q: Can I use this with multiple workspaces?**  
A: Yes! Each workspace can have its own context. Just pass the workspace path as the second arg.

**Q: What if I'm using a different port?**  
A: Set the `MEMORY_ROUTER_URL` env var in the config.

**Q: Does this work with Cursor's AI chat?**  
A: Yes! Use `@cursor-memory-studio` in chat to invoke tools.

**Q: How do I disable it temporarily?**  
A: Set `"disabled": true` in the config, or remove the server entirely.

---

## Support

If you're still having issues:

1. **Check logs:**
   - Cursor Output panel → "MCP Servers"
   - Docker logs: `docker logs memory-router --tail 50`

2. **Verify manually:**
   ```bash
   node E:/GitHub/MemoryAgent/memory-router-mcp-wrapper.js E:/GitHub/MemoryAgent
   ```
   Then type:
   ```json
   {"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}
   ```

3. **Test HTTP directly:**
   ```bash
   curl -X POST http://localhost:5010/api/mcp \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
   ```

---

## Conclusion

Once configured:
- ✅ Cursor connects to MemoryRouter via STDIO wrapper
- ✅ All 44+ tools available via `@cursor-memory-studio`
- ✅ Background indexing works automatically
- ✅ AI-powered tool selection and routing
- ✅ Full workspace context awareness

**Next Steps:**
1. Configure `settings.json`
2. Restart Cursor
3. Test with `@cursor-memory-studio list tools`
4. Start indexing with `@cursor-memory-studio index workspace`

Your MemoryRouter is working perfectly - it just needs to be connected to Cursor! 🚀
