# Router Tool Categories

## Overview

The MemoryRouter uses a **consolidated 6-category system** for organizing tools discovered from MemoryAgent and CodingOrchestrator services. Categories are assigned through:
1. **Category hints** from source services (priority)
2. **Pattern matching** on tool names and descriptions (fallback)

This streamlined approach enhances tool organization, filtering, and helps FunctionGemma make better routing decisions.

## Core Categories

All tools are automatically assigned to one of these **6 core categories** during the discovery process:

### 🔍 Discovery
**Purpose:** Search, find, and analyze existing code, patterns, and dependencies

**Combines:** Search + Analysis + Indexing operations

- **Keywords:** search, find, query, analyze, explain, understand, discover
- **Use Cases:**
  - Find existing code or files
  - Search for patterns and examples
  - Analyze code structure
  - Understand dependencies
  - Discover how features work
  - Map system architecture

**Category Hint:** Use `"discovery"`, `"search"`, `"find"`, or `"analyze"`

### 🚀 Generation
**Purpose:** Create and generate new code, features, and designs

**Combines:** Code Generation + Design + Build operations

- **Keywords:** generate, create, build, implement, develop, write, design, brand
- **Use Cases:**
  - Generate new code from scratch
  - Create complete features
  - Build REST APIs or services
  - Design UI/UX systems
  - Develop applications
  - Generate style guides

**Category Hint:** Use `"generation"`, `"generate"`, `"create"`, or `"design"`

### ✅ Validation
**Purpose:** Review, validate, and check code quality and security

- **Keywords:** validate, check, review, quality, security, compliance
- **Use Cases:**
  - Review code quality
  - Check for security issues
  - Validate best practices
  - Security audits
  - Code standards compliance

**Category Hint:** Use `"validation"`, `"validate"`, `"check"`, or `"review"`

### 📋 Planning
**Purpose:** Plan, organize, and manage tasks, todos, and roadmaps

**Combines:** Planning + Task Management + Todo operations

- **Keywords:** plan, todo, task, strategy, breakdown, roadmap, organize, manage
- **Use Cases:**
  - Create execution plans
  - Break down complex projects
  - Manage tasks and todos
  - Track work items
  - Organize development strategy

**Category Hint:** Use `"planning"`, `"plan"`, `"todo"`, or `"task"`

### 🧠 Knowledge
**Purpose:** Store, index, and retrieve project knowledge and context

**Combines:** Learning + Knowledge Management + Indexing operations

- **Keywords:** learn, knowledge, remember, store, index, fact, qa, question
- **Use Cases:**
  - Index and store knowledge
  - Learn from conversations
  - Remember project decisions
  - Answer questions
  - Retrieve context
  - Enable semantic search

**Category Hint:** Use `"knowledge"`, `"learn"`, `"index"`, or `"store"`

### 📊 Management
**Purpose:** Monitor status, control operations, and manage processes

**Combines:** Status + Control + Monitoring operations

- **Keywords:** status, list, monitor, control, cancel, stop, manage, view
- **Use Cases:**
  - Check status and progress
  - Monitor running operations
  - Control background jobs
  - List completed work
  - Cancel operations

**Category Hint:** Use `"management"`, `"status"`, `"control"`, or `"monitor"`

### 🔧 Other
**Purpose:** Uncategorized tools
- Tools that don't fit into other categories

## Usage

### Listing All Tools

```bash
# List all available tools
curl -X POST http://localhost:5100/api/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
      "name": "list_available_tools",
      "arguments": {}
    }
  }'
```

### Filtering by Category

```bash
# List only search tools
curl -X POST http://localhost:5100/api/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
      "name": "list_available_tools",
      "arguments": {
        "category": "search"
      }
    }
  }'
```

Available category filters:
- `all` - Show all tools (default)
- `discovery` - Search, find, and analyze tools
- `generation` - Create and generate code/designs
- `validation` - Validation and review tools
- `planning` - Planning and task management tools
- `knowledge` - Learn, store, and index tools
- `management` - Status, control, and monitoring tools
- `other` - Uncategorized tools

## Implementation Details

### ToolCategory Enum

```csharp
/// <summary>
/// Tool categories for better organization and filtering
/// Consolidated to 6 core categories for simplicity
/// </summary>
public enum ToolCategory
{
    Discovery,   // Search, find, analyze code/patterns/dependencies
    Generation,  // Create/generate new code, features, designs
    Validation,  // Review, validate, check quality/security
    Planning,    // Plan, organize, manage tasks/todos
    Knowledge,   // Learn, store, retrieve facts/context
    Management,  // Status, control, monitoring operations
    Other        // Uncategorized tools
}
```

### ToolDefinition Model

```csharp
public class ToolDefinition
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Service { get; set; }
    public required Dictionary<string, object> InputSchema { get; set; }
    public ToolCategory Category { get; set; } = ToolCategory.Other;
    public List<string> UseCases { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
}
```

### Automatic Category Assignment

Categories are automatically assigned during tool discovery in `ToolRegistry.AugmentToolMetadata()` based on:
1. Tool name patterns (e.g., contains "search", "validate", "orchestrate")
2. Description content
3. Predefined rules for each category

### API Methods

**IToolRegistry Interface:**
- `GetAllTools()` - Returns all tools ordered by category, service, and name
- `GetToolsByCategory(ToolCategory category)` - Returns tools filtered by category
- `GetTool(string name)` - Gets a specific tool by name
- `SearchTools(string query)` - Searches tools by keywords or description

## Benefits

1. **Better Organization:** Tools are logically grouped by function
2. **Easier Discovery:** Users can filter tools by category
3. **Improved AI Routing:** FunctionGemma can make better decisions with explicit categories
4. **Clearer Documentation:** Tool listings are organized and easy to navigate
5. **Enhanced Filtering:** Category-based filtering is more reliable than keyword matching

## Example Output

When listing tools by category, the output is organized hierarchically:

```
# 🛠️ Available Tools (24)

## 🔍 Search (3 tools)

### 🧠 memory-agent

#### `smartsearch`
Search codebase for existing code, patterns, functions, or files using semantic search
**Use Cases:** Find existing code or files, Search for patterns or examples

## 🚀 CodeGen (2 tools)

### 🎯 coding-orchestrator

#### `orchestrate_task`
Generate complete applications, features, or code files from natural language descriptions
**Use Cases:** Generate new code from scratch, Create complete features or apps
```

## Testing

Test the category system using:

```bash
# Test all categories
for category in search index analysis validation planning todo codegen design knowledge status control other; do
  echo "Testing category: $category"
  curl -X POST http://localhost:5100/api/mcp \
    -H "Content-Type: application/json" \
    -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"list_available_tools\",\"arguments\":{\"category\":\"$category\"}}}"
done
```

## Future Enhancements

Potential improvements:
1. User-defined custom categories
2. Multi-category support (tools can belong to multiple categories)
3. Category-based analytics and usage tracking
4. Dynamic category suggestions based on user behavior
5. Category hierarchy (parent-child relationships)
