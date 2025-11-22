# MCP Cursor Integration

## Overview

The Memory Agent now exposes **TODO and Plan management** through MCP (Model Context Protocol) for seamless Cursor integration.

## Available MCP Tools

### Code Analysis Tools (Existing)

1. **index_file** - Index a single file
2. **index_directory** - Index entire directory
3. **query / search** - Semantic code search
4. **reindex** - Smart reindex (detects changes)
5. **smartsearch** - Intelligent hybrid search
6. **impact_analysis** - See what breaks if code changes
7. **dependency_chain** - View dependency tree
8. **find_circular_dependencies** - Detect circular deps

### TODO Management Tools (NEW ✨)

#### 9. **add_todo**
Create a TODO item to track technical debt, bugs, or improvements.

**Parameters:**
- `context` (required): Project context
- `title` (required): TODO title
- `description` (optional): Detailed description
- `priority` (optional): Low, Medium, High, Critical (default: Medium)
- `filePath` (optional): File path
- `lineNumber` (optional): Line number
- `assignedTo` (optional): Assignee email

**Example usage in Cursor:**
```
@memory add a TODO for refactoring UserService with high priority
```

#### 10. **search_todos**
Search and filter TODO items.

**Parameters:**
- `context` (optional): Filter by context
- `status` (optional): Pending, InProgress, Completed, Cancelled
- `priority` (optional): Low, Medium, High, Critical
- `assignedTo` (optional): Filter by assignee

**Example usage in Cursor:**
```
@memory show me all high priority TODOs
@memory list pending TODOs assigned to chris
```

#### 11. **update_todo_status**
Update the status of a TODO item.

**Parameters:**
- `todoId` (required): TODO ID
- `status` (required): Pending, InProgress, Completed, Cancelled

**Example usage in Cursor:**
```
@memory mark TODO {id} as completed
```

### Plan Management Tools (NEW ✨)

#### 12. **create_plan**
Create a development plan with multiple tasks and dependencies.

**Parameters:**
- `context` (required): Project context
- `name` (required): Plan name
- `description` (optional): Plan description
- `tasks` (required): Array of tasks with title, description, orderIndex

**Example usage in Cursor:**
```
@memory create a plan for implementing user authentication with tasks:
1. Research OAuth providers
2. Implement OAuth client
3. Migrate users
```

#### 13. **get_plan_status**
Get the status and progress of a development plan.

**Parameters:**
- `planId` (required): Plan ID

**Example usage in Cursor:**
```
@memory show plan status for {planId}
@memory what's the progress on my auth plan?
```

#### 14. **update_task_status**
Update the status of a task in a plan.

**Parameters:**
- `planId` (required): Plan ID
- `taskId` (required): Task ID
- `status` (required): Pending, InProgress, Blocked, Completed, Cancelled

**Example usage in Cursor:**
```
@memory mark task {taskId} in plan {planId} as InProgress
```

#### 15. **complete_plan**
Mark a development plan as completed.

**Parameters:**
- `planId` (required): Plan ID

**Example usage in Cursor:**
```
@memory complete the authentication plan
```

#### 16. **search_plans**
Search and filter development plans.

**Parameters:**
- `context` (optional): Filter by context
- `status` (optional): Draft, Active, Completed, Cancelled, OnHold

**Example usage in Cursor:**
```
@memory show all active plans
@memory list completed plans for CBC_AI
```

## How to Use from Cursor

### 1. Direct MCP Tool Calls

In Cursor, use the `@memory` mention to interact with the Memory Agent:

```
@memory add a TODO to fix N+1 query in UserService line 45 with high priority
```

```
@memory create a plan to refactor the auth system with these tasks:
- Update JWT library
- Add refresh tokens  
- Migrate existing users
```

```
@memory show me all pending TODOs
```

```
@memory what's the status of my refactoring plan?
```

### 2. Natural Language Queries

The MCP service will interpret your natural language requests:

```
@memory add a high priority TODO for the bug in line 45 of UserService.cs
```

```
@memory show progress on the authentication plan
```

```
@memory mark the first task as completed
```

### 3. Code Analysis + TODO Creation

Combine code analysis with TODO creation:

```
@memory find all methods with high complexity and create TODOs for them
```

```
@memory search for async methods without error handling and add TODOs
```

## MCP Endpoint Configuration

### Default Endpoints

The MCP server exposes two endpoints:

1. **SSE (Server-Sent Events):**
   ```
   POST http://localhost:5098/sse
   ```

2. **HTTP POST:**
   ```
   POST http://localhost:5098/mcp
   ```

3. **Tools List:**
   ```
   GET http://localhost:5098/tools
   ```

### Configuration in Cursor

Add to your Cursor MCP settings (`.cursor/mcp.json`):

```json
{
  "mcpServers": {
    "memory-agent": {
      "url": "http://localhost:5098",
      "command": null,
      "args": [],
      "env": {},
      "sse": true
    }
  }
}
```

## Workflow Examples

### Example 1: Bug Fix Workflow

```
1. @memory add a TODO for fixing login bug in AuthController line 123, high priority

2. @memory create a plan for fixing authentication issues:
   - Reproduce the bug
   - Fix the login logic
   - Add unit tests
   - Test in staging

3. @memory show plan status

4. @memory mark task 1 as completed

5. @memory update task 2 status to InProgress

6. @memory complete the bug fix plan
```

### Example 2: Refactoring Workflow

```
1. @memory find all classes with more than 500 lines

2. @memory create a plan to refactor UserService:
   - Extract validation logic
   - Move database access to repository
   - Add logging
   - Update tests

3. @memory add TODO for each task in the plan

4. @memory show all pending TODOs for UserService

5. @memory mark UserService validation TODO as completed
```

### Example 3: Code Quality Improvement

```
1. @memory search for methods with cyclomatic complexity > 10

2. @memory add high priority TODOs for refactoring complex methods

3. @memory create a plan to improve code quality:
   - Refactor complex methods
   - Add missing error handling
   - Improve test coverage
   - Document public APIs

4. @memory track progress on code quality plan
```

## Response Formats

### TODO Response

```
✅ TODO added successfully!

ID: todo-abc-123
Title: Fix N+1 query in UserService
Priority: High
Status: Pending
Created: 2025-11-22 15:30
```

### Plan Status Response

```
📋 User Authentication Refactor

Status: Active
Progress: 33.3% (1/3 tasks completed)

Tasks:
  ✅ Research OAuth providers (Completed)
  🔄 Implement OAuth client (InProgress)
  ⏳ Migrate users (Pending)
```

### Search Results

```
Found 3 TODO(s):

📌 Fix N+1 query
   ID: todo-123
   Priority: High
   Status: Pending
   Assigned: chris@example.com
   Created: 2025-11-22

📌 Add error handling
   ID: todo-456
   Priority: Medium
   Status: InProgress
   Created: 2025-11-21

📌 Update documentation
   ID: todo-789
   Priority: Low
   Status: Pending
   Created: 2025-11-20
```

## Benefits of MCP Integration

1. **Natural Language**: Talk to Memory Agent naturally from Cursor
2. **Context Aware**: Understands your current project context
3. **Integrated Workflow**: Seamless TODO/Plan management while coding
4. **Visual Feedback**: Rich responses with emojis and formatting
5. **No Context Switching**: Stay in Cursor, no need for external tools

## Advanced Usage

### Combining Tools

```
# Find complex code and create TODOs
@memory find methods with complexity > 10 and create a TODO for each

# Create plan from code analysis
@memory analyze UserService dependencies and create a refactoring plan

# Track technical debt
@memory show all high priority TODOs and create a plan to address them
```

### Automation

```
# Weekly planning
@memory create a weekly plan for current sprint tasks

# Daily standup
@memory show my InProgress TODOs and plan status

# Code review prep
@memory find all TODOs in changed files since last commit
```

## Troubleshooting

### MCP Connection Issues

If Cursor can't connect to MCP server:

1. **Check server is running:**
   ```powershell
   docker ps | findstr memory-agent-server
   ```

2. **Check port is accessible:**
   ```powershell
   curl http://localhost:5098/tools
   ```

3. **Review server logs:**
   ```powershell
   docker logs memory-agent-server
   ```

### Tool Not Found

If MCP says "Unknown tool":

1. **List available tools:**
   ```powershell
   curl http://localhost:5098/tools
   ```

2. **Restart server:**
   ```powershell
   docker-compose restart mcp-server
   ```

## Future Enhancements

Potential MCP integrations:
- [ ] Auto-create TODOs from linter warnings
- [ ] GitHub issue synchronization
- [ ] Jira integration
- [ ] AI-suggested task breakdowns
- [ ] Automated progress reporting
- [ ] Time tracking integration

