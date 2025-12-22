# MemoryAgent - AI-Powered Development Intelligence

A multi-agent system for code understanding, generation, and quality assurance. Built for Cursor IDE integration via MCP (Model Context Protocol).

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          CURSOR IDE                                     │
│                              │                                          │
│              ┌───────────────┴───────────────┐                          │
│              ▼                               ▼                          │
│     🧠 memory-agent                  🚀 code-generator                  │
│     (Search & Analysis)              (Code Generation)                  │
└──────────────┬───────────────────────────────┬──────────────────────────┘
               ▼                               ▼
┌──────────────────────────┐     ┌────────────────────────────────────────┐
│   MemoryRouter :5010     │     │   CodingOrchestrator :5003             │
│         │                │     │         │                              │
│   FunctionGemma AI       │     │   ┌─────┼─────┬─────────┐              │
│         │                │     │   ▼     ▼     ▼         ▼              │
│   MemoryAgent :5000      │     │ Coding  Valid Design  Memory           │
│   (33 tools)             │     │ Agent   Agent Agent   Agent            │
└──────────────────────────┘     │ :5001   :5002 :5004   :5000            │
                                 └────────────────────────────────────────┘
```

## 🚀 Quick Start

### 1. Start Services

```powershell
docker-compose -f docker-compose-shared-Calzaretta.yml up -d
```

### 2. Configure Cursor

Copy `CURSOR_MCP_CONFIG.json` to your Cursor settings, or add manually:

```json
{
  "mcpServers": {
    "memory-agent": {
      "command": "node",
      "args": ["E:\\GitHub\\MemoryAgent\\memory-router-mcp-wrapper.js", "${workspaceFolder}"]
    },
    "code-generator": {
      "command": "node",
      "args": ["E:\\GitHub\\MemoryAgent\\orchestrator-mcp-wrapper.js", "${workspaceFolder}"]
    }
  }
}
```

### 3. Use in Cursor

**For search/analysis** (memory-agent):
```
@memory-agent Find all authentication code in this project
@memory-agent Explain how the database layer works
@memory-agent What security patterns are used here?
```

**For code generation** (code-generator):
```
@code-generator Create a REST API for user management in Python
@code-generator Build a Blazor chess game with all standard rules
@code-generator Generate a React dashboard with charts
```

---

## 🧠 Memory Agent (Search & Analysis)

AI-powered semantic search and code understanding. Routes through MemoryRouter with FunctionGemma for intelligent tool selection.

### Key Capabilities

| Category | What it Does |
|----------|--------------|
| **🔍 Search** | Semantic code search, pattern matching, graph traversal |
| **📊 Analysis** | Code complexity, impact analysis, security scanning |
| **🧠 Knowledge** | Q&A storage, project learning, historical context |
| **📋 Planning** | Task breakdown, complexity estimation, roadmaps |
| **📚 Indexing** | Workspace indexing, auto-reindex, incremental updates |

### Example Commands

```javascript
// Search
execute_task({ request: "Find all database query code" })
execute_task({ request: "Search for authentication patterns" })

// Analysis
execute_task({ request: "Explain how the payment system works" })
execute_task({ request: "Analyze security vulnerabilities" })

// Knowledge
execute_task({ request: "Store this solution for future reference" })
execute_task({ request: "What patterns have we used before?" })

// Planning
execute_task({ request: "Create a plan for implementing OAuth" })
execute_task({ request: "Estimate complexity of this refactoring" })
```

---

## 🚀 Code Generator (Multi-Agent Generation)

Direct access to CodingOrchestrator for code generation. **No routing overhead** - requests go straight to the orchestrator.

### Key Capabilities

| Category | What it Does |
|----------|--------------|
| **💻 Generation** | Multi-agent code generation with validation |
| **✅ Validation** | Automatic quality checks (score >= 8/10 required) |
| **🎨 Design** | Brand systems, design tokens, UI validation |
| **📁 Files** | Generate and apply multiple files at once |

### Available Tools

| Tool | Description |
|------|-------------|
| `orchestrate_task` | Start multi-agent code generation |
| `get_task_status` | Check job progress (% complete, current phase) |
| `cancel_task` | Stop a running job |
| `list_tasks` | See all active jobs |
| `apply_task_files` | Get generated files ready for writing |
| `design_create_brand` | Create complete brand system |
| `design_validate` | Validate code against brand guidelines |
| `design_get_brand` | Get existing brand definition |
| `design_questionnaire` | Get brand builder questions |

### Example Usage

```javascript
// Generate code
orchestrate_task({ 
  task: "Create a user authentication service with JWT",
  language: "typescript"
})

// Check progress
get_task_status({ jobId: "job_20241219_abc123" })

// Get generated files
apply_task_files({ 
  jobId: "job_20241219_abc123",
  basePath: "E:\\MyProject"
})

// Create brand system
design_create_brand({
  brand_name: "CloudSync",
  industry: "SaaS",
  personality_traits: ["Professional", "Trustworthy", "Modern"],
  // ... more options
})
```

---

## 🏛️ Service Architecture

| Service | Port | Purpose |
|---------|------|---------|
| **MemoryRouter** | 5010 | AI routing for MemoryAgent tools |
| **MemoryAgent** | 5000 | Search, indexing, knowledge storage |
| **CodingOrchestrator** | 5003 | Multi-agent code generation |
| **CodingAgent** | 5001 | LLM-powered code generation |
| **ValidationAgent** | 5002 | Code quality validation |
| **DesignAgent** | 5004 | Brand & design system management |
| **Qdrant** | 6333 | Vector database for embeddings |
| **Neo4j** | 7474/7687 | Graph database for relationships |

---

## 🔧 Configuration

### Environment Variables

**MemoryRouter:**
- `Ollama__BaseUrl` - Ollama server URL (default: http://ollama:11434)
- `MemoryAgent__BaseUrl` - MemoryAgent URL (default: http://memory-agent:5000)

**CodingOrchestrator:**
- `MemoryAgent__BaseUrl` - For search-before-write
- `CodingAgent__BaseUrl` - Code generation service
- `ValidationAgent__BaseUrl` - Validation service
- `DesignAgent__BaseUrl` - Design validation

### AI Models Used

| Model | Purpose | GPU |
|-------|---------|-----|
| `deepseek-coder-v2:16b` | Code generation | Primary (3090) |
| `phi4` | Validation, reasoning | Secondary (5070 Ti) |
| `functiongemma` | Tool selection routing | Primary |
| `mxbai-embed-large` | Embeddings | Primary |

---

## 📊 Tool Distribution

### Memory Agent Tools (33)

```
🔍 SEARCH           📊 ANALYSIS         🧠 KNOWLEDGE
- semantic_search   - explain_code      - store_qa
- smart_search      - impact_analysis   - get_insights  
- graph_search      - complexity        - start_session
- pattern_search    - security_scan     - record_context

📋 PLANNING         📚 INDEXING         🔧 UTILITIES
- create_plan       - index_workspace   - validate
- estimate          - reindex_all       - transform
- manage_tasks      - auto_reindex      - detect_patterns
```

### Code Generator Tools (12)

```
💻 GENERATION       📁 FILES            🎨 DESIGN
- orchestrate_task  - apply_task_files  - design_create_brand
- get_task_status   - list_tasks        - design_validate
- cancel_task                           - design_get_brand
                                        - design_questionnaire
                                        - design_list_brands
                                        - design_update_brand
```

---

## 🚦 Health Checks

```bash
# Check all services
curl http://localhost:5010/health  # MemoryRouter
curl http://localhost:5000/health  # MemoryAgent
curl http://localhost:5003/health  # CodingOrchestrator
curl http://localhost:5001/health  # CodingAgent
curl http://localhost:5002/health  # ValidationAgent
curl http://localhost:5004/health  # DesignAgent
```

---

## 📁 Key Files

| File | Purpose |
|------|---------|
| `CURSOR_MCP_CONFIG.json` | Cursor MCP configuration |
| `memory-router-mcp-wrapper.js` | Memory Agent STDIO wrapper |
| `orchestrator-mcp-wrapper.js` | Code Generator STDIO wrapper |
| `docker-compose-shared-Calzaretta.yml` | Docker services |
| `.cursor/cursorrules.mdc` | AI assistant rules |

---

## 🎓 Best Practices

### For Memory/Search
- Be specific in your queries
- Use semantic search for concept-based lookups
- Let the AI choose between search strategies

### For Code Generation
- Provide clear, detailed task descriptions
- Specify the target language when needed
- Check job status for long-running generations
- Use `apply_task_files` to get ready-to-write output

### General
- Use the right server for the right task
- Memory Agent = understanding existing code
- Code Generator = creating new code

---

## 🆘 Troubleshooting

### MCP Server Not Connecting

1. Check Docker services are running: `docker ps`
2. Verify health endpoints respond
3. Check wrapper logs in `orchestrator-wrapper.log`

### Code Generation Timeout

1. Jobs run in background - check status with `get_task_status`
2. Complex tasks may take 60+ seconds
3. Cancel stuck jobs with `cancel_task`

### Search Returns Empty

1. Ensure workspace is indexed: `execute_task({ request: "Index this workspace" })`
2. Check Qdrant is healthy
3. Try broader search terms

---

## 📜 License

MIT License - See LICENSE file for details.


