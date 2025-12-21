# Generate Code - Code Generator

**Server:** `code-generator` (direct to CodingOrchestrator)

Use this for **creating new code** with multi-agent validation.

## Main Tool: orchestrate_task

```javascript
orchestrate_task({
  task: "Create a user authentication service with JWT",
  language: "typescript",  // auto, python, csharp, typescript, etc.
  maxIterations: 50,       // default: 50
  minValidationScore: 8    // default: 8 (must score >= 8/10)
})
```

## Examples

### REST API
```javascript
orchestrate_task({
  task: "Create a REST API for user management with CRUD operations",
  language: "python"
})
```

### Full Application
```javascript
orchestrate_task({
  task: "Create a Blazor chess game with standard rules, piece movement, check/checkmate detection",
  language: "csharp",
  maxIterations: 100
})
```

### React Component
```javascript
orchestrate_task({
  task: "Create a data table component with sorting, filtering, and pagination",
  language: "typescript"
})
```

### Backend Service
```javascript
orchestrate_task({
  task: "Create a payment processing service with Stripe integration",
  language: "typescript"
})
```

## Job Management

### Check Status
```javascript
get_task_status({ jobId: "job_20241219_abc123" })
```

### Get Generated Files
```javascript
apply_task_files({
  jobId: "job_20241219_abc123",
  basePath: "E:\\MyProject"
})
```

### List All Jobs
```javascript
list_tasks()
```

### Cancel a Job
```javascript
cancel_task({ jobId: "job_20241219_abc123" })
```

## Design Tools

### Create Brand System
```javascript
design_create_brand({
  brand_name: "CloudSync",
  industry: "SaaS",
  personality_traits: ["Professional", "Trustworthy", "Modern"],
  visual_style: "Minimal",
  theme_preference: "Both",
  platforms: ["Web"],
  frameworks: ["React"]
})
```

### Validate Against Brand
```javascript
design_validate({
  context: "cloudsync",
  code: "<your HTML/CSS code>"
})
```

### Get Existing Brand
```javascript
design_get_brand({ context: "cloudsync" })
```

## How It Works

1. Your task goes **directly** to CodingOrchestrator (port 5003)
2. CodingAgent generates code using LLM
3. ValidationAgent reviews and scores (must be >= 8/10)
4. If score < 8, iterate with feedback
5. Return validated, quality-checked code

## Why Direct?

- **No routing overhead** - saves 3-5 seconds
- **Faster response** - code generation already takes 60+ seconds
- **Clearer flow** - you know exactly which service handles it

## When to Use

✅ **Use code-generator for:**
- Creating new code from scratch
- Generating complete features
- Building applications
- Creating/validating design systems
- Getting validated, quality-checked code

❌ **For searching/understanding code, use `memory-agent` instead**
- See `ExecuteTask.md` for search and analysis
