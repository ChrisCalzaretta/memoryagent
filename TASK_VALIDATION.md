# Task Validation System

## Overview

The Task Validation System ensures that development tasks are actually completed correctly before being marked as "Done". It prevents premature task completion and can automatically fix common issues like missing tests or documentation.

## Why Task Validation?

**Without Validation:**
```
Developer: "I finished the UserService!"
Code: [No tests, no docs, complexity 47] ❌
Status: "Completed" ✅ <- Wrong!
```

**With Validation:**
```
Developer: "I finished the UserService!"
System: "❌ Validation failed:
  - No tests found for UserService
  - Missing XML documentation
  - Method complexity exceeds 10"
Status: Still "In Progress"
System: "🔧 Auto-created UserServiceTests.cs"
Developer: Fills in tests & fixes complexity
System: "✅ Validation passed!"
Status: "Completed" ✅
```

## Features

### 1. **Validation Rules**

Each task can have multiple validation rules that must pass before completion:

| Rule Type | Description | Auto-Fix |
|-----------|-------------|----------|
| `requires_test` | Task must have unit tests | ✅ Creates test scaffolding |
| `requires_file` | Specific file must exist | ✅ Creates from template |
| `min_test_coverage` | Minimum % of methods tested | ✅ Suggests missing tests |
| `max_complexity` | Cyclomatic complexity limit | ❌ Requires manual refactoring |
| `requires_documentation` | Public APIs need XML docs | ✅ Generates doc templates |
| `no_code_smells` | No quality issues detected | ❌ Requires manual fixes |

### 2. **Auto-Fix**

When validation fails, the system can automatically:
- Generate test file scaffolding
- Create missing files from templates
- Add XML documentation stubs
- Suggest refactoring strategies

### 3. **Cursor Integration**

Use the `validate_task` MCP tool from Cursor to check tasks before marking complete.

## Usage

### Creating a Plan with Validation Rules

**API Request:**
```json
POST /api/plan/add
{
  "context": "cbc_ai",
  "name": "Add User Authentication",
  "description": "Implement JWT-based auth",
  "tasks": [
    {
      "title": "Create UserService",
      "description": "Service for user management",
      "orderIndex": 0,
      "validationRules": [
        {
          "ruleType": "requires_test",
          "target": "UserService",
          "autoFix": true
        },
        {
          "ruleType": "max_complexity",
          "target": "UserService",
          "parameters": {
            "max_complexity": 10
          },
          "autoFix": false
        },
        {
          "ruleType": "requires_documentation",
          "target": "UserService",
          "autoFix": true
        }
      ]
    }
  ]
}
```

### Manual Validation via Cursor

```javascript
// In Cursor, call the validate_task tool
{
  "planId": "plan-123",
  "taskId": "task-456",
  "autoFix": true  // Automatically fix issues
}
```

**Response:**
```
❌ Task 'Create UserService' failed validation:

• requires_test: No tests found for UserService
  💡 Auto-fix available: Create UserServiceTests.cs with basic test scaffolding

• max_complexity: 1 method(s) exceed max complexity 10
  💡 Refactor: CreateUserWithValidation

🔧 Attempting auto-fix...
✅ Auto-fix completed! Please re-validate to confirm.

💡 Suggestions:
• Auto-fix available: Create UserServiceTests.cs with basic test scaffolding

Run with autoFix: true to automatically fix these issues.
```

### Automatic Validation on Task Completion

When you try to mark a task as `Completed`, validation runs automatically:

```json
POST /api/plan/task/status
{
  "planId": "plan-123",
  "taskId": "task-456",
  "status": "Completed"
}
```

**If validation fails:**
```json
{
  "error": "Task validation failed: No tests found for UserService, 1 method(s) exceed max complexity 10"
}
```

**If auto-fix enabled and successful:**
- Auto-fixes issues
- Re-validates
- Marks complete if all rules pass

## Validation Rule Reference

### `requires_test`

**Purpose:** Ensure code has unit tests

**Configuration:**
```json
{
  "ruleType": "requires_test",
  "target": "UserService",  // Class name or file
  "autoFix": true
}
```

**Checks:**
- Searches for test methods containing the target class name
- Looks for test framework markers (xUnit, NUnit, MSTest, pytest)

**Auto-Fix:**
- Creates `{ClassName}Tests.cs`
- Adds basic xUnit test scaffolding
- Includes TODO comments for developer

### `requires_file`

**Purpose:** Ensure specific file exists

**Configuration:**
```json
{
  "ruleType": "requires_file",
  "target": "README.md",
  "parameters": {
    "template": "default_readme"
  },
  "autoFix": true
}
```

**Checks:**
- File.Exists(target)

**Auto-Fix:**
- Creates file from template parameter

### `min_test_coverage`

**Purpose:** Ensure minimum percentage of methods have tests

**Configuration:**
```json
{
  "ruleType": "min_test_coverage",
  "target": "UserService",
  "parameters": {
    "min_coverage": 80
  },
  "autoFix": true
}
```

**Checks:**
- Counts methods in target class
- Counts tests for that class
- Calculates coverage %

**Auto-Fix:**
- Suggests which methods need tests
- Provides count of missing tests

### `max_complexity`

**Purpose:** Enforce cyclomatic complexity limits

**Configuration:**
```json
{
  "ruleType": "max_complexity",
  "target": "UserService",
  "parameters": {
    "max_complexity": 10
  },
  "autoFix": false
}
```

**Checks:**
- Analyzes cyclomatic complexity of all methods
- Flags methods exceeding threshold

**Auto-Fix:**
- Not available (requires manual refactoring)
- Provides list of complex methods

### `requires_documentation`

**Purpose:** Ensure public APIs have XML documentation

**Configuration:**
```json
{
  "ruleType": "requires_documentation",
  "target": "UserService",
  "autoFix": true
}
```

**Checks:**
- Finds public methods in target
- Checks for XML doc comments (`///`)

**Auto-Fix:**
- Generates XML doc comment templates
- Includes `<summary>`, `<param>`, `<returns>` tags

### `no_code_smells`

**Purpose:** Ensure no quality issues detected

**Configuration:**
```json
{
  "ruleType": "no_code_smells",
  "target": "UserService",
  "autoFix": false
}
```

**Checks:**
- Looks for code smells in metadata:
  - Long method
  - Too many parameters
  - Deep nesting
  - God class

**Auto-Fix:**
- Not available (requires design changes)
- Provides list of issues

## Workflow Examples

### Example 1: Feature Development

**Plan:**
```
Feature: Add Payment Processing
  Task 1: Create PaymentService
    ✓ requires_test: PaymentService
    ✓ max_complexity: 10
    ✓ requires_documentation

  Task 2: Add Payment Controller
    ✓ requires_test: PaymentController
    ✓ requires_file: PaymentController.cs
```

**Developer Workflow:**
1. Create `PaymentService.cs`
2. Try to mark Task 1 complete
3. ❌ Validation fails: "No tests found"
4. System auto-creates `PaymentServiceTests.cs`
5. Developer fills in actual test logic
6. Try again
7. ✅ Validation passes
8. Task 1 marked complete

### Example 2: Refactoring Task

**Plan:**
```
Task: Reduce UserService Complexity
  ✓ max_complexity: 8 (down from 15)
  ✓ no_code_smells
```

**Developer Workflow:**
1. Refactor complex methods
2. Try to mark task complete
3. ❌ Validation fails: "CreateUser has complexity 12"
4. Continue refactoring
5. Try again
6. ✅ Validation passes
7. Task marked complete

### Example 3: Documentation Sprint

**Plan:**
```
Task: Document Public APIs
  ✓ requires_documentation: UserService
  ✓ requires_documentation: PaymentService
  ✓ requires_documentation: AuthService
```

**Developer Workflow:**
1. System checks all public methods
2. Auto-generates doc templates
3. Developer fills in descriptions
4. ✅ Validation passes

## Best Practices

### 1. **Enable Auto-Fix for Simple Rules**

```json
{
  "ruleType": "requires_test",
  "autoFix": true  // ✅ Let the system help!
}
```

### 2. **Use Dependencies for Multi-Step Tasks**

```json
{
  "tasks": [
    {
      "title": "Create Model",
      "orderIndex": 0,
      "dependencies": []
    },
    {
      "title": "Add Tests",
      "orderIndex": 1,
      "dependencies": ["task-0"],  // Must complete model first
      "validationRules": [
        { "ruleType": "min_test_coverage", "parameters": { "min_coverage": 80 } }
      ]
    }
  ]
}
```

### 3. **Combine Rules for Quality Gates**

```json
{
  "validationRules": [
    { "ruleType": "requires_test" },
    { "ruleType": "max_complexity", "parameters": { "max_complexity": 10 } },
    { "ruleType": "requires_documentation" },
    { "ruleType": "no_code_smells" }
  ]
}
```

### 4. **Use Reasonable Thresholds**

```json
{
  "ruleType": "max_complexity",
  "parameters": {
    "max_complexity": 10  // ✅ Reasonable
    // "max_complexity": 2  // ❌ Too strict, everything fails
  }
}
```

## API Reference

### Validate Task

**Endpoint:** MCP Tool `validate_task`

**Request:**
```json
{
  "planId": "string",
  "taskId": "string",
  "autoFix": true
}
```

**Response:**
```json
{
  "isValid": false,
  "failures": [
    {
      "ruleType": "requires_test",
      "message": "No tests found for UserService",
      "canAutoFix": true,
      "fixDescription": "Create UserServiceTests.cs with basic test scaffolding"
    }
  ],
  "suggestions": [
    "Auto-fix available: Create UserServiceTests.cs"
  ]
}
```

### Update Task Status

**Endpoint:** `POST /api/plan/task/status`

**Request:**
```json
{
  "planId": "string",
  "taskId": "string",
  "status": "Completed"
}
```

**Behavior:**
- If `status == Completed` → Runs validation
- If validation fails → Throws error (or auto-fixes if enabled)
- If validation passes → Updates status

## Architecture

```
┌─────────────────────────────────────────────────┐
│  Developer marks task complete                  │
└────────────────┬────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────┐
│  PlanService.UpdateTaskStatusAsync              │
│  └─ Check if status == Completed                │
│  └─ Has validation rules?                       │
└────────────────┬────────────────────────────────┘
                 │ Yes
                 ▼
┌─────────────────────────────────────────────────┐
│  TaskValidationService.ValidateTaskAsync        │
│  └─ For each rule:                              │
│     ├─ requires_test?                           │
│     ├─ requires_file?                           │
│     ├─ max_complexity?                          │
│     └─ etc.                                     │
└────────────────┬────────────────────────────────┘
                 │
         ┌───────┴───────┐
         │               │
    Fails ❌         Passes ✅
         │               │
         ▼               ▼
┌──────────────┐  ┌──────────────┐
│  Auto-Fix?   │  │ Mark Complete│
│  ├─ Yes: Fix │  └──────────────┘
│  └─ No: Error│
└──────────────┘
```

## Future Enhancements

### Planned Features

1. **Custom Rule Types**
   - Allow users to define custom validation logic
   - Plugin system for domain-specific rules

2. **Team Rules**
   - Organization-wide rule templates
   - Enforce standards across projects

3. **Progressive Validation**
   - Warn at 50% complete
   - Block at 100% complete

4. **Integration with CI/CD**
   - Run validation in GitHub Actions
   - Block PR merge if validation fails

5. **AI-Powered Auto-Fix**
   - Use LLM to generate full test implementations
   - Auto-refactor complex methods
   - Write comprehensive documentation

## Troubleshooting

### "Validation failed: No tests found" but tests exist

**Cause:** Test file not indexed yet

**Fix:**
1. Re-index the codebase
2. Wait for file watcher to pick up changes
3. Check that test file follows naming convention (`*Tests.cs`)

### "Auto-fix failed"

**Causes:**
- File system permissions
- Template not found
- Invalid file path

**Fix:**
1. Check application logs for detailed error
2. Verify file paths are correct
3. Ensure templates exist in configuration

### "Validation takes too long"

**Cause:** Large codebase with many methods

**Fix:**
1. Use more specific `target` (e.g., `UserService.CreateUser` vs `UserService`)
2. Increase `minimumScore` threshold in vector search
3. Limit rules to critical checks only

## Conclusion

The Task Validation System transforms development planning from a simple checklist into an intelligent quality gate. By enforcing rules before task completion, it:

- ✅ **Prevents technical debt** (no untested code)
- ✅ **Maintains code quality** (complexity limits)
- ✅ **Ensures documentation** (public APIs documented)
- ✅ **Saves time** (auto-generates boilerplate)
- ✅ **Builds confidence** (objective completion criteria)

**Next Steps:**
1. Add validation rules to your plans
2. Enable auto-fix for test creation
3. Set reasonable complexity thresholds
4. Let the system help you write better code!

