# MemoryRouter Commands

Quick reference for MemoryRouter MCP commands available in Cursor.

## Primary Commands

### 🧠 execute_task
**Main entry point for ALL operations**

Use natural language to describe what you want - the AI figures out which tools to call.

```javascript
execute_task({ request: "Your task in natural language" })
```

See [ExecuteTask.md](./ExecuteTask.md) for details.

### 📋 list_available_tools
**Discover available tools by category**

Explore the 44+ tools organized into 6 core categories.

```javascript
list_available_tools()                         // All tools
list_available_tools({ category: "discovery" }) // Specific category
```

See [ListTools.md](./ListTools.md) for details.

## Tool Categories

- **🔍 Discovery**: Search, find, and analyze code
- **🚀 Generation**: Create and generate new code/designs
- **✅ Validation**: Review, validate, and check quality
- **📋 Planning**: Plan, organize, and manage tasks
- **🧠 Knowledge**: Learn, store, and retrieve context
- **📊 Management**: Monitor status and control operations

See [DiscoverByCategory.md](./DiscoverByCategory.md) for category guide.

## Quick Start

**Most common workflow:**
```javascript
// Just use natural language!
execute_task({ request: "Find all authentication code and validate it for security" })
```

**The AI automatically:**
1. Analyzes your request
2. Chooses the right tools
3. Creates an execution plan
4. Returns complete results

## When to Use What

### Use `execute_task` (99% of the time)
- Any coding task
- Code search and analysis
- Code generation
- Validation and review
- Planning and task management

### Use `list_available_tools` (rarely)
- Exploring capabilities
- Learning what's available
- Discovering tools for specific categories

**Remember**: You almost never need to know which specific tools exist. Just describe what you want with `execute_task`!
