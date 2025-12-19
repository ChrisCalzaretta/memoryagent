# Cursor Commands - MemoryRouter

## 🧠 AI-Powered Orchestration

**MemoryRouter** is the single entry point for all operations. It uses **FunctionGemma AI** to intelligently orchestrate 44+ tools.

## 🎯 Main Commands

### `ExecuteTask` - The Main Entry Point
Uses AI to figure out which tools to call based on your natural language request.

**When to use:** Almost always! For any task.

### `ListTools` - Discover Capabilities
Shows all 44+ available tools and their capabilities.

**When to use:** When exploring what's possible or looking for specific functionality.

---

## 📚 Legacy Commands (Informational Only)

The other command files in this directory describe specific tools, but **you don't need to call them directly!**

Instead:
1. Use `ExecuteTask` with a natural language description
2. The AI orchestrator will call the right tools automatically

### Example Workflow

**Old Way (Manual):**
```
1. smartsearch(query: "authentication")
2. analyze_dependencies(filePath: "auth.py")
3. validate(scope: "security", code: "...")
```

**New Way (AI-Orchestrated):**
```
execute_task(request: "Find authentication code, analyze its dependencies, and validate security")
```

**The AI handles the tool selection, parameter passing, and execution order automatically!**

---

## 🔧 Tool Categories Available

Via `execute_task`, you have access to:

### MemoryAgent (33 tools)
- **Search**: Smart search, find examples, similar questions
- **Understanding**: Explain code, dependencies, complexity
- **Validation**: Security, patterns, best practices, imports
- **Planning**: Task plans, TODOs, management
- **Intelligence**: Recommendations, important files, insights
- **Learning**: Q&A storage, task lessons, workspace status
- **Transformation**: CSS, UI components, patterns
- **Indexing**: File, directory, workspace indexing

### CodingOrchestrator (11 tools)
- **Generation**: Multi-agent code creation with validation
- **Tasks**: Status, listing, cancellation, file extraction
- **Design**: Brand creation, validation, tokens, questionnaires

---

## 💡 Pro Tips

1. **Be Specific**: "Create a REST API for user management with JWT auth" is better than "create API"
2. **Combine Operations**: The AI can handle multi-step workflows in one request
3. **Trust the AI**: It knows which tools to use and when
4. **Explore with ListTools**: Use it to understand capabilities

---

## 🚀 Quick Start

```
# Discover what's available
list_available_tools(filter: "search")

# Execute any task with natural language
execute_task(request: "Find all database code and check for SQL injection vulnerabilities")

# That's it! The AI handles everything else.
```
