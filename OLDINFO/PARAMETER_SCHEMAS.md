# Tool Parameter Schemas - DEFINITIVE REFERENCE

## Critical Tools (Must Get Parameters Right!)

### Status Category

#### `get_task_status`
**Purpose:** Get the status of ONE running or completed coding task  
**Parameters:**
```json
{
  "jobId": "string" // REQUIRED - The job ID returned by orchestrate_task
}
```
**Example:**
```json
{"name": "get_task_status", "parameters": {"jobId": "3bf191d0-4384-42e0-887c-1df901577710"}}
```

#### `list_tasks`
**Purpose:** List ALL active and recent coding tasks  
**Parameters:**
```json
{} // EMPTY OBJECT - NO PARAMETERS REQUIRED
```
**Example:**
```json
{"name": "list_tasks", "parameters": {}}
```

#### `workspace_status`
**Purpose:** Get overview of what Agent Lightning knows about this workspace  
**Parameters:**
```json
{} // EMPTY OBJECT - NO PARAMETERS REQUIRED
```
**Example:**
```json
{"name": "workspace_status", "parameters": {}}
```

### Search Category

#### `smartsearch`
**Purpose:** Find/search/locate existing code  
**Parameters:**
```json
{
  "query": "string", // REQUIRED - Search query
  "context": "string" // OPTIONAL - Project context name
}
```
**Example:**
```json
{"name": "smartsearch", "parameters": {"query": "authentication code"}}
```

### Index Category

#### `index`
**Purpose:** Index code into memory  
**Parameters:**
```json
{
  "path": "string", // REQUIRED - File or directory path
  "scope": "string", // REQUIRED - "file", "directory", or "reindex"
  "context": "string", // OPTIONAL - Project context name
  "background": boolean // OPTIONAL - Run in background (default: true)
}
```
**Example:**
```json
{"name": "index", "parameters": {"path": "/src/app.py", "scope": "file"}}
```

### CodeGen Category

#### `orchestrate_task`
**Purpose:** Generate/create/build NEW code  
**Parameters:**
```json
{
  "task": "string", // REQUIRED - The coding task description
  "language": "string", // OPTIONAL - Target language (default: "auto")
  "context": "string", // OPTIONAL - Project context name
  "workspacePath": "string", // OPTIONAL - Workspace root path
  "background": boolean, // OPTIONAL - Run as background job (default: true)
  "maxIterations": integer, // OPTIONAL - Max iterations (default: 100)
  "minValidationScore": integer, // OPTIONAL - Min validation score (default: 8)
  "validationMode": "string", // OPTIONAL - "standard" or "enterprise" (default: "standard")
  "autoWriteFiles": boolean // OPTIONAL - Auto-write files (default: true)
}
```
**Example:**
```json
{"name": "orchestrate_task", "parameters": {"task": "Create a REST API in Python"}}
```

### Planning Category

#### `manage_plan`
**Purpose:** Create execution plans  
**Parameters:**
```json
{
  "action": "string", // REQUIRED - "create", "get_status", "update_task", "complete", "search", "validate_task"
  "goal": "string", // REQUIRED for "create" action
  "planId": "string", // REQUIRED for non-create actions
  "taskId": "string", // REQUIRED for task-specific actions
  // ... other action-specific parameters
}
```
**Example:**
```json
{"name": "manage_plan", "parameters": {"action": "create", "goal": "Build user authentication system"}}
```

---

## Common Mistakes FunctionGemma Makes

### ❌ WRONG: Passing generic parameters to status tools
```json
// DON'T DO THIS
{"name": "list_tasks", "parameters": {"query": "list all tasks", "context": "CBC_AI"}}
{"name": "get_task_status", "parameters": {"query": "job id abc", "context": "CBC_AI"}}
```

### ✅ CORRECT: Use proper parameters
```json
// DO THIS
{"name": "list_tasks", "parameters": {}}
{"name": "get_task_status", "parameters": {"jobId": "abc123"}}
```

---

## Parameter Extraction Rules

1. **`list_tasks`**: ALWAYS use empty object `{}`
2. **`get_task_status`**: Extract the UUID from the user's request
   - Pattern: `[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}`
   - If no UUID found, ask user or error
3. **`workspace_status`**: ALWAYS use empty object `{}`
4. **`smartsearch`**: Extract the search query from user's request
5. **`index`**: Extract `path` and determine `scope` ("file" or "directory")
