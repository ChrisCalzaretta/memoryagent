# Generate Code - Multi-Agent Workflow

**Purpose:** Start a multi-agent coding task where CodingAgent generates code and ValidationAgent validates it iteratively until quality passes.

## When to Use
- Creating new services, classes, or components
- Complex multi-file implementations
- Tasks requiring automatic quality validation
- When you want iterative refinement

## When NOT to Use
- Simple one-line changes
- Quick fixes without validation needs
- Tasks you can complete faster manually

---

## Execute This Command

### Step 1: Start the Task
```
Use the `orchestrate_task` tool from the coding-orchestrator MCP server:

orchestrate_task(
  task: "[DESCRIBE WHAT TO BUILD]",
  maxIterations: 5,
  minValidationScore: 8,
  autoWriteFiles: false  // Set to true to auto-write files to workspace
)
```

**Options:**
- `autoWriteFiles: false` (default) - Files returned in response for review
- `autoWriteFiles: true` - Files auto-written to workspace/generated/ folder

### Step 2: Monitor Progress
```
Use `get_task_status` with the returned jobId to check progress:

get_task_status(jobId: "[JOB_ID]")
```

### Step 3: Apply Generated Files
When status is "Complete":
```
apply_task_files(
  jobId: "[JOB_ID]",
  basePath: "[WORKSPACE_PATH]"  // e.g., "E:\\GitHub\\MyProject"
)
```

This returns each file with explicit write instructions.

### Step 4: Write Each File
For EACH file in the response, agent MUST:
```
write(
  file_path: "[FULL_PATH_FROM_RESPONSE]",
  contents: "[CODE_FROM_RESPONSE]"
)
```

User sees **"Keep/Review" UI** for each file.

**⚠️ Agent MUST write files - do NOT just display them as text!**

---

## What Happens Behind the Scenes

1. **CodingAgent** generates code using `deepseek-v2:16b`
2. **ValidationAgent** validates with rules + `phi4` LLM analysis
3. If score < 8: CodingAgent fixes using a **different model** (smart rotation)
4. Repeats until score >= 8 or max iterations reached
5. **MemoryAgent** stores Q&A and tracks patterns

---

## Example Tasks

```
# Create a service
orchestrate_task(task: "Create a UserService with CRUD operations for managing users")

# Add a feature
orchestrate_task(task: "Add caching to the ProductRepository using IMemoryCache")

# Create an API endpoint
orchestrate_task(task: "Create a REST API endpoint for user registration with validation")

# Complex implementation
orchestrate_task(task: "Implement a retry policy handler using Polly with exponential backoff")
```

---

## Tips

- **Be specific** in your task description for better results
- Use **maxIterations: 3** for simple tasks, **5** for complex ones
- If task fails, check `get_task_status` for error details
- Use `list_tasks` to see all active/recent tasks

