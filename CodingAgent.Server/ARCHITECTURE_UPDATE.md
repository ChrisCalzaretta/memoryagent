# 🏗️ ARCHITECTURE UPDATE - CodingAgent v2

## ✅ WHAT CHANGED:

### **OLD Architecture (REMOVED):**
```
Memory Router (5010)
  ↓
CodingOrchestrator (5003) ← Coordinates workflow
  ├→ CodingAgent (5001) ← Generates code
  ├→ ValidationAgent (5002) ← Validates code
  └→ DesignAgent (5004) ← Design validation
```

### **NEW Architecture (CURRENT):**
```
Memory Router (5010)
  ↓
CodingAgent v2 (5001) ← 10-ATTEMPT RETRY LOOP + JobManager
  ├→ ValidationAgent (5002) ← Validates code
  └→ MemoryAgent (5000) ← Lightning context

DesignAgent (5004) ← Standalone (accessed separately)
```

---

## 🔥 WHY THE CHANGE:

### **OLD: CodingOrchestrator coordinated everything**
- Orchestrator called CodingAgent to generate
- Orchestrator called ValidationAgent to validate
- Orchestrator managed the retry loop
- **3 services needed for code generation!**

### **NEW: CodingAgent v2 has built-in orchestration**
- JobManager has the 10-attempt retry loop
- JobManager calls ValidationAgent directly
- JobManager tracks history and progress
- **2 services needed for code generation!**

---

## 📊 SERVICES:

### **CodingAgent.Server (Port 5001) - NEW!**
**What it has:**
- ✅ JobManager - Background jobs with retry loop
- ✅ CodeGenerationService - Phi4 + Deepseek + Claude
- ✅ Phi4ThinkingService - Strategic planning
- ✅ ValidationAgentClient - Calls ValidationAgent
- ✅ StubGenerator - Graceful degradation
- ✅ TemplateService - Project scaffolding

**What it does:**
1. Receives `orchestrate_task` request
2. Starts background job with JobManager
3. For each iteration (1-10):
   - Phi4 thinks strategically
   - Generate code (Deepseek/Claude)
   - Validate with ValidationAgent
   - Track history
   - Break if score >= 6.5 (after 3 attempts) or >= 8
4. Returns result

### **ValidationAgent.Server (Port 5002)**
**What it has:**
- ✅ ValidationService - Code quality scoring
- ✅ Phi4 validation model

**What it does:**
- Receives validation request
- Analyzes code with Phi4
- Returns score 0-10 + issues

### **MemoryAgent.Server (Port 5000)**
**What it has:**
- ✅ Lightning context and learning
- ✅ Pattern detection
- ✅ Q&A storage

**What it does:**
- Provides context for code generation
- Learns from successful generations

### **DesignAgent.Server (Port 5004)**
**What it has:**
- ✅ Brand system management
- ✅ Design validation

**What it does:**
- Validates UI code against brand
- Standalone, not part of retry loop

---

## 🚨 WHAT WAS REMOVED:

### **CodingOrchestrator.Server (Port 5003) ❌ REMOVED**
- Old architecture
- Retry loop moved to CodingAgent.Server/JobManager
- Dockerfile archived
- Docker-compose service removed

---

## 📝 FILES UPDATED:

1. ✅ **docker-compose-shared-Calzaretta.yml**
   - Removed `coding-orchestrator` service
   - Updated `memory-router` to point to `coding-agent:5001`
   - Updated `mcp-server` to point to `coding-agent:5001`
   - Added ValidationAgent dependency to `coding-agent`
   - Added job persistence volume
   - Added ValidationAgent config

2. ✅ **.cursorrules**
   - Simplified to focus on HOW TO USE
   - Removed implementation details
   - Clear tool descriptions

3. ✅ **orchestrator-mcp-wrapper.js**
   - Already pointing to port 5001 ✅
   - No changes needed!

4. ✅ **CodingAgent.Server/Services/JobManager.cs**
   - 10-attempt retry loop
   - ValidationAgent integration
   - History tracking
   - Smart break logic (6.5/8.0)

5. ✅ **CodingAgent.Server/Program.cs**
   - ValidationAgent client registration
   - All services wired up

---

## 🚀 HOW TO USE:

### **In Cursor:**
```
Can you use orchestrate_task to create a Calculator class?
```

### **Flow:**
```
1. Cursor → MCP Wrapper (orchestrator-mcp-wrapper.js)
2. MCP Wrapper → CodingAgent.Server:5001 (POST /api/orchestrator/orchestrate)
3. CodingAgent → JobManager starts background job
4. JobManager → 10-attempt retry loop:
   a. Phi4ThinkingService → Strategic planning
   b. CodeGenerationService → Generate code
   c. ValidationAgentClient → Validate (calls port 5002)
   d. Check score → Break if >= 6.5 (after 3) or >= 8
   e. Track history
5. Return job ID to Cursor
6. User calls get_task_status to check progress
7. User calls apply_task_files to write files
```

---

## ✅ SUMMARY:

**Before:**
- CodingOrchestrator (5003) coordinated everything
- 3 services needed for code generation

**After:**
- CodingAgent v2 (5001) has built-in orchestration
- 2 services needed for code generation
- Simpler, faster, more robust!

**All wired up and ready to test!** 🔥

