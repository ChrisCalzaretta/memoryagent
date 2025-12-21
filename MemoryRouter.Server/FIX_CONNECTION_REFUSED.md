# 🔧 FIX: Connection Refused Error

## Problem

```
Unhandled exception. System.Net.Http.HttpRequestException: Connection refused (localhost:5003)
System.Net.Sockets.SocketException (111): Connection refused
```

## Root Cause

**MemoryRouter was trying to connect to CodingOrchestrator on port 5003, but:**
1. CodingOrchestrator no longer exists (replaced by CodingAgent)
2. CodingAgent runs on port 5001 (not 5003)
3. MemoryRouter crashed on startup if the service wasn't available

---

## ✅ Solution Applied

### 1. **Updated Port Configuration**

Changed from port **5003** → **5001**

**Files updated:**
- `appsettings.json`
- `appsettings.Development.json`
- `Program.cs`

### 2. **Added Graceful Failure Handling**

**Before:**
```csharp
// Crashed if service unavailable
var mcpTools = await _codingOrchestrator.GetToolsAsync(cancellationToken);
```

**After:**
```csharp
// Continues gracefully if service unavailable
try
{
    var mcpTools = await _codingOrchestrator.GetToolsAsync(cancellationToken);
    // ... register tools ...
}
catch (HttpRequestException ex)
{
    _logger.LogWarning("⚠️ CodingAgent unavailable - continuing without coding tools");
}
```

### 3. **Updated Service Names**

- "CodingOrchestrator" → "CodingAgent" in logs
- Service name in ToolRegistry: `"coding-agent"`

---

## 🚀 How to Start Services

### Option 1: Start All Services

```bash
# Terminal 1: MemoryAgent
cd MemoryAgent.Server
dotnet run

# Terminal 2: CodingAgent
cd CodingAgent.Server
dotnet run

# Terminal 3: ValidationAgent
cd ValidationAgent.Server
dotnet run

# Terminal 4: MemoryRouter
cd MemoryRouter.Server
dotnet run
```

### Option 2: Start MemoryRouter Only (Partial Mode)

```bash
# Terminal 1: MemoryAgent (required)
cd MemoryAgent.Server
dotnet run

# Terminal 2: MemoryRouter (works without CodingAgent)
cd MemoryRouter.Server
dotnet run
```

**Output:**
```
⚠️ CodingAgent unavailable (connection refused) - continuing without coding tools
✅ ToolRegistry initialized with 50+ tools
   📦 MemoryAgent tools: 50+
   🎯 CodingAgent tools: 0
```

---

## 🎯 Expected Behavior

### With CodingAgent Running

```
🔧 Initializing ToolRegistry...
🔍 Discovering MemoryAgent tools...
✅ Discovered 50+ MemoryAgent tools
🔍 Discovering CodingAgent tools...
✅ Fetched 10+ tools from CodingAgent
✅ ToolRegistry initialized with 60+ tools
```

### Without CodingAgent Running

```
🔧 Initializing ToolRegistry...
🔍 Discovering MemoryAgent tools...
✅ Discovered 50+ MemoryAgent tools
🔍 Discovering CodingAgent tools...
⚠️ CodingAgent unavailable (connection refused) - continuing without coding tools
✅ ToolRegistry initialized with 50+ tools
⚠️ No coding tools available (CodingAgent not running)
```

---

## 🔍 Verify Fix

### 1. Check MemoryRouter Starts

```bash
cd MemoryRouter.Server
dotnet run
```

**Should see:**
- ✅ No crash
- ✅ Warning message (if CodingAgent not running)
- ✅ MemoryAgent tools discovered
- ✅ Server listening on port 5004

### 2. Check CodingAgent is Running

```bash
# Check if CodingAgent is running
curl http://localhost:5001/health
```

**If not running:**
```bash
cd CodingAgent.Server
dotnet run
```

### 3. Verify Tool Discovery

```bash
# After starting both services
curl http://localhost:5004/api/mcp/tools
```

**Should return:**
- MemoryAgent tools (50+)
- CodingAgent tools (10+)

---

## 📊 Service Status Check

```bash
# Check all services
curl http://localhost:5000/health  # MemoryAgent
curl http://localhost:5001/health  # CodingAgent
curl http://localhost:5002/health  # ValidationAgent
curl http://localhost:5004/health  # MemoryRouter
```

---

## 🎉 Summary

**Problem:** MemoryRouter crashed trying to connect to non-existent CodingOrchestrator (port 5003)

**Solution:**
1. ✅ Updated port: 5003 → 5001
2. ✅ Updated service name: CodingOrchestrator → CodingAgent
3. ✅ Added graceful failure handling
4. ✅ MemoryRouter now starts even if CodingAgent is down

**Result:** MemoryRouter is now resilient and won't crash on startup! 🎯

