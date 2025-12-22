# Generate Code - Code Agent

**Server:** `@code-agent` (via orchestrator-mcp-wrapper.js)

Use this for **creating new code** with multi-agent AI.

## Main Tool: orchestrate_task

```javascript
orchestrate_task({
  task: "Create a Calculator class with Add, Subtract, Multiply, Divide methods",
  language: "csharp",      // csharp, python, typescript, javascript, flutter, go, rust, etc.
  maxIterations: 20,       // default: 50, minimum: 50
  workspacePath: "E:\\GitHub\\MyProject"  // optional, auto-detected
})
```

## Examples

### Simple Class
```javascript
orchestrate_task({
  task: "Create a Calculator class with Add and Subtract methods",
  language: "csharp",
  maxIterations: 10
})
```

### REST API
```javascript
orchestrate_task({
  task: "Create a REST API for user management with CRUD operations",
  language: "python",
  maxIterations: 20
})
```

### React Component
```javascript
orchestrate_task({
  task: "Create a data table component with sorting and filtering",
  language: "typescript",
  maxIterations: 15
})
```

### Full Application
```javascript
orchestrate_task({
  task: "Create a Blazor chess game with standard rules, drag-and-drop, move validation, and AI opponent",
  language: "csharp",
  maxIterations: 30
})
```

## How It Works

1. **`orchestrate_task`** starts a background job
   - Returns jobId immediately
   - Multi-model thinking (Phi4, Gemma3, Qwen debate strategy)
   - Adaptive code generation (Solo → Duo → Trio → Collaborative)
   - Ensemble validation (5 models score quality)

2. **`get_task_status`** checks progress
   ```javascript
   get_task_status({ jobId: "job_20251222_abc123" })
   ```

3. **`apply_task_files`** writes generated files to workspace
   ```javascript
   apply_task_files({
     jobId: "job_20251222_abc123",
     basePath: "E:\\GitHub\\MyProject"
   })
   ```

4. **Auto-write** - Files automatically saved to workspace/Generated/

## Other Tools Available

### Check Job Status
```javascript
get_task_status({
  jobId: "job_20251222_abc123"
})
```

### Cancel Job
```javascript
cancel_task({
  jobId: "job_20251222_abc123"
})
```

### List All Jobs
```javascript
list_tasks()
```

### Apply Generated Files
```javascript
apply_task_files({
  jobId: "job_20251222_abc123",
  basePath: "E:\\GitHub\\MyProject"
})
```

## When to Use

✅ **Use @code-agent for:**
- Creating new code from scratch
- Generating complete features
- Building applications
- Multi-file projects
- Complex implementations

❌ **For search/analysis, use @memory-agent:**
- Finding existing code
- Understanding patterns
- Analyzing architecture
- See `ExecuteTask.md`

---

## Response Format

When you call `orchestrate_task`, you'll get:

```
🚀 Multi-Agent Coding Task Started

Job ID: job_20251222_abc123
Task: Create a Calculator class with Add, Subtract, Multiply, Divide methods
Language: csharp
Message: Job started successfully

The CodingAgent is now working on your task.

Progress:
- Max iterations: 20

To check status: Call get_task_status with jobId: job_20251222_abc123
```

Then use `get_task_status` to see progress and results:

```
📊 Task Status: ✅ COMPLETED

Job ID: job_20251222_abc123
Task: Create a Calculator class...
Progress: 100%
Started: 2025-12-22T00:00:00Z
Completed: 2025-12-22T00:05:00Z

✅ COMPLETED
- Success: true
- Model Used: gemma3-12b-it
- Tokens Used: 2500
- Files Generated: 3
- Explanation: Created a Calculator class with full unit tests

---
### 📄 Calculator.cs
**Type:** create
**Reason:** Main calculator implementation

​```csharp
public class Calculator {
    public double Add(double a, double b) => a + b;
    public double Subtract(double a, double b) => a - b;
    public double Multiply(double a, double b) => a * b;
    public double Divide(double a, double b) => b != 0 ? a / b : throw new DivideByZeroException();
}
​```

---
### 📄 ICalculator.cs
**Type:** create
**Reason:** Interface for dependency injection

​```csharp
public interface ICalculator {
    double Add(double a, double b);
    double Subtract(double a, double b);
    double Multiply(double a, double b);
    double Divide(double a, double b);
}
​```

---
### 📄 CalculatorTests.cs
**Type:** create
**Reason:** Unit tests for all methods

​```csharp
[TestClass]
public class CalculatorTests {
    [TestMethod]
    public void Add_TwoNumbers_ReturnsSum() {
        var calc = new Calculator();
        Assert.AreEqual(5, calc.Add(2, 3));
    }
    // ... more tests
}
​```

Files written to: workspace/Generated/calculator_20251222/
```

---

## Tips

1. **Be specific** in your task description
2. **Include language** for best results
3. **Increase maxIterations** for complex tasks (30-50 for full apps)
4. **Check status** periodically with `get_task_status`
5. **Use apply_task_files** to write generated code to your workspace

---

## Complete Workflow Example

```javascript
// Step 1: Start code generation
orchestrate_task({
  task: "Create a Blazor chess game",
  language: "csharp",
  maxIterations: 30
})

// Returns: Job ID: job_20251222_abc123

// Step 2: Check progress (call multiple times)
get_task_status({ jobId: "job_20251222_abc123" })

// Step 3: When complete, apply files
apply_task_files({
  jobId: "job_20251222_abc123",
  basePath: "E:\\GitHub\\MyProject"
})

// Now files are written to your workspace!
```

---

**🎯 Main tool: `orchestrate_task` - Starts multi-agent code generation!**
