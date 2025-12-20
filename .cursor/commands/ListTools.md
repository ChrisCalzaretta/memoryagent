---
description: 📋 Discover all available tools organized by category
alwaysApply: false
---

# List Available Tools

Discover all 44+ available tools from MemoryAgent and CodingOrchestrator, organized into 6 core categories.

## Usage

```javascript
list_available_tools()                         // Show all tools
list_available_tools({ category: "discovery" }) // Filter by category
```

## Categories

### 🔍 Discovery
Search, find, and analyze existing code
```javascript
list_available_tools({ category: "discovery" })
```

### 🚀 Generation
Create and generate new code, features, and designs
```javascript
list_available_tools({ category: "generation" })
```

### ✅ Validation
Review, validate, and check quality/security
```javascript
list_available_tools({ category: "validation" })
```

### 📋 Planning
Plan, organize, and manage tasks/todos
```javascript
list_available_tools({ category: "planning" })
```

### 🧠 Knowledge
Learn, store, and retrieve facts/context
```javascript
list_available_tools({ category: "knowledge" })
```

### 📊 Management
Monitor status, control operations
```javascript
list_available_tools({ category: "management" })
```

## What You Get

For each tool:
- **Name**: Tool identifier
- **Description**: What it does
- **Service**: Backend provider (memory-agent or coding-orchestrator)
- **Category**: Functional grouping
- **Use Cases**: Common scenarios
- **Keywords**: Related terms

## Remember

**You don't usually need to call specific tools directly!**

Use `execute_task` with natural language instead - the AI will choose the right tools automatically.

Use this command when you want to **explore capabilities** or **understand what's available**.
