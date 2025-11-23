# Memory Code Agent

AI-powered code memory system that uses vector embeddings and graph databases to understand your codebase at a deep level.

## 🚀 Quick Start

### 1. Start the Agent for Your Project

```powershell
.\start-project.ps1 -ProjectPath "E:\GitHub\YourProject" -AutoIndex
```

This will:
- Create isolated containers for your project
- Index all code files automatically
- Track 37,525+ relationships per project (methods, classes, dependencies, etc.)

### 2. Configure Cursor

Add this to your Cursor settings (`.cursor/mcp_settings.json`):

```json
{
  "mcpServers": {
    "code-memory": {
      "command": "node",
      "args": [
        "E:\\GitHub\\MemoryAgent\\mcp-stdio-wrapper.js"
      ],
      "env": {
        "MCP_PORT": "5098"
      }
    }
  }
}
```

**Important:** Update `MCP_PORT` to match your project's port (shown when you run `start-project.ps1`).

### 3. Restart Cursor

Close and reopen Cursor to load the MCP server.

---

## 🔍 Available Search Commands

Once configured, you can use these tools in Cursor:

### 1. **Semantic Code Search** (`query`)
Ask natural language questions about your code:

```
Query: "How do we handle database errors?"
Query: "Where is user authentication implemented?"
Query: "Show me all dependency injection patterns"
```

**Parameters:**
- `query` (required): Natural language question
- `context` (optional): Project context name
- `limit` (default: 5): Maximum results
- `minimumScore` (default: 0.7): Similarity threshold

**Example in Cursor:**
Just ask the AI: "Search the code memory for error handling patterns" and it will use this tool.

---

### 2. **Impact Analysis** (`impact_analysis`)
Find out what would break if you change a class:

```
className: "DataPrepPlatform.Core.Services.UserService"
```

Returns all classes, methods, and files that depend on this class.

**Use case:** Before refactoring, see what will be affected.

---

### 3. **Dependency Chain** (`dependency_chain`)
Trace the full dependency tree for a class:

```
className: "DataPrepPlatform.Data.ProjectDbContext"
maxDepth: 5
```

Returns the complete chain: A depends on B, B depends on C, etc.

**Use case:** Understand complex dependencies, find circular references.

---

### 4. **Find Circular Dependencies** (`find_circular_dependencies`)
Detect circular dependency issues in your codebase:

```
context: "MyProject"
```

Returns all circular dependency loops found.

**Use case:** Code quality checks, architectural reviews.

---

### 5. **Reindex** (`reindex`)
Update the code memory after making changes:

```
path: "/workspace/MyProject/src"
context: "MyProject"
removeStale: true
```

This will:
- Detect new files
- Update modified files
- Remove deleted files from memory

**Use case:** Keep the memory in sync with code changes.

---

## 📊 What Gets Tracked

The agent tracks **13 relationship types**:

1. **CALLS** - Method A calls Method B
2. **INJECTS** - Constructor dependencies (Dependency Injection)
3. **HASTYPE** - Properties and their types
4. **ACCEPTSTYPE** - Method parameter types
5. **RETURNSTYPE** - Method return types
6. **IMPORTS** - Using/namespace directives
7. **INHERITS** - Class inheritance
8. **IMPLEMENTS** - Interface implementations
9. **HASATTRIBUTE** - Attributes/decorations
10. **USESGENERIC** - Generic type parameters
11. **THROWS** - Exception declarations
12. **CATCHES** - Exception handling
13. **DEFINES** - Code element definitions

**Example Results:**
- 536 files indexed
- 577 classes tracked
- 1,526 methods analyzed
- 37,525+ relationships discovered

---

## 🗂️ Multi-Project Support

Each project gets its own isolated environment:

```powershell
# Start project 1
.\start-project.ps1 -ProjectPath "E:\GitHub\TradingSystem"

# Start project 2
.\start-project.ps1 -ProjectPath "E:\GitHub\DataPrepPlatform"

# Stop a specific project
.\stop-project.ps1 -ProjectName "tradingsystem"
```

All data is stored in `d:\Memory\{project-name}\`.

---

## 🧠 How to Use in Cursor

Once the MCP server is configured, you can naturally ask questions like:

### Example Queries:

**Understanding Code:**
- "Search the code memory for all error handling patterns"
- "Find all classes that use dependency injection"
- "Show me how authentication is implemented"

**Impact Analysis:**
- "What would break if I change the UserService class?"
- "Show me all dependencies for ProjectDbContext"

**Code Quality:**
- "Find circular dependencies in this project"
- "Show me all exception handling patterns"

**Navigation:**
- "Find all methods that call SaveChangesAsync"
- "Show me all classes that implement IRepository"

The AI will automatically use the appropriate MCP tool based on your question.

---

## 🛠️ Technology Stack

- **MCP Server**: C# .NET 9 (ASP.NET Core)
- **Vector DB**: Qdrant (semantic search)
- **Graph DB**: Neo4j (relationships & dependencies)
- **Embeddings**: Ollama (mxbai-embed-large) - runs on your GPU
- **Code Parser**: Roslyn (Microsoft's C# compiler)
- **Container**: Docker Compose

---

## 📈 Performance

- **Indexing Speed**: ~2-3 minutes for 536 files
- **Memory Usage**: 8GB per service (24GB total)
- **GPU Acceleration**: Enabled for embeddings
- **Query Speed**: Sub-second for most searches

---

## 🔧 Advanced Configuration

### Using External Services

Instead of local containers, use cloud-hosted Qdrant/Neo4j:

```powershell
.\start-project.ps1 `
  -ProjectPath "E:\GitHub\MyProject" `
  -QdrantUrl "https://your-qdrant-instance.com:6333" `
  -Neo4jUrl "bolt://your-neo4j-instance.com:7687" `
  -Neo4jUser "neo4j" `
  -Neo4jPassword "yourpassword"
```

### Port Configuration

If you have port conflicts, specify custom ports:

```powershell
.\start-project.ps1 `
  -ProjectPath "E:\GitHub\MyProject" `
  -McpPort 5099 `
  -QdrantHttpPort 6335 `
  -Neo4jHttpPort 7475
```

---

## 🐛 Troubleshooting

### MCP Server Not Connecting

1. Check containers are running:
   ```powershell
   docker ps --filter "name=agent"
   ```

2. Verify the port in your Cursor config matches the MCP server port

3. Check logs:
   ```powershell
   docker logs {project-name}-agent-server --tail 50
   ```

### Indexing Timeout

For very large projects (1000+ files), indexing runs as a background job. Check status:

```powershell
docker logs {project-name}-agent-server -f
```

### Tools Not Showing in Cursor

1. Restart Cursor completely
2. Check MCP logs in Cursor: View → Output → select "MCP" from dropdown
3. Verify wrapper script path is correct in config

---

## 📝 Example Workflow

```powershell
# 1. Start the agent for your project
.\start-project.ps1 -ProjectPath "E:\GitHub\CBC_AI" -AutoIndex

# 2. Update Cursor config with port 5098
# (Edit .cursor/mcp_settings.json)

# 3. Restart Cursor

# 4. Ask the AI:
#    "Search code memory for authentication patterns"
#    "What would break if I refactor UserService?"
#    "Find all dependency injection in the project"

# 5. After making code changes:
#    "Reindex the code memory for CBC_AI project"
```

---

## 🎯 Use Cases

✅ **Onboarding** - Quickly understand large codebases  
✅ **Refactoring** - See impact before making changes  
✅ **Code Review** - Find similar patterns and inconsistencies  
✅ **Architecture** - Visualize dependencies and detect issues  
✅ **Documentation** - Auto-generate based on actual code patterns  
✅ **AI Context** - Give AI deep understanding of your entire codebase  

---

## 📚 Documentation

All documentation has been moved to the `/docs` directory:

- **[📖 Documentation Index](docs/INDEX.md)** - Complete documentation catalog
- **[🔍 Pattern Validation Guide](docs/PATTERN_VALIDATION_AND_RECOMMENDATIONS.md)** - Quality scoring & recommendations
- **[🤖 AI Agent Patterns](docs/AI_AGENT_PATTERNS_DEEP_CRAWL_COMPLETE.md)** - 60 AI agent patterns
- **[☁️ Azure Patterns](docs/AZURE_PATTERNS_COMPREHENSIVE.md)** - 33 Azure best practice patterns
- **[🔧 Smart Search API](docs/SMART_SEARCH_API.md)** - Advanced search features
- **[📦 MCP Integration](docs/MCP_CURSOR_INTEGRATION.md)** - Cursor setup guide

**Total:** 42 documentation files covering all aspects of the system.

---

## 📦 Repository

https://github.com/ChrisCalzaretta/memoryagent

---

## 🔐 Security Note

- All code processing happens locally
- No data sent to external services (except Azure OpenAI if configured)
- Embeddings generated on your GPU
- All storage in `d:\Memory\` on your local machine

