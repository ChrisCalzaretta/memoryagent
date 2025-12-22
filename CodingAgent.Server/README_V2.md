# CodingAgent.Server v2.0 - Architecture & Integration Guide

## 🚀 Overview

**CodingAgent.Server v2.0** is a complete rewrite of the code generation system with a simplified, production-ready architecture.

### Key Changes from v1.0

| Feature | v1.0 (Old) | v2.0 (New) |
|---------|-----------|-----------|
| **Architecture** | Multi-agent orchestrator with ValidationAgent | Single CodingAgent with template scaffolding |
| **Validation** | Separate ValidationAgent.Server | No validation (focus on generation speed) |
| **Job Management** | Complex state machine | Simple background jobs with persistence |
| **Templates** | None | Built-in templates for C#, Flutter, etc. |
| **API Endpoints** | `/api/orchestrator/task` | `/api/orchestrator/orchestrate` |
| **Response Format** | Complex with iterations/phases | Simple job status with files |
| **Focus** | Quality validation loops | Fast code generation |

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  orchestrator-mcp-wrapper.js            │
│                  (MCP Server - Port stdio)              │
│  Tools: orchestrate_task, get_task_status, cancel_task │
└────────────────────┬────────────────────────────────────┘
                     │ HTTP (localhost:5001)
                     ▼
┌─────────────────────────────────────────────────────────┐
│              CodingAgent.Server (ASP.NET Core)          │
│                     Port: 5001                          │
├─────────────────────────────────────────────────────────┤
│  Controllers:                                           │
│  - OrchestratorController: /api/orchestrator/*          │
│  - AgentController: /api/agent/* (direct generation)    │
├─────────────────────────────────────────────────────────┤
│  Services:                                              │
│  - JobManager: Background job execution                 │
│  - ProjectOrchestrator: Template + LLM generation       │
│  - CodeGenerationService: Multi-language code gen       │
│  - TemplateService: Project scaffolding                 │
│  - ModelOrchestrator: Smart model selection             │
│  - PromptBuilder: Lightning-powered prompts             │
├─────────────────────────────────────────────────────────┤
│  Clients:                                               │
│  - OllamaClient: Local LLM (Deepseek, Phi4, etc.)      │
│  - AnthropicClient: Claude API (optional)               │
│  - MemoryAgentClient: Lightning integration             │
└─────────────────────────────────────────────────────────┘
```

---

## 📡 API Endpoints

### OrchestratorController (Background Jobs)

#### POST `/api/orchestrator/orchestrate`
Start a background code generation job.

**Request:**
```json
{
  "task": "Create a UserService with CRUD operations",
  "language": "csharp",
  "maxIterations": 50
}
```

**Response:**
```json
{
  "jobId": "job_20250120123456_abc123",
  "message": "Job started successfully"
}
```

#### GET `/api/orchestrator/status/{jobId}`
Get job status and generated files.

**Response:**
```json
{
  "jobId": "job_20250120123456_abc123",
  "task": "Create a UserService with CRUD operations",
  "status": "completed",
  "progress": 100,
  "startedAt": "2025-01-20T12:34:56Z",
  "completedAt": "2025-01-20T12:35:30Z",
  "result": {
    "success": true,
    "fileChanges": [
      {
        "path": "Services/UserService.cs",
        "content": "...",
        "type": "Created",
        "reason": "Generated C# service"
      }
    ],
    "explanation": "Created a UserService with CRUD methods",
    "tokensUsed": 1500,
    "modelUsed": "deepseek-coder-v2:16b"
  }
}
```

#### GET `/api/orchestrator/jobs`
List all active and recent jobs.

**Response:**
```json
[
  {
    "jobId": "job_20250120123456_abc123",
    "task": "Create a UserService",
    "status": "completed",
    "progress": 100,
    "startedAt": "2025-01-20T12:34:56Z"
  }
]
```

#### POST `/api/orchestrator/cancel/{jobId}`
Cancel a running job.

---

### AgentController (Direct Generation)

#### POST `/api/agent/generate`
Generate code directly (synchronous, no background job).

**Request:**
```json
{
  "task": "Create a Calculator class",
  "language": "csharp",
  "context": null,
  "workspacePath": null
}
```

**Response:**
```json
{
  "success": true,
  "fileChanges": [
    {
      "path": "Calculator.cs",
      "content": "...",
      "type": "Created"
    }
  ],
  "explanation": "Created a Calculator class",
  "modelUsed": "deepseek-coder-v2:16b",
  "tokensUsed": 800
}
```

#### POST `/api/agent/fix`
Fix code based on validation feedback.

**Request:**
```json
{
  "task": "Fix the Calculator class",
  "language": "csharp",
  "existingFiles": [
    {
      "path": "Calculator.cs",
      "content": "..."
    }
  ],
  "previousFeedback": {
    "score": 5,
    "issues": ["Missing error handling"],
    "hasBuildErrors": false
  }
}
```

---

## 🔧 MCP Wrapper Integration

### Tool: `orchestrate_task`

**Description:** Start a background code generation job.

**Parameters:**
- `task` (required): The coding task description
- `language` (optional): Target language (auto-detected if not provided)
- `maxIterations` (optional): Maximum iterations (default: 50)

**Example:**
```javascript
{
  "task": "Create a REST API for user management with CRUD operations",
  "language": "csharp",
  "maxIterations": 50
}
```

**Returns:**
```
🚀 **Multi-Agent Coding Task Started**

**Job ID:** `job_20250120123456_abc123`
**Task:** Create a REST API for user management
**Language:** csharp
**Message:** Job started successfully

The CodingAgent is now working on your task.

**Progress:**
- Max iterations: 50

**To check status:** Call `get_task_status` with jobId: `job_20250120123456_abc123`
```

### Tool: `get_task_status`

**Description:** Check job progress and get generated files when complete.

**Parameters:**
- `jobId` (required): The job ID from `orchestrate_task`

**Returns (when completed):**
```
📊 **Task Status: ✅ COMPLETED**

**Job ID:** `job_20250120123456_abc123`
**Task:** Create a REST API for user management
**Progress:** 100%
**Started:** 2025-01-20T12:34:56Z
**Completed:** 2025-01-20T12:35:30Z

**✅ COMPLETED**
- Success: true
- Model Used: deepseek-coder-v2:16b
- Tokens Used: 1500
- Files Generated: 3
- Explanation: Created UserController, UserService, and User model

---
### 📄 Services/UserService.cs
**Type:** Created

```csharp
// Generated code here
```

---
### 📄 Controllers/UserController.cs
**Type:** Created

```csharp
// Generated code here
```
```

### Tool: `apply_task_files`

**Description:** Get generated files in a format ready for writing with explicit instructions.

**Parameters:**
- `jobId` (required): The job ID
- `basePath` (required): Base path for file writes (e.g., "E:\\GitHub\\MyProject")

**Returns:**
```
## 🚨 AGENT ACTION REQUIRED: Write 3 file(s)

**Base Path:** `E:\GitHub\MyProject`
**Job ID:** `job_20250120123456_abc123`
**Status:** ✅ Complete

---

**YOU MUST NOW USE THE `write` TOOL FOR EACH FILE BELOW:**

### File 1/3: Services/UserService.cs

**Action:** `write(file_path: "E:\GitHub\MyProject\Services\UserService.cs", contents: <code below>)`

```csharp
// Code here
```

---

### File 2/3: Controllers/UserController.cs

**Action:** `write(file_path: "E:\GitHub\MyProject\Controllers\UserController.cs", contents: <code below>)`

```csharp
// Code here
```

---

## ✅ After writing all files, confirm to user that code has been applied.
```

---

## 🎯 Supported Languages

The system supports **ALL major programming languages**:

- **C#** (.cs, .csproj)
- **Python** (.py, requirements.txt)
- **TypeScript/JavaScript** (.ts, .tsx, .js, .jsx)
- **Dart/Flutter** (.dart, pubspec.yaml)
- **Go** (.go, go.mod)
- **Rust** (.rs, Cargo.toml)
- **Java** (.java, pom.xml)
- **Swift** (.swift)
- **Kotlin** (.kt)
- **Ruby** (.rb)
- **PHP** (.php)
- **SQL** (.sql)
- **Shell** (.sh, .ps1, .bat)
- **HTML/CSS** (.html, .css, .scss)
- **YAML/JSON** (.yaml, .json)
- **Docker** (Dockerfile)
- **Terraform** (.tf)

---

## 🚀 Model Selection Strategy

### Primary Model (Always Loaded)
- **deepseek-coder-v2:16b** - Fast, high-quality code generation
- Runs on port 11434 (default Ollama port)

### Secondary Models (Loaded on demand)
- **phi4:14b** - Planning, complexity estimation, validation
- **qwen2.5-coder:7b** - Lightweight alternative
- **claude-3.5-sonnet** - Premium cloud model (requires API key)

### Smart Selection Logic
1. **First attempt:** Use primary model (deepseek-coder-v2:16b)
2. **Retry:** Rotate to different model (phi4, qwen, etc.)
3. **10+ attempts with score < 3:** Escalate to Claude (if configured)

---

## 📦 Template System

### Built-in Templates

1. **C# Console App**
   - Program.cs with Main method
   - .csproj with .NET 9.0
   - Basic structure

2. **C# Web API**
   - Program.cs with WebApplication
   - Controllers, Services, Models
   - Swagger/OpenAPI

3. **Flutter App**
   - lib/main.dart
   - pubspec.yaml
   - Material Design structure

### Template Detection

Templates are auto-detected from task descriptions:
- "Create a new Flutter app" → Flutter template
- "Build a C# console app" → C# Console template
- "Generate a REST API" → C# Web API template

---

## 🔄 Job Persistence

Jobs are persisted to disk at `/data/jobs/{jobId}.json` for:
- Crash recovery
- Historical tracking
- Long-running tasks

**Job States:**
- `running` - Currently executing
- `completed` - Successfully finished
- `failed` - Error occurred
- `cancelled` - User cancelled

---

## 🧪 Testing

### Build the Server
```bash
cd CodingAgent.Server
dotnet build
```

### Run the Server
```bash
dotnet run --urls "http://localhost:5001"
```

### Test Endpoints

**Health Check:**
```bash
curl http://localhost:5001/health
```

**Start Job:**
```bash
curl -X POST http://localhost:5001/api/orchestrator/orchestrate \
  -H "Content-Type: application/json" \
  -d '{"task":"Create a Calculator class","language":"csharp","maxIterations":50}'
```

**Check Status:**
```bash
curl http://localhost:5001/api/orchestrator/status/{jobId}
```

---

## 🔗 Integration with Memory Agent

CodingAgent.Server integrates with **MemoryAgent.Server (Lightning)** for:

1. **Prompt Management** - Fetch LLM prompts from Neo4j
2. **Similar Solutions** - Find past Q&A for context
3. **Pattern Detection** - Get code patterns and best practices
4. **Model Performance** - Record and query model success rates

**Configuration:**
```json
{
  "MemoryAgent": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

---

## 📝 Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "MemoryAgent": {
    "BaseUrl": "http://localhost:5000"
  },
  "JobPersistence": {
    "Path": "/data/jobs"
  },
  "Ollama": {
    "BaseUrls": [
      "http://localhost:11434",
      "http://localhost:11435",
      "http://localhost:11436"
    ]
  }
}
```

### Environment Variables

- `ANTHROPIC_API_KEY` - Claude API key (optional, for cloud generation)
- `ASPNETCORE_URLS` - Server URLs (default: http://localhost:5001)
- `WORKSPACE_PATH` - Default workspace path

---

## 🐛 Troubleshooting

### Issue: "Prompt not found in Lightning"
**Solution:** Ensure MemoryAgent.Server is running and prompts are seeded:
```bash
# Check MemoryAgent health
curl http://localhost:5000/health

# Prompts should be auto-seeded on startup
```

### Issue: "No models available"
**Solution:** Ensure Ollama is running with models loaded:
```bash
ollama list
ollama pull deepseek-coder-v2:16b
ollama pull phi4:14b
```

### Issue: "Job not found"
**Solution:** Check job persistence directory:
```bash
ls /data/jobs/
# or on Windows
dir E:\GitHub\MemoryAgent\data\jobs\
```

---

## 🎯 Best Practices

1. **Always specify language** - Improves model selection and code quality
2. **Use descriptive tasks** - "Create a UserService with CRUD operations" is better than "make a service"
3. **Set appropriate maxIterations** - 50 for simple tasks, 100+ for complex projects
4. **Check job status periodically** - Jobs run in background, poll every 5-10 seconds
5. **Use apply_task_files** - Get explicit write instructions for generated files

---

## 📊 Metrics & Monitoring

### Health Endpoint
```
GET /health
```

Returns:
```json
{
  "status": "healthy",
  "service": "CodingAgent.Server v2.0 (NEW)",
  "timestamp": "2025-01-20T12:34:56Z"
}
```

### Logging

Logs are written to console with structured logging:
- `🚀` - Job started
- `✅` - Job completed
- `❌` - Job failed
- `🛑` - Job cancelled
- `💻` - Code generation
- `🧠` - Model selection

---

## 🚀 Future Enhancements

- [ ] Add validation agent back (optional)
- [ ] Support for more templates (React, Vue, Angular, etc.)
- [ ] Real-time progress updates via WebSockets
- [ ] Multi-file project planning with Phi4
- [ ] Stub generation for failed attempts
- [ ] Failure report generation
- [ ] Integration with DesignAgent for UI validation

---

## 📚 Related Documentation

- [orchestrator-mcp-wrapper.js](../orchestrator-mcp-wrapper.js) - MCP wrapper implementation
- [AgentContracts](../Shared/AgentContracts/) - Shared contracts and models
- [MemoryAgent.Server](../MemoryAgent.Server/) - Lightning integration

---

**Version:** 2.0.0  
**Last Updated:** January 20, 2025  
**Status:** ✅ Production Ready



